using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// The Play In-App Update decision table, pinned with the cases of the Android sample's
/// AppUpdateCheckerTest and the Flutter sample's update tests, plus where the sample surfaces it.
/// </summary>
public class OctopusSampleAppUpdateTests
{
    private readonly List<GameObject> _spawned = new List<GameObject>();

    [SetUp]
    public void SetUp()
    {
        PlayerPrefs.DeleteKey(OctopusSampleAppUpdate.DismissedVersionCodeKey);
        OctopusSampleAppUpdate.ResetForTests();
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var host in _spawned)
        {
            if (host == null) continue;
            var shell = host.GetComponent<OctopusSampleShell>();
            if (shell != null) shell.Shutdown();
            if (host != null) Object.DestroyImmediate(host);
        }
        _spawned.Clear();
        var about = Object.FindAnyObjectByType<OctopusSampleAboutView>();
        if (about != null) about.Close();
        PlayerPrefs.DeleteKey(OctopusSampleAppUpdate.DismissedVersionCodeKey);
        OctopusSampleAppUpdate.ResetForTests();
    }

    [TestCase(1, false, OctopusSampleUpdateState.UpToDate)]   // UPDATE_NOT_AVAILABLE
    [TestCase(0, true, OctopusSampleUpdateState.UpToDate)]    // UNKNOWN
    [TestCase(3, true, OctopusSampleUpdateState.UpToDate)]    // DEVELOPER_TRIGGERED_UPDATE_IN_PROGRESS
    [TestCase(2, true, OctopusSampleUpdateState.UpdateAvailable)]
    [TestCase(2, false, OctopusSampleUpdateState.UpdateNotInstallable)]
    public void PlayAnswerMapsLikeTheOtherSamples(int availability, bool immediate, OctopusSampleUpdateState expected)
    {
        Assert.AreEqual(expected, OctopusSampleUpdateStatus.Of(availability, immediate, 42).State);
    }

    [Test]
    public void AvailableUpdateCarriesItsVersionCode()
    {
        Assert.AreEqual(42, OctopusSampleUpdateStatus.Of(2, true, 42).AvailableVersionCode);
    }

    [TestCase(-6)]
    [TestCase(-9)]
    [TestCase(-10)]
    public void NotOwnedByPlayFoldsIntoNotFromPlayStore(int code)
    {
        var status = OctopusSampleUpdateStatus.StatusOf(code);
        Assert.AreEqual(OctopusSampleUpdateState.NotFromPlayStore, status.State);
        Assert.AreEqual("Not installed from Play Store", status.Text);
        Assert.IsFalse(status.IsAttention, "A sideload is a normal answer, not an error.");
    }

    [Test]
    public void OtherFailuresStayRetryableWithTheirCode()
    {
        var coded = OctopusSampleUpdateStatus.StatusOf(-100);
        Assert.AreEqual(OctopusSampleUpdateState.Error, coded.State);
        Assert.AreEqual(-100, coded.ErrorCode);
        Assert.AreEqual("Update check failed (code -100) — tap to retry", coded.Text);
        var bare = OctopusSampleUpdateStatus.StatusOf(null);
        Assert.AreEqual(OctopusSampleUpdateState.Error, bare.State);
        Assert.IsNull(bare.ErrorCode);
        Assert.AreEqual("Update check failed — tap to retry", bare.Text);
        Assert.IsTrue(bare.IsAttention);
    }

    [Test]
    public void LabelsMatchTheAndroidCard()
    {
        Assert.AreEqual("Tap to check for updates", OctopusSampleUpdateStatus.Unknown.Text);
        Assert.AreEqual("App is up to date", OctopusSampleUpdateStatus.UpToDate.Text);
        Assert.AreEqual("A new version is available", OctopusSampleUpdateStatus.Available(7).Text);
        Assert.AreEqual("A new version exists, but Play won't install it over this build",
            OctopusSampleUpdateStatus.NotInstallable.Text);
        Assert.IsTrue(OctopusSampleUpdateStatus.NotInstallable.IsAttention);
        Assert.AreEqual("Update now", OctopusSampleUpdateStatus.Available(7).ActionText);
        Assert.AreEqual("Check for update", OctopusSampleUpdateStatus.NotInstallable.ActionText);
        Assert.AreEqual("Check for update", OctopusSampleUpdateStatus.NotFromPlayStore.ActionText);
    }

    [Test]
    public void OnlyAnUndismissedInstallableUpdateIsAnnounced()
    {
        Assert.IsTrue(OctopusSampleUpdateStatus.Available(7).ShouldAnnounce(0));
        Assert.IsFalse(OctopusSampleUpdateStatus.Available(7).ShouldAnnounce(7));
        Assert.IsTrue(OctopusSampleUpdateStatus.Available(8).ShouldAnnounce(7), "A newer build is announced again.");
        Assert.IsFalse(OctopusSampleUpdateStatus.NotInstallable.ShouldAnnounce(0));
        Assert.IsFalse(OctopusSampleUpdateStatus.NotFromPlayStore.ShouldAnnounce(0));
        Assert.IsFalse(OctopusSampleUpdateStatus.StatusOf(-100).ShouldAnnounce(0));
    }

    [Test]
    public void EditorIsUnsupportedAndAboutShowsNoUpdateCard()
    {
        Assert.IsFalse(OctopusSampleAppUpdate.IsSupported);
        Assert.AreEqual(OctopusSampleUpdateState.Unsupported, OctopusSampleAppUpdate.Status.State);
        Assert.AreEqual(string.Empty, OctopusSampleAppUpdate.Status.Text);
        var about = OctopusSampleAboutView.Open();
        _spawned.Add(about.gameObject);
        Assert.IsNull(Find(about.transform, OctopusSampleAboutView.UpdateCardId));
    }

    [Test]
    public void AboutCardShowsTheStatusAndFollowsIt()
    {
        OctopusSampleAppUpdate.Publish(OctopusSampleUpdateStatus.NotFromPlayStore);
        var about = OctopusSampleAboutView.Open();
        _spawned.Add(about.gameObject);
        Assert.IsNotNull(Find(about.transform, OctopusSampleAboutView.UpdateCardId));
        Assert.AreEqual("Not installed from Play Store",
            Find(about.transform, OctopusSampleAboutView.UpdateStatusId).GetComponent<TMP_Text>().text);
        Assert.AreEqual("Check for update", ActionLabel(about.transform, OctopusSampleAboutView.UpdateActionId));

        // Edit mode rebuilds synchronously on the status event.
        OctopusSampleAppUpdate.Publish(OctopusSampleUpdateStatus.Available(9));
        Assert.AreEqual("A new version is available",
            Find(about.transform, OctopusSampleAboutView.UpdateStatusId).GetComponent<TMP_Text>().text);
        Assert.AreEqual("Update now", ActionLabel(about.transform, OctopusSampleAboutView.UpdateActionId));
    }

    [Test]
    public void SettingsAboutRowLeadsWithAnAvailableUpdate()
    {
        StringAssert.DoesNotStartWith("Update available", OctopusSampleSettingsView.AboutSubtitle());
        OctopusSampleAppUpdate.Publish(OctopusSampleUpdateStatus.Available(9));
        StringAssert.StartsWith("Update available · Sample ", OctopusSampleSettingsView.AboutSubtitle());
    }

    [Test]
    public void LaunchBannerShowsOnlyForAnUndismissedUpdate()
    {
        var shell = OctopusSampleShell.Create();
        _spawned.Add(shell.gameObject);
        Assert.IsNull(Find(shell.transform, OctopusSampleShell.UpdateBannerId), "Silent before any answer.");

        OctopusSampleAppUpdate.Publish(OctopusSampleUpdateStatus.NotFromPlayStore);
        Assert.IsNull(Find(shell.transform, OctopusSampleShell.UpdateBannerId), "Silent on a sideload.");

        OctopusSampleAppUpdate.Publish(OctopusSampleUpdateStatus.Available(9));
        Assert.IsNotNull(Find(shell.transform, OctopusSampleShell.UpdateBannerId));

        OctopusSampleAppUpdate.DismissAnnouncement();
        Assert.AreEqual(9, OctopusSampleAppUpdate.DismissedVersionCode);
        Assert.IsNull(Find(shell.transform, OctopusSampleShell.UpdateBannerId), "Dismissed for this build.");

        OctopusSampleAppUpdate.Publish(OctopusSampleUpdateStatus.Available(10));
        Assert.IsNotNull(Find(shell.transform, OctopusSampleShell.UpdateBannerId), "A newer build comes back.");
    }

    private static string ActionLabel(Transform root, string id)
    {
        return Find(root, id).GetComponentInChildren<TMP_Text>(true).text;
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
