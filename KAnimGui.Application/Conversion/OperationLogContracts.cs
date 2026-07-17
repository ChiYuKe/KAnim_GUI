namespace KAnimGui.Application.Conversion;

public interface IOperationLogSink
{
    void Append(OperationEvent operationEvent);
}
