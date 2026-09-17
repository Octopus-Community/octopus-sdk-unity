using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Licensed Material Symbols, exported at 2x and 3x and shared by the sample.</summary>
public static class SampleUiIcons
{
    private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

    public static Sprite Get(string symbol)
    {
        // Canvas reference is three units per dp; 72px is native resolution on a 3x phone.
        string key = symbol + (Screen.dpi > 320f || Screen.dpi <= 0f ? "@3x" : "@2x");
        Sprite sprite;
        if (!Cache.TryGetValue(key, out sprite))
        {
            sprite = Resources.Load<Sprite>("SampleIcons/" + key);
            Cache[key] = sprite;
        }
        return sprite;
    }

    public static bool Apply(Image image, string symbol, Color ink)
    {
        var sprite = Get(symbol);
        if (sprite == null) return false;
        image.sprite = sprite;
        image.type = Image.Type.Simple;
        image.preserveAspect = true;
        image.color = ink;
        image.raycastTarget = false;
        return true;
    }

    public static void Row(RectTransform row, string symbol)
    {
        var icon = SampleUi.Panel("Icon", row, OctopusSampleBranding.Clear);
        Apply(icon.GetComponent<Image>(), symbol, OctopusSampleBranding.Palette.Muted);
        var size = icon.gameObject.AddComponent<LayoutElement>();
        size.preferredWidth = size.preferredHeight = OctopusSampleBranding.Dp(24f);
        size.minWidth = size.preferredWidth;
        size.flexibleWidth = 0f;
        var column = row.GetComponent<VerticalLayoutGroup>();
        if (column != null)
        {
            size.ignoreLayout = true;
            icon.anchorMin = icon.anchorMax = icon.pivot = new Vector2(0f, 1f);
            icon.sizeDelta = Vector2.one * OctopusSampleBranding.Dp(24f);
            icon.anchoredPosition = new Vector2(column.padding.left, -column.padding.top);
            column.padding.left += Mathf.RoundToInt(OctopusSampleBranding.Dp(32f));
        }
        // Stroke ignores layout; keep all existing text children and their names intact.
        icon.SetAsFirstSibling();
    }

    // Same 24dp, 2dp-stroke geometry as the tab icons' code-built fallback. No font glyph
    // dependency: the arrow remains available in the public sample without another asset.
    public static void Back(RectTransform parent, Color ink)
    {
        var icon = SampleUi.Panel("Icon", parent, OctopusSampleBranding.Clear);
        icon.anchorMin = icon.anchorMax = icon.pivot = new Vector2(0.5f, 0.5f);
        icon.sizeDelta = Vector2.one * OctopusSampleBranding.Dp(OctopusSampleBranding.MinInteractiveIconDp);
        icon.anchoredPosition = Vector2.zero;
        float[,] lines = { { 20, 12, 4, 12 }, { 4, 12, 11, 5 }, { 4, 12, 11, 19 } };
        for (int i = 0; i < lines.GetLength(0); i++)
        {
            var from = new Vector2(lines[i, 0] - 12f, 12f - lines[i, 1]);
            var to = new Vector2(lines[i, 2] - 12f, 12f - lines[i, 3]);
            var segment = SampleUi.Panel(SampleUi.StrokeName, icon, ink, 1f, ink, 0f);
            segment.GetComponent<Image>().raycastTarget = false;
            segment.anchorMin = segment.anchorMax = new Vector2(0.5f, 0.5f);
            segment.sizeDelta = new Vector2(OctopusSampleBranding.Dp((to - from).magnitude),
                OctopusSampleBranding.Dp(2f));
            segment.anchoredPosition = (from + to) * (0.5f * OctopusSampleBranding.UnitsPerDp);
            segment.localRotation = Quaternion.Euler(0f, 0f,
                Mathf.Atan2(to.y - from.y, to.x - from.x) * Mathf.Rad2Deg);
        }
    }

    public static string ForRow(string id)
    {
        if (id == OctopusSampleSettingsView.AppearanceRowId) return "palette";
        if (id == OctopusSampleSettingsView.AboutRowId) return "info";
        if (id == OctopusSampleAboutView.RepositoryRowId) return "code";
        if (id == OctopusSampleAboutView.DesignReferenceRowId) return "design_services";
        if (id == OctopusSampleAboutView.LicenceLinkId) return "description";
        if (id == "settings-feedback-row") return "feedback";
        return "bug_report";
    }
}
