using KanimLib;

namespace KAnimGui.Core.Kanim;

/// <summary>
/// UI-neutral KAnim package metadata used by diagnostics and preview rules.
/// </summary>
public sealed record KAnimDataPackage(
    KBuild? Build,
    KAnim? Anim,
    int? TextureWidth = null,
    int? TextureHeight = null)
{
    public bool HasTexture => TextureWidth is > 0 && TextureHeight is > 0;
    public bool HasBuild => Build != null;
    public bool HasAnim => Anim != null;
    public bool HasAnyData => HasTexture || HasBuild || HasAnim;
    public bool IsValidAtlas => HasTexture && HasBuild;
    public bool IsComplete => HasTexture && HasBuild && HasAnim;
}
