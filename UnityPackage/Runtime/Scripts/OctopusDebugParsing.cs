using System;
using System.Collections.Generic;

internal static class OctopusDebugParsing
{
    internal static string LockToWire(OctopusProfileFieldLockState state)
    {
        switch (state)
        {
            case OctopusProfileFieldLockState.Editable: return "editable";
            case OctopusProfileFieldLockState.ReadOnly: return "readOnly";
            case OctopusProfileFieldLockState.Disabled: return "disabled";
            default: throw new ArgumentOutOfRangeException("state");
        }
    }

    internal static string TermsToWire(OctopusTermsAcceptanceMode mode)
    {
        switch (mode)
        {
            case OctopusTermsAcceptanceMode.Implicit: return "implicit";
            case OctopusTermsAcceptanceMode.ExplicitMultiCheckbox: return "explicitMultiCheckbox";
            case OctopusTermsAcceptanceMode.ExplicitSingleCheckbox: return "explicitSingleCheckbox";
            default: throw new ArgumentOutOfRangeException("mode");
        }
    }

    internal static string ProfileLockToJson(OctopusProfileFieldsLock value)
    {
        if (value == null) return "null";
        return ObjectToJson(new Dictionary<string, string>
        {
            { "nickname", LockToWire(value.Nickname) },
            { "avatar", LockToWire(value.Avatar) },
            { "bio", LockToWire(value.Bio) }
        }, new HashSet<string>());
    }

    internal static string ContentOptionsToJson(OctopusContentOptions value)
    {
        if (value == null) return "null";
        var row = new Dictionary<string, string>
        {
            { "postEnablePictures", value.Post.EnablePictures ? "true" : "false" },
            { "postEnablePolls", value.Post.EnablePolls ? "true" : "false" },
            { "commentEnablePictures", value.Comment.EnablePictures ? "true" : "false" },
            { "replyEnablePictures", value.Reply.EnablePictures ? "true" : "false" }
        };
        return ObjectToJson(row, new HashSet<string>(row.Keys));
    }

    private static string ObjectToJson(Dictionary<string, string> row, HashSet<string> rawKeys)
    {
        string array = OctopusJson.WriteArray(new List<Dictionary<string, string>> { row }, rawKeys);
        return array.Substring(1, array.Length - 2);
    }

    internal static OctopusCommunityConfig ConfigFromJson(string json)
    {
        if (json != null && json.Trim() == "null") return null;
        if (string.IsNullOrEmpty(json) || !json.Trim().StartsWith("{") || !json.Trim().EndsWith("}"))
            throw new FormatException("Expected a config object or null");
        var row = OctopusJson.ParseRawObject(json);
        string raw;
        if (!row.TryGetValue("termsAcceptanceMode", out raw)) throw new FormatException("Missing termsAcceptanceMode");
        string mode = OctopusJson.StringFromRaw(raw);
        OctopusTermsAcceptanceMode terms;
        // Accept native Android and Swift spellings as well as the normalized bridge spelling.
        switch (mode)
        {
            case "implicit":
            case "IMPLICIT": terms = OctopusTermsAcceptanceMode.Implicit; break;
            case "explicitMultiCheckbox":
            case "EXPLICIT_MULTI_CHECKBOX": terms = OctopusTermsAcceptanceMode.ExplicitMultiCheckbox; break;
            case "explicitSingleCheckbox":
            case "EXPLICIT_SINGLE_CHECKBOX": terms = OctopusTermsAcceptanceMode.ExplicitSingleCheckbox; break;
            default: throw new FormatException("Unknown termsAcceptanceMode");
        }
        return new OctopusCommunityConfig
        {
            ExposeClientUserId = ReadBool(row, "exposeClientUserId"),
            ForceLoginOnStrongActions = ReadBool(row, "forceLoginOnStrongActions"),
            DisplayAccountAge = ReadBool(row, "displayAccountAge"),
            TermsAcceptanceMode = terms
        };
    }

    private static bool ReadBool(Dictionary<string, string> row, string key)
    {
        string raw;
        if (!row.TryGetValue(key, out raw) || (raw != "true" && raw != "false"))
            throw new FormatException("Expected boolean " + key);
        return raw == "true";
    }
}
