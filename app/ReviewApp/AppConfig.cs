namespace ReviewApp;

public sealed record AppConfig(
    string CopilotToken,
    string BaseBranchName,
    string BranchName,
    string ReviewPrompt,
    string CodeAnalysisReportPrompt,
    string EditorConfig,
    bool AnalyzersEnabled);
