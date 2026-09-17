#if UNITY_EDITOR
using System;
using System.Threading.Tasks;

public partial class OctopusSDK
{
    public static partial class Mock
    {
        /// <summary>Failure for the next valid callback-based ConnectUser call, consumed once even when
        /// Mock.Enabled is false. Null succeeds by default. Reset clears it; the legacy Task call
        /// does not consume it.</summary>
        public static OctopusClientUserError NextConnectUserError { get; set; }
    }

    internal static partial class MockBackend
    {
        internal static async void ConnectUserWithResult(int requestId, string userId, string nickname,
            string bio, string picture, Func<Task<string>> provider)
        {
            Mock.Record("ConnectUser", userId, nickname);
            var error = Mock.NextConnectUserError;
            Mock.NextConnectUserError = null;
            if (error == null)
            {
                string token = await provider();
                // Completion also consults any failure recorded by the safe token provider.
                if (string.IsNullOrEmpty(token))
                {
                    CompleteConnectUser(requestId, null);
                    return;
                }
                if (Mock.Enabled) Mock.EmitProfileChanged(new OctopusProfile(clientUserId: userId));
            }
            CompleteConnectUser(requestId, error);
        }
    }
}
#endif
