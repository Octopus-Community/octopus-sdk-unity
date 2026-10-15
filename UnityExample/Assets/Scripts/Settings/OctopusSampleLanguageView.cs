using UnityEngine;

public sealed class OctopusSampleLanguageView : OctopusSampleSettingsDetailView
{
    public const string ScreenId = "language-screen";
    public static OctopusSampleLanguageView Open()
    {
        var existing = FindAnyObjectByType<OctopusSampleLanguageView>();
        if (existing != null) return existing;
        var view = new GameObject("OctopusSampleLanguageView").AddComponent<OctopusSampleLanguageView>();
        view.InitializeView();
        return view;
    }
    public void Select(string code) { OctopusSampleLanguage.Select(code); Refresh(); }
    protected override void Build()
    {
        var content = Content(ScreenId, "Language", "language-back", true);
        var card = SampleUi.Card("language-choices", content);
        SampleUi.FlexibleLabel(card, "SDK language", SampleUi.TextTitle, SampleUi.TitleColor);
        SampleUi.FlexibleLabel(card, "Applies to the Octopus screens only — the sample itself stays in English.", SampleUi.TextCaption, SampleUi.Muted);
        for (var i = 0; i < OctopusSampleLanguage.Codes.Length; i++)
        {
            var code = OctopusSampleLanguage.Codes[i];
            bool selected = (OctopusSampleState.LocaleOverride ?? "") == code;
            SampleUi.Button("language-" + (code == "" ? "system" : code), card, OctopusSampleLanguage.Names[i],
                selected ? SampleUiButtonVariant.Secondary : SampleUiButtonVariant.Tertiary, () => Select(code));
        }
        SampleUi.FlexibleLabel(content, "The override survives a restart. Follow system applies the current device language again when the sample resumes; the Unity bridge cannot clear a locale override.", SampleUi.TextCaption, SampleUi.Muted);
    }
}
