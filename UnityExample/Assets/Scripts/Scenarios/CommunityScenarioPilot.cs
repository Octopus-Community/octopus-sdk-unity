using System;
using System.Collections.Generic;

// Shared execution and weak observation for the community pilots. Views need no new controls.
public abstract class CommunityScenarioPilot : OctopusScenarioPilot, IDisposable
{
    private readonly OctopusScenarioFields _fields;
    protected readonly List<OctopusScenarioPreset> Items = new List<OctopusScenarioPreset>();
    private readonly List<Action> _cancel = new List<Action>();

    protected CommunityScenarioPilot(string id, params OctopusScenarioField[] fields) : base(id)
    {
        _fields = new OctopusScenarioFields(fields);
    }

    public override OctopusScenarioFields Fields { get { return _fields; } }
    public override IReadOnlyList<OctopusScenarioPreset> Presets { get { return Items; } }
    public override bool CanCustomize { get { return true; } }
    public override void RunCustom() { RunSafely(() => Run(Fields)); }
    protected abstract void Run(OctopusScenarioFields fields);

    protected void Add(int index, string label, params string[] values)
    {
        Items.Add(new OctopusScenarioPreset(PresetTestId(index), label,
            fields =>
            {
                for (var i = 0; i < values.Length; i++) fields.Set(Fields.All[i].Key, values[i]);
            }, fields => RunSafely(() => Run(fields))));
    }

    protected void RunSafely(Action action)
    {
        try
        {
            string reason;
            if (OctopusScenarioSdk.EnsureInitialized(OctopusScenarioSdk.PilotMode(),
                    OctopusScenarioSdk.PilotModeLabel, out reason) == null)
            {
                Report(reason);
                return;
            }
            action();
        }
        catch (Exception error) { Report("Run failed: " + error.Message); }
    }

    protected static void Announce(string method, string detail = "")
    {
        OctopusSampleLog.Current.LogApiCall("OctopusSDK." + method, detail);
    }

    // The current view has no disposal hook. Native events must not retain old pilots (and
    // their ResultChanged views). A dead subscription detaches on its next event; Dispose also
    // allows a future view to detach immediately. Delivery lambdas must not capture the pilot.
    protected Action Observe<T>(Action<Action<T>> subscribe, Action<Action<T>> unsubscribe,
                                Action<CommunityScenarioPilot, T> deliver)
    {
        var weak = new WeakReference(this);
        Action<T> handler = null;
        handler = value =>
        {
            var owner = weak.Target as CommunityScenarioPilot;
            if (owner == null) unsubscribe(handler);
            else deliver(owner, value);
        };
        subscribe(handler);
        var subscribed = true;
        Action cancel = () =>
        {
            if (!subscribed) return;
            unsubscribe(handler);
            subscribed = false;
        };
        _cancel.Add(cancel);
        return cancel;
    }

    public void Dispose()
    {
        foreach (var cancel in _cancel) cancel();
        _cancel.Clear();
    }
}
