using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

/// <summary>One shared dynamic SDF atlas per Inter weight, backed by bundled font data.</summary>
public static class SampleUiFonts
{
    private static TMP_FontAsset _regular;
    private static TMP_FontAsset _bold;

    public static TMP_FontAsset Regular
    {
        get
        {
            if (_regular != null) return _regular;
            _regular = Create("Inter-Regular");
            _bold = Create("Inter-Bold");
            if (_regular != null && _regular != TMP_Settings.defaultFontAsset && _bold != null)
            {
                // Bold style uses real outlines rather than artificially dilating Regular's SDF.
                _regular.fontWeightTable[OctopusSampleBranding.StrongFontWeight / 100] =
                    new TMP_FontWeightPair { regularTypeface = _bold };
            }
            return _regular;
        }
    }

    private static TMP_FontAsset Create(string name)
    {
        var source = Resources.Load<Font>("SampleFonts/" + name);
        if (source == null) return TMP_Settings.defaultFontAsset;
        var font = TMP_FontAsset.CreateFontAsset(source, 90, 9, GlyphRenderMode.SDFAA,
            1024, 1024, AtlasPopulationMode.Dynamic, true);
        if (font == null) return TMP_Settings.defaultFontAsset;
        font.name = name + " SDF (sample cache)";
        font.hideFlags = HideFlags.DontSave;
        font.material.hideFlags = HideFlags.DontSave;
        font.fallbackFontAssetTable = new List<TMP_FontAsset>();
        if (TMP_Settings.defaultFontAsset != null)
            font.fallbackFontAssetTable.Add(TMP_Settings.defaultFontAsset);
        // ASCII is ready before the first frame. Other scripts populate on demand; atlases are
        // shared across all labels, screens and themes, never created for a label instance.
        font.TryAddCharacters(" !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~");
        foreach (var texture in font.atlasTextures) if (texture != null) texture.hideFlags = HideFlags.DontSave;
        return font;
    }
}
