using KanimLib;
using KAnimGui.Core.Kanim;

namespace KAnimGui.KAnimCore;

/// <summary>
/// WPF package adapter for the UI-neutral diagnostics engine.
/// </summary>
public static class KAnimDiagnostics
{
    public static IReadOnlyList<KAnimDiagnostic> Analyze(KAnimPackage package)
    {
        ArgumentNullException.ThrowIfNull(package);
        var corePackage = ToCorePackage(package);
        return KAnimDiagnosticsEngine.Analyze(corePackage)
            .Select(diagnostic => new KAnimDiagnostic(
                (KAnimDiagnosticSeverity)diagnostic.Severity,
                diagnostic.Code,
                diagnostic.Location,
                diagnostic.Message))
            .ToList();
    }

    public static string FormatReport(KAnimPackage package, IReadOnlyList<KAnimDiagnostic> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(diagnostics);
        var summary = KAnimSummary.FromPackage(package);
        var lines = new List<string>
        {
            "KAnim 诊断报告",
            string.Empty,
            $"Texture: {summary.TextureSummary}",
            $"Build: {summary.BuildSummary}",
            $"Anim: {summary.AnimSummary}",
            string.Empty,
            "问题列表:"
        };

        foreach (var diagnostic in diagnostics)
        {
            lines.Add($"[{diagnostic.Severity}] {diagnostic.Code} @ {diagnostic.Location}: {diagnostic.Message}");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static KAnimDataPackage ToCorePackage(KAnimPackage package) =>
        new(package.Build, package.Anim, package.Texture?.PixelWidth, package.Texture?.PixelHeight);
}
