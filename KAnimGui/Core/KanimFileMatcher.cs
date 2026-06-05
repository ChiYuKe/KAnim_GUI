using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace KAnimGui.Core
{
    public sealed record KanimFileSet(string Name, string PngPath, string AnimPath, string BuildPath);

    public sealed record KanimFileSetValidation(bool IsValid, string? ErrorMessage = null);

    public static class KanimFileMatcher
    {
        public static KanimFileSetValidation ValidateFileSet(
            string pngPath,
            string animPath,
            string buildPath,
            bool allowTxt)
        {
            var kanimExtensions = allowTxt ? new[] { ".bytes", ".txt" } : new[] { ".bytes" };

            var pngValidation = ValidateExistingFile(pngPath, new[] { ".png" }, "PNG文件");
            if (!pngValidation.IsValid) return pngValidation;

            var animValidation = ValidateExistingFile(animPath, kanimExtensions, "Anim文件");
            if (!animValidation.IsValid) return animValidation;

            var buildValidation = ValidateExistingFile(buildPath, kanimExtensions, "Build文件");
            if (!buildValidation.IsValid) return buildValidation;

            if (!Path.GetFileNameWithoutExtension(animPath).EndsWith("_anim", StringComparison.OrdinalIgnoreCase))
            {
                return new KanimFileSetValidation(false, "Anim文件名应以 _anim 结尾。");
            }

            if (!Path.GetFileNameWithoutExtension(buildPath).EndsWith("_build", StringComparison.OrdinalIgnoreCase))
            {
                return new KanimFileSetValidation(false, "Build文件名应以 _build 结尾。");
            }

            var pngBase = NormalizePngBaseName(Path.GetFileNameWithoutExtension(pngPath));
            var animBase = TrimSuffix(Path.GetFileNameWithoutExtension(animPath), "_anim");
            var buildBase = TrimSuffix(Path.GetFileNameWithoutExtension(buildPath), "_build");

            if (!string.Equals(pngBase, animBase, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(animBase, buildBase, StringComparison.OrdinalIgnoreCase))
            {
                return new KanimFileSetValidation(false, "PNG、Anim、Build 的基础文件名不一致，请确认是否为同一套资源。");
            }

            return new KanimFileSetValidation(true);
        }

        public static IEnumerable<KanimFileSet> FindFileSets(string folderPath, bool allowTxt)
        {
            var files = Directory.EnumerateFiles(folderPath, "*.*", SearchOption.TopDirectoryOnly).ToList();
            var pngFiles = files
                .Where(path => Path.GetExtension(path).Equals(".png", StringComparison.OrdinalIgnoreCase))
                .GroupBy(path => NormalizePngBaseName(Path.GetFileNameWithoutExtension(path)), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group => group.OrderBy(path => path, StringComparer.OrdinalIgnoreCase).First(),
                    StringComparer.OrdinalIgnoreCase);

            var animFiles = files
                .Where(path => IsAnimFile(path, allowTxt))
                .GroupBy(path => TrimSuffix(Path.GetFileNameWithoutExtension(path), "_anim"), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => ChoosePreferredKanimDataFile(group), StringComparer.OrdinalIgnoreCase);

            var buildFiles = files
                .Where(path => IsBuildFile(path, allowTxt))
                .GroupBy(path => TrimSuffix(Path.GetFileNameWithoutExtension(path), "_build"), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => ChoosePreferredKanimDataFile(group), StringComparer.OrdinalIgnoreCase);

            foreach (var pair in animFiles.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
            {
                var baseName = pair.Key;

                if (buildFiles.TryGetValue(baseName, out var buildPath) &&
                    pngFiles.TryGetValue(baseName, out var pngPath))
                {
                    yield return new KanimFileSet(baseName, pngPath, pair.Value, buildPath);
                }
            }
        }

        public static bool IsKanimFile(string path, bool allowTxt) =>
            path.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
            IsAnimFile(path, allowTxt) ||
            IsBuildFile(path, allowTxt);

        public static bool IsAnimFile(string path, bool allowTxt) =>
            path.EndsWith("_anim.bytes", StringComparison.OrdinalIgnoreCase) ||
            (allowTxt && path.EndsWith("_anim.txt", StringComparison.OrdinalIgnoreCase));

        public static bool IsBuildFile(string path, bool allowTxt) =>
            path.EndsWith("_build.bytes", StringComparison.OrdinalIgnoreCase) ||
            (allowTxt && path.EndsWith("_build.txt", StringComparison.OrdinalIgnoreCase));

        public static string NormalizePngBaseName(string value)
        {
            var underscoreIndex = value.LastIndexOf('_');
            if (underscoreIndex < 0 || underscoreIndex == value.Length - 1)
            {
                return value;
            }

            var suffix = value[(underscoreIndex + 1)..];
            return suffix.All(char.IsDigit) ? value[..underscoreIndex] : value;
        }

        private static KanimFileSetValidation ValidateExistingFile(
            string path,
            string[] expectedExtensions,
            string displayName)
        {
            if (!File.Exists(path))
            {
                return new KanimFileSetValidation(false, $"{displayName}不存在：{path}");
            }

            var extension = Path.GetExtension(path);
            if (!expectedExtensions.Any(ext => extension.Equals(ext, StringComparison.OrdinalIgnoreCase)))
            {
                return new KanimFileSetValidation(
                    false,
                    $"{displayName}类型不正确，应为：{string.Join(" / ", expectedExtensions)}");
            }

            return new KanimFileSetValidation(true);
        }

        private static string ChoosePreferredKanimDataFile(IEnumerable<string> paths)
        {
            return paths
                .OrderBy(path => Path.GetExtension(path).Equals(".bytes", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                .ThenBy(path => path, StringComparer.OrdinalIgnoreCase)
                .First();
        }

        private static string TrimSuffix(string value, string suffix)
        {
            return value.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)
                ? value.Substring(0, value.Length - suffix.Length)
                : value;
        }
    }
}
