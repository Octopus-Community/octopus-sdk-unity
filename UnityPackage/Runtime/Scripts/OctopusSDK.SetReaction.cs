using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Threading;
using UnityEngine;

/// <summary>The three SetReaction business error cases shared with Flutter.</summary>
public enum OctopusSetReactionErrorCode
{
    /// <summary>An unknown or invalid reaction kind was supplied.</summary>
    UnknownReaction,
    /// <summary>The post does not exist or the user cannot read it.</summary>
    PostNotFound,
    /// <summary>A reaction, connection, authentication or bridge failure; see Message.</summary>
    ReactionError
}

/// <summary>A typed failure from SetReaction, including its diagnostic message.</summary>
public sealed class OctopusSetReactionError
{
    /// <summary>The classified failure. Unfamiliar native errors map to ReactionError.</summary>
    public OctopusSetReactionErrorCode Code { get; private set; }
    /// <summary>The native diagnostic message, or a fallback when none was supplied.</summary>
    public string Message { get; private set; }

    /// <summary>Create a failure, including for an Editor mock response.</summary>
    public OctopusSetReactionError(OctopusSetReactionErrorCode code, string message)
    {
        Code = code;
        Message = string.IsNullOrEmpty(message) ? "Unknown error" : message;
    }
}

internal static class OctopusSetReactionParsing
{
    // Empty wire kind is the removal sentinel on both native bridges.
    internal static bool TryKindToWire(OctopusReactionKind? kind, out string wire)
    {
        wire = "";
        if (!kind.HasValue || kind.Value == OctopusReactionKind.None) return true;
        switch (kind.Value)
        {
            case OctopusReactionKind.Heart: wire = "heart"; return true;
            case OctopusReactionKind.Joy: wire = "joy"; return true;
            case OctopusReactionKind.MouthOpen: wire = "mouthOpen"; return true;
            case OctopusReactionKind.Clap: wire = "clap"; return true;
            case OctopusReactionKind.Cry: wire = "cry"; return true;
            case OctopusReactionKind.Rage: wire = "rage"; return true;
            default: return false;
        }
    }

    internal static OctopusSetReactionError ErrorFromJson(string json)
    {
        try
        {
            var row = OctopusJson.ParseObject(json);
            string type;
            string message;
            row.TryGetValue("type", out type);
            row.TryGetValue("message", out message);
            var code = OctopusSetReactionErrorCode.ReactionError;
            switch (type)
            {
                case "unknownReaction": code = OctopusSetReactionErrorCode.UnknownReaction; break;
                case "postNotFound": code = OctopusSetReactionErrorCode.PostNotFound; break;
            }
            return new OctopusSetReactionError(code, message);
        }
        catch (FormatException)
        {
            return new OctopusSetReactionError(OctopusSetReactionErrorCode.ReactionError,
                "Malformed reaction error payload");
        }
    }
}

public partial class OctopusSDK
{
    private sealed class SetReactionCallbacks
    {
        internal Action Completed;
        internal Action<OctopusSetReactionError> Error;
    }

    private static int _nextSetReactionRequestId;
    private static readonly ConcurrentDictionary<int, SetReactionCallbacks> _setReactionCallbacks
        = new ConcurrentDictionary<int, SetReactionCallbacks>();

    /// <summary>Set the current user's reaction on a visible post identified by contentId.
    /// Pass null or None to remove it. Unknown and invalid enum values fail with UnknownReaction.
    /// Call on the Unity main thread after Initialize; callbacks run on a subsequent player update
    /// and wait while the native UI pauses Unity. Connection and bridge failures use ReactionError.
    /// On iOS 1.13.2, removing an absent reaction can return ReactionError.</summary>
    public static void SetReaction(string contentId, OctopusReactionKind? kind,
        Action onCompleted, Action<OctopusSetReactionError> onError)
    {
        OctopusMainThread.EnsureExists();
        int id = RegisterSetReactionCallbacks(onCompleted, onError);
        string wire;
        if (!OctopusSetReactionParsing.TryKindToWire(kind, out wire))
        {
            CompleteSetReaction(id, new OctopusSetReactionError(
                OctopusSetReactionErrorCode.UnknownReaction, "Unknown reaction not permitted"));
            return;
        }
        if (string.IsNullOrEmpty(contentId))
        {
            CompleteSetReaction(id, new OctopusSetReactionError(
                OctopusSetReactionErrorCode.PostNotFound, "A post id is required"));
            return;
        }
        try
        {
#if UNITY_EDITOR
            MockBackend.SetReaction(id, contentId, kind);
#elif UNITY_ANDROID
            using (AndroidJavaClass plugin = new AndroidJavaClass("com.octopuscommunity.bridge.Bridge"))
            {
                plugin.CallStatic("setReaction", id, contentId, wire);
            }
#elif UNITY_IOS
            OctopusSdkSetReaction(id, contentId, wire);
#else
            CompleteSetReaction(id, new OctopusSetReactionError(
                OctopusSetReactionErrorCode.ReactionError, "SetReaction requires Android or iOS"));
#endif
        }
        catch (Exception e)
        {
            CompleteSetReaction(id, new OctopusSetReactionError(
                OctopusSetReactionErrorCode.ReactionError, e.Message));
        }
    }

    internal static int RegisterSetReactionCallbacks(Action completed, Action<OctopusSetReactionError> error)
    {
        int id = Interlocked.Increment(ref _nextSetReactionRequestId);
        _setReactionCallbacks[id] = new SetReactionCallbacks { Completed = completed, Error = error };
        return id;
    }

    internal static void CompleteSetReaction(int id, OctopusSetReactionError error)
    {
        SetReactionCallbacks callbacks;
        if (!_setReactionCallbacks.TryRemove(id, out callbacks)) return;
        OctopusMainThread.Post(() =>
        {
            if (error == null) callbacks.Completed?.Invoke();
            else callbacks.Error?.Invoke(error);
        });
    }

    internal static void ReceiveSetReactionResult(string payload, bool failed)
    {
        int id;
        string json;
        if (string.IsNullOrEmpty(payload) || !SplitRequest(payload, out id, out json) || id <= 0) return;
        CompleteSetReaction(id, failed ? OctopusSetReactionParsing.ErrorFromJson(json) : null);
    }

    public partial class OctopusChannel : MonoBehaviour
    {
        /// <summary>Native landing pad for a successful reaction request.</summary>
        public void OnSetReactionResult(string payload)
        {
            ReceiveSetReactionResult(payload, false);
        }
        /// <summary>Native landing pad for a reaction error envelope.</summary>
        public void OnSetReactionError(string payload)
        {
            ReceiveSetReactionResult(payload, true);
        }
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    private partial class OctopusBridgeListenerProxy
    {
        /// <summary>Android listener completion envelope.</summary>
        public void onSetReactionResult(string payload)
        {
            ReceiveSetReactionResult(payload, false);
        }
        /// <summary>Android listener error envelope.</summary>
        public void onSetReactionError(string payload)
        {
            ReceiveSetReactionResult(payload, true);
        }
    }
#endif

#if UNITY_IOS && !UNITY_EDITOR
    [DllImport("__Internal")] private static extern void OctopusSdkSetReaction(int requestId, string contentId, string kind);
#endif
}
