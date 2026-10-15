using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Runtime versions, links and a plain-text platform notice. No Made with Unity badge artwork:
/// trademark usage and an official asset need separate validation (#139, decision D5).
/// </summary>
public class OctopusSampleAboutView : MonoBehaviour
{
    /// <summary>The screen's root, and the id a QA script waits on.</summary>
    public const string ScreenId = "about-screen";

    /// <summary>The card carrying the version rows.</summary>
    public const string VersionsCardId = "about-versions-card";

    /// <summary>The card carrying the licence notice.</summary>
    public const string LicencesCardId = "about-licences-card";

    /// <summary>The row opening the shared design target. Absent when no URL is configured.</summary>
    public const string DesignReferenceRowId = "about-design-reference-row";

    /// <summary>The card carrying the platform attribution and its trademark notice.</summary>
    public const string AttributionCardId = "about-attribution-card";

    /// <summary>The control that closes the screen.</summary>
    public const string BackId = "about-back";

    /// <summary>
    /// The Play update card, under the ids every sample shares (pm-tools AUTO_UPDATE.md). Built
    /// only on an Android player: in the Editor and on iOS there is no row at all, never an error.
    /// </summary>
    public const string UpdateCardId = "sample-update-card";

    /// <summary>The update card's status line.</summary>
    public const string UpdateStatusId = "sample-update-status";

    /// <summary>The update card's single button: "Update now" or "Check for update".</summary>
    public const string UpdateActionId = "sample-update-action";

    /// <summary>Shares the detail layer with scenario screens; Debug console and feedback can open above it.</summary>
    public const int SortingOrder = SampleUi.DetailSortingOrder;
    private RectTransform _root;
    private bool _rebuildRequested;

    /// <summary>
    /// Opens the screen over the current scene. A second call while one is open returns the open
    /// screen rather than stacking a duplicate — the same contract
    /// <see cref="OctopusScenarioScreenView.Open"/> honours.
    /// </summary>
    public static OctopusSampleAboutView Open()
    {
        var existing = FindAnyObjectByType<OctopusSampleAboutView>();
        if (existing != null) return existing;

        var host = new GameObject("OctopusSampleAboutView");
        var view = host.AddComponent<OctopusSampleAboutView>();
        // Built here rather than from Start(), so the screen is fully formed the moment Open
        // returns — for the caller, and for an EditMode test, where Start never runs.
        view.Build();
        OctopusSampleBranding.ThemeChanged += view.OnThemeChanged;
        OctopusSampleAppUpdate.Changed += view.OnThemeChanged;
        return view;
    }

    /// <summary>Whether an About screen is currently open.</summary>
    public static bool IsOpen
    {
        get { return FindAnyObjectByType<OctopusSampleAboutView>() != null; }
    }

    /// <summary>
    /// The sample's own version line. Pure and public so a test can pin the wording without
    /// naming a `UnityEngine.UI` type — the EditMode assembly cannot reference one.
    /// </summary>
    public static string SampleVersionText()
    {
        var version = Application.version;
        return string.IsNullOrEmpty(version) ? "unknown" : version;
    }

    public static string BuildText()
    {
#if UNITY_EDITOR
        return UnityEditor.EditorUserBuildSettings.activeBuildTarget == UnityEditor.BuildTarget.iOS
            ? UnityEditor.PlayerSettings.iOS.buildNumber : UnityEditor.PlayerSettings.Android.bundleVersionCode.ToString();
#elif UNITY_ANDROID
        using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
        using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
        using (var manager = activity.Call<AndroidJavaObject>("getPackageManager"))
        using (var info = manager.Call<AndroidJavaObject>("getPackageInfo", activity.Call<string>("getPackageName"), 0))
            return info.Get<int>("versionCode").ToString();
#else
        var build = OctopusSampleSystemAppearance.IosBundleVersion();
        return string.IsNullOrEmpty(build) ? "Unavailable" : build;
#endif
    }

    /// <summary>The engine line, for the same reason.</summary>
    public static string UnityVersionText()
    {
        return Application.unityVersion;
    }

    /// <summary>
    /// The Octopus SDK for Unity's own version, straight from the package's runtime constant. Not
    /// the native Android or iOS SDK's version: those run their own patch streams behind the pins,
    /// and this package may lag their minor.
    /// </summary>
    public static string SdkVersionText()
    {
        var version = OctopusSDK.Version;
        return string.IsNullOrEmpty(version) ? "unknown" : version;
    }

    /// <summary>
    /// The design target this sample implements, from the git-ignored
    /// <see cref="OctopusExampleConfig"/> asset, or an empty string when none is configured.
    /// Whitespace is trimmed, so an asset field a developer cleared by hand reads as absent rather
    /// than as a link to nowhere.
    /// </summary>
    public static string DesignReferenceUrl()
    {
#if OCTOPUS_INTERNAL
        var config = OctopusExampleConfig.LoadedOrNull;
        return ResolveDesignReferenceUrl(config == null ? null : config.DesignReferenceUrl);
#else
        return string.Empty;
#endif
    }

    public static string ResolveDesignReferenceUrl(string configuredUrl)
    {
        System.Uri uri;
        var url = string.IsNullOrEmpty(configuredUrl) ? string.Empty : configuredUrl.Trim();
        if (System.Uri.TryCreate(url, System.UriKind.Absolute, out uri) &&
            (uri.Scheme == System.Uri.UriSchemeHttps || uri.Scheme == System.Uri.UriSchemeHttp))
            return url;
        return string.Empty;
    }

    public const string RepositoryRowId = "about-repository-row";
    public const string LicenceLinkId = "about-licence-link";
    public const string RepositoryUrl = "https://github.com/Octopus-Community/octopus-sdk-unity";
    public const string LicenceUrl = RepositoryUrl + "/blob/main/LICENSE.md";

    /// <summary>
    /// Plain-text attribution; the sample carries no badge artwork.
    /// </summary>
    public static string MadeWithUnityText()
    {
        return "Made with Unity";
    }

    /// <summary>
    /// The notice Unity's trademark guidelines require from anything claiming the line above, in
    /// their own wording with this application's name substituted in.
    /// </summary>
    public static string NonAffiliationNoticeText()
    {
        var product = Application.productName;
        if (string.IsNullOrEmpty(product)) product = "This application";
        return product + " is not sponsored by or affiliated with Unity Technologies or its " +
               "affiliates. Unity is a trademark or registered trademark of Unity Technologies " +
               "or its affiliates in the U.S. and elsewhere.";
    }

    /// <summary>Closes the screen. Wired to <see cref="BackId"/>, and callable from a test.</summary>
    public void Close()
    {
        // The entry rides in this header: hand it back before the hierarchy goes away.
        OctopusSampleBranding.ThemeChanged -= OnThemeChanged;
        OctopusSampleAppUpdate.Changed -= OnThemeChanged;
        SampleUiDebugEntryHost.ReleaseFor(gameObject);
        gameObject.SetActive(false);
        if (Application.isPlaying) Destroy(gameObject);
        else DestroyImmediate(gameObject);
    }

    private void OnDestroy()
    {
        OctopusSampleBranding.ThemeChanged -= OnThemeChanged;
        OctopusSampleAppUpdate.Changed -= OnThemeChanged;
    }

    /// <summary>A theme change or a new update status: either way the screen rebuilds.</summary>
    private void OnThemeChanged()
    {
        if (this == null)
        {
            OctopusSampleBranding.ThemeChanged -= OnThemeChanged;
            OctopusSampleAppUpdate.Changed -= OnThemeChanged;
            return;
        }
        if (Application.isPlaying) _rebuildRequested = true;
        else Rebuild();
    }

    private void LateUpdate()
    {
        if (!_rebuildRequested) return;
        _rebuildRequested = false;
        Rebuild();
    }

    private void Rebuild()
    {
        var scroll = _root.GetComponentInChildren<ScrollRect>(true);
        float position = scroll == null ? 1f : scroll.verticalNormalizedPosition;
        SampleUiDebugEntryHost.ReleaseFor(gameObject);
        _root.gameObject.SetActive(false);
        _root.SetParent(null, false);
        if (Application.isPlaying) Destroy(_root.gameObject); else DestroyImmediate(_root.gameObject);
        Build();
        Canvas.ForceUpdateCanvases();
        scroll = _root.GetComponentInChildren<ScrollRect>(true);
        if (scroll != null) scroll.verticalNormalizedPosition = position;
    }

    private void Build()
    {
        if (GetComponent<Canvas>() == null) SampleUi.OverlayCanvas(gameObject, SortingOrder);

        // Full-bleed ground; readable and tappable content stays inside the safe area.
        var root = SampleUi.DetailPage(ScreenId, transform, Close);
        _root = root;

        BuildHeader(root);

        var content = SampleUi.VerticalScroll(root, SampleUi.OverlayPadding());
        content.GetComponent<VerticalLayoutGroup>().spacing = OctopusSampleBranding.Dp(OctopusSampleBranding.SpaceLg);
        var app = SampleUi.Card("about-app-card", content);
        SampleUi.FlexibleLabel(app, "App", SampleUi.TextTitle, SampleUi.TitleColor);
        SampleUi.FlexibleLabel(app, "Sample " + SampleVersionText() + " (" + BuildText() + ")", SampleUi.TextBody, SampleUi.TitleColor);
        BuildUpdateCard(content);

        var versions = SampleUi.Card(VersionsCardId, content);
        SampleUi.FlexibleLabel(versions, "Versions", SampleUi.TextTitle, SampleUi.TitleColor);
        BuildValueRow(versions, "Sample version", SampleVersionText());
        BuildValueRow(versions, "Build", BuildText());
        BuildValueRow(versions, "Native build pins", OctopusSampleNativePins.Current());
        BuildValueRow(versions, "SDK version", SdkVersionText());
        BuildValueRow(versions, "Unity version", UnityVersionText());

        SampleUi.ListRow(RepositoryRowId, content, "SDK repository", "Source and documentation",
            () => Application.OpenURL(RepositoryUrl));
        BuildDesignReferenceRow(content);
        SampleUi.ListRow(OctopusSampleShell.LegacyScenesId, content, "Legacy examples", "Transitional Unity examples",
            () => UnityEngine.SceneManagement.SceneManager.LoadScene(OctopusSampleShell.LegacyMenuScene));

        var licences = SampleUi.Card(LicencesCardId, content);
        SampleUi.FlexibleLabel(licences, "Licences", SampleUi.TextTitle, SampleUi.TitleColor);
        SampleUi.FlexibleLabel(licences, "Open-source notices for what this sample links against.", SampleUi.TextCaption, SampleUi.Muted);
        BuildValueRow(licences, "Octopus SDK", "Proprietary");
        BuildValueRow(licences, "Unity", "Unity terms of service");
        BuildValueRow(licences, "Firebase / EDM4U", "Apache-2.0");
        BuildValueRow(licences, "Liberation Sans font", "SIL Open Font Licence 1.1");
        SampleUi.ListRow("about-firebase-licence", licences, "Firebase / EDM4U notices", "Open-source licences",
            () => Application.OpenURL("https://github.com/firebase/firebase-unity-sdk/blob/main/LICENSE"));

        SampleUi.ListRow(LicenceLinkId, licences, "Read licence", "Opens the SDK licence in your browser",
            () => Application.OpenURL(LicenceUrl));

        var attribution = SampleUi.Card(AttributionCardId, content);
        SampleUi.FlexibleLabel(attribution, MadeWithUnityText(), SampleUi.TextTitle,
                               SampleUi.TitleColor);
        SampleUi.FlexibleLabel(attribution, NonAffiliationNoticeText(), SampleUi.TextCaption,
                               SampleUi.Muted);

        SampleUi.FlexibleLabel(content, "Built with the Octopus SDK for Unity.",
                               SampleUi.TextCaption, SampleUi.Muted);
    }

    /// <summary>
    /// Play's answer for this install, with one button. Its tap is the only way the update flow
    /// starts; any other state re-checks. Absent where Play cannot be asked.
    /// </summary>
    private static void BuildUpdateCard(RectTransform content)
    {
        var status = OctopusSampleAppUpdate.Status;
        if (status.State == OctopusSampleUpdateState.Unsupported) return;

        var card = SampleUi.Card(UpdateCardId, content);
        SampleUi.FlexibleLabel(card, "App update", SampleUi.TextTitle, SampleUi.TitleColor);
        var line = SampleUi.FlexibleLabel(card, status.Text, SampleUi.TextBody,
            status.IsAttention ? SampleUi.Attention : SampleUi.Muted);
        line.gameObject.name = UpdateStatusId;
        line.richText = false;
        if (status.State == OctopusSampleUpdateState.Checking) return;
        SampleUi.Button(UpdateActionId, card, status.ActionText,
            status.State == OctopusSampleUpdateState.UpdateAvailable ? SampleUiButtonVariant.Primary : SampleUiButtonVariant.Secondary,
            OctopusSampleAppUpdate.StartUpdate);
    }

    /// <summary>
    /// The internal design route is built only for a configured, usable web URL.
    /// </summary>
    private static void BuildDesignReferenceRow(RectTransform content)
    {
        var url = DesignReferenceUrl();
        if (string.IsNullOrEmpty(url)) return;

        SampleUi.ListRow(DesignReferenceRowId, content, "Design reference", "The shared sample design",
            () => Application.OpenURL(url));
    }

    private void BuildHeader(RectTransform root)
    {
        var header = SampleUi.AppBar("Header", root, "About", Close, BackId);
        SampleUiDebugEntryHost.Attach(header);
    }

    private static void BuildValueRow(RectTransform card, string label, string value)
    {
        var row = SampleUi.Panel(label, card, OctopusSampleBranding.Clear);
        var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = OctopusSampleBranding.Dp(OctopusSampleBranding.SpaceLg);
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        SampleUiIcons.Row(row, "info");
        var name = SampleUi.FlexibleLabel(row, label, SampleUi.TextCaption, SampleUi.Muted);
        name.gameObject.name = "Label";
        var read = SampleUi.FlexibleLabel(row, value, SampleUi.TextCaption, SampleUi.TitleColor);
        read.gameObject.name = "Value";
        read.alignment = TextAlignmentOptions.TopRight;
    }
}
