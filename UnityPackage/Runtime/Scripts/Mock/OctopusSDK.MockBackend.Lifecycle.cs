#if UNITY_EDITOR
public partial class OctopusSDK
{
    internal static partial class MockBackend
    {
        internal static bool LifecycleInitialized;

        internal static void SwitchCommunity(int requestId, ConnectionMode mode)
        {
            // Do not record credentials in the lifecycle call log or console.
            Mock.Record("SwitchCommunity", mode.Mode, mode.AppManagedFieldsAsIntArray);
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
