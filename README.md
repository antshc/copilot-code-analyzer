## Create patch
```
### Create patch from the feature branch
git format-patch main..feature_branch
### Apply patch on the main branch
git apply --3way --whitespace=fix 0001-Describe-PrioritizedException.patch
### Click review button on the commit
```

## Make changes available for review vscode
```
checkout the branch locally
create a new branch based on that branch (just to be safe)
use git reset --soft $(git merge-base HEAD origin/main)
Then all the changes or staged and available for the #changes
```

## Use dotnet format to review the file
```
# install copilot cli
winget install GitHub.Copilot

# login
gh auth login

echo $GH_TOKEN | gh auth login --with-token

grep -Po 'CA\d+(?=.*= *error)' .editorconfig | paste -sd, -
dotnet format analyzers "./project.sln" --no-restore --verify-no-changes --include $(git diff --name-only HEAD -- '*.cs' | paste -sd' ' -) --report ./

# get rules with error severity from the file, use space seperation
grep -Po 'CA\d+(?=.*= *error)' .editorconfig | paste -sd' ' -

echo $GH_TOKEN | gh auth login --with-token
copilot -p "Read @test_r, do code review*" --yolo --model gpt-5.2 > output.md

echo $GH_TOKEN | gh auth login --with-token
copilot -p "Read @format-report.json report, use data from "FileName", "FilePath", "FileChanges" fields to generate code review report in md format. create file with review report.
Use following template:
### Issue description
- **FilePath:**
- **Line(s):**
- **Problem:**
- *Review comment to add in the PR*" --yolo --model gpt-5.2 > output.md


# Get git changes, use space seperation
git diff --name-only HEAD -- '*.cs'
git diff --name-only HEAD -- '*.cs' | paste -sd' ' -
```

```
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
```

### Helpers
```
git am --abort
```
