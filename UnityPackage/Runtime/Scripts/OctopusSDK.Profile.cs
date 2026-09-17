using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>The connected user's public profile, shared by Android and iOS.</summary>
public sealed class OctopusProfile
{
    /// <summary>Held entitlement identifiers, unique and unordered. Display only; access is resolved by the backend.</summary>
    public IReadOnlyCollection<string> Entitlements { get; private set; }

    /// <summary>The host user id supplied in SSO mode; null for guests and Octopus authentication.
    /// Independent of the setting that exposes other members' client user ids.</summary>
    public string ClientUserId { get; private set; }

    /// <summary>Creates an immutable profile snapshot. Null entitlements become an empty collection.</summary>
    public OctopusProfile(IEnumerable<string> entitlements = null, string clientUserId = null)
    {
        var unique = new HashSet<string>(entitlements ?? new string[0]);
        unique.Remove(null);
        Entitlements = new List<string>(unique).AsReadOnly();
        ClientUserId = clientUserId;
    }
}

internal static class OctopusProfileParsing
{
    internal static OctopusProfile FromJson(string json)
    {
        if (string.IsNullOrEmpty(json) || json.Trim() == "null") return null;
        if (!json.TrimStart().StartsWith("{", StringComparison.Ordinal)) return null;
        try
        {
            var fields = OctopusJson.ParseRawObject(json);
            string raw;
            var entitlements = fields.TryGetValue("entitlements", out raw)
                ? OctopusJson.ParseStringArray(raw) : new List<string>();
            string clientUserId = fields.TryGetValue("clientUserId", out raw)
                ? OctopusJson.StringFromRaw(raw) : null;
            return new OctopusProfile(entitlements, clientUserId);
        }
        catch (FormatException) { return null; }
        catch (ArgumentException) { return null; }
        catch (OverflowException) { return null; }
    }
}

public partial class OctopusSDK
{
    /// <summary>Latest profile delivered on the Unity thread; null before observation or when native has no profile.
    /// A non-null profile may represent a guest.</summary>
    public static OctopusProfile CurrentProfile { get; private set; }

    /// <summary>Raised on the Unity main thread after CurrentProfile is updated. The argument may be null.
    /// Subscribe before Initialize to receive the initial native value; late subscribers can read CurrentProfile.</summary>
    public static event Action<OctopusProfile> OnProfileChanged;

    internal static void ReceiveProfile(string json)
    {
        var profile = OctopusProfileParsing.FromJson(json);
        QueueProfile(profile);
    }

    private static int _profileDispatchGeneration;

    internal static void ResetProfileObservation()
    {
        _profileDispatchGeneration++;
        CurrentProfile = null;
    }

    private static void QueueProfile(OctopusProfile profile)
    {
        int generation = _profileDispatchGeneration;
        OctopusMainThread.Post(() =>
        {
            if (generation != _profileDispatchGeneration) return;
            CurrentProfile = profile;
            OnProfileChanged?.Invoke(profile);
        });
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    private partial class OctopusBridgeListenerProxy
    {
        /// <summary>Receives the Android observer snapshot and queues Unity-thread delivery.</summary>
        public void onProfileChanged(string json) { ReceiveProfile(json); }
    }
#endif

    public partial class OctopusChannel : MonoBehaviour
    {
        /// <summary>Native profile observer landing pad; payload is a profile JSON object or null.</summary>
        public void OnProfileChanged(string json) { ReceiveProfile(json); }
    }
}
