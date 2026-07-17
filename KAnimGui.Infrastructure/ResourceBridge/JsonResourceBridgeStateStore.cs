using System.Text.Json;
using KAnimGui.Application.ResourceBridge;

namespace KAnimGui.Infrastructure.ResourceBridge;

public sealed class JsonResourceBridgeStateStore : IResourceBridgeStateStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IApplicationPathProvider paths;

    public JsonResourceBridgeStateStore(IApplicationPathProvider paths)
    {
        this.paths = paths ?? throw new ArgumentNullException(nameof(paths));
    }

    public async Task<BridgeState> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(paths.ResourceBridgeStateFilePath))
        {
            return BridgeState.Empty;
        }

        try
        {
            await using FileStream stream = new(
                paths.ResourceBridgeStateFilePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                4096,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            StateFile? file = await JsonSerializer.DeserializeAsync<StateFile>(
                stream,
                JsonOptions,
                cancellationToken).ConfigureAwait(false);

            if (file == null || file.SchemaVersion != 1)
            {
                return BridgeState.Empty;
            }

            var favorites = new HashSet<string>(
                file.FavoriteResourceIds ?? [],
                StringComparer.OrdinalIgnoreCase);
            var tags = (file.ResourceTags ?? new Dictionary<string, List<string>>())
                .Where(pair => !string.IsNullOrWhiteSpace(pair.Key))
                .ToDictionary(
                    pair => pair.Key,
                    pair => (IReadOnlyList<string>)(pair.Value ?? [])
                        .Where(tag => !string.IsNullOrWhiteSpace(tag))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase)
                        .ToList(),
                    StringComparer.OrdinalIgnoreCase);
            BridgeExportLayout layout = Enum.IsDefined(file.ExportLayout)
                ? file.ExportLayout
                : BridgeExportLayout.Grouped;

            return new BridgeState(1, favorites, tags, layout);
        }
        catch (JsonException)
        {
            await QuarantineCorruptStateAsync(cancellationToken).ConfigureAwait(false);
            return BridgeState.Empty;
        }
        catch (IOException)
        {
            return BridgeState.Empty;
        }
    }

    public async Task SaveAsync(BridgeState state, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);
        string? directory = Path.GetDirectoryName(paths.ResourceBridgeStateFilePath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new InvalidOperationException("资源桥状态文件没有有效的目录。");
        }

        Directory.CreateDirectory(directory);
        var file = new StateFile(
            1,
            state.FavoriteResourceIds
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Order(StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            state.ResourceTags
                .Where(pair => !string.IsNullOrWhiteSpace(pair.Key))
                .ToDictionary(
                    pair => pair.Key,
                    pair => pair.Value
                        .Where(tag => !string.IsNullOrWhiteSpace(tag))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .Order(StringComparer.OrdinalIgnoreCase)
                        .ToList(),
                    StringComparer.OrdinalIgnoreCase),
            state.ExportLayout);
        string temporaryPath = ResourceBridgePath.TemporaryPath(paths.ResourceBridgeStateFilePath);

        try
        {
            await using (FileStream stream = new(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                4096,
                FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                await JsonSerializer.SerializeAsync(stream, file, JsonOptions, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            File.Move(temporaryPath, paths.ResourceBridgeStateFilePath, true);
        }
        finally
        {
            TryDelete(temporaryPath);
        }
    }

    private async Task QuarantineCorruptStateAsync(CancellationToken cancellationToken)
    {
        string quarantinePath = paths.ResourceBridgeStateFilePath +
            ".corrupt-" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff") + ".json";
        try
        {
            await Task.Run(
                () => File.Move(paths.ResourceBridgeStateFilePath, quarantinePath, true),
                cancellationToken).ConfigureAwait(false);
        }
        catch (IOException)
        {
            // A corrupt state file must not prevent the application from starting.
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
    }

    private sealed record StateFile(
        int SchemaVersion,
        string[]? FavoriteResourceIds,
        Dictionary<string, List<string>>? ResourceTags,
        BridgeExportLayout ExportLayout);
}
