#!/usr/bin/env bash

BRANCH_NAME="${1}"
if [[ -z "$BRANCH_NAME" ]]; then
  echo "Usage: $0 BRANCH_NAME"
  exit 1
fi

git fetch
git checkout "origin/$BRANCH_NAME"
git reset --soft $(git merge-base HEAD origin/main)

REPORT_OUT="./report"
rm -rf "$REPORT_OUT"
mkdir -p "$REPORT_OUT"
FORMAT_PROMPT=$(curl -fsSL https://raw.githubusercontent.com/antshc/copilot-code-analyzer/main/prompts/format.prompt.md)
FILES=$(git diff --name-only HEAD -- '*.cs' | paste -sd' ' -)
echo $FILES
dotnet format analyzers "./CodeSmellApp/CodeSmellApp.sln" --no-restore --verify-no-changes --include $FILES --report "$REPORT_OUT"
# echo $FORMAT_PROMPT
printf "%s" "$GH_TOKEN" | tr -d '\r' | gh auth login --with-token
copilot -p "$FORMAT_PROMPT @$REPORT_OUT/format-report.json. Save output to the file $REPORT_OUT/format-report.md" --yolo --model gpt-5.2

OUTPUT_DIR="_changes"

rm -rf "$OUTPUT_DIR"
mkdir -p "$OUTPUT_DIR"

mapfile -t files < <(git diff --name-only HEAD -- '*.cs')

for file in "${files[@]}"; do
  target_path="$OUTPUT_DIR/$file"
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

git checkout -B $BRANCH_NAME origin/$BRANCH_NAME


REVIEW_PROMPT=$(curl -fsSL https://raw.githubusercontent.com/antshc/copilot-code-analyzer/main/prompts/review.prompt.md)
# gh auth status
copilot -p "${REVIEW_PROMPT} @_changes. save results to review-report.md" --yolo --model gpt-5.2
rm -rf "$OUTPUT_DIR"