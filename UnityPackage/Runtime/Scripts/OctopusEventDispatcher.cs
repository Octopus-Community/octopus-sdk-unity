using System;
using System.Collections.Concurrent;
using System.Threading;
using UnityEngine;

// Single, shared delivery path for Octopus events on device, so OnOctopusEvent behaves
// identically on both platforms. Both native paths Enqueue raw JSON here:
//   - Android: the OctopusBridgeListener proxy (background JNI thread),
//   - iOS:     OctopusChannel.OnOctopusEventJson (Unity main thread, via UnitySendMessage).
// One dedicated background consumer parses (OctopusEventParsing.FromJson) and raises
// OnOctopusEvent, in FIFO order — uniform contract: background thread, ordered, real-time.
// The Editor Mock path bypasses this and raises synchronously (deterministic tests).
internal static class OctopusEventDispatcher
{
    static readonly BlockingCollection<string> _queue = new BlockingCollection<string>();
    static volatile Thread _thread;   // volatile: correct double-checked-locking on the lazy start
    static readonly object _lock = new object();

    internal static void Enqueue(string json)
    {
        EnsureStarted();
        _queue.Add(json);
    }

    static void EnsureStarted()
    {
        if (_thread != null) return;
        lock (_lock)
        {
            if (_thread != null) return;
            _thread = new Thread(Consume) { IsBackground = true, Name = "OctopusEventDispatcher" };
            _thread.Start();
        }
    }

    static void Consume()
    {
        foreach (var json in _queue.GetConsumingEnumerable())
        {
            try { OctopusSDK.TriggerOnOctopusEvent(OctopusEventParsing.FromJson(json)); }
            catch (Exception e) { Debug.LogException(e); }
        }
    }
}
