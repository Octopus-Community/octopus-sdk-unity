using System;
using System.Collections.Generic;
using System.Threading;

public static partial class OctopusScenarioSdk
{
    public static int? ObservedNotSeenCount { get; private set; }
    public static bool? ObservedAccess { get; private set; }
    public const int MaximumEvents = 50;
    private static readonly List<string> _eventLines = new List<string>();
    private static readonly List<WeakReference> _observers = new List<WeakReference>();
    private static bool _pilotObserving;
    private static int _mainThreadId;

    /// <summary>
    /// How callbacks raised off the main thread reach it. Defaults to
    /// <see cref="OctopusMainThread.Post"/>; tests substitute a capturing queue.
    /// </summary>
    public static Action<Action> MainThreadPoster = OctopusMainThread.Post;
    public static IReadOnlyList<string> EventLines { get { return _eventLines.AsReadOnly(); } }

    internal static void Observe(OctopusScenarioPilot pilot)
    {
        _observers.RemoveAll(reference => !reference.IsAlive);
        _observers.Add(new WeakReference(pilot));
    }

    internal static void EnsurePilotObservations()
    {
        if (_pilotObserving) return;
        _pilotObserving = true;
        _mainThreadId = Thread.CurrentThread.ManagedThreadId;
        OctopusSampleLog.Current.LogApiCall("OctopusSDK.OnOctopusEvent", "subscribe");
        _sdk.OnOctopusEvent += RecordEvent;
        OctopusSampleLog.Current.LogApiCall("OctopusSDK.OnHasAccessToCommunityChanged", "subscribe");
        _sdk.OnHasAccessToCommunityChanged += RecordAccess;
        OctopusSampleLog.Current.LogApiCall("OctopusSDK.OnNotSeenNotificationsCount", "subscribe");
        _sdk.OnNotSeenNotificationsCount += RecordCount;
    }

    /// <summary>
    /// <see cref="OctopusSDK.OnOctopusEvent"/> fires on the dispatcher's background thread on both
    /// iOS and Android (see OctopusEventDispatcher); only the Editor mock delivers synchronously on
    /// the calling thread. Pilots and the
    /// renderers bound to <see cref="OctopusScenarioPilot.ResultChanged"/> are main-thread only, so
    /// everything past pure formatting is marshalled there. Synchronous on the main thread itself,
    /// which keeps the Editor (no Update loop in EditMode) and the ordering guarantees intact.
    /// </summary>
    private static void OnMainThread(Action action)
    {
        if (Thread.CurrentThread.ManagedThreadId == _mainThreadId) action();
        else MainThreadPoster(action);
    }

    private static void RecordCount(int value)
    {
        OnMainThread(() =>
        {
            ObservedNotSeenCount = value;
            NotifyPilots();
        });
    }

    internal static void ClearCommunityObservations()
    {
        ObservedAccess = null;
        ObservedNotSeenCount = null;
        NotifyPilots();
    }

    private static void RecordAccess(bool value)
    {
        OnMainThread(() =>
        {
            ObservedAccess = value;
            NotifyPilots();
        });
    }

    private static void RecordEvent(OctopusEvent value)
    {
        if (value == null) return;
        var line = EventsScenario.Describe(value); // pure formatting, safe off the main thread
        OnMainThread(() =>
        {
            _eventLines.Insert(0, line);
            if (_eventLines.Count > MaximumEvents) _eventLines.RemoveAt(MaximumEvents);
            NotifyPilots();
        });
    }

    public static void ClearEvents()
    {
        _eventLines.Clear();
        NotifyPilots();
    }

    private static void NotifyPilots()
    {
        foreach (var reference in _observers.ToArray())
        {
            var pilot = reference.Target as OctopusScenarioPilot;
            if (pilot != null) pilot.ObservationsChanged();
            else _observers.Remove(reference);
        }
    }

    private static void ResetPilotObservations()
    {
        if (_pilotObserving) _sdk.OnOctopusEvent -= RecordEvent;
        if (_pilotObserving) _sdk.OnHasAccessToCommunityChanged -= RecordAccess;
        if (_pilotObserving) _sdk.OnNotSeenNotificationsCount -= RecordCount;
        ObservedAccess = null;
        ObservedNotSeenCount = null;
        _pilotObserving = false;
        MainThreadPoster = OctopusMainThread.Post;
        _eventLines.Clear();
        _observers.Clear();
    }
}
