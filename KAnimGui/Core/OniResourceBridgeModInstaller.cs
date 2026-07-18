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
        private static readonly Lazy<string> BundledVersionValue = new(ReadBundledVersion);

        public static string ModsRootDirectory => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "Klei",
            "OxygenNotIncluded",
            "mods");

        public static string LocalModsDirectory => Path.Combine(ModsRootDirectory, "Local");

        public static string TargetModDirectory => Path.Combine(LocalModsDirectory, ModFolderName);

        public static string BundledVersion => BundledVersionValue.Value;

        public static bool IsOlderVersion(string currentVersion, string bundledVersion)
        {
            return Version.TryParse(currentVersion, out Version? current) &&
                Version.TryParse(bundledVersion, out Version? bundled) &&
                current < bundled;
        }

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
            Assembly?[] assemblies =
            {
                Assembly.GetExecutingAssembly(),
                Assembly.GetEntryAssembly()
            };
            foreach (Assembly assembly in assemblies
                .Where(assembly => assembly != null)
                .Cast<Assembly>()
                .Distinct())
            {
                Stream? embedded = assembly.GetManifestResourceStream("KAnimGui.ONIResourceBridge-clean.zip");
                if (embedded != null)
                {
                    return embedded;
                }
            }

            string devPath = GetBundledZipPath();
            return File.Exists(devPath) ? File.OpenRead(devPath) : null;
        }

        private static string ReadBundledVersion()
        {
            try
            {
                using Stream? zipStream = OpenBundledZipStream();
                if (zipStream == null)
                {
                    return string.Empty;
                }

                using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);
                ZipArchiveEntry? entry = archive.Entries.FirstOrDefault(item =>
                    item.FullName.EndsWith("mod_info.yaml", StringComparison.OrdinalIgnoreCase));
                if (entry == null)
                {
                    return string.Empty;
                }

                using var reader = new StreamReader(entry.Open());
                while (reader.ReadLine() is { } line)
                {
                    const string prefix = "version:";
                    if (line.TrimStart().StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    {
                        return line[(line.IndexOf(':') + 1)..].Trim();
                    }
                }
            }
            catch (InvalidDataException)
            {
            }
            catch (IOException)
            {
            }

            return string.Empty;
        }

        private static bool HasEmbeddedBundledZip()
        {
            Assembly?[] assemblies =
            {
                Assembly.GetExecutingAssembly(),
                Assembly.GetEntryAssembly()
            };
            return assemblies
                .Where(assembly => assembly != null)
                .Cast<Assembly>()
                .Distinct()
                .Any(assembly => assembly.GetManifestResourceNames()
                    .Contains("KAnimGui.ONIResourceBridge-clean.zip", StringComparer.Ordinal));
        }

        private static string GetBundledZipPath()
        {
            string outputPath = Path.Combine(AppContext.BaseDirectory, BundledZipName);
            if (File.Exists(outputPath))
            {
                return outputPath;
            }

            string devPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Resources", BundledZipName));
            return devPath;
        }
    }
}
