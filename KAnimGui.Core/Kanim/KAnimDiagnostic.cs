namespace KAnimGui.Core.Kanim;

public sealed record KAnimDiagnostic(
    KAnimDiagnosticSeverity Severity,
    string Code,
    string Location,
    string Message);
