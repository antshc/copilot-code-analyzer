#!/bin/bash
set -e

PR_URL="$1"

if [[ -z "$PR_URL" ]]; then
  echo "Usage: $0 <pull-request-url>"
  exit 1
fi

# Must be run inside repo
REPO_ROOT=$(git rev-parse --show-toplevel)

echo "Getting C# files from PR (skipping deleted)..."

mapfile -t FILES < <(
  gh pr view "$PR_URL" --json files --jq '
    .files[]
    | select(.status != "removed")
    | .path
    | select(endswith(".cs"))
  '
)

if [[ ${#FILES[@]} -eq 0 ]]; then
  echo "No C# files found in PR"
  exit 0
fi

echo "Opening ${#FILES[@]} files in Rider..."

# Open repo + all files in existing Rider instance
rider64.exe "$REPO_ROOT" "${FILES[@]/#/$REPO_ROOT/}"
