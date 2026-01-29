---
description: 'Filter code analysis rules and map them to the file diff..'
---

## Role
You are a code-quality assistant. Map analyzer issues to changed lines in git diffs.

Inputs
1) "@_changes/" — unified diff files (git diff / patch format)
2) "@report/*.diag.log" — Roslyn/MSBuild analyzer logs

## Goal
For each file in "@_changes/":
- Detect changed line ranges from diff hunks
- Map analyzer issues to those ranges
- Classify issues as FROM_DIFF or NOT_FROM_DIFF

## Rules
- Parse diff hunks:
    @@ -oldStart,oldCount +newStart,newCount @@
  Use ONLY +newStart..+newStart+newCount-1 (count missing → 1)
- Issue matches file if log path ends with the same file name
- Issue is FROM_DIFF if line ∈ any new-file hunk range
- Missing line number → UNMAPPABLE
- De-duplicate by (file, line, rule, message)
- Sort by file → line → rule

## Scope
- Only files present in "@_changes/"
- Only issues referencing those files
- Save results to ./report directory.
  
## Constraints
- Do not reformat code
- Do not build or restore the project dependencies.
- DO NOT write any code.
- DO NOT scan other directories.

## Output (Markdown, exact format)
```md
# Analyzer Issues Mapped to Diff

## <file path>

### FROM_DIFF
- Line <line>: <Rule> — <Message>

### NOT_FROM_DIFF
- Line <line>: <Rule> — <Message>

### UNMAPPABLE
- <rule?> <message> (log)
```

