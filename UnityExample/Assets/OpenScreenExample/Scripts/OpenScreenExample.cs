using System;
using UnityEngine;

// Demonstrates OctopusSDK.OpenPost and OctopusSDK.OpenCreatePost, including a
// prefilled CTA and intercepting CTA/link taps.
// Wire the public methods below to UI buttons in the example scene.
public class OpenScreenExample : MonoBehaviour
{
    [SerializeField] private string postId = "";
    [SerializeField] private string prefilledText = "Hello from Unity!";
    [SerializeField] private string prefilledTopicId = "";
    [SerializeField] private string prefilledImagePath = "";
    [SerializeField] private string prefilledCtaLabel = "Visit our shop";
    [SerializeField] private string prefilledCtaUrl = "https://www.octopuscommunity.com";

    void Start()
    {
        OctopusSDK.Initialize(OctopusExampleConfig.Instance.Default.apiKey, ConnectionMode.OctopusAuth());

        // Intercept CTA/link taps so the app can deep-link or defer to Octopus.
        OctopusSDK.NavigateToUrlHandler = OnNavigateToUrl;
        // Taps on a post linked to one of our own objects (bridge CTA).
        OctopusSDK.OnNavigateToClientObject += OnNavigateToClientObject;
    }

    void OnDestroy()
    {
        // NavigateToUrlHandler is a single shared slot (not a multicast event), so only
        // clear it if it's still ours — avoid clobbering a handler set by another scene.
        if (OctopusSDK.NavigateToUrlHandler == OnNavigateToUrl)
        {
            OctopusSDK.NavigateToUrlHandler = null;
        }
        OctopusSDK.OnNavigateToClientObject -= OnNavigateToClientObject;
    }

    public void OnOpenPostClicked()
    {
        OctopusSDK.OpenPost(postId);
    }

    public void OnOpenCreatePostClicked()
    {
        OctopusSDK.OpenCreatePost(new OctopusPrefilledPost
        {
            Text = prefilledText,
            TopicId = string.IsNullOrEmpty(prefilledTopicId) ? null : prefilledTopicId,
            ImagePath = string.IsNullOrEmpty(prefilledImagePath) ? null : prefilledImagePath,
            CtaLabel = string.IsNullOrEmpty(prefilledCtaLabel) ? null : prefilledCtaLabel,
            CtaUrl = string.IsNullOrEmpty(prefilledCtaUrl) ? null : prefilledCtaUrl
        });
    }

    // Demo URL interception: handle our own deep links in-game, let Octopus open the rest.
    private UrlOpeningStrategy OnNavigateToUrl(string url)
    {
        Debug.Log($"[OpenScreenExample] URL tapped: {url}");
        // "mygame://" is a placeholder — replace with your own deep-link scheme.
        if (!string.IsNullOrEmpty(url) && url.StartsWith("mygame://"))
        {
            Debug.Log("[OpenScreenExample] handled in-game; not opening a browser");
            return UrlOpeningStrategy.HandledByApp;
        }
        return UrlOpeningStrategy.HandledByOctopus;
    }

    // Demo bridge CTA: a post linked to one of our own objects was tapped.
    private void OnNavigateToClientObject(string clientObjectId)
    {
        Debug.Log($"[OpenScreenExample] navigate to client object: {clientObjectId}");
    }

#if UNITY_EDITOR
    // Editor-only mock drivers — invoke from the component's context menu (⋮) while in Play mode
    // to simulate Octopus callbacks without a device build. See "Iterating in the Editor" in the manual.
    [ContextMenu("Mock: Emit not-seen count = 3")]
    private void MockEmitNotSeen() => OctopusSDK.Mock.EmitNotSeenCount(3);

    [ContextMenu("Mock: Emit login required")]
    private void MockEmitLoginRequired() => OctopusSDK.Mock.EmitLoginRequired();
#endif
}
