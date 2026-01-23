#!/bin/bash
readonly REPO_ROOT="$(git rev-parse --show-toplevel 2>/dev/null)"
readonly REPORT_OUT="$REPO_ROOT/report"
readonly OUTPUT_DIR="$REPO_ROOT/_changes"
readonly FORMAT_PROMPT_URL="https://raw.githubusercontent.com/antshc/copilot-code-analyzer/main/prompts/format.prompt.md"
readonly REVIEW_PROMPT_URL="https://raw.githubusercontent.com/antshc/copilot-code-analyzer/main/prompts/review.prompt.md"

log_status() {
  printf "\033[0;32m%s\033[0m\n" "$1"
}

# Validates required inputs and environment so later steps fail fast.
validate_inputs() {
  local ghTokenArg="${1:-}"
  local baseBranchName="${2:-}"
  local branchName="${3:-}"
  local solutionPath="${4:-}"
  local formatFlag="${5:-}"
  local formatValue="${6:-}"
  local formatPromptToggle="disable"
  if [[ -z "$ghTokenArg" || -z "$baseBranchName" || -z "$branchName" || -z "$solutionPath" ]]; then
    echo "Usage: $0 GH_TOKEN BASE_BRANCH_NAME BRANCH_NAME SOLUTION_PATH [-format enable|disable] (defaults to disable)" >&2
    exit 1
  fi

  if [[ -n "$formatFlag" || -n "$formatValue" ]]; then
    if [[ "$formatFlag" != "-format" ]]; then
      echo "Optional format toggle must start with '-format'." >&2
      exit 1
    fi

    if [[ -z "$formatValue" ]]; then
      echo "'-format' requires a value of 'enable' or 'disable'." >&2
      exit 1
    fi

    if [[ "$formatValue" != "enable" && "$formatValue" != "disable" ]]; then
      echo "FORMAT_PROMPT value must be 'enable' or 'disable'." >&2
      exit 1
    fi

    formatPromptToggle="$formatValue"
  fi

  printf "%s\n%s\n%s\n%s\n%s" "$ghTokenArg" "$baseBranchName" "$branchName" "$solutionPath" "$formatPromptToggle"
}

# Synchronizes to the remote branch and rewinds to the merge-base for a clean diff.
prepare_branch_state() {
  local baseBranchName="$1"
  local branchName="$2"
  log_status "Preparing branch state using base '$baseBranchName' against '$branchName'"
  git fetch
  git checkout "origin/$branchName"
  git reset --soft "$(git merge-base HEAD "origin/$baseBranchName")"
}

# Removes and recreates a working directory to ensure deterministic outputs.
recreate_directory() {
  local targetDir="$1"
  log_status "Resetting directory at $targetDir"
  rm -rf "$targetDir"
  mkdir -p "$targetDir"
}

# Downloads prompt text via curl so Copilot invocations stay up to date.
download_prompt() {
  local url="$1"
  log_status "Downloading prompt from $url"
  curl -fsSL "$url"
}

# Runs dotnet-format analyzers limited to changed C# files to generate the report artifact.
run_dotnet_format_for_changes() {
  local solutionPath="$1"
  local fileList
  fileList=$(git diff --name-only HEAD -- '*.cs' | paste -sd' ' -)

  if [[ -z "$fileList" ]]; then
    log_status "No changed C# files detected; skipping analyzer run"
    exit 1
  fi

  log_status "Running dotnet format analyzers on:"
  IFS=' ' read -r -a files <<< "$fileList"
  for file in "${files[@]}"; do
    echo "$file"
  done

  # dotnet-format CLI consumes external analyzers for consistency with IDE diagnostics.
  dotnet format analyzers "$solutionPath" --no-restore --verify-no-changes --include $fileList --report "$REPORT_OUT"
}

# Authenticates the GitHub CLI using the provided personal access token.
authenticate_github() {
  local ghToken="$1"
  log_status "Authenticating GitHub CLI session"
  printf "%s" "$ghToken" | tr -d '\r' | gh auth login --with-token
}

# Invokes Copilot CLI to summarize formatting diagnostics using the generated JSON report.
run_format_prompt() {
  local formatPrompt="$1"
  log_status "Running Copilot format prompt to summarize analyzer findings"
  copilot -p "$formatPrompt @$REPORT_OUT/format-report.json. Save output to the file $REPORT_OUT/format-report.md" --yolo --model gpt-5.1-codex-mini
}

# Captures original file content plus diffs for each changed C# file under the _changes folder.
collect_file_diffs() {
  log_status "Collecting file diffs for changed C# files"
  mapfile -t files < <(git diff --name-only HEAD -- '*.cs')

  for file in "${files[@]}"; do
    local target_path="$OUTPUT_DIR/$file"
    local target_dir
    target_dir="$(dirname "$target_path")"
    mkdir -p "$target_dir"

    {
      echo "FILE: $file"
      echo
      echo "----- ORIGINAL (HEAD) -----"
      if git cat-file -e "HEAD:$file" 2>/dev/null; then
        git show "HEAD:$file"
      else
        echo "[File not present in HEAD]"
      fi
      echo
      echo "----- DIFF -----"
      git diff HEAD -- "$file"
    } > "$target_path"

  done
}

# Restores the contributor branch locally so Copilot reviews the current remote state.
restore_branch_state() {
  local branchName="$1"
  log_status "Restoring branch state for $branchName"
  git checkout -B "$branchName" "origin/$branchName"
}

# Invokes Copilot CLI to perform the review prompt against the assembled change snapshots.
run_review_prompt() {
  local reviewPrompt="$1"
  log_status "Running Copilot review prompt on collected diffs"
  copilot -p "${reviewPrompt} @$OUTPUT_DIR. save results to $REPORT_OUT/review-report.md" --yolo --model gpt-5.2
}

# Deletes the _changes folder so subsequent runs start clean.
cleanup_change_artifacts() {
  log_status "Cleaning up change artifacts"
  rm -rf "$OUTPUT_DIR"
}

main() {
  log_status "Starting automated review workflow"
  local -a parsedArgs
  mapfile -t parsedArgs < <(validate_inputs "$@")
  local ghToken="${parsedArgs[0]}"
  local baseBranchName="${parsedArgs[1]}"
  local branchName="${parsedArgs[2]}"
  local solutionPath="${parsedArgs[3]}"
  local formatPromptToggle="${parsedArgs[4]}"

  local formatPrompt
  local reviewPrompt

  prepare_branch_state "$baseBranchName" "$branchName"
  recreate_directory "$REPORT_OUT"

  if [[ "$formatPromptToggle" == "enable" ]]; then
    run_dotnet_format_for_changes "$solutionPath"
    authenticate_github "$ghToken"
    formatPrompt=$(download_prompt "$FORMAT_PROMPT_URL")
    run_format_prompt "$formatPrompt"
  else
    log_status "Format prompt disabled; skipping analyzer and summary steps"
  fi

  recreate_directory "$OUTPUT_DIR"
  collect_file_diffs

  reviewPrompt=$(download_prompt "$REVIEW_PROMPT_URL")
  run_review_prompt "$reviewPrompt"
  cleanup_change_artifacts
  restore_branch_state "$branchName"
  log_status "Review workflow completed"
}

main "$@"
