using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml.Linq;
using KanimLib;

namespace KAnimGui.KAnimCore
{
    public static class KAnimToScmlExporter
    {
        public static string Export(string texturePath, string buildPath, string animPath, string outputDirectory)
        {
            var package = KAnimDecoder.LoadPackage(texturePath, buildPath, animPath);
            return ExportPackage(package, outputDirectory, Path.GetFileNameWithoutExtension(animPath));
        }

        public static string ExportPackage(KAnimPackage package, string outputDirectory, string baseName)
        {
            Directory.CreateDirectory(outputDirectory);
            if (package.Build != null && package.Anim != null)
            {
                package.Anim.RepairStringsFromBuild(package.Build);
            }

            var fileMap = BuildFileMap(package, outputDirectory);
            var root = new XElement(
                "spriter_data",
                new XAttribute("scml_version", "1.0"),
                new XAttribute("generator", "KAnimGui"),
                new XAttribute("generator_version", "1"));

            root.Add(fileMap.Folder);
            root.Add(BuildEntityElement(package, fileMap, TrimAnimSuffix(baseName)));

            var scmlPath = Path.Combine(outputDirectory, $"{NormalizeFileName(TrimAnimSuffix(baseName))}.scml");
            new XDocument(root).Save(scmlPath);
            return scmlPath;
        }

        private static ScmlFileMap BuildFileMap(KAnimPackage package, string outputDirectory)
        {
            var folder = new XElement("folder", new XAttribute("id", 0));
            var files = new Dictionary<(int SymbolHash, int FrameNumber), ScmlFileRef>();
            var nextFileId = 0;

            if (package.Build != null)
            {
                foreach (var symbol in package.Build.Symbols)
                {
                    foreach (var frame in symbol.Frames)
                    {
                        var fileName = $"{NormalizeFileName(symbol.Name)}_{frame.Index}.png";
                        var size = GetFrameExportSize(frame, package.Texture?.PixelWidth, package.Texture?.PixelHeight);
                        var width = size.Width;
                        var height = size.Height;

                        if (package.Texture != null)
                        {
                            var rect = GetFrameExportRectangle(frame, package.Texture.PixelWidth, package.Texture.PixelHeight);
                            TryWriteFrameImage(package.Texture, rect, Path.Combine(outputDirectory, fileName));
                        }
                        else
                        {
                            WritePlaceholderPng(Path.Combine(outputDirectory, fileName));
                        }

                        var fileId = nextFileId++;
                        folder.Add(new XElement(
                            "file",
                            new XAttribute("id", fileId),
                            new XAttribute("name", fileName),
                            new XAttribute("width", width),
                            new XAttribute("height", height),
                            new XAttribute("pivot_x", Format(frame.SpriterPivotX)),
                            new XAttribute("pivot_y", Format(GetSpriterPivotY(frame)))));

                        files[(symbol.Hash, frame.Index)] = new ScmlFileRef(0, fileId);
                    }

                    foreach (var pair in KAnimBuildResolver.BuildFrameLookup(symbol))
                    {
                        if (files.TryGetValue((symbol.Hash, pair.Value.Index), out var fileRef))
                        {
                            files[(symbol.Hash, pair.Key)] = fileRef;
                        }
                    }
                }
            }

            if (package.Anim != null)
            {
                int? placeholderFileId = null;
                foreach (var element in package.Anim.Banks.SelectMany(bank => bank.Frames).SelectMany(frame => frame.Elements))
                {
                    var key = (element.SymbolHash, element.FrameNumber);
                    if (files.ContainsKey(key))
                    {
                        continue;
                    }

                    var precedingFile = FindPrecedingFile(files, element.SymbolHash, element.FrameNumber);
                    if (precedingFile != null)
                    {
                        files[key] = precedingFile;
                        continue;
                    }

                    var symbolName = package.Anim.SymbolNames.TryGetValue(element.SymbolHash, out var resolvedName)
                        ? resolvedName
                        : $"hash_{element.SymbolHash}";
                    var fileName = $"{NormalizeFileName(symbolName)}_{element.FrameNumber}.png";
                    WritePlaceholderPng(Path.Combine(outputDirectory, fileName));

                    placeholderFileId ??= nextFileId == 0 ? 0 : nextFileId + 1;
                    folder.Add(new XElement(
                        "file",
                        new XAttribute("id", placeholderFileId.Value),
                        new XAttribute("name", fileName),
                        new XAttribute("width", 1),
                        new XAttribute("height", 1),
                        new XAttribute("pivot_x", 0),
                        new XAttribute("pivot_y", 0)));

                    files[key] = new ScmlFileRef(0, placeholderFileId.Value);
                }
            }

            return new ScmlFileMap(folder, files);
        }

        private static ScmlFileRef? FindPrecedingFile(
            IReadOnlyDictionary<(int SymbolHash, int FrameNumber), ScmlFileRef> files,
            int symbolHash,
            int frameNumber)
        {
            for (var index = frameNumber - 1; index >= 0; index--)
            {
                if (files.TryGetValue((symbolHash, index), out var fileRef))
                {
                    return fileRef;
                }
            }

            return null;
        }

        private static XElement BuildEntityElement(KAnimPackage package, ScmlFileMap fileMap, string baseName)
        {
            var name = package.Build?.Name;
            if (string.IsNullOrWhiteSpace(name) || name == "Uninitialized_Name")
            {
                name = string.IsNullOrWhiteSpace(baseName) ? "KAnim" : baseName;
            }

            var entity = new XElement(
                "entity",
                new XAttribute("id", 0),
                new XAttribute("name", name));

            if (package.Anim == null || package.Anim.Banks.Count == 0)
            {
                entity.Add(BuildStaticAnimation(package, fileMap));
                return entity;
            }

            for (var animationId = 0; animationId < package.Anim.Banks.Count; animationId++)
            {
                entity.Add(BuildAnimationElement(package.Anim.Banks[animationId], package.Anim, fileMap, animationId));
            }

            return entity;
        }

        private static XElement BuildStaticAnimation(KAnimPackage package, ScmlFileMap fileMap)
        {
            var animation = new XElement(
                "animation",
                new XAttribute("id", 0),
                new XAttribute("name", "static"),
                new XAttribute("length", 1000),
                new XAttribute("looping", "false"));
            var mainlineKey = new XElement("key", new XAttribute("id", 0), new XAttribute("time", 0));
            var mainline = new XElement("mainline", mainlineKey);
            animation.Add(mainline);

            if (package.Build == null)
            {
                return animation;
            }

            var timelineId = 0;
            foreach (var symbol in package.Build.Symbols)
            {
                var frame = symbol.Frames.FirstOrDefault();
                if (frame == null || !fileMap.Files.TryGetValue((symbol.Hash, frame.Index), out var fileRef))
                {
                    continue;
                }

                mainlineKey.Add(new XElement(
                    "object_ref",
                    new XAttribute("id", timelineId),
                    new XAttribute("timeline", timelineId),
                    new XAttribute("key", 0),
                    new XAttribute("z_index", timelineId)));

                animation.Add(new XElement(
                    "timeline",
                    new XAttribute("id", timelineId),
                    new XAttribute("name", symbol.Name),
                    new XElement(
                        "key",
                        new XAttribute("id", 0),
                        new XAttribute("time", 0),
                        new XElement(
                            "object",
                            new XAttribute("folder", fileRef.FolderId),
                            new XAttribute("file", fileRef.FileId),
                            new XAttribute("x", 0),
                            new XAttribute("y", 0)))));

                timelineId++;
            }

            return animation;
        }

        private static XElement BuildAnimationElement(KAnimBank bank, KAnim anim, ScmlFileMap fileMap, int animationId)
        {
            var frameDurationMs = bank.Rate > 0 ? 1000.0 / bank.Rate : 1000.0 / 30.0;
            var interval = Math.Max(1, (int)Math.Round(frameDurationMs));
            var animationLength = Math.Max(1, interval * Math.Max(1, bank.Frames.Count));
            var animation = new XElement(
                "animation",
                new XAttribute("id", animationId),
                new XAttribute("name", bank.Name),
                new XAttribute("length", animationLength),
                new XAttribute("interval", interval));

            var mainline = new XElement("mainline");
            animation.Add(mainline);

            var timelineIds = BuildTimelineIdMap(bank, anim);
            var timelines = timelineIds
                .OrderBy(pair => pair.Value)
                .ToDictionary(
                    pair => pair.Key,
                    pair => new XElement("timeline", new XAttribute("id", pair.Value), new XAttribute("name", pair.Key)));

            for (var frameIndex = 0; frameIndex < bank.Frames.Count; frameIndex++)
            {
                var frame = bank.Frames[frameIndex];
                var time = frameIndex * interval;
                var mainlineKey = new XElement(
                    "key",
                    new XAttribute("id", frameIndex),
                    new XAttribute("time", time));

                var occurrences = new Dictionary<string, int>();
                for (var elementIndex = 0; elementIndex < frame.Elements.Count; elementIndex++)
                {
                    var element = frame.Elements[elementIndex];
                    if (!fileMap.Files.TryGetValue((element.SymbolHash, element.FrameNumber), out var fileRef))
                    {
                        continue;
                    }

                    var timelineName = GetTimelineOccurrenceName(anim, element, occurrences);
                    var timelineId = timelineIds[timelineName];

                    mainlineKey.Add(new XElement(
                        "object_ref",
                        new XAttribute("id", timelineId),
                        new XAttribute("timeline", timelineId),
                        new XAttribute("key", frameIndex),
                        new XAttribute("z_index", frame.Elements.Count - elementIndex)));

                    timelines[timelineName].Add(new XElement(
                        "key",
                        new XAttribute("id", frameIndex),
                        new XAttribute("time", time),
                        new XElement(
                            "object",
                            new XAttribute("folder", fileRef.FolderId),
                            new XAttribute("file", fileRef.FileId),
                            new XAttribute("x", Format(element.M02 / 2.0)),
                            new XAttribute("y", Format(-element.M12 / 2.0)),
                            new XAttribute("angle", Format(GetTransform(element).Angle)),
                            new XAttribute("scale_x", Format(GetTransform(element).ScaleX)),
                            new XAttribute("scale_y", Format(GetTransform(element).ScaleY)),
                            new XAttribute("a", Format(Math.Clamp(element.Alpha, 0, 1))))));
                }

                mainline.Add(mainlineKey);
            }

            foreach (var timeline in timelines.Values.OrderBy(timeline => int.Parse(timeline.Attribute("id")!.Value, CultureInfo.InvariantCulture)))
            {
                animation.Add(timeline);
            }

            return animation;
        }

        private static Dictionary<string, int> BuildTimelineIdMap(KAnimBank bank, KAnim anim)
        {
            var maxOccurrences = new Dictionary<string, int>();
            var symbolOrder = new List<string>();

            foreach (var frame in bank.Frames)
            {
                var perFrame = new Dictionary<string, int>();
                foreach (var element in frame.Elements)
                {
                    var symbolName = ResolveSymbolName(anim, element.SymbolHash);
                    if (!perFrame.ContainsKey(symbolName))
                    {
                        perFrame[symbolName] = 0;
                    }

                    perFrame[symbolName]++;
                    if (!maxOccurrences.ContainsKey(symbolName))
                    {
                        maxOccurrences[symbolName] = 0;
                        symbolOrder.Add(symbolName);
                    }

                    maxOccurrences[symbolName] = Math.Max(maxOccurrences[symbolName], perFrame[symbolName]);
                }
            }

            var ids = new Dictionary<string, int>();
            var id = 0;
            foreach (var symbolName in symbolOrder)
            {
                for (var occurrence = 0; occurrence < maxOccurrences[symbolName]; occurrence++)
                {
                    ids[$"{symbolName}_{occurrence}"] = id++;
                }
            }

            return ids;
        }

        private static string GetTimelineOccurrenceName(KAnim anim, KAnimElement element, Dictionary<string, int> occurrences)
        {
            var symbolName = ResolveSymbolName(anim, element.SymbolHash);
            if (!occurrences.ContainsKey(symbolName))
            {
                occurrences[symbolName] = 0;
            }
            else
            {
                occurrences[symbolName]++;
            }

            return $"{symbolName}_{occurrences[symbolName]}";
        }

        private static string ResolveSymbolName(KAnim anim, int symbolHash)
        {
            if (anim.SymbolNames.TryGetValue(symbolHash, out var name))
            {
                return name;
            }

            return $"hash_{symbolHash}";
        }

        private static void TryWriteFrameImage(BitmapSource texture, System.Drawing.Rectangle rect, string outputPath)
        {
            if (rect.Width <= 0 || rect.Height <= 0)
            {
                WritePlaceholderPng(outputPath);
                return;
            }

            var cropped = new CroppedBitmap(texture, new Int32Rect(rect.X, rect.Y, rect.Width, rect.Height));
            using var stream = File.Create(outputPath);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(cropped));
            encoder.Save(stream);
        }

        private static void WritePlaceholderPng(string outputPath)
        {
            if (File.Exists(outputPath))
            {
                return;
            }

            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, 1, 1));
            }

            var bitmap = new RenderTargetBitmap(1, 1, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(visual);
            using var stream = File.Create(outputPath);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            encoder.Save(stream);
        }

        private static KanimTransform GetTransform(KAnimElement element)
        {
            var scaleX = Math.Sqrt(element.M00 * element.M00 + element.M10 * element.M10);
            var scaleY = Math.Sqrt(element.M01 * element.M01 + element.M11 * element.M11);
            var det = element.M00 * element.M11 - element.M01 * element.M10;
            if (det < 0)
            {
                scaleY *= -1;
            }

            var sinApprox = 0.5 * (element.M01 / scaleY - element.M10 / scaleX);
            var cosApprox = 0.5 * (element.M00 / scaleX + element.M11 / scaleY);
            var angle = Math.Atan2(sinApprox, cosApprox);
            if (angle < 0)
            {
                angle += 2 * Math.PI;
            }

            return new KanimTransform(
                angle * 180.0 / Math.PI,
                scaleX,
                scaleY);
        }

        private static double GetSpriterPivotY(KFrame frame)
        {
            if (frame.PivotHeight == 0)
            {
                return 0;
            }

            return (frame.PivotY / frame.PivotHeight) + 0.5;
        }

        private static System.Drawing.Size GetFrameExportSize(KFrame frame, int? textureWidth, int? textureHeight)
        {
            if (textureWidth.HasValue && textureHeight.HasValue)
            {
                var width = (int)((frame.UV_X2 - frame.UV_X1) * textureWidth.Value);
                var height = (int)((frame.UV_Y2 - frame.UV_Y1) * textureHeight.Value);
                return new System.Drawing.Size(Math.Max(1, width), Math.Max(1, height));
            }

            return new System.Drawing.Size(
                Math.Max(1, (int)Math.Floor(frame.PivotWidth / 2.0)),
                Math.Max(1, (int)Math.Floor(frame.PivotHeight / 2.0)));
        }

        private static System.Drawing.Rectangle GetFrameExportRectangle(KFrame frame, int textureWidth, int textureHeight)
        {
            var x = Math.Max(0, (int)(frame.UV_X1 * textureWidth));
            var y = Math.Max(0, (int)(frame.UV_Y1 * textureHeight));
            var size = GetFrameExportSize(frame, textureWidth, textureHeight);
            var width = Math.Min(size.Width, textureWidth - x);
            var height = Math.Min(size.Height, textureHeight - y);
            return new System.Drawing.Rectangle(x, y, Math.Max(1, width), Math.Max(1, height));
        }

        private static string TrimAnimSuffix(string value)
        {
            return value.EndsWith("_anim", StringComparison.OrdinalIgnoreCase)
                ? value[..^"_anim".Length]
                : value;
        }

        private static string NormalizeFileName(string value)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var chars = string.IsNullOrWhiteSpace(value) ? "unnamed".ToCharArray() : value.ToCharArray();
            for (var i = 0; i < chars.Length; i++)
            {
                if (invalid.Contains(chars[i]))
                {
                    chars[i] = '_';
                }
            }

            return new string(chars);
        }

        private static string Format(double value) =>
            value.ToString("0.###############", CultureInfo.InvariantCulture);

        private sealed record ScmlFileRef(int FolderId, int FileId);

        private sealed record ScmlFileMap(XElement Folder, IReadOnlyDictionary<(int SymbolHash, int FrameNumber), ScmlFileRef> Files);

        private sealed record KanimTransform(double Angle, double ScaleX, double ScaleY);
    }
}
