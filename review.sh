#!/usr/bin/env bash
readonly REPORT_OUT="./report"
readonly OUTPUT_DIR="_changes"
readonly FORMAT_PROMPT_URL="https://raw.githubusercontent.com/antshc/copilot-code-analyzer/main/prompts/format.prompt.md"
readonly REVIEW_PROMPT_URL="https://raw.githubusercontent.com/antshc/copilot-code-analyzer/main/prompts/review.prompt.md"

# Validates required inputs and environment so later steps fail fast.
validate_inputs() {
  local ghTokenArg="${1:-}"
  local branchName="${2:-}"
  if [[ -z "$ghTokenArg" || -z "$branchName" ]]; then
    echo "Usage: $0 GH_TOKEN BRANCH_NAME" >&2
    exit 1
  fi

  printf "%s\n%s" "$ghTokenArg" "$branchName" 
}

# Synchronizes to the remote branch and rewinds to the merge-base for a clean diff.
prepare_branch_state() {
  local branchName="$1"
  git fetch
  git checkout "origin/$branchName"
  git reset --soft "$(git merge-base HEAD origin/main)"
}

# Removes and recreates a working directory to ensure deterministic outputs.
recreate_directory() {
  local targetDir="$1"
  rm -rf "$targetDir"
  mkdir -p "$targetDir"
}

# Downloads prompt text via curl so Copilot invocations stay up to date.
download_prompt() {
  local url="$1"
  curl -fsSL "$url"
}

# Runs dotnet-format analyzers limited to changed C# files to generate the report artifact.
run_dotnet_format_for_changes() {
  local fileList
  fileList=$(git diff --name-only HEAD -- '*.cs' | paste -sd' ' -)
  echo "$fileList"

  if [[ -z "$fileList" ]]; then
    dotnet format analyzers "./CodeSmellApp/CodeSmellApp.sln" --no-restore --verify-no-changes --report "$REPORT_OUT"
    return
  fi

  # dotnet-format CLI consumes external analyzers for consistency with IDE diagnostics.
  dotnet format analyzers "./CodeSmellApp/CodeSmellApp.sln" --no-restore --verify-no-changes --include $fileList --report "$REPORT_OUT"
}

# Authenticates the GitHub CLI using the provided personal access token.
authenticate_github() {
  local ghToken="$1"
  printf "%s" "$ghToken" | tr -d '\r' | gh auth login --with-token
}

# Invokes Copilot CLI to summarize formatting diagnostics using the generated JSON report.
run_format_prompt() {
  local formatPrompt="$1"
  copilot -p "$formatPrompt @$REPORT_OUT/format-report.json. Save output to the file $REPORT_OUT/format-report.md" --yolo --model gpt-5.1-codex-mini
}

# Captures original file content plus diffs for each changed C# file under the _changes folder.
collect_file_diffs() {
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
  git checkout -B "$branchName" "origin/$branchName"
}

# Invokes Copilot CLI to perform the review prompt against the assembled change snapshots.
run_review_prompt() {
  local reviewPrompt="$1"
  copilot -p "${reviewPrompt} @$OUTPUT_DIR. save results to $REPORT_OUT/review-report.md" --yolo --model gpt-5.2
}

# Deletes the _changes folder so subsequent runs start clean.
cleanup_change_artifacts() {
  rm -rf "$OUTPUT_DIR"
}

main() {
  local -a parsedArgs
  mapfile -t parsedArgs < <(validate_inputs "$@")
  local ghToken="${parsedArgs[0]}"
  local branchName="${parsedArgs[1]}"

  local formatPrompt
  local reviewPrompt

  prepare_branch_state "$branchName"
  recreate_directory "$REPORT_OUT"
  formatPrompt=$(download_prompt "$FORMAT_PROMPT_URL")
  reviewPrompt=$(download_prompt "$REVIEW_PROMPT_URL")

  run_dotnet_format_for_changes
  authenticate_github "$ghToken"
  run_format_prompt "$formatPrompt"

  recreate_directory "$OUTPUT_DIR"
  collect_file_diffs

  run_review_prompt "$reviewPrompt"
  cleanup_change_artifacts
  restore_branch_state "$branchName"
}

main "$@"
