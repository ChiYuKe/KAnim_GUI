using System.IO;
using KanimLib;

namespace KAnimGui.KAnimCore;

/// <summary>
/// File-system adapter for the stream-only Core KAnim codec.
/// </summary>
public static class KAnimBinaryFileCodec
{
    public static KBuild ReadBuild(string path)
    {
        using var stream = File.OpenRead(path);
        return KAnimBinaryCodec.ReadBuild(stream);
    }

    public static KAnim ReadAnim(string path)
    {
        using var stream = File.OpenRead(path);
        return KAnimBinaryCodec.ReadAnim(stream);
    }

    public static bool WriteBuild(string path, KBuild build)
    {
        using var stream = File.Create(path);
        return KAnimBinaryCodec.WriteBuild(stream, build);
    }

    public static bool WriteAnim(string path, KAnim anim)
    {
        using var stream = File.Create(path);
        return KAnimBinaryCodec.WriteAnim(stream, anim);
    }
}
