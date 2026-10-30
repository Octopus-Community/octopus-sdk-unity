using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class LanguageOverrideExample : MonoBehaviour
{
    private TMP_Text statusText;

    void Start()
    {
        MainMenu.RegisterSsoLoginRoute();
        OctopusSDK.Initialize(OctopusExampleConfig.Instance.Default.apiKey, ConnectionMode.SSO(),
            OctopusExampleConfig.Instance.ApiServerHost);

        var canvas = new GameObject("Canvas");
        var canvasComp = canvas.AddComponent<Canvas>();
        canvasComp.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvas.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(800, 600);
        canvas.AddComponent<GraphicRaycaster>();

        // Title
        statusText = CreateText(canvas.transform, "Select a language, then open Octopus", 0, 200, 30);

        // Language buttons
        CreateButton(canvas.transform, "English (en)", 0, 100, () => SetLanguage("en"));
        CreateButton(canvas.transform, "French (fr)", 0, 30, () => SetLanguage("fr"));
        CreateButton(canvas.transform, "Spanish (es)", 0, -40, () => SetLanguage("es"));
        CreateButton(canvas.transform, "Turkish (tr)", 0, -110, () => SetLanguage("tr"));

        // Open Octopus button
        CreateButton(canvas.transform, "Open Octopus", 0, -210, OnOpenOctopus);
    }

    void OnDestroy()
    {
        MainMenu.UnregisterSsoLoginRoute();
    }

    void SetLanguage(string languageCode)
    {
        OctopusSDK.OverrideDefaultLocale(languageCode);
        statusText.text = "Language set to: " + languageCode;
    }

    void OnOpenOctopus()
    {
        OctopusSDK.Open();
    }

    void CreateButton(Transform parent, string label, float x, float y, UnityEngine.Events.UnityAction onClick)
    {
        var buttonObj = new GameObject(label + "Button");
        buttonObj.transform.SetParent(parent, false);

        var rect = buttonObj.AddComponent<RectTransform>();
        rect.anchoredPosition = new Vector2(x, y);
        rect.sizeDelta = new Vector2(400, 60);

        var image = buttonObj.AddComponent<Image>();
        image.color = new Color(0.9f, 0.9f, 0.9f);

        var button = buttonObj.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(onClick);

        var text = SampleUi.Label("Text", buttonObj.transform, label, 30,
            SampleUi.TitleColor, TextAnchor.MiddleCenter);
        SampleUi.Stretch(text.rectTransform, Vector2.zero, Vector2.one);
        text.fontStyle = FontStyles.Bold;
    }

    TMP_Text CreateText(Transform parent, string content, float x, float y, int fontSize)
    {
        var text = SampleUi.Label("StatusText", parent, content, fontSize,
            OctopusSampleBranding.Palette.OnChrome, TextAnchor.MiddleCenter);
        text.rectTransform.anchoredPosition = new Vector2(x, y);
        text.rectTransform.sizeDelta = new Vector2(700, 60);
        return text;
    }
}
