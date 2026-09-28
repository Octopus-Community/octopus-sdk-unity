#if UNITY_EDITOR
public partial class OctopusSDK
{
    internal static partial class MockBackend
    {
        internal static bool LifecycleInitialized;

        internal static void SwitchCommunity(int requestId, ConnectionMode mode, string apiServerHost,
            int apiServerPort)
        {
            // Do not record credentials in the lifecycle call log or console. The server host is
            // not a credential: recording it lets a test prove the overload forwards it.
            Mock.Record("SwitchCommunity", mode.Mode, mode.AppManagedFieldsAsIntArray, apiServerHost,
                apiServerPort);
            Mock.CurrentScreen = null;
            Mock.LastOpenedPost = null;
            Mock.LastPrefilledPost = null;
            LifecycleInitialized = true;
            QueueLifecycleResponse(requestId + "\n", false);
        }

        internal static void ResetLifecycle(int requestId)
        {
            Mock.Record("Reset");
            Mock.CurrentScreen = null;
            Mock.LastOpenedPost = null;
            Mock.LastPrefilledPost = null;
            QueueLifecycleResponse(requestId + "\n", false);
        }

        internal static void StopLifecycle(int requestId)
        {
            Mock.Record("Stop");
            Mock.CurrentScreen = null;
            Mock.LastOpenedPost = null;
            Mock.LastPrefilledPost = null;
            LifecycleInitialized = false;
            QueueLifecycleResponse(requestId + "\n", false);
        }

        internal static void Close()
        {
            Mock.Record("Close");
            Mock.CurrentScreen = null;
        }
    }
}
#endif
