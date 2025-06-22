using KAnimGui.Models;
using System.IO;

public static class KseLocator
{
    /// <summary>
    /// 查找 kanimal-cli.exe 的可执行文件路径。
    /// 优先使用用户自定义路径（如果启用且路径有效），否则按默认规则搜索。
    /// </summary>
    /// <returns>找到的可执行文件完整路径，找不到返回 null。</returns>
    public static string FindExecutable()
    {
        // 如果用户启用了自定义路径且路径非空且文件存在，则直接返回该路径
        if (AppSettings.UseCustomKsePath
            && !string.IsNullOrEmpty(AppSettings.CustomKsePath)
            && File.Exists(AppSettings.CustomKsePath))
        {
            return AppSettings.CustomKsePath;
        }

        // 否则按照默认规则查找：

        // 1. 当前工作目录下查找
        var localPath = Path.Combine(Directory.GetCurrentDirectory(), "kanimal-cli.exe");
        if (File.Exists(localPath))
            return localPath;

        // 2. 应用程序所在目录查找
        var appDir = AppDomain.CurrentDomain.BaseDirectory;
        var appDirPath = Path.Combine(appDir, "kanimal-cli.exe");
        if (File.Exists(appDirPath))
            return appDirPath;

        // 3. 系统环境变量 PATH 中的目录依次查找
        var paths = Environment.GetEnvironmentVariable("PATH")?.Split(';');
        if (paths != null)
        {
            foreach (var path in paths)
            {
                try
                {
                    var fullPath = Path.Combine(path, "kanimal-cli.exe");
                    if (File.Exists(fullPath))
                        return fullPath;
                }
                catch
                {
                    // 某些路径可能无效，忽略异常继续查找
                }
            }
        }

        // 找不到则返回 null
        return null;
    }
}
