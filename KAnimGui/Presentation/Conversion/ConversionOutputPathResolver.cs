using System.IO;

namespace KAnimGui.Presentation.Conversion;

public static class ConversionOutputPathResolver
{
    public static string RootDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "KSE_Output");

    public static string KanimToScmlDirectory => Path.Combine(RootDirectory, "KSE_Scml");

    public static string ScmlToKanimDirectory => Path.Combine(RootDirectory, "KSE_Kanim");
}
