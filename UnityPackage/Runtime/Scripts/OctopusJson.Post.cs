using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

internal static partial class OctopusJson
{
    internal static OctopusPost PostFromJson(string json)
    {
        if (string.IsNullOrEmpty(json) || json.Trim() == "null") return null;
        if (!json.TrimStart().StartsWith("{", StringComparison.Ordinal)) return null;
        try
        {
            var fields = ParseRawObject(json);
            string raw;
            string id = fields.TryGetValue("id", out raw) ? StringFromRaw(raw) : null;
            var reactions = new List<OctopusReactionCount>();
            if (fields.TryGetValue("reactions", out raw))
            {
                foreach (var item in PostArrayValues(raw))
                {
                    if (!item.StartsWith("{", StringComparison.Ordinal)) continue;
                    var row = ParseRawObject(item);
                    string kind;
                    row.TryGetValue("reactionKind", out kind);
                    reactions.Add(new OctopusReactionCount(PostReactionFromRaw(kind), PostCount(row, "count")));
                }
            }
            OctopusReactionKind? userReaction = null;
            if (fields.TryGetValue("userReactionKind", out raw) && raw != "null")
                userReaction = PostReactionFromRaw(raw);
            return new OctopusPost(id, reactions, PostCount(fields, "commentCount"),
                PostCount(fields, "viewCount"), userReaction);
        }
        catch (FormatException) { return null; }
        catch (ArgumentException) { return null; }
        catch (OverflowException) { return null; }
    }

    private static List<string> PostArrayValues(string json)
    {
        var values = new List<string>();
        if (string.IsNullOrEmpty(json) || json[0] != '[') return values;
        int i = 1;
        while (i < json.Length)
        {
            SkipWs(json, ref i);
            if (i == json.Length || json[i] == ']') break;
            values.Add(ReadRawValue(json, ref i));
            SkipWs(json, ref i);
            if (i == json.Length || json[i++] != ',') break;
        }
        return values;
    }

    private static int PostCount(Dictionary<string, string> row, string key)
    {
        string raw;
        int count;
        return row.TryGetValue(key, out raw) && int.TryParse(raw, NumberStyles.Integer,
            CultureInfo.InvariantCulture, out count) ? count : 0;
    }

    private static OctopusReactionKind PostReactionFromRaw(string raw)
    {
        switch (StringFromRaw(raw))
        {
            case "Heart": return OctopusReactionKind.Heart;
            case "Joy": return OctopusReactionKind.Joy;
            case "MouthOpen": return OctopusReactionKind.MouthOpen;
            case "Clap": return OctopusReactionKind.Clap;
            case "Cry": return OctopusReactionKind.Cry;
            case "Rage": return OctopusReactionKind.Rage;
            default: return OctopusReactionKind.Unknown;
        }
    }

    internal static OctopusClientPostError ClientPostErrorFromJson(string json)
    {
        try
        {
            var row = ParseObject(json);
            string type, message;
            row.TryGetValue("type", out type);
            row.TryGetValue("message", out message);
            var code = OctopusClientPostErrorCode.Other;
            switch (type)
            {
                case "textMissing": code = OctopusClientPostErrorCode.TextMissing; break;
                case "textTooLong": code = OctopusClientPostErrorCode.TextTooLong; break;
                case "fileEmpty": code = OctopusClientPostErrorCode.FileEmpty; break;
                case "fileTooLarge": code = OctopusClientPostErrorCode.FileTooLarge; break;
                case "fileBadFormat": code = OctopusClientPostErrorCode.FileBadFormat; break;
                case "fileUpload": code = OctopusClientPostErrorCode.FileUpload; break;
                case "fileDownload": code = OctopusClientPostErrorCode.FileDownload; break;
                case "missingObjectId": code = OctopusClientPostErrorCode.MissingObjectId; break;
                case "missingCta": code = OctopusClientPostErrorCode.MissingCta; break;
                case "postUnavailable": code = OctopusClientPostErrorCode.PostUnavailable; break;
                case "postNotFound": code = OctopusClientPostErrorCode.PostNotFound; break;
                case "postAlreadyExists": code = OctopusClientPostErrorCode.PostAlreadyExists; break;
                case "invalidGroupId": code = OctopusClientPostErrorCode.InvalidGroupId; break;
                case "invalidAuthor": code = OctopusClientPostErrorCode.InvalidAuthor; break;
                case "tokenInvalid": code = OctopusClientPostErrorCode.TokenInvalid; break;
                case "tokenExpired": code = OctopusClientPostErrorCode.TokenExpired; break;
            }
            return new OctopusClientPostError(code, message);
        }
        catch (FormatException) { return new OctopusClientPostError(OctopusClientPostErrorCode.Other, "Malformed client post error"); }
    }

    internal static string ClientObjectToJson(OctopusClientObject value)
    {
        var row = new Dictionary<string, string>
        {
            { "objectId", value.ObjectId }, { "text", value.Text }, { "groupId", value.GroupId },
            { "catchPhrase", value.CatchPhrase }, { "viewObjectButtonText", value.ViewObjectButtonText },
            { "imagePath", value.ImagePath }, { "imageUrl", value.ImageUrl }
        };
        string array = WriteArray(new List<Dictionary<string, string>> { row }, new HashSet<string>());
        // The shared flat writer handles common escapes; JSON also forbids every other
        // raw control character (for example backspace and form feed) in content fields.
        var json = new StringBuilder();
        for (int i = 1; i < array.Length - 1; i++)
        {
            char c = array[i];
            if (c < 32) json.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
            else json.Append(c);
        }
        return json.ToString();
    }
}
