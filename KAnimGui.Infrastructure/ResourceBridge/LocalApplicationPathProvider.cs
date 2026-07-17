using KAnimGui.Application.ResourceBridge;

namespace KAnimGui.Infrastructure.ResourceBridge;

public sealed class LocalApplicationPathProvider : IApplicationPathProvider
{
    private readonly string localApplicationData;
    private readonly string documents;

    public LocalApplicationPathProvider(
        string? localApplicationData = null,
        string? documents = null)
    {
        this.localApplicationData = localApplicationData ??
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        this.documents = documents ??
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
    }

    public string StatusFilePath => Path.Combine(
        localApplicationData,
        "KAnimGui",
        "ONIResourceBridge.json");

    public string ResourceBridgeStateFilePath => Path.Combine(
        localApplicationData,
        "KAnimGui",
        "ResourceBridgeState.json");

    public string ResourceBridgeCacheDirectory => Path.Combine(
        localApplicationData,
        "KAnimGui",
        "Cache",
        "ResourceBridge");

    public string ResourceBridgeExportDirectory => Path.Combine(
        documents,
        "KSE_Output",
        "ONI_Bridge");
}
