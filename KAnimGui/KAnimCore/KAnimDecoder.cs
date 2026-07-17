using System;
using System.IO;
using System.Windows.Media.Imaging;
using KanimLib;

namespace KAnimGui.KAnimCore
{
    public static class KAnimDecoder
    {
        public static KAnimPackage LoadPackage(string? texturePath, string? buildPath, string? animPath)
        {
            var package = new KAnimPackage
            {
                Texture = LoadTexture(texturePath),
                Build = LoadBuild(buildPath),
                Anim = LoadAnim(animPath)
            };

            if (package.Build != null && package.Anim != null)
            {
                package.Anim.RepairStringsFromBuild(package.Build);
            }

            return package;
        }

        public static KBuild? LoadBuild(string? buildPath)
        {
            return HasExistingFile(buildPath)
                ? KAnimBinaryFileCodec.ReadBuild(buildPath!)
                : null;
        }

        public static KAnim? LoadAnim(string? animPath)
        {
            return HasExistingFile(animPath)
                ? KAnimBinaryFileCodec.ReadAnim(animPath!)
                : null;
        }

        private static BitmapImage? LoadTexture(string? texturePath)
        {
            if (!HasExistingFile(texturePath))
            {
                return null;
            }

            var texture = new BitmapImage();
            texture.BeginInit();
            texture.CacheOption = BitmapCacheOption.OnLoad;
            texture.UriSource = new Uri(texturePath!, UriKind.Absolute);
            texture.EndInit();
            texture.Freeze();
            return texture;
        }

        private static bool HasExistingFile(string? path) =>
            !string.IsNullOrWhiteSpace(path) && File.Exists(path);
    }
}
