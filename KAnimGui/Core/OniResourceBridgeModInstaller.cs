using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;

namespace KAnimGui.Core
{
    public static class OniResourceBridgeModInstaller
    {
        private const string ModFolderName = "ONIResourceBridge";
        private const string ModDllName = "ONIResourceBridge.dll";
        private const string BundledZipName = "ONIResourceBridge-clean.zip";

        public static string ModsRootDirectory => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "Klei",
            "OxygenNotIncluded",
            "mods");

        public static string LocalModsDirectory => Path.Combine(ModsRootDirectory, "Local");

        public static string TargetModDirectory => Path.Combine(LocalModsDirectory, ModFolderName);

        public static bool IsInstalled()
        {
            string[] searchRoots =
            {
                Path.Combine(ModsRootDirectory, "Local"),
                Path.Combine(ModsRootDirectory, "Dev"),
                Path.Combine(ModsRootDirectory, "Steam")
            };

            return searchRoots
                .Where(Directory.Exists)
                .SelectMany(root => Directory.EnumerateFiles(root, ModDllName, SearchOption.AllDirectories))
                .Any(path => path.Contains(ModFolderName, StringComparison.OrdinalIgnoreCase));
        }

        public static bool CanInstallBundledMod(out string zipPath)
        {
            zipPath = GetBundledZipPath();
            return HasEmbeddedBundledZip() || File.Exists(zipPath);
        }

        public static void InstallBundledMod()
        {
            using Stream? zipStream = OpenBundledZipStream();
            if (zipStream == null)
            {
                throw new FileNotFoundException("没有找到内置 ONI Resource Bridge 模组压缩包。", GetBundledZipPath());
            }

            Directory.CreateDirectory(LocalModsDirectory);
            if (Directory.Exists(TargetModDirectory))
            {
                Directory.Delete(TargetModDirectory, recursive: true);
            }

            using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);
            archive.ExtractToDirectory(LocalModsDirectory, overwriteFiles: true);
        }

        private static Stream? OpenBundledZipStream()
        {
            Stream? embedded = Assembly.GetExecutingAssembly()
                .GetManifestResourceStream("KAnimGui.ONIResourceBridge-clean.zip");
            if (embedded != null)
            {
                return embedded;
            }

            string devPath = GetBundledZipPath();
            return File.Exists(devPath) ? File.OpenRead(devPath) : null;
        }

        private static bool HasEmbeddedBundledZip()
        {
            return Assembly.GetExecutingAssembly()
                .GetManifestResourceNames()
                .Contains("KAnimGui.ONIResourceBridge-clean.zip", StringComparer.Ordinal);
        }

        private static string GetBundledZipPath()
        {
            string outputPath = Path.Combine(AppContext.BaseDirectory, BundledZipName);
            if (File.Exists(outputPath))
            {
                return outputPath;
            }

            string devPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "artifacts", BundledZipName));
            return devPath;
        }
    }
}
