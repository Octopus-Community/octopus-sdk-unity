using System;
using UnityEngine;
using UnityEngine.UI;

public sealed class OctopusSampleHostIntegrationView : OctopusSampleSettingsDetailView
{
    private Action _returnTo;
    public const string ScreenId = "host-integration-screen";
    public static OctopusSampleHostIntegrationView Open(Action returnTo = null)
    {
        var existing = FindAnyObjectByType<OctopusSampleHostIntegrationView>();
        if (existing != null) return existing;
        var view = new GameObject("OctopusSampleHostIntegrationView").AddComponent<OctopusSampleHostIntegrationView>();
        view._returnTo = returnTo;
        view.InitializeView();
        OctopusSampleUnifiedProfile.Changed += view.Refresh;
        return view;
    }
    protected override void Unsubscribe()
    {
        base.Unsubscribe();
        OctopusSampleUnifiedProfile.Changed -= Refresh;
    }
    public override void Close() { var back = _returnTo; base.Close(); if (back != null) back(); }
    protected override void Dismiss() { base.Close(); }
    protected override void Build()
    {
        var content = Content(ScreenId, "Host app integration", "host-integration-back", true);
        SampleUi.FlexibleLabel(content, "What the sample app handles itself when the community asks.", SampleUi.TextBody, SampleUi.Muted);
        var navigation = SampleUi.Card("community-navigation-select", content);
        SampleUi.FlexibleLabel(navigation, "Community navigation", SampleUi.TextTitle, SampleUi.TitleColor);
        SampleUi.FlexibleLabel(navigation, "Full screen · the Unity SDK opens a native screen. In-tab navigation is unavailable.", SampleUi.TextCaption, SampleUi.Muted);
        var callbacks = SampleUi.Card("host-integration-callbacks", content);
        SampleUi.FlexibleLabel(callbacks, "Callbacks", SampleUi.TextTitle, SampleUi.TitleColor);
        Switch(callbacks, "profile", "Route profile taps to your own profile screen", "NavigateToProfileHandler · Enable the Unified Profile scenario to wire this callback.",
            OctopusSampleUnifiedProfile.RouteWired && (OctopusSampleUnifiedProfile.Effective == true),
            (OctopusSampleUnifiedProfile.Effective == true), () => { OctopusSampleUnifiedProfile.SetRouteWired(!OctopusSampleUnifiedProfile.RouteWired); Refresh(); });
        Switch(callbacks, "profile-edit", "App profile editor", "Unavailable: the Unity SDK has no profile-edit callback.", false, false, null);
        Switch(callbacks, "login", "Open your sign-in screen", "OnLoginRequired · Required in SSO mode: opens Account. Always wired.", true, false, null);
        Switch(callbacks, "client-object", "Handle taps on the content button", "OnNavigateToClientObject · A host message confirms taps; Reef Run challenges open the game.", OctopusSampleHostIntegration.ClientObject, true,
            () => { OctopusSampleHostIntegration.SetCallback("ClientObject", !OctopusSampleHostIntegration.ClientObject); Refresh(); });
        Switch(callbacks, "group-access-denied", "Gate access to locked groups", "OnGroupAccessDenied · Opening a locked group lets the app answer with its own dialog.", OctopusSampleHostIntegration.GroupAccess, true,
            () => { OctopusSampleHostIntegration.SetCallback("GroupAccess", !OctopusSampleHostIntegration.GroupAccess); Refresh(); });
        var urls = SampleUi.Card("host-integration-url-select", content);
        SampleUi.FlexibleLabel(urls, "Intercept URL openings", SampleUi.TextTitle, SampleUi.TitleColor);
        var labels = new[] { "Sample rule (default)", "Always open in the app", "Always let Octopus open" };
        var ids = new[] { "default", "app", "octopus" };
        for (var i = 0; i < labels.Length; i++)
        {
            var mode = i;
            SampleUi.Button("host-integration-url-" + ids[i], urls, labels[i],
                OctopusSampleHostIntegration.UrlMode == i ? SampleUiButtonVariant.Secondary : SampleUiButtonVariant.Tertiary,
                () => { OctopusSampleHostIntegration.SetUrlMode(mode); Refresh(); });
        }
        SampleUi.FlexibleLabel(urls, "The sample rule handles the app's contact link; Octopus opens every other link. The app reports handled links in its own screen.", SampleUi.TextCaption, SampleUi.Muted);
        SampleUi.FlexibleLabel(content, "Changes apply immediately. URL, content and group choices survive a restart; Unified Profile owns its profile route.", SampleUi.TextCaption, SampleUi.Muted);
    }
    private static void Switch(Transform parent, string id, string title, string detail, bool value, bool enabled, UnityEngine.Events.UnityAction action)
    {
        var row = SampleUi.Card("host-integration-" + id + "-row", parent);
        SampleUi.FlexibleLabel(row, title, SampleUi.TextBody, SampleUi.TitleColor);
        SampleUi.FlexibleLabel(row, detail, SampleUi.TextCaption, SampleUi.Muted);
        SampleUi.Switch("host-integration-" + id + "-switch", row, value, enabled, action);
    }
}
