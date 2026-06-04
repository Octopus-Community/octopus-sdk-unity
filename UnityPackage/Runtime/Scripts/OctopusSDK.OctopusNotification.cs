using System.Collections.Generic;

public partial class OctopusSDK
{
    const string IS_OCTOPUS_NOTIFICATION_KEY = "is_octopus_notification";
    const string DATA_ENVELOPE_KEY = "data";

    // Returns the octopus-key dictionary from either a flat payload (Android) or the
    // iOS shape where the keys are a JSON string under "data".
    static IDictionary<string, string> ExtractOctopusData(IDictionary<string, string> payload)
    {
        if (payload == null) return new Dictionary<string, string>();
        if (payload.ContainsKey(IS_OCTOPUS_NOTIFICATION_KEY)) return payload;        // flat (Android)
        if (payload.ContainsKey(DATA_ENVELOPE_KEY))
            return OctopusJson.ParseObject(payload[DATA_ENVELOPE_KEY]);              // nested JSON (iOS)
        return new Dictionary<string, string>();
    }

    public static bool IsOctopusNotification(IDictionary<string, string> payload)
    {
        var data = ExtractOctopusData(payload);
        return data.ContainsKey(IS_OCTOPUS_NOTIFICATION_KEY)
            && data[IS_OCTOPUS_NOTIFICATION_KEY].ToLower() == "true";
    }

    public static OctopusNotification GetOctopusNotification(IDictionary<string, string> payload)
    {
        return new OctopusNotification(ExtractOctopusData(payload));
    }
}

public class OctopusNotification
{
    const string INTERNAL_DEEP_LINK_BASE_PATH = "octopus-sdk://";
    const string TITLE_KEY = "title";
    const string BODY_KEY = "body";
    const string LINK_PATH_KEY = "link_path";
    const string POST_ID_KEY = "post_id";
    const string COMMENT_ID_KEY = "comment_id";
    const string REPLY_ID_KEY = "reply_id";
    readonly IDictionary<string, string> data;

    public OctopusNotification(IDictionary<string, string> data)
    {
        this.data = data ?? new Dictionary<string, string>();
    }

    public string Title { get { return GetVal(TITLE_KEY); } }
    public string Body { get { return GetVal(BODY_KEY); } }
    private string LinkPath { get { return GetVal(LINK_PATH_KEY); } }
    public string PostId { get { return GetVal(POST_ID_KEY); } }
    public string CommentId { get { return GetVal(COMMENT_ID_KEY); } }
    public string ReplyId { get { return GetVal(REPLY_ID_KEY); } }
    public string DeepLink { get { return LinkPath.Length > 0 ? INTERNAL_DEEP_LINK_BASE_PATH + LinkPath : ""; } }

    // Compact JSON of the octopus keys — what the iOS native bridge consumes.
    internal string DataAsJson
    {
        get
        {
            var rows = new List<Dictionary<string, string>> { new Dictionary<string, string>(data) };
            string arr = OctopusJson.WriteArray(rows, new HashSet<string>()); // all values quoted
            return arr.Length >= 2 ? arr.Substring(1, arr.Length - 2) : "{}";
        }
    }

    private string GetVal(string key)
    {
        return data.ContainsKey(key) ? data[key] : "";
    }
}
