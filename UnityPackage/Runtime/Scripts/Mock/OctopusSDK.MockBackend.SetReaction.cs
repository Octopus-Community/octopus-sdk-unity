#if UNITY_EDITOR
public partial class OctopusSDK
{
    public static partial class Mock
    {
        /// <summary>Failure to deliver for the next valid SetReaction call, then cleared.
        /// Null (default) completes successfully. Consumed even when Mock.Enabled is false.</summary>
        public static OctopusSetReactionError NextSetReactionError { get; set; }
    }

    internal static partial class MockBackend
    {
        internal static void SetReaction(int requestId, string contentId, OctopusReactionKind? kind)
        {
            Mock.Record("SetReaction", contentId, kind);
            var error = Mock.NextSetReactionError;
            Mock.NextSetReactionError = null;
            CompleteSetReaction(requestId, error);
        }
    }
}
#endif
