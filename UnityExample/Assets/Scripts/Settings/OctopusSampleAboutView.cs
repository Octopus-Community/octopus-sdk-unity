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

    /// <summary>Shares the detail layer with scenario screens; Debug console and feedback can open above it.</summary>
    public const int SortingOrder = SampleUi.DetailSortingOrder;

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
        SampleUiDebugEntryHost.ReleaseAll();
        if (Application.isPlaying) Destroy(gameObject);
        else DestroyImmediate(gameObject);
    }

    private void Build()
    {
        SampleUi.OverlayCanvas(gameObject, SortingOrder);

        // Full-bleed ground; readable and tappable content stays inside the safe area.
        var ground = SampleUi.Panel("Ground", transform, SampleUi.Background);
        SampleUi.Stretch(ground, Vector2.zero, Vector2.one);

        var root = SampleUi.SafeArea(ScreenId, ground);

        BuildHeader(root);

        var content = SampleUi.VerticalScroll(root, SampleUi.OverlayPadding());
        content.GetComponent<VerticalLayoutGroup>().spacing = OctopusSampleBranding.Dp(OctopusSampleBranding.SpaceLg);
        SampleUi.FlexibleLabel(content, OctopusSampleBranding.AppName, SampleUi.TextTitleXl, SampleUi.TitleColor);
        SampleUi.FlexibleLabel(content, "Explore the Octopus SDK for Unity software.", SampleUi.TextBody, SampleUi.Muted);

        var versions = SampleUi.Card(VersionsCardId, content);
        SampleUi.FlexibleLabel(versions, "Versions", SampleUi.TextTitle, SampleUi.TitleColor);
        BuildValueRow(versions, "Sample version", SampleVersionText());
        BuildValueRow(versions, "SDK version", SdkVersionText());
        BuildValueRow(versions, "Unity version", UnityVersionText());

        SampleUi.ListRow(RepositoryRowId, content, "SDK repository", "Source and documentation",
            () => Application.OpenURL(RepositoryUrl));
        BuildDesignReferenceRow(content);

        var licences = SampleUi.Card(LicencesCardId, content);
        SampleUi.FlexibleLabel(licences, "Licences", SampleUi.TextTitle, SampleUi.TitleColor);
        SampleUi.FlexibleLabel(licences,
                               "Octopus SDK for Unity — full terms in LICENSE.md, at the root of " +
                               "the SDK repository.",
                               SampleUi.TextCaption, SampleUi.Muted);

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
