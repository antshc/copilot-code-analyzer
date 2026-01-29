#!/bin/bash
set -euo pipefail

readonly SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
readonly REVIEW_SCRIPT="$SCRIPT_DIR/artifactory/reviewv2.exe"

if [[ -z "${GH_TOKEN:-}" ]]; then
  echo "GH_TOKEN environment variable must be set before running this wrapper." >&2
  exit 1
fi

branchName="${1:-}"
if [[ -z "$branchName" ]]; then
  echo "Usage: $0 BRANCH_NAME" >&2
  exit 1
fi

REVIEW_PROMPT=$(curl -fsSL https://raw.githubusercontent.com/antshc/copilot-code-analyzer/refs/heads/main/prompts/review.prompt.md)
EDITORCONFIG=$(curl -fsSL https://raw.githubusercontent.com/antshc/copilot-code-analyzer/refs/heads/main/rules/minimal.editorconfig)

"$REVIEW_SCRIPT" --token "$GH_TOKEN" --base-branch "main" --branch "$branchName" --review-prompt "$REVIEW_PROMPT" --editorconfig "$EDITORCONFIG" --analyzers enable
