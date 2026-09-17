using TMPro;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Proves that the catalogue's `communityData` preset 6 now reaches a destination on Unity: the
/// scenario screen presents <see cref="OctopusSampleClientProfileView"/>, that page carries the
/// destination ids the QA Tester asserts on (`clientProfile-data` / `clientProfile-unknown` /
/// `clientProfile-error`) plus `clientProfile-back`, and it follows the pilot's fetch as it
/// completes. SDK calls use the recorder, never a network.
/// </summary>
public class OctopusSampleClientProfileViewTests
{
    private readonly List<GameObject> _spawned = new List<GameObject>();
    private OctopusRecordingScenarioSdk _sdk;
    private OctopusSampleTheme _previousTheme;

    [SetUp]
    public void SetUp()
    {
        _previousTheme = OctopusSampleBranding.Theme;
        OctopusSampleState.Reset();
        OctopusSampleFeatureToggles.Reset();
        _sdk = new OctopusRecordingScenarioSdk
        {
            Profile = new OctopusExampleConfig.ExampleProfile
            {
                apiKey = "test-key", authToken = "not-a-real-token", userId = "client-user-1"
            }
        };
        OctopusScenarioSdk.Use(_sdk);
    }

    [TearDown]
    public void TearDown()
    {
        var page = Object.FindAnyObjectByType<OctopusSampleClientProfileView>();
        if (page != null) Object.DestroyImmediate(page.gameObject);
        foreach (var host in _spawned)
        {
            if (host != null) Object.DestroyImmediate(host);
        }
        _spawned.Clear();
        OctopusSampleLog.Current = OctopusSampleLog.None;
        OctopusScenarioSdk.Use(null);
        OctopusSampleState.Reset();
        OctopusSampleFeatureToggles.Reset();
        OctopusSampleBranding.Theme = _previousTheme;
    }

    [Test]
    public void Preset6OpensTheHostProfilePageWithTheFetchedData()
    {
        _sdk.CommunityDataResult = new OctopusCommunityData("profile-42", 14, new OctopusGamification(2));
        var view = OpenCommunityData();

        Tap(view.transform, "qa-preset-communityData-6");

        var page = Object.FindAnyObjectByType<OctopusSampleClientProfileView>();
        Assert.IsNotNull(page, "Preset 6 must present the host profile page.");
        Assert.IsNotNull(Find(page.transform, "clientProfile-data"));
        Assert.IsNull(Find(page.transform, "clientProfile-unknown"));
        Assert.IsNull(Find(page.transform, "clientProfile-error"));
        Assert.IsNull(Find(page.transform, OctopusSampleClientProfileView.LoadingTestId));
        Assert.IsNotNull(Find(page.transform, OctopusSampleClientProfileView.BackTestId));
        var text = AllText(page.transform);
        StringAssert.Contains("client-user-1", text);
        StringAssert.Contains("profile-42", text);
        StringAssert.Contains("14", text);
        StringAssert.Contains("2", text);
        StringAssert.Contains("Host profile requested", Result(view).text);
        Assert.AreEqual(1, Count(_sdk.ScenarioMethods, "FetchCommunityData"), "Rendering the page must not fetch again.");
    }

    [Test]
    public void PageShowsLoadingThenErrorAsTheFetchCompletes()
    {
        _sdk.DeferCompletions = true;
        var view = OpenCommunityData();

        Tap(view.transform, "qa-preset-communityData-6");
        var page = Object.FindAnyObjectByType<OctopusSampleClientProfileView>();
        Assert.IsNotNull(page);
        Assert.IsNotNull(Find(page.transform, OctopusSampleClientProfileView.LoadingTestId));
        Assert.IsNull(Find(page.transform, "clientProfile-data"));

        _sdk.FetchError("network down");

        Assert.IsNull(Find(page.transform, OctopusSampleClientProfileView.LoadingTestId));
        Assert.IsNotNull(Find(page.transform, "clientProfile-error"));
        StringAssert.Contains("network down", AllText(page.transform));
    }

    [Test]
    public void UnknownMemberRendersTheUnknownPanel()
    {
        _sdk.CommunityDataResult = null;
        var view = OpenCommunityData();

        Tap(view.transform, "qa-preset-communityData-6");

        var page = Object.FindAnyObjectByType<OctopusSampleClientProfileView>();
        Assert.IsNotNull(page);
        Assert.IsNotNull(Find(page.transform, "clientProfile-unknown"));
        Assert.IsNull(Find(page.transform, "clientProfile-data"));
    }

    [Test]
    public void BackClosesThePageAndLeavesTheScenarioScreenOpen()
    {
        _sdk.CommunityDataResult = new OctopusCommunityData("profile-42");
        var view = OpenCommunityData();
        Tap(view.transform, "qa-preset-communityData-6");
        var page = Object.FindAnyObjectByType<OctopusSampleClientProfileView>();
        Assert.IsNotNull(page);

        Tap(page.transform, OctopusSampleClientProfileView.BackTestId);

        Assert.IsNull(Object.FindAnyObjectByType<OctopusSampleClientProfileView>());
        Assert.IsNotNull(Object.FindAnyObjectByType<OctopusScenarioScreenView>());
    }

    [Test]
    public void DismissingTheScenarioScreenClosesThePageToo()
    {
        _sdk.CommunityDataResult = new OctopusCommunityData("profile-42");
        var view = OpenCommunityData();
        Tap(view.transform, "qa-preset-communityData-6");
        Assert.IsNotNull(Object.FindAnyObjectByType<OctopusSampleClientProfileView>());

        view.Dismiss();

        Assert.IsNull(Object.FindAnyObjectByType<OctopusSampleClientProfileView>());
    }

    [Test]
    public void ASecondRequestRebindsTheOpenPageInsteadOfStackingAnother()
    {
        var first = new OctopusScenarioHostProfile("member-a");
        var second = new OctopusScenarioHostProfile("member-b");
        var page = OctopusSampleClientProfileView.Open(first);
        _spawned.Add(page.gameObject);

        var again = OctopusSampleClientProfileView.Open(second);

        Assert.AreSame(page, again);
        Assert.AreSame(second, page.Profile);
        StringAssert.Contains("member-b", AllText(page.transform));
        var rootBefore = page.transform.GetChild(0);
        first.Complete(new OctopusCommunityData("stale"));
        Assert.AreSame(rootBefore, page.transform.GetChild(0), "The page must not rebuild for a destination it left.");
        Assert.IsNotNull(Find(page.transform, OctopusSampleClientProfileView.LoadingTestId));
        second.Complete(new OctopusCommunityData("profile-b"));
        Assert.IsNotNull(Find(page.transform, "clientProfile-data"));
        StringAssert.Contains("profile-b", AllText(page.transform));
    }

    [Test]
    public void ADestinationCompletingAfterDismissDoesNotTouchTheClosedPage()
    {
        var profile = new OctopusScenarioHostProfile("member-a");
        var page = OctopusSampleClientProfileView.Open(profile);

        page.Dismiss();

        Assert.DoesNotThrow(() => profile.Complete(new OctopusCommunityData("late")));
        Assert.DoesNotThrow(() => OctopusSampleBranding.Theme = OctopusSampleBranding.Theme == OctopusSampleTheme.Dark
            ? OctopusSampleTheme.Light : OctopusSampleTheme.Dark);
        Assert.IsNull(Object.FindAnyObjectByType<OctopusSampleClientProfileView>());
    }

    private OctopusScenarioScreenView OpenCommunityData()
    {
        var view = OctopusScenarioScreenView.Open(new CommunityDataScenario());
        _spawned.Add(view.gameObject);
        return view;
    }

    private static void Tap(Transform root, string id)
    {
        var node = Find(root, id);
        Assert.IsNotNull(node, "Missing control " + id);
        Assert.IsTrue(node.gameObject.activeInHierarchy, "Hidden control " + id);
        node.GetComponent<Button>().onClick.Invoke();
    }

    private static TMP_Text Result(OctopusScenarioScreenView view)
    {
        return Find(view.transform, view.ScenarioId + "-result").GetComponentInChildren<TMP_Text>();
    }

    private static string AllText(Transform root)
    {
        var builder = new System.Text.StringBuilder();
        foreach (var label in root.GetComponentsInChildren<TMP_Text>()) builder.AppendLine(label.text);
        return builder.ToString();
    }

    private static int Count(IEnumerable<string> methods, string name)
    {
        var count = 0;
        foreach (var method in methods) if (method == name) count++;
        return count;
    }

    private static Transform Find(Transform root, string name)
    {
        if (root.gameObject.name == name) return root;
        for (var i = 0; i < root.childCount; i++)
        {
            var found = Find(root.GetChild(i), name);
            if (found != null) return found;
        }
        return null;
    }
}
