#if UNITY_EDITOR
using System.Collections.Generic;

public partial class OctopusSDK
{
    public static partial class Mock
    {
        internal static readonly Dictionary<string, OctopusPost> ClientPosts = new Dictionary<string, OctopusPost>();
        /// <summary>Failure consumed by the next valid fetch/create call, then cleared.</summary>
        public static OctopusClientPostError NextClientPostError { get; set; }

        /// <summary>Stores a post (null removes it) and queues an event for its active observation.
        /// Runs even when Mock.Enabled is false. Call on the Unity main thread.</summary>
        public static void SetClientObjectRelatedPost(string objectId, OctopusPost post)
        {
            if (!ValidClientObjectId(objectId)) throw new System.ArgumentException("A valid object id is required", "objectId");
            if (post == null) ClientPosts.Remove(objectId);
            else ClientPosts[objectId] = post;
            QueueClientObjectRelatedPost(objectId, post);
        }

        internal static void ClearClientPostState()
        {
            ClientPosts.Clear();
            NextClientPostError = null;
        }
    }

    internal static partial class MockBackend
    {
        internal static void FetchOrCreateClientObjectRelatedPost(int requestId, OctopusClientObject value)
        {
            Mock.Record("FetchOrCreateClientObjectRelatedPost", value.ObjectId);
            var error = Mock.NextClientPostError;
            Mock.NextClientPostError = null;
            OctopusPost post = null;
            if (error == null && !Mock.ClientPosts.TryGetValue(value.ObjectId, out post))
            {
                if (string.IsNullOrEmpty(value.Text))
                    error = new OctopusClientPostError(OctopusClientPostErrorCode.TextMissing, "Post text is required");
                else if (value.Text.Length > 5000)
                    error = new OctopusClientPostError(OctopusClientPostErrorCode.TextTooLong, "Post text is too long");
                else
                {
                    post = new OctopusPost("mock-post-" + value.ObjectId);
                    Mock.SetClientObjectRelatedPost(value.ObjectId, post);
                }
            }
            CompleteClientPost(requestId, post == null ? null : post.Id, error);
        }

        internal static void StartObservingClientObjectRelatedPost(string objectId)
        {
            Mock.Record("StartObservingClientObjectRelatedPost", objectId);
            OctopusPost post;
            Mock.ClientPosts.TryGetValue(objectId, out post);
            QueueClientObjectRelatedPost(objectId, post);
        }
    }
}
#endif
