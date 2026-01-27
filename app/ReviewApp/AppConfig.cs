namespace ReviewApp;

public sealed record AppConfig(
    string CopilotToken,
    string BaseBranchName,
    string BranchName,
    bool AnalyzersEnabled);
