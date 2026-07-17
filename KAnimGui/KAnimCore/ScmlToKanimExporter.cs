using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using KanimLib;
using DrawingColor = System.Drawing.Color;

namespace KAnimGui.KAnimCore
{
    public static class ScmlToKanimExporter
    {
        public static ScmlToKanimResult Export(string scmlPath, string outputDirectory)
        {
            if (!File.Exists(scmlPath))
            {
                throw new FileNotFoundException("SCML 文件不存在。", scmlPath);
            }

            Directory.CreateDirectory(outputDirectory);

            var document = XDocument.Load(scmlPath);
            var folder = document.Root?.Elements("folder").FirstOrDefault()
                ?? throw new InvalidDataException("SCML 中缺少 <folder>。");
            var entity = document.Root.Elements("entity").FirstOrDefault()
                ?? throw new InvalidDataException("SCML 中缺少 <entity>。");

            var scmlDirectory = Path.GetDirectoryName(Path.GetFullPath(scmlPath)) ?? Environment.CurrentDirectory;
            var files = ReadFiles(folder, scmlDirectory);
            var buildName = GetAttr(entity, "name", Path.GetFileNameWithoutExtension(scmlPath));
            var atlas = PackAtlas(files.Values.Where(file => file.Image != null).ToList());
            var build = BuildKanimBuild(buildName, files, atlas);
            var anim = BuildKanimAnim(entity, files);

            var pngPath = Path.Combine(outputDirectory, $"{buildName}.png");
            var buildPath = Path.Combine(outputDirectory, $"{buildName}_build.bytes");
            var animPath = Path.Combine(outputDirectory, $"{buildName}_anim.bytes");

            atlas.Bitmap.Save(pngPath, ImageFormat.Png);
            if (!KAnimBinaryFileCodec.WriteBuild(buildPath, build))
            {
                throw new IOException("写入 build.bytes 失败。");
            }

            if (!KAnimBinaryFileCodec.WriteAnim(animPath, anim))
            {
                throw new IOException("写入 anim.bytes 失败。");
            }

            foreach (var file in files.Values)
            {
                file.Image?.Dispose();
            }

            atlas.Bitmap.Dispose();

            return new ScmlToKanimResult(pngPath, buildPath, animPath);
        }

        private static Dictionary<int, ScmlFile> ReadFiles(XElement folder, string scmlDirectory)
        {
            var files = new Dictionary<int, ScmlFile>();
            foreach (var fileNode in folder.Elements("file"))
            {
                var id = GetInt(fileNode, "id");
                var name = GetRequiredAttr(fileNode, "name");
                var width = GetInt(fileNode, "width");
                var height = GetInt(fileNode, "height");
                var pivotX = GetFloat(fileNode, "pivot_x", 0f);
                var pivotY = GetFloat(fileNode, "pivot_y", 1f);
                var sourcePath = Path.Combine(scmlDirectory, name);
                Bitmap? image = null;
                if (File.Exists(sourcePath))
                {
                    image = new Bitmap(sourcePath);
                    width = image.Width;
                    height = image.Height;
                }

                files[id] = new ScmlFile(id, name, GetSpriteBaseName(name), GetSpriteFrameNumber(name), width, height, pivotX, pivotY, image);
            }

            return files;
        }

        private static PackedAtlas PackAtlas(List<ScmlFile> files)
        {
            if (files.Count == 0)
            {
                var empty = new Bitmap(1, 1, PixelFormat.Format32bppArgb);
                return new PackedAtlas(empty, new Dictionary<int, Rectangle>());
            }

            var maxWidth = Math.Max(64, NextPowerOfTwo((int)Math.Ceiling(Math.Sqrt(files.Sum(file => file.Width * file.Height)))));
            maxWidth = Math.Min(4096, Math.Max(maxWidth, files.Max(file => file.Width)));

            var positions = new Dictionary<int, Rectangle>();
            var x = 0;
            var y = 0;
            var rowHeight = 0;
            foreach (var file in files.OrderBy(file => file.SymbolName, StringComparer.Ordinal).ThenBy(file => file.FrameNumber))
            {
                if (x > 0 && x + file.Width > maxWidth)
                {
                    x = 0;
                    y += rowHeight;
                    rowHeight = 0;
                }

                positions[file.Id] = new Rectangle(x, y, file.Width, file.Height);
                x += file.Width;
                rowHeight = Math.Max(rowHeight, file.Height);
            }

            var atlasWidth = NextPowerOfTwo(Math.Max(1, positions.Values.Max(rect => rect.Right)));
            var atlasHeight = NextPowerOfTwo(Math.Max(1, positions.Values.Max(rect => rect.Bottom)));
            var atlas = new Bitmap(atlasWidth, atlasHeight, PixelFormat.Format32bppArgb);
            using (var graphics = Graphics.FromImage(atlas))
            {
                graphics.Clear(DrawingColor.Transparent);
                foreach (var file in files)
                {
                    if (file.Image == null)
                    {
                        continue;
                    }

                    graphics.DrawImage(file.Image, positions[file.Id]);
                }
            }

            return new PackedAtlas(atlas, positions);
        }

        private static KBuild BuildKanimBuild(string buildName, IReadOnlyDictionary<int, ScmlFile> files, PackedAtlas atlas)
        {
            var build = new KBuild
            {
                Version = KBuild.CURRENT_BUILD_VERSION,
                Name = buildName
            };

            foreach (var group in files.Values
                         .Where(file => file.Image != null)
                         .GroupBy(file => file.SymbolName)
                         .OrderBy(group => group.Key, StringComparer.Ordinal))
            {
                var symbol = new KSymbol(build)
                {
                    Hash = group.Key.KHash(),
                    Path = group.Key.KHash(),
                    Color = DrawingColor.FromArgb(0),
                    Flags = 0
                };

                build.SymbolNames[symbol.Hash] = group.Key;

                foreach (var file in group.OrderBy(file => file.FrameNumber))
                {
                    var rect = atlas.Positions[file.Id];
                    var frame = new KFrame(symbol)
                    {
                        Index = file.FrameNumber,
                        Duration = 1,
                        ImageIndex = 0,
                        PivotWidth = file.Width * 2f,
                        PivotHeight = file.Height * 2f,
                        PivotX = -(file.PivotX - 0.5f) * file.Width * 2f,
                        PivotY = (file.PivotY - 0.5f) * file.Height * 2f,
                        UV_X1 = (float)rect.Left / atlas.Bitmap.Width,
                        UV_Y1 = (float)rect.Top / atlas.Bitmap.Height,
                        UV_X2 = (float)rect.Right / atlas.Bitmap.Width,
                        UV_Y2 = (float)rect.Bottom / atlas.Bitmap.Height
                    };
                    symbol.Frames.Add(frame);
                }

                symbol.FrameCount = symbol.Frames.Count;
                build.Symbols.Add(symbol);
            }

            build.SymbolCount = build.Symbols.Count;
            build.FrameCount = build.Symbols.Sum(symbol => symbol.Frames.Count);
            return build;
        }

        private static KAnim BuildKanimAnim(XElement entity, IReadOnlyDictionary<int, ScmlFile> files)
        {
            var anim = new KAnim
            {
                Version = 5
            };

            foreach (var animationNode in entity.Elements("animation"))
            {
                var bankName = GetRequiredAttr(animationNode, "name");
                var interval = GetAnimationInterval(animationNode);
                var bank = new KAnimBank(anim)
                {
                    Name = bankName,
                    Hash = bankName.KHash(),
                    Rate = interval > 0 ? 1000f / interval : 30f
                };

                anim.SymbolNames[bank.Hash] = bankName;
                var timelines = animationNode.Elements("timeline")
                    .ToDictionary(timeline => GetInt(timeline, "id"));
                var mainline = animationNode.Element("mainline")
                    ?? throw new InvalidDataException($"动画 {bankName} 缺少 <mainline>。");

                foreach (var keyNode in mainline.Elements("key").OrderBy(key => GetInt(key, "id")))
                {
                    var frame = new KAnimFrame(bank);
                    var bounds = new BoundsAccumulator();
                    foreach (var objectRef in keyNode.Elements("object_ref")
                                 .OrderByDescending(node => GetInt(node, "z_index", 0)))
                    {
                        var timelineId = GetInt(objectRef, "timeline");
                        var keyId = GetInt(objectRef, "key");
                        if (!timelines.TryGetValue(timelineId, out var timeline))
                        {
                            continue;
                        }

                        var timelineKey = timeline.Elements("key").FirstOrDefault(key => GetInt(key, "id") == keyId);
                        var objectNode = timelineKey?.Element("object");
                        if (objectNode == null)
                        {
                            continue;
                        }

                        var fileId = GetInt(objectNode, "file");
                        if (!files.TryGetValue(fileId, out var file))
                        {
                            continue;
                        }

                        var symbolHash = file.SymbolName.KHash();
                        var element = new KAnimElement(frame)
                        {
                            SymbolHash = symbolHash,
                            FrameNumber = file.FrameNumber,
                            FolderHash = symbolHash,
                            Flags = 0,
                            Alpha = GetFloat(objectNode, "a", 1f),
                            Blue = 1f,
                            Green = 1f,
                            Red = 1f,
                            M02 = GetFloat(objectNode, "x", 0f) * 2f,
                            M12 = -GetFloat(objectNode, "y", 0f) * 2f,
                            Unused = 0f
                        };

                        var angle = GetFloat(objectNode, "angle", 0f) * MathF.PI / 180f;
                        var scaleX = GetFloat(objectNode, "scale_x", 1f);
                        var scaleY = GetFloat(objectNode, "scale_y", 1f);
                        element.M00 = scaleX * MathF.Cos(angle);
                        element.M10 = scaleX * -MathF.Sin(angle);
                        element.M01 = scaleY * MathF.Sin(angle);
                        element.M11 = scaleY * MathF.Cos(angle);

                        frame.Elements.Add(element);
                        anim.SymbolNames[symbolHash] = file.SymbolName;
                        bounds.Include(GetFloat(objectNode, "x", 0f), GetFloat(objectNode, "y", 0f), file.Width, file.Height, scaleX, scaleY);
                    }

                    frame.ElementCount = frame.Elements.Count;
                    frame.X = bounds.CenterX;
                    frame.Y = bounds.CenterY;
                    frame.Width = bounds.Width;
                    frame.Height = bounds.Height;
                    bank.Frames.Add(frame);
                }

                bank.FrameCount = bank.Frames.Count;
                anim.Banks.Add(bank);
            }

            anim.BankCount = anim.Banks.Count;
            anim.FrameCount = anim.Banks.Sum(bank => bank.Frames.Count);
            anim.ElementCount = anim.Banks.SelectMany(bank => bank.Frames).Sum(frame => frame.Elements.Count);
            anim.MaxVisSymbols = anim.Banks.SelectMany(bank => bank.Frames).Select(frame => frame.Elements.Count).DefaultIfEmpty(0).Max();
            return anim;
        }

        private static int GetAnimationInterval(XElement animationNode)
        {
            if (animationNode.Attribute("interval") != null)
            {
                return GetInt(animationNode, "interval");
            }

            var keys = animationNode.Element("mainline")?.Elements("key").OrderBy(key => GetInt(key, "id")).ToList();
            if (keys == null || keys.Count < 2)
            {
                return Math.Max(1, GetInt(animationNode, "length", 1000));
            }

            return Math.Max(1, GetInt(keys[1], "time") - GetInt(keys[0], "time"));
        }

        private static string GetSpriteBaseName(string fileName)
        {
            var name = Path.GetFileNameWithoutExtension(fileName);
            var index = name.LastIndexOf('_');
            return index > 0 && int.TryParse(name[(index + 1)..], out _) ? name[..index] : name;
        }

        private static int GetSpriteFrameNumber(string fileName)
        {
            var name = Path.GetFileNameWithoutExtension(fileName);
            var index = name.LastIndexOf('_');
            return index > 0 && int.TryParse(name[(index + 1)..], out var frameNumber) ? frameNumber : 0;
        }

        private static int NextPowerOfTwo(int value)
        {
            var result = 1;
            while (result < value)
            {
                result *= 2;
            }

            return result;
        }

        private static string GetRequiredAttr(XElement element, string name) =>
            element.Attribute(name)?.Value ?? throw new InvalidDataException($"节点 <{element.Name}> 缺少属性 {name}。");

        private static string GetAttr(XElement element, string name, string defaultValue) =>
            element.Attribute(name)?.Value ?? defaultValue;

        private static int GetInt(XElement element, string name, int defaultValue = 0) =>
            int.TryParse(element.Attribute(name)?.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
                ? value
                : defaultValue;

        private static float GetFloat(XElement element, string name, float defaultValue = 0f) =>
            float.TryParse(element.Attribute(name)?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
                ? value
                : defaultValue;

        private sealed record ScmlFile(
            int Id,
            string FileName,
            string SymbolName,
            int FrameNumber,
            int Width,
            int Height,
            float PivotX,
            float PivotY,
            Bitmap? Image);

        private sealed record PackedAtlas(Bitmap Bitmap, IReadOnlyDictionary<int, Rectangle> Positions);

        private sealed class BoundsAccumulator
        {
            private float minX = float.MaxValue;
            private float minY = float.MaxValue;
            private float maxX = float.MinValue;
            private float maxY = float.MinValue;

            public float CenterX => HasValue ? (minX + maxX) / 2f : 0f;
            public float CenterY => HasValue ? (minY + maxY) / 2f : 0f;
            public float Width => HasValue ? maxX - minX : 0f;
            public float Height => HasValue ? maxY - minY : 0f;

            private bool HasValue => minX != float.MaxValue;

            public void Include(float x, float y, int width, int height, float scaleX, float scaleY)
            {
                var scaledWidth = Math.Abs(width * scaleX);
                var scaledHeight = Math.Abs(height * scaleY);
                minX = Math.Min(minX, x);
                minY = Math.Min(minY, y);
                maxX = Math.Max(maxX, x + scaledWidth);
                maxY = Math.Max(maxY, y + scaledHeight);
            }
        }
    }

    public sealed record ScmlToKanimResult(string PngPath, string BuildPath, string AnimPath);
}
