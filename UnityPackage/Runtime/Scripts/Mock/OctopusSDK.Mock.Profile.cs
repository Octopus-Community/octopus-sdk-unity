#if UNITY_EDITOR
using System;

public partial class OctopusSDK
{
    public static partial class Mock
    {
        /// <summary>Failure returned by the next mock refresh calls; null simulates success for a connected SSO user.</summary>
        public static OctopusRefreshEntitlementsError RefreshEntitlementsError { get; set; }

        /// <summary>Simulates a native profile update on the next Unity update, even when recording is disabled.</summary>
        public static void EmitProfileChanged(OctopusProfile profile) { QueueProfile(profile); }

        private static void ResetProfileMock()
        {
            ResetProfileObservation();
            RefreshEntitlementsError = null;
            _entitlementsRequests.Clear();
            MockBackend.ResetProfileState();
        }
    }

    internal static partial class MockBackend
    {
        internal static void ResetProfileState() { TokenProvider = null; }

        internal static void InitializeProfileMock(ConnectionMode mode)
        {
            TokenProvider = null;
            if (Mock.Enabled) Mock.EmitProfileChanged(null);
        }

        internal static void RefreshEntitlements(int requestId)
        {
            Mock.Record("RefreshEntitlements");
            var error = Mock.RefreshEntitlementsError;
            if (error == null && (CurrentProfile == null || CurrentProfile.ClientUserId == null))
                error = new OctopusRefreshEntitlementsError(OctopusRefreshEntitlementsErrorKind.UserNotConnected);
            CompleteEntitlementsRequest(requestId, error);
        }
    }
}
#endif
