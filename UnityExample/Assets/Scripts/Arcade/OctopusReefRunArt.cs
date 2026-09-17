using UnityEngine;

/// <summary>
/// The two things the Reef Run stage draws with: the sample's own mark, loaded once, and a single
/// white pixel every rectangle in the scene is scaled and tinted from.
///
/// One pixel is all a reef needs. Keeping the shapes procedural means the stage follows
/// <see cref="OctopusSampleBranding.Palette"/> into dark mode with no second set of PNGs, and the
/// only binary the game ships is the character — see `Assets/Art/Arcade/README.md`.
/// </summary>
public static class OctopusReefRunArt
{
    /// <summary>`Resources` path of the swimming character.</summary>
    public const string MarkPath = "ReefRun/octopus-mark";

    private static Sprite _mark;
    private static bool _markLoaded;
    private static Sprite _pixel;

    /// <summary>The character sprite, or null when the asset is missing — the stage then draws a disc.</summary>
    public static Sprite Mark()
    {
        if (!_markLoaded)
        {
            _mark = Resources.Load<Sprite>(MarkPath);
            _markLoaded = true;
        }
        return _mark;
    }

    /// <summary>A 1x1 white sprite at one pixel per unit: scale it to get a world-sized rectangle.</summary>
    public static Sprite Pixel()
    {
        if (_pixel != null) return _pixel;
        var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
        {
            name = "ReefRunPixel",
            hideFlags = HideFlags.HideAndDontSave,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
        };
        texture.SetPixel(0, 0, Color.white);
        texture.Apply(false, false);
        _pixel = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        _pixel.name = "ReefRunPixel";
        _pixel.hideFlags = HideFlags.HideAndDontSave;
        return _pixel;
    }

    /// <summary>Drops the cached sprite so a test can assert a fresh load. The app never calls it.</summary>
    public static void Forget()
    {
        _mark = null;
        _markLoaded = false;
    }

    /// <summary>The palette, mixed for water: the sample's colours, never a literal.</summary>
    public static Color Water(float depth)
    {
        var p = OctopusSampleBranding.Palette;
        return Color.Lerp(p.Page, p.Accent, Mathf.Clamp01(depth));
    }
}
