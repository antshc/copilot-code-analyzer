#!/usr/bin/env bash

BRANCH_NAME="${1}"
if [[ -z "$BRANCH_NAME" ]]; then
  echo "Usage: $0 BRANCH_NAME"
  exit 1
fi

git fetch
git checkout "origin/$BRANCH_NAME"
git reset --soft $(git merge-base HEAD origin/main)

OUTPUT_DIR="tests_results/_changes"

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
printf "%s" "$GH_TOKEN" | tr -d '\r' | gh auth login --with-token
# gh auth status
copilot -p "Read @tests_results/_changes, do code review*" --yolo --model gpt-5.2 > output1.md