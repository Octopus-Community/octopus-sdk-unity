using System;
using System.Collections.Generic;
using System.Globalization;

internal static partial class OctopusJson
{
    internal static OctopusCommunityData CommunityDataFromJson(string json)
    {
        if (string.IsNullOrEmpty(json) || !json.TrimStart().StartsWith("{", StringComparison.Ordinal)) return null;
        try
        {
            var fields = ParseRawObject(json);
            string raw;
            string profileId = fields.TryGetValue("profileId", out raw) ? StringFromRaw(raw) : null;
            OctopusGamification gamification = null;
            if (fields.TryGetValue("gamification", out raw) && raw.StartsWith("{", StringComparison.Ordinal))
            {
                var standing = ParseRawObject(raw);
                gamification = new OctopusGamification(CommunityDataInteger(standing, "level") ?? 0,
                    CommunityDataInteger(standing, "score"));
            }
            return new OctopusCommunityData(profileId, CommunityDataInteger(fields, "messageCount"), gamification);
        }
        catch (FormatException) { return null; }
        catch (ArgumentException) { return null; }
        catch (OverflowException) { return null; }
    }

    private static int? CommunityDataInteger(Dictionary<string, string> fields, string key)
    {
        string raw;
        int value;
        return fields.TryGetValue(key, out raw) && int.TryParse(raw, NumberStyles.AllowLeadingSign,
            CultureInfo.InvariantCulture, out value) ? (int?)value : null;
    }
}
