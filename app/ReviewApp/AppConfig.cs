namespace ReviewApp;

public sealed record AppConfig(
    string CopilotToken,
    string BaseBranchName,
    string BranchName,
    string ReviewPrompt,
    string EditorConfig,
    bool AnalyzersEnabled);
