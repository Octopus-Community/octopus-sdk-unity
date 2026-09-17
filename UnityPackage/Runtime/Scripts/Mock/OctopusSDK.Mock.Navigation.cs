#if UNITY_EDITOR

public partial class OctopusSDK
{
    public static partial class Mock
    {
        /// <summary>Simulate a profile tap. Interception dismisses the mock screen and queues
        /// the host callback and event; without a handler the native profile stays in Octopus.</summary>
        public static void EmitNavigateToProfile(string clientUserId)
        {
            if (NavigateToProfileHandler == null || string.IsNullOrWhiteSpace(clientUserId)) return;
            CurrentScreen = null;
            QueueNavigateToProfile(clientUserId);
        }
    }

    internal static partial class MockBackend
    {
        internal static void OpenProfile(string clientUserId, OctopusNavigationMode? navigationMode)
        {
            Mock.Record("OpenProfile", clientUserId ?? "", navigationMode);
            if (Mock.Enabled) Mock.CurrentScreen = string.IsNullOrWhiteSpace(clientUserId)
                ? "Current profile" : "Profile " + clientUserId.Trim();
        }

        internal static void OpenActivity(OctopusNavigationMode? navigationMode)
        {
            Mock.Record("OpenActivity", navigationMode);
            if (Mock.Enabled) Mock.CurrentScreen = "Activity";
        }
    }
}
#endif
