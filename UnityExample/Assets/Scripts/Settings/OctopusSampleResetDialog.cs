using UnityEngine;
using UnityEngine.UI;

public static class OctopusSampleResetDialog
{
    public const string Wording = "Clears the sample's saved setup — server, community, SSO user, theme — and restarts on the Config screen. Nothing is deleted on the server.";
    public static GameObject Open(UnityEngine.Events.UnityAction cancel, UnityEngine.Events.UnityAction confirm)
    {
        var host = new GameObject("settings-reset-dialog");
        SampleUi.OverlayCanvas(host, SampleUi.DebugConsoleSortingOrder);
        var scrim = OctopusSampleBranding.DarkBackground; scrim.a = 0.65f;
        var ground = SampleUi.Panel("Scrim", host.transform, scrim);
        SampleUi.Stretch(ground, Vector2.zero, Vector2.one);
        var dismiss = ground.gameObject.AddComponent<Button>();
        dismiss.onClick.AddListener(cancel);
        var safe = SampleUi.SafeArea("SafeArea", host.transform);
        var card = SampleUi.Card("settings-reset-confirmation", safe);
        card.anchorMin = card.anchorMax = card.pivot = new Vector2(0.5f, 0.5f);
        card.sizeDelta = new Vector2(OctopusSampleBranding.Dp(312f), 0f);
        // Unlike cards in a scroll's layout stack, this centered card owns its height.
        card.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        SampleUi.FlexibleLabel(card, "Reset Configuration?", SampleUi.TextTitle, SampleUi.TitleColor);
        SampleUi.FlexibleLabel(card, Wording, SampleUi.TextBody, SampleUi.Muted);
        // Android AlertDialog: two 48dp text buttons side by side, right-aligned, Reset in red.
        var actions = SampleUi.Panel("Actions", card, OctopusSampleBranding.Clear);
        var row = actions.gameObject.AddComponent<HorizontalLayoutGroup>();
        row.spacing = OctopusSampleBranding.Dp(OctopusSampleBranding.SpaceSm);
        row.childAlignment = TextAnchor.MiddleRight;
        row.childControlWidth = row.childControlHeight = true;
        row.childForceExpandWidth = row.childForceExpandHeight = false;
        var p = OctopusSampleBranding.Palette;
        var back = SampleUiBackCancel.Mark(TextButton(OctopusSampleSettingsView.ResetCancelId, actions, "Cancel", p.Accent, cancel));
        card.gameObject.AddComponent<SampleUiBackTarget>().Back = back.GetComponent<Button>();
        TextButton(OctopusSampleSettingsView.ResetConfirmId, actions, "Reset", p.Negative, confirm);
        return host;
    }

    private static RectTransform TextButton(string name, Transform parent, string text, Color ink,
                                            UnityEngine.Events.UnityAction onClick)
    {
        const float radius = 20f;
        var rect = SampleUi.Button(name, parent, text, SampleUiButtonVariant.Tertiary, onClick, radius);
        var button = rect.GetComponent<SampleUiButton>();
        button.PageVariant = null;
        var label = rect.Find("Label").GetComponent<TMPro.TMP_Text>();
        label.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
        int side = Mathf.RoundToInt(OctopusSampleBranding.Dp(OctopusSampleBranding.SpaceMd));
        rect.GetComponent<VerticalLayoutGroup>().padding = new RectOffset(side, side, 0, 0);
        button.Configure(rect.GetComponent<Image>(), rect.Find(SampleUi.StrokeName).GetComponent<Image>(), label,
            OctopusSampleBranding.Clear, ink, OctopusSampleBranding.Clear, radius);
        var size = rect.GetComponent<LayoutElement>();
        size.minHeight = size.preferredHeight = OctopusSampleBranding.MinTouchUnits;
        size.flexibleHeight = size.flexibleWidth = 0f;
        size.preferredWidth = SampleUi.PreferredButtonWidth(rect);
        return rect;
    }
}
