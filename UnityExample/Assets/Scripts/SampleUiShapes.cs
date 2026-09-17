using System.Collections.Generic;
using UnityEngine;

/// <summary>White nine-slice masks, shared by all instances and both themes for the process lifetime.</summary>
public static class SampleUiShapes
{
    private static readonly Dictionary<int, Sprite> Cache = new Dictionary<int, Sprite>();
    private static readonly List<Object> Owned = new List<Object>();

    static SampleUiShapes()
    {
        Application.quitting += Release;
#if UNITY_EDITOR
        UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += Release;
#endif
    }

    /// <summary>Releases both managed cache entries and their native sprite/texture objects.</summary>
    public static void Release()
    {
        foreach (var resource in Owned)
        {
            if (resource == null) continue;
#if UNITY_EDITOR
            // Reload can discard deferred Destroy work; these are owned, transient native assets.
            Object.DestroyImmediate(resource);
#else
            Object.Destroy(resource);
#endif
        }
        Owned.Clear();
        Cache.Clear();
    }

    // Geometry is quantised to canvas pixels. Only token radii/strokes should be requested.
    public static Sprite Rounded(float radiusDp, float strokeDp = 0f)
    {
        int radius = Mathf.Clamp(Mathf.RoundToInt(OctopusSampleBranding.Dp(radiusDp)), 1, 144);
        int stroke = Mathf.Clamp(Mathf.RoundToInt(OctopusSampleBranding.Dp(strokeDp)), 0, radius);
        int key = radius * 256 + stroke;
        Sprite sprite;
        if (Cache.TryGetValue(key, out sprite) && sprite != null) return sprite;

        // The middle two pixels stretch. uGUI Image.GetAdjustedBorders proportionally shrinks
        // both borders when the target is smaller than their sum; tiny decorations stay bounded.
        // SmallSlicedShapesKeepEveryVertexInsideTheirRectangle locks that engine behaviour in.
        int size = radius * 2 + 2;
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float qx = Mathf.Max(Mathf.Abs(x + 0.5f - size / 2f) - 1f, 0f);
                float qy = Mathf.Max(Mathf.Abs(y + 0.5f - size / 2f) - 1f, 0f);
                float distance = Mathf.Sqrt(qx * qx + qy * qy) - radius;
                float alpha = Mathf.Clamp01(0.5f - distance);
                if (stroke > 0) alpha *= Mathf.Clamp01(distance + stroke + 0.5f);
                Color ink = OctopusSampleBranding.ShapeInk;
                ink.a = alpha;
                pixels[y * size + x] = ink;
            }
        }
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = "Sample rounded mask";
        texture.hideFlags = HideFlags.HideAndDontSave;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f),
            100f, 0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
        sprite.name = "Sample rounded " + radius + "/" + stroke;
        sprite.hideFlags = HideFlags.HideAndDontSave;
        Owned.Add(sprite);
        Owned.Add(texture);
        Cache[key] = sprite;
        return sprite;
    }
}
