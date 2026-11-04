using System.Collections.Generic;
using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public partial class OctopusSDK
{
    private static Action<int> _onNotSeenNotificationsCount;

    // Last count the native SDK delivered, or null before the first one. Both bridges push the
    // count once when they subscribe at Initialize and then on change (Android's flow is
    // distinct), so a handler attached after that first push would otherwise wait for the next
    // change to learn the count.
    private static int? _lastNotSeenNotificationsCount;

    // Thread the counts are delivered on (UnitySendMessage, the mock): the Unity main thread.
    // Known whenever a count is cached, which is the only time a replay is needed.
    private static int _notSeenNotificationsCountThreadId;

    // Custom accessors lose the compiler's atomic subscription, so the delegate and the cache
    // are guarded here. Handlers are always invoked outside the lock.
    private static readonly object _notSeenNotificationsCountLock = new object();

    /// <summary>Raised on the Unity main thread with the number of notifications the user has not
    /// seen yet, whenever the native SDK reports it. A handler added once a count is known also
    /// receives that count: immediately, from within the subscription, when subscribing on the
    /// Unity main thread, otherwise on the next frame (once Initialize has run), unless the
    /// handler is removed first. Subscribing after Initialize therefore still delivers the current
    /// value.</summary>
    public static event Action<int> OnNotSeenNotificationsCount
    {
        add
        {
            if (value == null) return;
            int? count;
            bool onDeliveryThread;
            lock (_notSeenNotificationsCountLock)
            {
                _onNotSeenNotificationsCount += value;
                count = _lastNotSeenNotificationsCount;
                onDeliveryThread = Thread.CurrentThread.ManagedThreadId == _notSeenNotificationsCountThreadId;
            }
            if (!count.HasValue) return;
            if (onDeliveryThread)
            {
                value(count.Value);
                return;
            }
            // Drained by the OctopusMainThread that Initialize creates. When it runs, the replay
            // reads the latest count, so a count delivered in between is not followed by an older
            // one, and reaches only the parts of the handler that are still subscribed.
            OctopusMainThread.Post(() => ReplayNotSeenNotificationsCount(value));
        }
        remove
        {
            lock (_notSeenNotificationsCountLock)
            {
                _onNotSeenNotificationsCount -= value;
            }
        }
    }

    /// <summary>Asks the native SDK to refresh the not-seen count from the server. The result is
    /// delivered through <see cref="OnNotSeenNotificationsCount"/>; a refresh that finds the same
    /// count may not raise it again.</summary>
    public static void UpdateNotSeenNotificationsCount()
    {
#if UNITY_EDITOR
        MockBackend.UpdateNotSeenNotificationsCount();
#elif UNITY_ANDROID
        using (AndroidJavaClass plugin = new AndroidJavaClass("com.octopuscommunity.bridge.Bridge"))
        {
            plugin.CallStatic("updateNotSeenNotificationsCount");
        }
#elif UNITY_IOS
        OctopusSdkUpdateNotSeenNotificationsCount();
#endif
    }

    private static void ReplayNotSeenNotificationsCount(Action<int> handler)
    {
        int? latest;
        Action<int> current;
        lock (_notSeenNotificationsCountLock)
        {
            latest = _lastNotSeenNotificationsCount;
            current = _onNotSeenNotificationsCount;
        }
        if (!latest.HasValue || current == null) return;
        Delegate[] subscribed = current.GetInvocationList();
        foreach (Delegate entry in handler.GetInvocationList())
        {
            if (Array.IndexOf(subscribed, entry) >= 0) ((Action<int>)entry)(latest.Value);
        }
    }

    static void TriggerOnNotSeenNotificationsCount(int count)
    {
        Action<int> listeners;
        lock (_notSeenNotificationsCountLock)
        {
            _lastNotSeenNotificationsCount = count;
            _notSeenNotificationsCountThreadId = Thread.CurrentThread.ManagedThreadId;
            listeners = _onNotSeenNotificationsCount;
        }
        if (listeners != null) listeners(count);
    }

    internal static void ResetNotSeenNotificationsCount()
    {
        lock (_notSeenNotificationsCountLock)
        {
            _lastNotSeenNotificationsCount = null;
        }
    }

    public partial class OctopusChannel : MonoBehaviour
    {
        public void OnNotSeenNotificationsCount(string count)
        {
            OctopusSDK.TriggerOnNotSeenNotificationsCount(int.Parse(count));
        }
    }

#if UNITY_IOS && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void OctopusSdkUpdateNotSeenNotificationsCount();
#endif 
}
