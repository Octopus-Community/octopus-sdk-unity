using TMPro;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

public class OctopusSampleDeveloperToolsViewTests
{
    private OctopusSampleDeveloperToolsView _view;
    private readonly List<OctopusSampleDeveloperToolsView.LogLine> _entries =
        new List<OctopusSampleDeveloperToolsView.LogLine>();
    private readonly List<OctopusSampleDeveloperToolsView.InfoFact> _facts =
        new List<OctopusSampleDeveloperToolsView.InfoFact>();
    private int _version;
    private int _consoleOpened;
    private string _clipboard;

    [SetUp]
    public void SetUp()
    {
        _clipboard = GUIUtility.systemCopyBuffer;
        _entries.Clear();
        _facts.Clear();
        _facts.Add(new OctopusSampleDeveloperToolsView.InfoFact("debug-info-sdk-version", "SDK version", "test"));
        _version = _consoleOpened = 0;
        OctopusSampleState.Reset();
        OctopusSampleFeatureToggles.Reset();
        _view = OctopusSampleDeveloperToolsView.Open(() => _version, () => _entries,
            () => _facts,
            () => _consoleOpened++);
    }

    [TearDown]
    public void TearDown()
    {
        if (_view != null) Object.DestroyImmediate(_view.gameObject);
        GUIUtility.systemCopyBuffer = _clipboard;
        OctopusSampleState.Reset();
        OctopusSampleFeatureToggles.Reset();
        OctopusSampleLog.Current = OctopusSampleLog.None;
    }

    private void Click(string id)
    {
        _view.GetComponentsInChildren<Button>().Single(b => b.name == id).onClick.Invoke();
    }

    [Test]
    public void IndexOffersThreeDestinationsAndReadOnlyFeatureCard()
    {
        Assert.AreEqual("developer-tools-screen", _view.CurrentScreen);
        foreach (var id in new[] { "devtools-events-row", "devtools-debug-info-row", "devtools-debug-console-row" })
            Assert.IsTrue(_view.GetComponentsInChildren<Button>().Any(b => b.name == id));
        Assert.IsTrue(_view.GetComponentsInChildren<Transform>().Any(t => t.name == "devtools-feature-toggles-card"));
        Click("devtools-debug-console-row");
        Assert.AreEqual(1, _consoleOpened);
        Assert.AreEqual("developer-tools-screen", _view.CurrentScreen);
    }

    [Test]
    public void DetailLayerKeepsDebugEntryConsoleAndFeedbackAboveIt()
    {
        Assert.AreEqual(SampleUi.DetailSortingOrder, _view.GetComponent<Canvas>().sortingOrder);
        Assert.Greater(SampleUi.DebugEntrySortingOrder, SampleUi.DetailSortingOrder);
        Assert.Greater(SampleUi.DebugConsoleSortingOrder, SampleUi.DebugEntrySortingOrder);
        Assert.Greater(SampleUi.FeedbackSortingOrder, SampleUi.DebugConsoleSortingOrder);
    }

    [Test]
    public void DebugInfoStatesTheLimitedScopeOfItsSummary()
    {
        Click("devtools-debug-info-row");
        var scope = _view.GetComponentsInChildren<TMP_Text>().Single(t => t.name == "debug-info-scope");
        StringAssert.Contains("build facts, sample configuration and recorded calls", scope.text);
        StringAssert.Contains("Native SDK metadata and callback coverage are outside this summary", scope.text);
    }

    [Test]
    public void BackReturnsFromEachDestinationToIndexThenCloses()
    {
        Click("devtools-events-row");
        Assert.AreEqual("events-log-screen", _view.CurrentScreen);
        Click("devtools-back");
        Assert.AreEqual("developer-tools-screen", _view.CurrentScreen);
        Click("devtools-debug-info-row");
        Assert.AreEqual("debug-info-screen", _view.CurrentScreen);
        Click("devtools-back");
        Assert.AreEqual("developer-tools-screen", _view.CurrentScreen);
        Click("devtools-back");
        Assert.IsTrue(_view == null);
    }

    [Test]
    public void LogChipsAndCopyMatchRenderedSnapshotIncludingMultilineDetails()
    {
        _entries.Add(new OctopusSampleDeveloperToolsView.LogLine(true, "[12:00:01] EVENT · Open"));
        _entries.Add(new OctopusSampleDeveloperToolsView.LogLine(false, "[12:00:00] STATE · Changed\nDetails <literal>"));
        Click("devtools-events-row");
        CollectionAssert.AreEqual(new[] { "SDK", "HOST" }, _view.GetComponentsInChildren<TMP_Text>()
            .Where(t => t.name == "events-origin-chip").Select(t => t.text).ToArray());
        _entries.Add(new OctopusSampleDeveloperToolsView.LogLine(false, "Not displayed yet"));
        _version++;
        Click("events-copy-log");
        Assert.AreEqual("SDK · [12:00:01] EVENT · Open\nHOST · [12:00:00] STATE · Changed\nDetails <literal>",
            GUIUtility.systemCopyBuffer);
        _view.RefreshEvents(false);
        Click("events-copy-log");
        StringAssert.EndsWith("\nHOST · Not displayed yet", GUIUtility.systemCopyBuffer);
    }

    [Test]
    public void EmptyAndClearedLogCopyNoPlaceholderOrOldLines()
    {
        Click("devtools-events-row");
        Click("events-copy-log");
        Assert.AreEqual(string.Empty, GUIUtility.systemCopyBuffer);
        _entries.Add(new OctopusSampleDeveloperToolsView.LogLine(true, "event"));
        _version++;
        _view.RefreshEvents(false);
        _entries.Clear();
        _version++;
        _view.RefreshEvents(false);
        Click("events-copy-log");
        Assert.AreEqual(string.Empty, GUIUtility.systemCopyBuffer);
        Assert.IsFalse(_view.GetComponentsInChildren<TMP_Text>().Any(t => t.name == "events-origin-chip"));
    }

    private Transform[] EventRows()
    {
        return _view.GetComponentsInChildren<Transform>()
            .Where(t => t.name == "events-log-entry").ToArray();
    }

    [TestCase(false)]
    [TestCase(true)]
    public void NewEntryKeepsExistingRowsWithFreshSnapshotObjects(bool newestFirst)
    {
        _entries.Add(new OctopusSampleDeveloperToolsView.LogLine(true, "first"));
        _entries.Add(new OctopusSampleDeveloperToolsView.LogLine(false, "second"));
        Click("devtools-events-row");
        var before = EventRows();
        // The real recorder creates fresh LogLine objects on every snapshot.
        _entries[0] = new OctopusSampleDeveloperToolsView.LogLine(true, "first");
        _entries[1] = new OctopusSampleDeveloperToolsView.LogLine(false, "second");
        _entries.Insert(newestFirst ? 0 : 2,
            new OctopusSampleDeveloperToolsView.LogLine(true, "new"));
        _version++;
        _view.RefreshEvents(false);
        var after = EventRows();
        Assert.AreEqual(3, after.Length);
        Assert.AreSame(before[0], after[newestFirst ? 1 : 0]);
        Assert.AreSame(before[1], after[newestFirst ? 2 : 1]);
        Click("events-copy-log");
        Assert.AreEqual(string.Join("\n", _entries.Select(e => e.ToString())), GUIUtility.systemCopyBuffer);
    }

    [Test]
    public void RenderCapEvictsOnlyOldestRowsAndCopiesOnlyVisibleEntries()
    {
        for (var i = 0; i < OctopusSampleDeveloperToolsView.MaxRenderedEvents + 2; i++)
            _entries.Add(new OctopusSampleDeveloperToolsView.LogLine(true, "event " + i));
        Click("devtools-events-row");
        var before = EventRows();
        Assert.AreEqual(OctopusSampleDeveloperToolsView.MaxRenderedEvents, before.Length);
        _entries.Insert(0, new OctopusSampleDeveloperToolsView.LogLine(false, "newest"));
        _version++;
        _view.RefreshEvents(false);
        var after = EventRows();
        Assert.AreEqual(before.Length, after.Length);
        for (var i = 0; i < after.Length - 1; i++) Assert.AreSame(before[i], after[i + 1]);
        Assert.IsTrue(before[before.Length - 1] == null);
        Click("events-copy-log");
        Assert.AreEqual(string.Join("\n", _entries.Take(after.Length).Select(e => e.ToString())),
            GUIUtility.systemCopyBuffer);
    }

    [Test]
    public void DuplicateLinesRemainDistinctAndRemovedRowsDoNotSurviveClearOrReopen()
    {
        _entries.Add(new OctopusSampleDeveloperToolsView.LogLine(true, "same"));
        _entries.Add(new OctopusSampleDeveloperToolsView.LogLine(true, "same"));
        Click("devtools-events-row");
        var before = EventRows();
        Assert.AreNotSame(before[0], before[1]);
        _entries.Insert(0, new OctopusSampleDeveloperToolsView.LogLine(false, "new"));
        _version++;
        _view.RefreshEvents(false);
        Assert.AreSame(before[0], EventRows()[1]);
        Assert.AreSame(before[1], EventRows()[2]);
        _entries.Clear();
        _version++;
        _view.RefreshEvents(false);
        Assert.IsEmpty(EventRows());
        Assert.IsTrue(before[0] == null);
        Assert.IsTrue(before[1] == null);
        Assert.AreEqual(1, _view.GetComponentsInChildren<TMP_Text>()
            .Count(t => t.text == "No events recorded yet."));
        _entries.Add(new OctopusSampleDeveloperToolsView.LogLine(true, "reopened"));
        Click("devtools-back");
        Click("devtools-events-row");
        Assert.AreEqual(1, EventRows().Length);
        Click("events-copy-log");
        Assert.AreEqual("SDK · reopened", GUIUtility.systemCopyBuffer);
    }

    [Test]
    public void DebugInfoCopiesDisplayedLabelValueRows()
    {
        Click("devtools-debug-info-row");
        Assert.IsTrue(_view.GetComponentsInChildren<TMP_Text>().Any(t => t.text == "SDK version"));
        Assert.IsTrue(_view.GetComponentsInChildren<TMP_Text>().Any(t => t.text == "test"));
        Click("debug-info-copy");
        Assert.AreEqual("SDK version: test", GUIUtility.systemCopyBuffer);
    }

    [Test]
    public void DebugInfoIdsSurviveLabelChangesAndKeepCopyReadable()
    {
        _facts.Clear();
        _facts.Add(new OctopusSampleDeveloperToolsView.InfoFact("debug-info-sdk-version", "Package version", "test"));
        _facts.Add(new OctopusSampleDeveloperToolsView.InfoFact("debug-info-native-build-pins", "Native versions", "pins"));
        _facts.Add(new OctopusSampleDeveloperToolsView.InfoFact("debug-info-last-connection-call", "Latest call", "NO CALL"));
        Click("devtools-debug-info-row");
        var card = _view.GetComponentsInChildren<Transform>().Single(t => t.name == "debug-info-facts");
        CollectionAssert.AreEqual(_facts.Select(f => f.Id).ToArray(),
            Enumerable.Range(0, card.childCount).Select(i => card.GetChild(i))
                .Where(t => t.GetComponent<HorizontalLayoutGroup>() != null).Select(t => t.name).ToArray());
        foreach (var fact in _facts)
        {
            var row = card.Find(fact.Id);
            CollectionAssert.AreEqual(new[] { fact.Label, fact.Value },
                row.GetComponentsInChildren<TMP_Text>().Select(t => t.text).ToArray());
        }
        Click("debug-info-copy");
        Assert.AreEqual("Package version: test\nNative versions: pins\nLatest call: NO CALL", GUIUtility.systemCopyBuffer);
    }

    [Test]
    public void FeatureRowsKeepIdsAndShowCurrentStateWithoutControls()
    {
        OctopusSampleFeatureToggles.SetForceLogin(true);
        OctopusSampleFeatureToggles.SetPushRegistration(false);
        _view.ShowIndex();
        foreach (var id in new[] { OctopusSampleFeatureToggles.ForceLoginId, OctopusSampleFeatureToggles.PushRegistrationId })
        {
            var row = _view.GetComponentsInChildren<Transform>().Single(t => t.name == id);
            Assert.AreEqual(0, row.GetComponentsInChildren<Selectable>().Length);
            var value = row.GetComponentsInChildren<TMP_Text>().Last().text;
            StringAssert.StartsWith(id == OctopusSampleFeatureToggles.ForceLoginId ? "On — " : "Off — ", value);
        }
    }
}
