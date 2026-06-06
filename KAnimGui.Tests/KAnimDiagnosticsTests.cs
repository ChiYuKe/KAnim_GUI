using KAnimGui.KAnimCore;
using KanimLib;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Media.Imaging;

namespace KAnimGui.Tests;

public sealed class KAnimDiagnosticsTests
{
    [Fact]
    public void Analyze_ReportsMissingElementSymbol()
    {
        var package = CreateMinimalPackage();
        package.Anim!.Banks[0].Frames[0].Elements[0].SymbolHash = 12345;

        var diagnostics = KAnimDiagnostics.Analyze(package);

        Assert.Contains(diagnostics, diagnostic =>
            diagnostic.Severity == KAnimDiagnosticSeverity.Error &&
            diagnostic.Code == "ELEMENT_SYMBOL_MISSING");
    }

    [Fact]
    public void Analyze_ReportsElementFrameOutOfRange()
    {
        var package = CreateMinimalPackage();
        package.Anim!.Banks[0].Frames[0].Elements[0].FrameNumber = 2;

        var diagnostics = KAnimDiagnostics.Analyze(package);

        Assert.Contains(diagnostics, diagnostic =>
            diagnostic.Severity == KAnimDiagnosticSeverity.Error &&
            diagnostic.Code == "ELEMENT_FRAME_MISSING");
    }

    [Fact]
    public void Analyze_UsesBuildFrameDurationForElementFrameLookup()
    {
        var package = CreateMinimalPackage();
        package.Build!.Symbols[0].Frames[0].Duration = 2;
        package.Anim!.Banks[0].Frames[0].Elements[0].FrameNumber = 1;

        var diagnostics = KAnimDiagnostics.Analyze(package);

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Code == "ELEMENT_FRAME_MISSING");
    }

    [Fact]
    public void Analyze_ReportsCountMismatches()
    {
        var package = CreateMinimalPackage();
        package.Build!.SymbolCount = 3;
        package.Anim!.ElementCount = 8;

        var diagnostics = KAnimDiagnostics.Analyze(package);

        Assert.Contains(diagnostics, diagnostic => diagnostic.Code == "BUILD_SYMBOL_COUNT");
        Assert.Contains(diagnostics, diagnostic => diagnostic.Code == "ANIM_ELEMENT_COUNT");
    }

    [Fact]
    public void Summary_DescribesBuildAndAnim()
    {
        var package = CreateMinimalPackage();

        var summary = KAnimSummary.FromPackage(package);

        Assert.Equal("未加载", summary.TextureSummary);
        Assert.Contains("1 symbols", summary.BuildSummary);
        Assert.Contains("1 banks", summary.AnimSummary);
    }

    [Fact]
    public void ExportPackage_WritesScmlWithoutCli()
    {
        using var temp = new TempFolder();
        var package = CreateMinimalPackage();

        var scmlPath = KAnimToScmlExporter.ExportPackage(package, temp.Path, "sample_anim");

        Assert.True(File.Exists(scmlPath));
        var scml = File.ReadAllText(scmlPath);
        Assert.Contains("<spriter_data", scml);
        Assert.Contains("<animation", scml);
    }

    [Fact]
    public void ExportPackage_NamesTimelinesFromElementsWhenPlaceholderFileIsShared()
    {
        using var temp = new TempFolder();
        var package = CreatePackageWithMissingSymbols();

        var scmlPath = KAnimToScmlExporter.ExportPackage(package, temp.Path, "missing_symbols_anim");
        var scml = File.ReadAllText(scmlPath);

        Assert.Contains("name=\"head_0\"", scml);
        Assert.Contains("name=\"body_0\"", scml);
    }

    [Fact]
    public void ExportPackage_UsesBuildFrameDurationLookup()
    {
        using var temp = new TempFolder();
        var package = CreateMinimalPackage();
        package.Build!.Symbols[0].Frames[0].Duration = 2;
        package.Anim!.Banks[0].Frames[0].Elements[0].FrameNumber = 1;

        var scmlPath = KAnimToScmlExporter.ExportPackage(package, temp.Path, "duration_lookup_anim");
        var scml = File.ReadAllText(scmlPath);

        Assert.Contains("<file id=\"0\"", scml);
        Assert.Contains("file=\"0\"", scml);
        Assert.DoesNotContain("width=\"1\" height=\"1\"", scml);
    }

    [Fact]
    public void ExportPackage_UsesUvRectangleForFileDimensions()
    {
        using var temp = new TempFolder();
        var package = CreateMinimalPackage();
        package.Texture = CreateTexture(temp.Path, 100, 80);
        package.Build!.Symbols[0].Frames[0].PivotWidth = 200;
        package.Build.Symbols[0].Frames[0].PivotHeight = 200;
        package.Build.Symbols[0].Frames[0].UV_X1 = 0;
        package.Build.Symbols[0].Frames[0].UV_Y1 = 0;
        package.Build.Symbols[0].Frames[0].UV_X2 = 0.25f;
        package.Build.Symbols[0].Frames[0].UV_Y2 = 0.5f;

        var scmlPath = KAnimToScmlExporter.ExportPackage(package, temp.Path, "uv_size_anim");
        var scml = File.ReadAllText(scmlPath);

        Assert.Contains("width=\"25\"", scml);
        Assert.Contains("height=\"40\"", scml);
    }

    [Fact]
    public void ExportPackage_UsesOccurrenceTimelineNamesForDuplicateSymbols()
    {
        using var temp = new TempFolder();
        var package = CreateMinimalPackage();
        var frame = package.Anim!.Banks[0].Frames[0];
        frame.Elements.Add(new KAnimElement(frame)
        {
            SymbolHash = package.Build!.Symbols[0].Hash,
            FolderHash = package.Build.Symbols[0].Hash,
            FrameNumber = 0,
            Alpha = 1,
            Red = 1,
            Green = 1,
            Blue = 1,
            M00 = 1,
            M11 = 1
        });
        frame.ElementCount = frame.Elements.Count;
        package.Anim.ElementCount = frame.Elements.Count;

        var scmlPath = KAnimToScmlExporter.ExportPackage(package, temp.Path, "duplicates_anim");
        var scml = File.ReadAllText(scmlPath);

        Assert.Contains("name=\"body_0\"", scml);
        Assert.Contains("name=\"body_1\"", scml);
    }

    [Fact]
    public void ScmlToKanimExporter_WritesReadableKanimPackage()
    {
        using var temp = new TempFolder();
        var spritePath = System.IO.Path.Combine(temp.Path, "body_0.png");
        using (var bitmap = new Bitmap(8, 8))
        {
            bitmap.Save(spritePath, ImageFormat.Png);
        }

        var scmlPath = System.IO.Path.Combine(temp.Path, "sample.scml");
        File.WriteAllText(scmlPath, """
            <spriter_data scml_version="1.0" generator="test" generator_version="1">
              <folder id="0">
                <file id="0" name="body_0.png" width="8" height="8" pivot_x="0.5" pivot_y="0.5" />
              </folder>
              <entity id="0" name="sample">
                <animation id="0" name="idle" length="1000" interval="1000">
                  <mainline>
                    <key id="0" time="0"><object_ref id="0" timeline="0" key="0" z_index="0" /></key>
                  </mainline>
                  <timeline id="0" name="body_0">
                    <key id="0" time="0"><object folder="0" file="0" x="3" y="-4" angle="0" scale_x="1" scale_y="1" a="1" /></key>
                  </timeline>
                </animation>
              </entity>
            </spriter_data>
            """);

        var result = ScmlToKanimExporter.Export(scmlPath, temp.Path);

        Assert.True(File.Exists(result.PngPath));
        Assert.True(File.Exists(result.BuildPath));
        Assert.True(File.Exists(result.AnimPath));

        var build = KAnimUtils.ReadBuild(result.BuildPath);
        var anim = KAnimUtils.ReadAnim(result.AnimPath);
        Assert.Equal("sample", build.Name);
        Assert.Single(build.Symbols);
        Assert.Single(anim.Banks);
        Assert.Equal("idle", anim.Banks[0].Name);
        Assert.Equal(6, anim.Banks[0].Frames[0].Elements[0].M02);
        Assert.Equal(8, anim.Banks[0].Frames[0].Elements[0].M12);
    }

    private static KAnimPackage CreateMinimalPackage()
    {
        var build = new KBuild
        {
            Version = KBuild.CURRENT_BUILD_VERSION,
            SymbolCount = 1,
            FrameCount = 1
        };

        var symbol = new KSymbol(build)
        {
            FrameCount = 1
        };
        build.Symbols.Add(symbol);
        build.SymbolNames[symbol.Hash] = "body";

        var buildFrame = new KFrame(symbol)
        {
            PivotWidth = 16,
            PivotHeight = 16,
            UV_X1 = 0,
            UV_Y1 = 0,
            UV_X2 = 1,
            UV_Y2 = 1
        };
        symbol.Frames.Add(buildFrame);

        var anim = new KAnim
        {
            Version = 5,
            BankCount = 1,
            FrameCount = 1,
            ElementCount = 1
        };

        var bank = new KAnimBank(anim)
        {
            Name = "idle",
            Hash = "idle".KHash(),
            FrameCount = 1,
            Rate = 30
        };
        anim.Banks.Add(bank);

        var animFrame = new KAnimFrame(bank)
        {
            ElementCount = 1
        };
        bank.Frames.Add(animFrame);

        animFrame.Elements.Add(new KAnimElement(animFrame)
        {
            SymbolHash = symbol.Hash,
            FolderHash = symbol.Hash,
            FrameNumber = 0,
            Alpha = 1,
            Red = 1,
            Green = 1,
            Blue = 1,
            M00 = 1,
            M11 = 1
        });

        return new KAnimPackage
        {
            Build = build,
            Anim = anim
        };
    }

    private static KAnimPackage CreatePackageWithMissingSymbols()
    {
        var anim = new KAnim
        {
            Version = 5,
            BankCount = 1,
            FrameCount = 1,
            ElementCount = 2
        };
        anim.SymbolNames["head".KHash()] = "head";
        anim.SymbolNames["body".KHash()] = "body";

        var bank = new KAnimBank(anim)
        {
            Name = "idle",
            Hash = "idle".KHash(),
            FrameCount = 1,
            Rate = 30
        };
        anim.Banks.Add(bank);

        var frame = new KAnimFrame(bank)
        {
            ElementCount = 2
        };
        bank.Frames.Add(frame);

        frame.Elements.Add(new KAnimElement(frame)
        {
            SymbolHash = "head".KHash(),
            FrameNumber = 0,
            Alpha = 1,
            M00 = 1,
            M11 = 1
        });
        frame.Elements.Add(new KAnimElement(frame)
        {
            SymbolHash = "body".KHash(),
            FrameNumber = 0,
            Alpha = 1,
            M00 = 1,
            M11 = 1
        });

        return new KAnimPackage
        {
            Anim = anim
        };
    }

    private static BitmapImage CreateTexture(string directory, int width, int height)
    {
        var path = System.IO.Path.Combine(directory, "texture.png");
        using (var bitmap = new Bitmap(width, height))
        {
            bitmap.Save(path, ImageFormat.Png);
        }

        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.UriSource = new Uri(path, UriKind.Absolute);
        image.EndInit();
        image.Freeze();
        return image;
    }

    private sealed class TempFolder : IDisposable
    {
        public TempFolder()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"KAnimGuiTests_{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
