using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The dark theme's fade above a docked action: the bottom <see cref="Height"/> of a scroll
/// viewport melting from transparent into the page ground, so the last card scrolls away under a
/// soft edge instead of the viewport mask's hard line right above the dock.
///
/// Only in dark, where <see cref="OctopusSamplePalette.Dock"/> is clear and the dock's button sits on
/// the page ground itself. In light the dock is an opaque surface band whose top edge already
/// separates it from the list, and the fade stays off.
///
/// A non-raycast <see cref="RawImage"/>, last child of the viewport: drawn over the content but
/// never hit, so a tap or a drag at the bottom of the list still reaches the card under it. The
/// texture is generated once, in code, like <see cref="SampleUiHalo"/>'s: one texel column, the page
/// colour throughout, alpha 0 at the top to 1 at the bottom, sRGB.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(RawImage))]
public class SampleUiDockFade : MonoBehaviour
{
    /// <summary>The GameObject name of every dock fade, last child of its viewport.</summary>
    public const string ObjectName = "DockFade";

    /// <summary>Texels along the gradient. Bilinear filtering smooths the rest.</summary>
    public const int TextureHeight = 32;

    /// <summary>The fade's height, in canvas units: 24dp, as on the other samples.</summary>
    public static float Height { get { return OctopusSampleBranding.Dp(24f); } }

    private static Texture2D _texture;
    private static Color _bakedPage;

    private RawImage _image;

#if UNITY_EDITOR
    // Same leak as SampleUiHalo's: a HideAndDontSave static survives a domain reload natively.
    [UnityEditor.InitializeOnLoadMethod]
    private static void ReleaseOnReload()
    {
        UnityEditor.AssemblyReloadEvents.beforeAssemblyReload -= ReleaseTexture;
        UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += ReleaseTexture;
    }
#endif

    /// <summary>
    /// Destroys the shared fade texture; the next <see cref="TextureFor"/> regenerates it. Called
    /// before an editor domain reload.
    /// </summary>
    public static void ReleaseTexture()
    {
        DestroyTexture(ref _texture);
    }

    // Immediate in the editor, as SampleUiShapes.Release does: a domain reload can discard
    // deferred Destroy work, and Destroy is refused outside play mode anyway.
    private static void DestroyTexture(ref Texture2D texture)
    {
        if (texture != null)
        {
#if UNITY_EDITOR
            Object.DestroyImmediate(texture);
#else
            Object.Destroy(texture);
#endif
        }
        texture = null;
    }

    /// <summary>
    /// Adds the fade to <paramref name="viewport"/> as its last child, pinned to its bottom edge,
    /// the one that meets the dock.
    /// </summary>
    public static RectTransform Attach(RectTransform viewport)
    {
        var go = new GameObject(ObjectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
        var rect = (RectTransform)go.transform;
        rect.SetParent(viewport, false);
        rect.SetAsLastSibling();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(0f, Height);
        go.GetComponent<RawImage>().raycastTarget = false;
        go.AddComponent<SampleUiDockFade>().Refresh();
        return rect;
    }

    /// <summary>
    /// The shared texture for <paramref name="page"/>, generated on first use and again only if the
    /// colour changes. Row 0 is the bottom: fully opaque.
    /// </summary>
    public static Texture2D TextureFor(Color page)
    {
        if (_texture != null && _bakedPage == page) return _texture;
        if (_texture == null)
        {
            // sRGB (linear: false), for the reason SampleUiHalo gives: the page colour is a
            // gamma-space value and must reach the screen unchanged where the fade is opaque.
            _texture = new Texture2D(1, TextureHeight, TextureFormat.RGBA32, false, false)
            {
                name = "OctopusSampleDockFade",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };
        }

        var pixels = new Color32[TextureHeight];
        for (var y = 0; y < TextureHeight; y++)
        {
            var alpha = 1f - (float)y / (TextureHeight - 1);
            pixels[y] = new Color(page.r, page.g, page.b, alpha);
        }
        _texture.SetPixels32(pixels);
        _texture.Apply(false);
        _bakedPage = page;
        return _texture;
    }

    /// <summary>Shows the fade over a clear dock (dark theme), hides it over an opaque one.</summary>
    public void Refresh()
    {
        if (_image == null) _image = GetComponent<RawImage>();
        var palette = OctopusSampleBranding.Palette;
        var visible = palette.Dock.a <= 0f;
        if (_image.enabled != visible) _image.enabled = visible;
        if (!visible) return;

        var texture = TextureFor(palette.Page);
        if (_image.texture != texture) _image.texture = texture;
        if (_image.color != Color.white) _image.color = Color.white;
    }

    // Every frame, but it only writes on a theme switch.
    private void LateUpdate()
    {
        Refresh();
    }
}
