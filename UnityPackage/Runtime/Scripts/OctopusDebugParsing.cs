using System;
using System.Collections.Generic;

internal static class OctopusDebugParsing
{
    internal static string NullableBoolToWire(bool? value)
    {
        return value.HasValue ? (value.Value ? "true" : "false") : "null";
    }

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
            { "postTextRequired", value.Post.TextRequired ? "true" : "false" },
            { "commentEnablePictures", value.Comment.EnablePictures ? "true" : "false" },
            { "replyEnablePictures", value.Reply.EnablePictures ? "true" : "false" }
        };
        return ObjectToJson(row, new HashSet<string>(row.Keys));
    }

    internal static string ConsentTextsToJson(OctopusConsentTexts value)
    {
        if (value == null) return "null";
        var row = new Dictionary<string, string>();
        var rawKeys = new HashSet<string>();
        switch (value.Mode)
        {
            case OctopusConsentTextsMode.Implicit:
                row["mode"] = "implicit";
                AddConsentField(row, rawKeys, "notice", value.Notice);
                break;
            case OctopusConsentTextsMode.MultiCheckbox:
                row["mode"] = "multiCheckbox";
                AddConsentField(row, rawKeys, "termsLabel", value.TermsLabel);
                AddConsentField(row, rawKeys, "privacyLabel", value.PrivacyLabel);
                AddConsentField(row, rawKeys, "rulesLabel", value.RulesLabel);
                break;
            case OctopusConsentTextsMode.SingleCheckbox:
                row["mode"] = "singleCheckbox";
                AddConsentField(row, rawKeys, "label", value.Label);
                AddConsentField(row, rawKeys, "belowText", value.BelowText);
                break;
            default: throw new ArgumentOutOfRangeException("value");
        }
        return ObjectToJson(row, rawKeys);
    }

    private static void AddConsentField(Dictionary<string, string> row, HashSet<string> rawKeys, string key, string value)
    {
        row[key] = value ?? "null";
        if (value == null) rawKeys.Add(key);
    }

    internal static OctopusConsentTexts ConsentTextsFromJson(string json)
    {
        if (json != null && json.Trim() == "null") return null;
        if (string.IsNullOrEmpty(json) || !json.Trim().StartsWith("{") || !json.Trim().EndsWith("}"))
            throw new FormatException("Expected consent texts object or null");
        var row = OctopusJson.ParseRawObject(json);
        string mode;
        if (!row.TryGetValue("mode", out mode)) throw new FormatException("Missing consent texts mode");
        switch (OctopusJson.StringFromRaw(mode))
        {
            case "implicit": return OctopusConsentTexts.Implicit(ReadConsentString(row, "notice"));
            case "multiCheckbox": return OctopusConsentTexts.MultiCheckbox(ReadConsentString(row, "termsLabel"),
                ReadConsentString(row, "privacyLabel"), ReadConsentString(row, "rulesLabel"));
            case "singleCheckbox": return OctopusConsentTexts.SingleCheckbox(ReadConsentString(row, "label"), ReadConsentString(row, "belowText"));
            default: throw new FormatException("Unknown consent texts mode");
        }
    }

    private static string ReadConsentString(Dictionary<string, string> row, string key)
    {
        string raw;
        if (!row.TryGetValue(key, out raw) || raw == "null") return null;
        if (!raw.StartsWith("\"") || !raw.EndsWith("\"")) throw new FormatException("Expected nullable string " + key);
        return OctopusJson.StringFromRaw(raw);
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
            TermsAcceptanceMode = terms,
            ForceNicknameChoice = row.ContainsKey("forceNicknameChoice") && ReadBool(row, "forceNicknameChoice"),
            ConsentTexts = row.TryGetValue("consentTexts", out raw) ? ConsentTextsFromJson(raw) : null
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
