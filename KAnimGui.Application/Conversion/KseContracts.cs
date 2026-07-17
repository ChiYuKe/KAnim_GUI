namespace KAnimGui.Application.Conversion;

public interface IKsePathSettings
{
    bool UseCustomKsePath { get; }
    string CustomKsePath { get; }
}

public interface IKseExecutableLocator
{
    string? FindExecutable();
}
