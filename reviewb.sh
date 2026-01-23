#!/usr/bin/env bash
set -euo pipefail

readonly SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
readonly REVIEW_SCRIPT="$SCRIPT_DIR/review.sh"

if [[ -z "${GH_TOKEN:-}" ]]; then
  echo "GH_TOKEN environment variable must be set before running this wrapper." >&2
  exit 1
fi

branchName="${1:-}"
if [[ -z "$branchName" ]]; then
  echo "Usage: $0 BRANCH_NAME" >&2
  exit 1
fi

"$REVIEW_SCRIPT" "$GH_TOKEN" "$branchName"
