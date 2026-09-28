using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The dark theme's corner halo: <see cref="OctopusSampleBranding.DarkHalo"/> fading to fully
/// transparent, centred on the top-right corner of the screen with a radius of one screen width —
/// the same geometry on the four samples.
///
/// Drawn as a <see cref="RawImage"/> filling a page ground, under the bleeds, the header (clear in
/// dark theme, see <see cref="OctopusSamplePalette.Header"/>) and the content. Its UV rect is
/// resolved from where that ground sits on screen, so the halo stays fixed to the viewport whatever
/// the ground's inset, and follows a rotation or a resize.
///
/// The texture is generated once, in code, and shared by every screen: no asset to import. Its
/// texels are the halo already composited over the page in sRGB and stored opaque, for the reason
/// <see cref="OctopusSamplePalette"/> gives for its muted roles: the project renders in linear
/// colour space, where uGUI would blend a translucent 12% blue about twice as bright as the sRGB
/// composite the other platforms draw. An opaque texel draws exactly the value computed here, and
/// its left and bottom edges are the page itself, so a clamped UV outside the quadrant is page too.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(RawImage))]
public class SampleUiHalo : MonoBehaviour
{
    /// <summary>The GameObject name of every halo, first child of its ground.</summary>
    public const string ObjectName = "Halo";

    /// <summary>Side of the generated texture, in texels. The gradient is smooth; bilinear filtering does the rest.</summary>
    public const int TextureSize = 256;

    private static Texture2D _texture, _plain;
    private static Color _bakedPage, _bakedHalo, _plainPage;

    private readonly Vector3[] _corners = new Vector3[4];
    private RawImage _image;
    private bool _hidden;

#if UNITY_EDITOR
    // The textures are HideAndDontSave statics: a domain reload drops the references but not the
    // native objects, so every script reload in the editor leaked one of each. Players never reload.
    [UnityEditor.InitializeOnLoadMethod]
    private static void ReleaseOnReload()
    {
        UnityEditor.AssemblyReloadEvents.beforeAssemblyReload -= ReleaseTextures;
        UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += ReleaseTextures;
    }
#endif

    /// <summary>
    /// Destroys the shared halo and plain-page textures; the next <see cref="TextureFor"/> or
    /// <see cref="PlainFor"/> regenerates them. Called before an editor domain reload.
    /// </summary>
    public static void ReleaseTextures()
    {
        DestroyTexture(ref _texture);
        DestroyTexture(ref _plain);
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
    /// Keeps the glow off whatever the theme, for a screen that must not show it: the Community tab
    /// (the other samples draw no halo there either). In dark the image still paints the plain page
    /// from <see cref="PlainFor"/>, for the precision reason given on the class: a uGUI vertex
    /// colour goes through 8-bit linear, where the ink page #070D17 comes out #0D0D16, and the
    /// ground would no longer match the docks' fade or the other tabs.
    /// </summary>
    public bool Hidden
    {
        get => _hidden;
        set
        {
            if (_hidden == value) return;
            _hidden = value;
            Refresh();
        }
    }

    /// <summary>Adds a halo to <paramref name="ground"/> as its first child, stretched over it.</summary>
    public static RectTransform Attach(RectTransform ground)
    {
        var go = new GameObject(ObjectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
        var rect = (RectTransform)go.transform;
        rect.SetParent(ground, false);
        rect.SetAsFirstSibling();
        SampleUi.Stretch(rect, Vector2.zero, Vector2.one);
        go.GetComponent<RawImage>().raycastTarget = false;
        go.AddComponent<SampleUiHalo>().Refresh();
        return rect;
    }

    /// <summary>
    /// The halo's strength at <paramref name="distance"/> from the top-right corner, in screen
    /// widths: 1 at the corner, 0 from one radius on.
    /// </summary>
    public static float Falloff(float distance)
    {
        return Mathf.Clamp01(1f - distance);
    }

    /// <summary>
    /// <paramref name="halo"/> at its own alpha scaled by <see cref="Falloff"/>, composited over
    /// <paramref name="page"/> in sRGB. What a texel holds, and what the screen shows.
    /// </summary>
    public static Color Composite(Color page, Color halo, float distance)
    {
        var ink = new Color(halo.r, halo.g, halo.b, 1f);
        var result = OctopusSampleBranding.Tint(ink, page, halo.a * Falloff(distance));
        result.a = 1f;
        return result;
    }

    /// <summary>
    /// The texture's UV rect for a ground occupying <paramref name="groundPixels"/> of a screen of
    /// <paramref name="screen"/> pixels (origin bottom-left). The texture covers the square of side
    /// one screen width anchored at the top-right corner, the only area the halo reaches.
    /// </summary>
    public static Rect UvRect(Rect groundPixels, Vector2 screen)
    {
        if (screen.x <= 0f || screen.y <= 0f || groundPixels.width <= 0f || groundPixels.height <= 0f)
            return new Rect(0f, 0f, 1f, 1f);
        var side = screen.x;
        var bottom = screen.y - side;
        return new Rect(groundPixels.xMin / side, (groundPixels.yMin - bottom) / side,
                        groundPixels.width / side, groundPixels.height / side);
    }

    /// <summary>
    /// The shared texture for <paramref name="halo"/> over <paramref name="page"/>, generated on
    /// first use and again only if either colour changes.
    /// </summary>
    public static Texture2D TextureFor(Color page, Color halo)
    {
        if (_texture != null && _bakedPage == page && _bakedHalo == halo) return _texture;
        if (_texture == null)
        {
            // sRGB (linear: false): the texels are gamma-space values, decoded on sampling and
            // re-encoded on output, so the screen shows them unchanged.
            _texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false, false)
            {
                name = "OctopusSampleHalo",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };
        }

        var pixels = new Color32[TextureSize * TextureSize];
        for (var y = 0; y < TextureSize; y++)
        {
            // Texel centres, in screen widths from the top-right corner (u = 1, v = 1).
            var dy = 1f - (y + 0.5f) / TextureSize;
            for (var x = 0; x < TextureSize; x++)
            {
                var dx = 1f - (x + 0.5f) / TextureSize;
                pixels[y * TextureSize + x] = Composite(page, halo, Mathf.Sqrt(dx * dx + dy * dy));
            }
        }
        _texture.SetPixels32(pixels);
        _texture.Apply(false);
        _bakedPage = page;
        _bakedHalo = halo;
        return _texture;
    }

    /// <summary>
    /// An opaque texture of <paramref name="page"/> alone, in sRGB: the ground of a
    /// <see cref="Hidden"/> halo. Generated on first use and again only if the colour changes.
    /// </summary>
    public static Texture2D PlainFor(Color page)
    {
        if (_plain != null && _plainPage == page) return _plain;
        if (_plain == null)
        {
            _plain = new Texture2D(4, 4, TextureFormat.RGBA32, false, false)
            {
                name = "OctopusSamplePlainPage",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Point,
                hideFlags = HideFlags.HideAndDontSave
            };
        }
        var pixels = new Color32[16];
        Color32 texel = new Color(page.r, page.g, page.b, 1f);
        for (var i = 0; i < pixels.Length; i++) pixels[i] = texel;
        _plain.SetPixels32(pixels);
        _plain.Apply(false);
        _plainPage = page;
        return _plain;
    }

    /// <summary>Shows or hides the halo for the theme in force and re-aims it at the screen corner.</summary>
    public void Refresh()
    {
        if (_image == null) _image = GetComponent<RawImage>();
        var palette = OctopusSampleBranding.Palette;
        var visible = palette.Halo.a > 0f;
        if (_image.enabled != visible) _image.enabled = visible;
        if (!visible) return;

        var texture = _hidden ? PlainFor(palette.Page) : TextureFor(palette.Page, palette.Halo);
        if (_image.texture != texture) _image.texture = texture;
        if (_image.color != Color.white) _image.color = Color.white;

        // Screen-space overlay: world corners are screen pixels.
        ((RectTransform)transform).GetWorldCorners(_corners);
        var ground = Rect.MinMaxRect(_corners[0].x, _corners[0].y, _corners[2].x, _corners[2].y);
        var uv = UvRect(ground, new Vector2(Screen.width, Screen.height));
        if (_image.uvRect != uv) _image.uvRect = uv;
    }

    private void OnRectTransformDimensionsChange()
    {
        if (isActiveAndEnabled) Refresh();
    }

    // Every frame, but it only writes on a change: a rotation, a resize or a theme switch.
    private void LateUpdate()
    {
        Refresh();
    }
}
