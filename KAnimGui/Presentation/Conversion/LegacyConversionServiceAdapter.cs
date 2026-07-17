using System.IO;
using KAnimGui.Application.Conversion;
using KAnimGui.Core;

namespace KAnimGui.Presentation.Conversion;

public sealed class LegacyConversionServiceAdapter : IConversionService
{
    private readonly IProcessRunner processRunner;
    private readonly IKseExecutableLocator executableLocator;

    public LegacyConversionServiceAdapter(
        IProcessRunner processRunner,
        IKseExecutableLocator executableLocator)
    {
        this.processRunner = processRunner ?? throw new ArgumentNullException(nameof(processRunner));
        this.executableLocator = executableLocator ?? throw new ArgumentNullException(nameof(executableLocator));
    }

    public async Task<ConversionBatchResult> ConvertBatchAsync(
        IEnumerable<ConversionRequest> requests,
        IProgress<ConversionBatchProgress>? batchProgress = null,
        IProgress<OperationEvent>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requests);
        List<ConversionRequest> requestList = requests.ToList();
        var results = new List<ConversionExecutionResult>(requestList.Count);
        for (int index = 0; index < requestList.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ConversionRequest request = requestList[index];
            ConversionExecutionResult result = await ConvertAsync(request, progress, cancellationToken).ConfigureAwait(false);
            results.Add(result);
            batchProgress?.Report(new ConversionBatchProgress(
                index + 1,
                requestList.Count,
                GetRequestName(request),
                result));
            if (result.WasCanceled)
            {
                return new ConversionBatchResult(results, true);
            }
        }

        return new ConversionBatchResult(results, false);
    }

    public async Task<ConversionExecutionResult> ConvertAsync(
        ConversionRequest request,
        IProgress<OperationEvent>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        string operationId = Guid.NewGuid().ToString("N");
        void Log(string message, bool isError) => progress?.Report(new OperationEvent(
            operationId,
            message,
            isError,
            Stage: "conversion"));

        try
        {
            return request switch
            {
                KanimToScmlRequest kanim => await ConvertKanimAsync(kanim, Log, cancellationToken).ConfigureAwait(false),
                ScmlToKanimRequest scml => await ConvertScmlAsync(scml, Log, cancellationToken).ConfigureAwait(false),
                _ => throw new NotSupportedException($"不支持的转换请求：{request.GetType().Name}")
            };
        }
        catch (OperationCanceledException)
        {
            return new ConversionExecutionResult(false, request.OutputDirectory, "转换已取消", false, true);
        }
        catch (Exception ex)
        {
            progress?.Report(new OperationEvent(operationId, ex.Message, true, Stage: "conversion", Exception: ex));
            return new ConversionExecutionResult(false, request.OutputDirectory, ex.Message, false, false);
        }
    }

    private async Task<ConversionExecutionResult> ConvertKanimAsync(
        KanimToScmlRequest request,
        Action<string, bool> log,
        CancellationToken cancellationToken)
    {
        var converter = new KanimConverter
        {
            PngPath = request.PngPath,
            AnimPath = request.AnimPath,
            BuildPath = request.BuildPath,
            OutputDir = request.OutputDirectory,
            StrictOrder = request.StrictOrder,
            StrictMode = request.StrictMode,
            ProcessRunner = processRunner,
            ExecutableLocator = executableLocator
        };
        Models.ConversionResult result = await converter.ConvertAsync(log, cancellationToken).ConfigureAwait(false);
        return new ConversionExecutionResult(
            result.Success,
            converter.ActualOutputDir,
            result.ErrorMessage,
            string.IsNullOrWhiteSpace(executableLocator.FindExecutable()),
            result.ErrorMessage == "转换已取消");
    }

    private static string GetRequestName(ConversionRequest request) => request switch
    {
        KanimToScmlRequest kanim => Path.GetFileName(kanim.AnimPath),
        ScmlToKanimRequest scml => Path.GetFileName(scml.ScmlPath),
        _ => request.GetType().Name
    };

    private async Task<ConversionExecutionResult> ConvertScmlAsync(
        ScmlToKanimRequest request,
        Action<string, bool> log,
        CancellationToken cancellationToken)
    {
        var converter = new ScmlConverter
        {
            ScmlPath = request.ScmlPath,
            OutputDir = request.OutputDirectory,
            Interpolate = request.Interpolate,
            Debone = request.Debone,
            ProcessRunner = processRunner,
            ExecutableLocator = executableLocator
        };
        Models.ConversionResult result = await converter.ConvertAsync(log, cancellationToken).ConfigureAwait(false);
        return new ConversionExecutionResult(
            result.Success,
            converter.ActualOutputDir,
            result.ErrorMessage,
            string.IsNullOrWhiteSpace(executableLocator.FindExecutable()),
            result.ErrorMessage == "转换已取消");
    }
}
