#!/bin/bash
readonly REPO_ROOT="$(git rev-parse --show-toplevel 2>/dev/null)"
readonly REPORT_OUT="$REPO_ROOT/report"
readonly OUTPUT_DIR="$REPO_ROOT/_changes"
readonly FORMAT_PROMPT_URL="https://raw.githubusercontent.com/antshc/copilot-code-analyzer/main/prompts/format.prompt.md"
readonly REVIEW_PROMPT_URL="https://raw.githubusercontent.com/antshc/copilot-code-analyzer/main/prompts/review.prompt.md"
readonly MINIMAL_EDITORCONFIG_URL="https://raw.githubusercontent.com/antshc/copilot-code-analyzer/main/rules/minimal.editorconfig"
readonly EDITORCONFIG_PATH="$REPO_ROOT/.editorconfig"
readonly EDITORCONFIG_BACKUP_PATH="$REPO_ROOT/.editorconfig.backup"
EDITORCONFIG_TEMP_APPLIED=0

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
    echo "Usage: $0 GH_TOKEN BASE_BRANCH_NAME BRANCH_NAME SOLUTION_PATH [-format enable|disable]" >&2
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

# Determines the .csproj that owns a given source file by walking up the directory tree.
find_project_for_file() {
  local sourceFile="$1"
  local searchDir
  searchDir="$(dirname "$sourceFile")"

  while true; do
    local currentDir="$REPO_ROOT/$searchDir"
    if [[ -d "$currentDir" ]]; then
      shopt -s nullglob
      local candidates=("$currentDir"/*.csproj)
      shopt -u nullglob
      local candidatePath
      for candidatePath in "${candidates[@]}"; do
        local relativePath="${candidatePath#"$REPO_ROOT"/}"
        printf "%s\n" "$relativePath"
        return 0
      done
    fi

    if [[ "$searchDir" == "." || "$searchDir" == "/" ]]; then
      break
    fi

    searchDir="$(dirname "$searchDir")"
  done

  return 1
}

# Runs analyzer-enabled dotnet builds limited to changed C# files to generate the report artifact.
run_analyzer_build_for_changes() {
  local solutionPath="$1"
  local -a changedFiles
  mapfile -t changedFiles < <(git diff --name-only HEAD -- '*.cs')

  if [[ ${#changedFiles[@]} -eq 0 ]]; then
    log_status "No changed C# files detected; skipping analyzer run"
    exit 1
  fi

  declare -A projectFileMap
  local sourceFile
  for sourceFile in "${changedFiles[@]}"; do
    local projectPath
    if ! projectPath=$(find_project_for_file "$sourceFile"); then
      echo "Unable to resolve a .csproj for $sourceFile; skipping" >&2
      continue
    fi

    if [[ -n "${projectFileMap[$projectPath]}" ]]; then
      projectFileMap["$projectPath"]+=$'\n'
    fi
    projectFileMap["$projectPath"]+="$sourceFile"
  done

  if [[ ${#projectFileMap[@]} -eq 0 ]]; then
    echo "Changed files detected but none matched a project; aborting analyzer run" >&2
    exit 1
  fi

  apply_minimal_editorconfig
  sleep 2

  local projectPath
  for projectPath in "${!projectFileMap[@]}"; do
    mapfile -t projectFiles <<< "${projectFileMap[$projectPath]}"
    log_status "Running analyzer-enabled build for $projectPath."
    local trackedFile
    local -a includeArgs
    includeArgs=()
    for trackedFile in "${projectFiles[@]}"; do
      includeArgs+=("$trackedFile")
    done

    log_status "run_analyzers_for_project for $projectPath."
    local startTime endTime elapsed
    startTime=$(date +%s)
    run_analyzers_for_project "$projectPath" includeArgs
    endTime=$(date +%s)
    elapsed=$((endTime - startTime))
    log_status "run_analyzers_for_project for $projectPath completed in ${elapsed} seconds."
  done

  local -a reportFiles
  mapfile -t reportFiles < <(find "$REPORT_OUT" -maxdepth 1 -name '*-format-report.json' -print | sort)

  if [[ ${#reportFiles[@]} -gt 0 ]]; then
    if ! jq -s '{projectReports: .}' "${reportFiles[@]}" > "$REPORT_OUT/format-report.json"; then
      echo "Failed to merge analyzer reports into $REPORT_OUT/format-report.json" >&2
    fi
  fi

  restore_editorconfig_state
  return 0
}

run_analyzers_for_project() {
  local projectPath="$1"
  local -n includeArgsRef=$2
  local projectName
  projectName="$(basename "$projectPath" .csproj)"
  local reportPath
  reportPath="$REPORT_OUT/${projectName}-format-report.json"
  local diagPath
  diagPath="$REPORT_OUT/${projectName}-diag.txt"
  local buildLogPath
  buildLogPath="$REPORT_OUT/${projectName}-build.log"

  local -a buildArgs
  buildArgs=(
    build "$projectPath"
    -p:EnableNETAnalyzers=true
    -p:AnalysisMode=Recommended
    -p:EnforceCodeStyleInBuild=true
    -p:AnalysisLevel=latest
    -p:TreatWarningsAsErrors=false
    -p:GenerateDocumentationFile=true
  )

  log_status "dotnet ${buildArgs[*]}"
  : > "$buildLogPath"

  dotnet "${buildArgs[@]}" >"$buildLogPath"

  local filteredOutput=""
  if [[ ${#includeArgsRef[@]} -gt 0 ]]; then
    local -a grepArgs
    grepArgs=(-F)
    local trackedFile
    for trackedFile in "${includeArgsRef[@]}"; do
      fileName="$(basename "$trackedFile")"
      grepArgs+=(-e "$fileName")
    done

    if ! filteredOutput=$(grep "${grepArgs[@]}" "$buildLogPath"); then
      filteredOutput=""
    fi
  fi

  echo "========================================================"
  echo "$filteredOutput"

  : > "$diagPath"
  declare -A fileDiagnostics
  local -a diagFileOrder
  diagFileOrder=()

  if [[ -n "$filteredOutput" ]]; then
    while IFS= read -r line; do
      [[ -z "$line" ]] && continue
      local filePath
      filePath="${line%%(*}"
      filePath="${filePath#${filePath%%[![:space:]]*}}"
      filePath="${filePath%${filePath##*[![:space:]]}}"
      if [[ -z "$filePath" ]]; then
        filePath="(unknown)"
      fi
      if [[ -z "${fileDiagnostics[$filePath]+set}" ]]; then
        diagFileOrder+=("$filePath")
      fi
      fileDiagnostics["$filePath"]+="$line"$'\n'
    done <<< "$filteredOutput"
  fi

  local hasDiagnostics=0
  local diagFile
  for diagFile in "${diagFileOrder[@]}"; do
    local diagLines="${fileDiagnostics[$diagFile]}"
    [[ -z "$diagLines" ]] && continue
    {
      printf 'FilePath: %s\n' "$diagFile"
      printf 'Diagnostics:\n'
      printf '%s' "$diagLines"
      printf '\n'
    } >> "$diagPath"
    hasDiagnostics=1
  done

  if [[ $hasDiagnostics -eq 0 ]]; then
    printf 'FilePath: (none)\nDiagnostics:\n' > "$diagPath"
  fi
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

# Temporarily enforces the curated minimal .editorconfig so dotnet format runs deterministically.
apply_minimal_editorconfig() {
  if [[ $EDITORCONFIG_TEMP_APPLIED -eq 1 ]]; then
    return
  fi

  if [[ -f "$EDITORCONFIG_PATH" ]]; then
    log_status "Backing up existing .editorconfig before running dotnet format"
    cp "$EDITORCONFIG_PATH" "$EDITORCONFIG_BACKUP_PATH"
  fi

  log_status "Downloading minimal .editorconfig used solely for analyzer execution"
  if ! curl -fsSL "$MINIMAL_EDITORCONFIG_URL" -o "$EDITORCONFIG_PATH"; then
    log_status "Failed to download minimal .editorconfig; aborting format run"
    if [[ -f "$EDITORCONFIG_BACKUP_PATH" ]]; then
      mv "$EDITORCONFIG_BACKUP_PATH" "$EDITORCONFIG_PATH"
    fi
    exit 1
  fi

  EDITORCONFIG_TEMP_APPLIED=1
}

# Restores the contributor's .editorconfig (or removes the temporary file) after formatting completes.
restore_editorconfig_state() {
  if [[ $EDITORCONFIG_TEMP_APPLIED -ne 1 ]]; then
    return
  fi

  if [[ -f "$EDITORCONFIG_BACKUP_PATH" ]]; then
    log_status "Restoring original .editorconfig after dotnet format run"
    mv "$EDITORCONFIG_BACKUP_PATH" "$EDITORCONFIG_PATH"
  else
    log_status "Removing temporary .editorconfig to leave the repo unchanged"
    rm -f "$EDITORCONFIG_PATH"
  fi

  EDITORCONFIG_TEMP_APPLIED=0
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
  sleep 2
  recreate_directory "$REPORT_OUT"

  if [[ "$formatPromptToggle" == "enable" ]]; then
    run_analyzer_build_for_changes "$solutionPath"
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
