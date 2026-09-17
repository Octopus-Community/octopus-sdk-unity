using TMPro;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// The presentation only: the optional recorder supplies snapshots and the console action through
// delegates. This assembly remains independent of the internal Debug folder in the public mirror.
public sealed class OctopusSampleDeveloperToolsView : MonoBehaviour
{
    public sealed class LogLine
    {
        public readonly string Origin;
        public readonly string Text;

        public LogLine(bool fromSdk, string text)
        {
            Origin = fromSdk ? "SDK" : "HOST";
            Text = text ?? string.Empty;
        }

        public override string ToString() { return Origin + " · " + Text; }
    }

    public sealed class InfoFact
    {
        public readonly string Id;
        public readonly string Label;
        public readonly string Value;

        public InfoFact(string id, string label, string value)
        {
            Id = id;
            Label = label;
            Value = value;
        }
    }

    // Keep the rendering budget independent of the recorder's retention policy.
    public const int MaxRenderedEvents = 500;

    private readonly Dictionary<string, Queue<RectTransform>> _eventRows =
        new Dictionary<string, Queue<RectTransform>>();
    private TMP_Text _emptyEvents;
    private Func<int> _logVersion;
    private Func<IList<LogLine>> _snapshot;
    private Func<IList<InfoFact>> _info;
    private Action _openConsole;
    private RectTransform _page;
    private RectTransform _events;
    private int _renderedVersion = -1;
    private string _copyText = string.Empty;

    public string CurrentScreen { get; private set; }

    public static OctopusSampleDeveloperToolsView Open(Func<int> logVersion,
        Func<IList<LogLine>> snapshot, Func<IList<InfoFact>> info,
        Action openConsole)
    {
        var existing = FindAnyObjectByType<OctopusSampleDeveloperToolsView>();
        if (existing != null) return existing;
        var view = new GameObject("OctopusSampleDeveloperToolsView")
            .AddComponent<OctopusSampleDeveloperToolsView>();
        view._logVersion = logVersion;
        view._snapshot = snapshot;
        view._info = info;
        view._openConsole = openConsole;
        SampleUi.OverlayCanvas(view.gameObject, SampleUi.DetailSortingOrder);
        view.ShowIndex();
        return view;
    }

    public void Back()
    {
        if (CurrentScreen != "developer-tools-screen") ShowIndex();
        else
        {
            // The entry rides in this header: hand it back before the hierarchy goes away.
            SampleUiDebugEntryHost.ReleaseAll();
            if (Application.isPlaying) Destroy(gameObject);
            else DestroyImmediate(gameObject);
        }
    }

    public void ShowIndex()
    {
        var content = BuildPage("developer-tools-screen", "Developer tools");
        var card = SampleUi.Card("devtools-tools-card", content);
        ToolRow(card, "devtools-events-row", "Events log", "SDK and host events, recorded live", ShowEvents);
        ToolRow(card, "devtools-debug-info-row", "Debug info", "SDK, configuration and build facts", ShowInfo);
        ToolRow(card, "devtools-debug-console-row", "Debug console", "SDK logs, as a sheet over this screen",
            () => _openConsole());
        BuildToggles(content);
    }

    public void ShowEvents()
    {
        var content = BuildPage("events-log-screen", "Events log");
        SampleUi.ChromeButton("events-copy-log", content, "Copy log", Copy);
        _events = SampleUi.Card("events-log-entries", content);
        RefreshEvents(true);
    }

    public void ShowInfo()
    {
        var content = BuildPage("debug-info-screen", "Debug info");
        var scope = SampleUi.FlexibleLabel(content,
            "This summary covers build facts, sample configuration and recorded calls. " +
            "Native SDK metadata and callback coverage are outside this summary.",
            SampleUi.TextCaption, SampleUi.Muted);
        scope.name = "debug-info-scope";
        SampleUi.ChromeButton("debug-info-copy", content, "Copy", Copy);
        var card = SampleUi.Card("debug-info-facts", content);
        var lines = new List<string>();
        foreach (var fact in _info())
        {
            ValueRow(card, fact.Id, fact.Label, fact.Value);
            lines.Add(fact.Label + ": " + fact.Value);
        }
        _copyText = string.Join("\n", lines);
    }

    // Copy the rendered snapshot, even if another entry arrived between Update and this click.
    public void Copy() { GUIUtility.systemCopyBuffer = _copyText; }

    private void Update()
    {
        if (CurrentScreen == "events-log-screen") RefreshEvents(false);
    }

    public void RefreshEvents(bool force)
    {
        var version = _logVersion();
        if (!force && version == _renderedVersion) return;
        _renderedVersion = version;
        var available = new Dictionary<string, Queue<RectTransform>>(_eventRows);
        _eventRows.Clear();
        var snapshot = _snapshot();
        var lines = new List<string>();
        for (var i = 0; i < snapshot.Count && i < MaxRenderedEvents; i++)
        {
            var entry = snapshot[i];
            var key = entry.ToString();
            Queue<RectTransform> matches;
            var row = available.TryGetValue(key, out matches) && matches.Count > 0
                ? matches.Dequeue() : BuildEventRow(entry);
            row.SetSiblingIndex(i);
            Queue<RectTransform> rendered;
            if (!_eventRows.TryGetValue(key, out rendered))
            {
                rendered = new Queue<RectTransform>();
                _eventRows.Add(key, rendered);
            }
            rendered.Enqueue(row);
            lines.Add(key);
        }
        foreach (var rows in available.Values)
            foreach (var row in rows) RemoveEventRow(row.gameObject);
        _copyText = string.Join("\n", lines);
        if (lines.Count == 0 && _emptyEvents == null)
            _emptyEvents = SampleUi.FlexibleLabel(_events, "No events recorded yet.",
                SampleUi.TextBody, SampleUi.Muted);
        else if (lines.Count > 0 && _emptyEvents != null)
        {
            RemoveEventRow(_emptyEvents.gameObject);
            _emptyEvents = null;
        }
    }

    private RectTransform BuildEventRow(LogLine entry)
    {
        var row = SampleUi.Panel("events-log-entry", _events, SampleUi.FieldBackground);
        SampleUi.VerticalStack(row, 4f, new RectOffset(24, 24, 20, 20), true);
        var badgeRow = SampleUi.Panel("Origin", row, SampleUi.FieldBackground);
        var badgeLayout = badgeRow.gameObject.AddComponent<HorizontalLayoutGroup>();
        badgeLayout.childControlWidth = badgeLayout.childControlHeight = true;
        badgeLayout.childForceExpandWidth = badgeLayout.childForceExpandHeight = false;
        var badge = SampleUi.Panel("Chip", badgeRow, SampleUi.RowBackground);
        SampleUi.VerticalStack(badge, 0f, new RectOffset(16, 16, 8, 8), true);
        badge.gameObject.AddComponent<LayoutElement>().preferredWidth = 160f;
        var chip = SampleUi.FlexibleLabel(badge, entry.Origin, SampleUi.TextCaption, SampleUi.Accent);
        chip.name = "events-origin-chip";
        var text = SampleUi.FlexibleLabel(row, entry.Text, SampleUi.TextCaption, SampleUi.TitleColor);
        text.richText = false;
        return row;
    }

    private static void ToolRow(RectTransform card, string id, string title, string subtitle,
        UnityEngine.Events.UnityAction action)
    {
        var row = SampleUi.Panel(id, card, SampleUi.FieldBackground);
        SampleUi.VerticalStack(row, 4f, new RectOffset(24, 24, 20, 20), true);
        row.gameObject.AddComponent<LayoutElement>().minHeight = OctopusSampleBranding.MinTouchUnits;
        SampleUi.FlexibleLabel(row, title, SampleUi.TextBody, SampleUi.TitleColor);
        SampleUi.FlexibleLabel(row, subtitle, SampleUi.TextCaption, SampleUi.Muted);
        var button = row.gameObject.AddComponent<Button>();
        button.targetGraphic = row.GetComponent<Image>();
        button.onClick.AddListener(action);
    }

    private static void BuildToggles(RectTransform content)
    {
        var card = SampleUi.Card("devtools-feature-toggles-card", content);
        SampleUi.FlexibleLabel(card, "Feature toggles", SampleUi.TextTitle, SampleUi.TitleColor);
        SampleUi.FlexibleLabel(card, "Read-only. Set on Scenarios.", SampleUi.TextCaption, SampleUi.Muted);
        ValueRow(card, OctopusSampleFeatureToggles.ForceLoginId,
            OctopusSampleFeatureToggles.ForceLoginLabel,
            ToggleValue(OctopusSampleFeatureToggles.ForceLogin,
                OctopusSampleFeatureToggles.ForceLoginEffect(OctopusSampleFeatureToggles.ForceLogin)));
        ValueRow(card, OctopusSampleFeatureToggles.PushRegistrationId,
            OctopusSampleFeatureToggles.PushRegistrationLabel,
            ToggleValue(OctopusSampleFeatureToggles.PushRegistration,
                OctopusSampleFeatureToggles.PushRegistrationEffect(OctopusSampleFeatureToggles.PushRegistration)));
    }

    private static string ToggleValue(bool enabled, string effect)
    {
        return (enabled ? "On" : "Off") + " — " + effect;
    }

    private RectTransform BuildPage(string id, string titleText)
    {
        // The header being thrown away hosts the entry: hand it back before it goes.
        SampleUiDebugEntryHost.ReleaseAll();
        _events = null;
        _eventRows.Clear();
        _emptyEvents = null;
        _copyText = string.Empty;
        if (_page != null)
        {
            _page.gameObject.SetActive(false);
            if (Application.isPlaying) Destroy(_page.gameObject);
            else DestroyImmediate(_page.gameObject);
        }
        CurrentScreen = id;
        _page = SampleUi.Panel("Ground", transform, SampleUi.Background);
        SampleUi.Stretch(_page, Vector2.zero, Vector2.one);
        var root = SampleUi.SafeArea(id, _page);
        var header = SampleUi.AppBar("Header", root, titleText, Back, "devtools-back");
        SampleUiDebugEntryHost.Attach(header);
        return SampleUi.VerticalScroll(root, SampleUi.OverlayPadding());
    }

    private static void ValueRow(RectTransform card, string id, string label, string value)
    {
        var row = SampleUi.Panel(id, card, SampleUi.RowBackground);
        var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 16f;
        layout.childControlWidth = layout.childForceExpandWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandHeight = false;
        row.gameObject.AddComponent<LayoutElement>();
        SampleUi.FlexibleLabel(row, label, SampleUi.TextCaption, SampleUi.Muted);
        var read = SampleUi.FlexibleLabel(row, value, SampleUi.TextCaption, SampleUi.TitleColor);
        read.richText = false;
        read.alignment = TextAlignmentOptions.TopRight;
        read.GetComponent<LayoutElement>().flexibleWidth = 1.8f;
    }

    private static void RemoveEventRow(GameObject row)
    {
        row.SetActive(false);
        row.transform.SetParent(null, false);
        if (Application.isPlaying) Destroy(row);
        else DestroyImmediate(row);
    }
}
