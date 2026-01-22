# Code Analysis Report

---

## Findings (grouped by file)

{{#each Files}}

### `{{FileName}}`

**FilePath:** `{{FilePath}}`

{{#each FileChanges}}

#### {{DiagnosticId}} — {{Severity}}

* **Line, Char:** [{{LineNumber}}, {{CharNumber}}]
* **Message:** {{Message}}
* **Problem:** {{ProblemDescription}}
* *Review comment:* {{ReviewComment}}

{{/each}}
{{/each}}
