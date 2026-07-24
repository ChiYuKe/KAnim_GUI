using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KAnimGui.Application.Conversion;
using KAnimGui.Application.Platform;

namespace KAnimGui.Presentation.Conversion;

public partial class ConversionWorkspaceViewModel : ObservableObject, IDisposable
{
    private readonly IConversionService conversionService;
    private readonly IInputFilePreparer inputFilePreparer;
    private readonly IFileSystemGateway fileSystem;
    private readonly IOperationLogSink operationLogSink;
    private CancellationTokenSource? kanimCancellation;
    private CancellationTokenSource? scmlCancellation;

    public ConversionWorkspaceViewModel(
        IConversionService conversionService,
        IInputFilePreparer inputFilePreparer,
        IFileSystemGateway fileSystem,
        IOperationLogSink? operationLogSink = null)
    {
        this.conversionService = conversionService ?? throw new ArgumentNullException(nameof(conversionService));
        this.inputFilePreparer = inputFilePreparer ?? throw new ArgumentNullException(nameof(inputFilePreparer));
        this.fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        this.operationLogSink = operationLogSink ?? NullOperationLogSink.Instance;
        KanimLog.CollectionChanged += (_, _) => OnPropertyChanged(nameof(KanimLogText));
        ScmlLog.CollectionChanged += (_, _) => OnPropertyChanged(nameof(ScmlLogText));
        ConvertKanimCommand = new AsyncRelayCommand(ConvertKanimAsync, CanConvertKanim);
        ConvertScmlCommand = new AsyncRelayCommand(ConvertScmlAsync, CanConvertScml);
        CancelKanimCommand = new RelayCommand(() => kanimCancellation?.Cancel(), () => IsKanimBusy);
        CancelScmlCommand = new RelayCommand(() => scmlCancellation?.Cancel(), () => IsScmlBusy);
        KanimOutputDirectory = ConversionOutputPathResolver.KanimToScmlDirectory;
        ScmlOutputDirectory = ConversionOutputPathResolver.ScmlToKanimDirectory;
    }

    public ObservableCollection<string> KanimLog { get; } = [];

    public ObservableCollection<string> ScmlLog { get; } = [];

    public string KanimLogText => string.Join(Environment.NewLine, KanimLog);

    public string ScmlLogText => string.Join(Environment.NewLine, ScmlLog);

    public event Action<string>? ConversionSucceeded;

    public void AppendKanimMessage(string message, bool isError = false)
    {
        AppendExternalMessage(KanimLog, message, isError, value => KanimStatus = value);
    }

    public void AppendScmlMessage(string message, bool isError = false)
    {
        AppendExternalMessage(ScmlLog, message, isError, value => ScmlStatus = value);
    }

    public async Task<ConversionBatchResult?> ConvertKanimBatchAsync(string folderPath)
    {
        if (IsKanimBusy || string.IsNullOrWhiteSpace(folderPath) || !fileSystem.DirectoryExists(folderPath))
        {
            return null;
        }

        List<KAnimGui.Core.KanimFileSet> fileSets = KAnimGui.Core.KanimFileMatcher
            .FindFileSets(folderPath, EnableTxtToBytes)
            .ToList();
        if (fileSets.Count == 0)
        {
            KanimStatus = "没有找到可转换的 KAnim 文件组。";
            return null;
        }

        KanimLog.Clear();
        kanimCancellation?.Dispose();
        kanimCancellation = new CancellationTokenSource();
        IsKanimBusy = true;
        try
        {
            var requests = new List<ConversionRequest>();
            foreach (KAnimGui.Core.KanimFileSet fileSet in fileSets)
            {
                try
                {
                    string animPath = await inputFilePreparer.PrepareBytesAsync(fileSet.AnimPath, EnableTxtToBytes, kanimCancellation.Token).ConfigureAwait(true);
                    string buildPath = await inputFilePreparer.PrepareBytesAsync(fileSet.BuildPath, EnableTxtToBytes, kanimCancellation.Token).ConfigureAwait(true);
                    requests.Add(new KanimToScmlRequest(fileSet.PngPath, animPath, buildPath, KanimOutputDirectory, StrictOrder, StrictMode));
                }
                catch (Exception ex) when (ex is IOException or InvalidOperationException)
                {
                    KanimLog.Add($"[错误] {fileSet.Name}：{ex.Message}");
                }
            }

            if (requests.Count == 0)
            {
                KanimStatus = "没有可执行的批量转换任务。";
                return null;
            }

            ConversionBatchResult result = await conversionService.ConvertBatchAsync(
                requests,
                new Progress<ConversionBatchProgress>(item =>
                {
                    if (IsKanimBusy)
                    {
                        KanimStatus = $"正在处理 {item.Completed} / {item.Total}: {item.Name}";
                    }
                }),
                new Progress<OperationEvent>(item => AppendLog(KanimLog, item, value =>
                {
                    if (IsKanimBusy)
                    {
                        KanimStatus = value;
                    }
                })),
                kanimCancellation.Token).ConfigureAwait(true);
            int successCount = result.Results.Count(item => item.Succeeded);
            KanimStatus = result.WasCanceled
                ? $"批量转换已取消：成功 {successCount} / {requests.Count}"
                : $"批量转换完成：成功 {successCount} / {requests.Count}";
            return result;
        }
        catch (OperationCanceledException)
        {
            KanimStatus = "批量转换已取消";
            return new ConversionBatchResult([], true);
        }
        finally
        {
            IsKanimBusy = false;
            kanimCancellation?.Dispose();
            kanimCancellation = null;
        }
    }

    public async Task<ConversionBatchResult?> ConvertScmlBatchAsync(IEnumerable<string> scmlPaths)
    {
        ArgumentNullException.ThrowIfNull(scmlPaths);
        List<string> paths = scmlPaths.Where(fileSystem.FileExists).ToList();
        if (IsScmlBusy || paths.Count == 0)
        {
            return null;
        }

        ScmlLog.Clear();
        scmlCancellation?.Dispose();
        scmlCancellation = new CancellationTokenSource();
        IsScmlBusy = true;
        try
        {
            var requests = paths
                .Select(path => new ScmlToKanimRequest(path, ScmlOutputDirectory, Interpolate, Debone))
                .Cast<ConversionRequest>()
                .ToList();
            ConversionBatchResult result = await conversionService.ConvertBatchAsync(
                requests,
                new Progress<ConversionBatchProgress>(item =>
                {
                    if (IsScmlBusy)
                    {
                        ScmlStatus = $"正在处理 {item.Completed} / {item.Total}: {item.Name}";
                    }
                }),
                new Progress<OperationEvent>(item => AppendLog(ScmlLog, item, value =>
                {
                    if (IsScmlBusy)
                    {
                        ScmlStatus = value;
                    }
                })),
                scmlCancellation.Token).ConfigureAwait(true);
            int successCount = result.Results.Count(item => item.Succeeded);
            ScmlStatus = result.WasCanceled
                ? $"批量转换已取消：成功 {successCount} / {requests.Count}"
                : $"批量转换完成：成功 {successCount} / {requests.Count}";
            return result;
        }
        catch (OperationCanceledException)
        {
            ScmlStatus = "批量转换已取消";
            return new ConversionBatchResult([], true);
        }
        finally
        {
            IsScmlBusy = false;
            scmlCancellation?.Dispose();
            scmlCancellation = null;
        }
    }

    public IAsyncRelayCommand ConvertKanimCommand { get; }

    public IAsyncRelayCommand ConvertScmlCommand { get; }

    public IRelayCommand CancelKanimCommand { get; }

    public IRelayCommand CancelScmlCommand { get; }

    [ObservableProperty]
    private string pngPath = string.Empty;

    [ObservableProperty]
    private string animPath = string.Empty;

    [ObservableProperty]
    private string buildPath = string.Empty;

    [ObservableProperty]
    private string kanimOutputDirectory = string.Empty;

    [ObservableProperty]
    private string scmlPath = string.Empty;

    [ObservableProperty]
    private string scmlOutputDirectory = string.Empty;

    [ObservableProperty]
    private bool strictOrder;

    [ObservableProperty]
    private bool strictMode;

    [ObservableProperty]
    private bool interpolate;

    [ObservableProperty]
    private bool debone;

    [ObservableProperty]
    private bool enableTxtToBytes;

    [ObservableProperty]
    private bool isKanimBusy;

    [ObservableProperty]
    private bool isScmlBusy;

    [ObservableProperty]
    private string kanimStatus = "就绪";

    [ObservableProperty]
    private string scmlStatus = "就绪";

    partial void OnPngPathChanged(string value) => ConvertKanimCommand.NotifyCanExecuteChanged();
    partial void OnAnimPathChanged(string value) => ConvertKanimCommand.NotifyCanExecuteChanged();
    partial void OnBuildPathChanged(string value) => ConvertKanimCommand.NotifyCanExecuteChanged();
    partial void OnKanimOutputDirectoryChanged(string value) => ConvertKanimCommand.NotifyCanExecuteChanged();
    partial void OnScmlPathChanged(string value) => ConvertScmlCommand.NotifyCanExecuteChanged();
    partial void OnScmlOutputDirectoryChanged(string value) => ConvertScmlCommand.NotifyCanExecuteChanged();

    partial void OnIsKanimBusyChanged(bool value)
    {
        ConvertKanimCommand.NotifyCanExecuteChanged();
        CancelKanimCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsScmlBusyChanged(bool value)
    {
        ConvertScmlCommand.NotifyCanExecuteChanged();
        CancelScmlCommand.NotifyCanExecuteChanged();
    }

    private async Task ConvertKanimAsync()
    {
        KanimStatus = "正在验证输入...";
        if (!ValidateFile(PngPath, "PNG") || !ValidateFile(AnimPath, "Anim") || !ValidateFile(BuildPath, "Build") || !ValidateDirectory(KanimOutputDirectory))
        {
            return;
        }

        kanimCancellation?.Dispose();
        kanimCancellation = new CancellationTokenSource();
        IsKanimBusy = true;
        KanimLog.Clear();
        try
        {
            string preparedAnimPath = await inputFilePreparer.PrepareBytesAsync(AnimPath, EnableTxtToBytes, kanimCancellation.Token).ConfigureAwait(true);
            string preparedBuildPath = await inputFilePreparer.PrepareBytesAsync(BuildPath, EnableTxtToBytes, kanimCancellation.Token).ConfigureAwait(true);
            AnimPath = preparedAnimPath;
            BuildPath = preparedBuildPath;
            var request = new KanimToScmlRequest(
                PngPath,
                preparedAnimPath,
                preparedBuildPath,
                KanimOutputDirectory,
                StrictOrder,
                StrictMode);
            ConversionExecutionResult result = await conversionService.ConvertAsync(
                request,
                new Progress<OperationEvent>(item => AppendLog(KanimLog, item, value =>
                {
                    if (IsKanimBusy)
                    {
                        KanimStatus = value;
                    }
                })),
                kanimCancellation.Token).ConfigureAwait(true);
            KanimStatus = result.Succeeded ? "转换成功" : result.ErrorMessage ?? "转换失败";
            if (result.Succeeded && !string.IsNullOrWhiteSpace(result.OutputDirectory))
            {
                ConversionSucceeded?.Invoke(result.OutputDirectory);
            }
        }
        catch (OperationCanceledException)
        {
            KanimStatus = "转换已取消";
        }
        catch (Exception ex)
        {
            KanimStatus = ex.Message;
            KanimLog.Add("[错误] " + ex.Message);
        }
        finally
        {
            IsKanimBusy = false;
            kanimCancellation?.Dispose();
            kanimCancellation = null;
        }
    }

    private async Task ConvertScmlAsync()
    {
        ScmlStatus = "正在验证输入...";
        if (!ValidateFile(ScmlPath, "SCML") || !ValidateDirectory(ScmlOutputDirectory))
        {
            return;
        }

        scmlCancellation?.Dispose();
        scmlCancellation = new CancellationTokenSource();
        IsScmlBusy = true;
        ScmlLog.Clear();
        try
        {
            var request = new ScmlToKanimRequest(ScmlPath, ScmlOutputDirectory, Interpolate, Debone);
            ConversionExecutionResult result = await conversionService.ConvertAsync(
                request,
                new Progress<OperationEvent>(item => AppendLog(ScmlLog, item, value =>
                {
                    if (IsScmlBusy)
                    {
                        ScmlStatus = value;
                    }
                })),
                scmlCancellation.Token).ConfigureAwait(true);
            ScmlStatus = result.Succeeded ? "转换成功" : result.ErrorMessage ?? "转换失败";
            if (result.Succeeded && !string.IsNullOrWhiteSpace(result.OutputDirectory))
            {
                ConversionSucceeded?.Invoke(result.OutputDirectory);
            }
        }
        catch (OperationCanceledException)
        {
            ScmlStatus = "转换已取消";
        }
        catch (Exception ex)
        {
            ScmlStatus = ex.Message;
            ScmlLog.Add("[错误] " + ex.Message);
        }
        finally
        {
            IsScmlBusy = false;
            scmlCancellation?.Dispose();
            scmlCancellation = null;
        }
    }

    private bool CanConvertKanim() => !IsKanimBusy &&
        fileSystem.FileExists(PngPath) && fileSystem.FileExists(AnimPath) && fileSystem.FileExists(BuildPath) &&
        !string.IsNullOrWhiteSpace(KanimOutputDirectory);

    private bool CanConvertScml() => !IsScmlBusy &&
        fileSystem.FileExists(ScmlPath) && !string.IsNullOrWhiteSpace(ScmlOutputDirectory);

    private bool ValidateFile(string path, string label)
    {
        return !string.IsNullOrWhiteSpace(path) && fileSystem.FileExists(path);
    }

    private static bool ValidateDirectory(string path)
    {
        return !string.IsNullOrWhiteSpace(path);
    }

    private void AppendLog(
        ObservableCollection<string> log,
        OperationEvent operationEvent,
        Action<string> setStatus)
    {
        log.Add((operationEvent.IsError ? "[错误] " : string.Empty) + operationEvent.Message);
        operationLogSink.Append(operationEvent);
        setStatus(operationEvent.Message);
    }

    private void AppendExternalMessage(
        ObservableCollection<string> log,
        string message,
        bool isError,
        Action<string> setStatus)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        AppendLog(
            log,
            new OperationEvent(Guid.NewGuid().ToString("N"), message, isError, Stage: "ui"),
            setStatus);
    }

    public void Dispose()
    {
        kanimCancellation?.Cancel();
        kanimCancellation?.Dispose();
        scmlCancellation?.Cancel();
        scmlCancellation?.Dispose();
        kanimCancellation = null;
        scmlCancellation = null;
    }

    private sealed class NullOperationLogSink : IOperationLogSink
    {
        public static NullOperationLogSink Instance { get; } = new();

        public void Append(OperationEvent operationEvent)
        {
        }
    }
}
