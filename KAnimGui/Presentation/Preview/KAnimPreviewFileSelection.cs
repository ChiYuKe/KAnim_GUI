namespace KAnimGui.Presentation.Preview;

public enum KAnimPreviewFileKind
{
    Texture,
    Anim,
    Build
}

public sealed record KAnimPreviewFileEntry(string Path, KAnimPreviewFileKind Kind)
{
    public string FileName => System.IO.Path.GetFileName(Path);
}

/// <summary>
/// Keeps the three files that make up one preview package independent from WPF dialogs.
/// </summary>
public sealed record KAnimPreviewFileSelection(
    string? TextureFile,
    string? AnimFile,
    string? BuildFile)
{
    public static KAnimPreviewFileSelection Empty { get; } = new(null, null, null);

    public bool HasAnyFile => TextureFile is not null || AnimFile is not null || BuildFile is not null;
    public bool IsComplete => TextureFile is not null && AnimFile is not null && BuildFile is not null;

    public IReadOnlyList<KAnimPreviewFileEntry> Entries => new[]
    {
        TextureFile is null ? null : new KAnimPreviewFileEntry(TextureFile, KAnimPreviewFileKind.Texture),
        AnimFile is null ? null : new KAnimPreviewFileEntry(AnimFile, KAnimPreviewFileKind.Anim),
        BuildFile is null ? null : new KAnimPreviewFileEntry(BuildFile, KAnimPreviewFileKind.Build)
    }.Where(entry => entry is not null).Select(entry => entry!).ToList();

    public KAnimPreviewFileSelection AddFiles(IEnumerable<string> paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        string? texture = TextureFile;
        string? anim = AnimFile;
        string? build = BuildFile;

        foreach (var path in paths.Where(path => !string.IsNullOrWhiteSpace(path)))
        {
            string fileName = System.IO.Path.GetFileName(path);
            if (fileName.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            {
                texture = path;
            }
            else if (fileName.EndsWith("_anim.bytes", StringComparison.OrdinalIgnoreCase))
            {
                anim = path;
            }
            else if (fileName.EndsWith("_build.bytes", StringComparison.OrdinalIgnoreCase))
            {
                build = path;
            }
        }

        return new KAnimPreviewFileSelection(texture, anim, build);
    }
}
