namespace KAnimGui.Application.Conversion;

public abstract record ConversionRequest(string OutputDirectory);

public sealed record KanimToScmlRequest(
    string PngPath,
    string AnimPath,
    string BuildPath,
    string OutputDirectory,
    bool StrictOrder,
    bool StrictMode)
    : ConversionRequest(OutputDirectory);

public sealed record ScmlToKanimRequest(
    string ScmlPath,
    string OutputDirectory,
    bool Interpolate,
    bool Debone)
    : ConversionRequest(OutputDirectory);

public sealed record ProcessExecutionResult(
    bool Succeeded,
    int ExitCode,
    string? ErrorMessage,
    bool WasCanceled);

public sealed record ConversionExecutionResult(
    bool Succeeded,
    string? OutputDirectory,
    string? ErrorMessage,
    bool UsedBuiltInKernel,
    bool WasCanceled);

public sealed record ConversionBatchProgress(
    int Completed,
    int Total,
    string Name,
    ConversionExecutionResult Result);

public sealed record ConversionBatchResult(
    IReadOnlyList<ConversionExecutionResult> Results,
    bool WasCanceled);

public sealed record OperationEvent(
    string OperationId,
    string Message,
    bool IsError = false,
    double? Progress = null,
    string? Stage = null,
    Exception? Exception = null);

public interface IProcessRunner
{
    Task<ProcessExecutionResult> RunAsync(
        string executablePath,
        IReadOnlyList<string> arguments,
        IProgress<OperationEvent>? progress = null,
        CancellationToken cancellationToken = default);
}

public interface IConversionService
{
    Task<ConversionExecutionResult> ConvertAsync(
        ConversionRequest request,
        IProgress<OperationEvent>? progress = null,
        CancellationToken cancellationToken = default);

    Task<ConversionBatchResult> ConvertBatchAsync(
        IEnumerable<ConversionRequest> requests,
        IProgress<ConversionBatchProgress>? batchProgress = null,
        IProgress<OperationEvent>? progress = null,
        CancellationToken cancellationToken = default);
}

public interface IInputFilePreparer
{
    Task<string> PrepareBytesAsync(
        string path,
        bool allowTxt,
        CancellationToken cancellationToken = default);
}
