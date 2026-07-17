using KanimLib;

namespace KAnimGui.Core.Kanim;

public enum KAnimTreeNodeKind
{
    Build,
    Symbol,
    Frame,
    Anim,
    Bank,
    AnimFrame,
    Element
}

public sealed class KAnimTreeNode
{
    public KAnimTreeNode(
        string title,
        KAnimTreeNodeKind kind,
        object value,
        KAnimBank? bank = null,
        int frameIndex = -1,
        int elementIndex = -1,
        bool isDeferred = false,
        IReadOnlyList<KAnimTreeNode>? children = null)
    {
        Title = title;
        Kind = kind;
        Value = value;
        Bank = bank;
        FrameIndex = frameIndex;
        ElementIndex = elementIndex;
        IsDeferred = isDeferred;
        Children = children ?? Array.Empty<KAnimTreeNode>();
    }

    public string Title { get; }
    public KAnimTreeNodeKind Kind { get; }
    public object Value { get; }
    public KAnimBank? Bank { get; }
    public int FrameIndex { get; }
    public int ElementIndex { get; }
    public bool IsDeferred { get; }
    public IReadOnlyList<KAnimTreeNode> Children { get; }
}

/// <summary>
/// Builds the preview tree model without creating WPF TreeViewItems.
/// </summary>
public sealed class KAnimTreeModelBuilder
{
    public IReadOnlyList<KAnimTreeNode> Build(KBuild? build, KAnim? anim, string? searchText)
    {
        string filter = searchText?.Trim() ?? string.Empty;
        var nodes = new List<KAnimTreeNode>();

        if (build != null)
        {
            var symbols = build.Symbols
                .Where(symbol => Matches(symbol.Name, filter))
                .OrderBy(symbol => symbol.Name, StringComparer.OrdinalIgnoreCase)
                .Select(symbol => new KAnimTreeNode(
                    $"{symbol.Name}  ({symbol.FrameCount})",
                    KAnimTreeNodeKind.Symbol,
                    symbol,
                    children: symbol.Frames.Select(frame => new KAnimTreeNode(
                        $"Frame {frame.Index}  {frame.SpriteWidth}x{frame.SpriteHeight}",
                        KAnimTreeNodeKind.Frame,
                        frame)).ToList()))
                .ToList();

            if (symbols.Count > 0 || filter.Length == 0)
            {
                nodes.Add(new KAnimTreeNode(
                    $"Build  ({build.SymbolCount} symbols, {build.FrameCount} frames)",
                    KAnimTreeNodeKind.Build,
                    build,
                    children: symbols));
            }
        }

        if (anim != null)
        {
            var banks = anim.Banks
                .Where(bank => Matches(bank.Name, filter) ||
                    filter.Length == 0 || bank.Frames.Any(frame => ContainsMatchingElement(frame, build, filter)))
                .OrderBy(bank => bank.Name, StringComparer.OrdinalIgnoreCase)
                .Select(bank => new KAnimTreeNode(
                    $"{bank.Name}  ({bank.FrameCount} frames, {bank.Rate:0.##} fps)",
                    KAnimTreeNodeKind.Bank,
                    bank,
                    bank: bank,
                    isDeferred: bank.Frames.Count > 0))
                .ToList();

            if (banks.Count > 0 || filter.Length == 0)
            {
                nodes.Add(new KAnimTreeNode(
                    $"Animations  ({anim.BankCount})",
                    KAnimTreeNodeKind.Anim,
                    anim,
                    children: banks));
            }
        }

        return nodes;
    }

    public IReadOnlyList<KAnimTreeNode> BuildExpandedBank(KAnimBank bank, KBuild? build, string? searchText)
    {
        string filter = searchText?.Trim() ?? string.Empty;
        bool bankMatches = Matches(bank.Name, filter);
        var nodes = new List<KAnimTreeNode>();

        for (int frameIndex = 0; frameIndex < bank.Frames.Count; frameIndex++)
        {
            KAnimFrame frame = bank.Frames[frameIndex];
            if (!bankMatches && !ContainsMatchingElement(frame, build, filter))
            {
                continue;
            }

            var elements = new List<KAnimTreeNode>();
            for (int elementIndex = 0; elementIndex < frame.Elements.Count; elementIndex++)
            {
                KAnimElement element = frame.Elements[elementIndex];
                string symbolName = build?.GetSymbol(element.SymbolHash)?.Name ?? $"#{element.SymbolHash}";
                if (!bankMatches && !Matches(symbolName, filter))
                {
                    continue;
                }

                elements.Add(new KAnimTreeNode(
                    $"{elementIndex}: {symbolName}  frame {element.FrameNumber}",
                    KAnimTreeNodeKind.Element,
                    element,
                    bank,
                    frameIndex,
                    elementIndex));
            }

            nodes.Add(new KAnimTreeNode(
                $"Frame {frameIndex}  ({frame.Elements.Count} elements)",
                KAnimTreeNodeKind.AnimFrame,
                frame,
                bank,
                frameIndex,
                children: elements));
        }

        return nodes;
    }

    private static bool ContainsMatchingElement(KAnimFrame frame, KBuild? build, string filter)
    {
        return filter.Length == 0 || frame.Elements.Any(element =>
            Matches(build?.GetSymbol(element.SymbolHash)?.Name, filter));
    }

    private static bool Matches(string? value, string filter)
    {
        return filter.Length == 0 ||
            (!string.IsNullOrWhiteSpace(value) && value.Contains(filter, StringComparison.OrdinalIgnoreCase));
    }
}
