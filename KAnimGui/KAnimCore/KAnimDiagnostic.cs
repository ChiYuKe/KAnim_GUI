namespace KAnimGui.KAnimCore
{
    public sealed record KAnimDiagnostic(
        KAnimDiagnosticSeverity Severity,
        string Code,
        string Location,
        string Message);
}
