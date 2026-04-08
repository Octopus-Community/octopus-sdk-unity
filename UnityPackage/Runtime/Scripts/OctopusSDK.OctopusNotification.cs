using System.Collections.Generic;

public partial class OctopusSDK
{
    const string IS_OCTOPUS_NOTIFICATION_KEY = "is_octopus_notification";

    public static bool IsOctopusNotification(IDictionary<string, string> data)
    {
        if(data.ContainsKey(IS_OCTOPUS_NOTIFICATION_KEY))
        {
            return data[IS_OCTOPUS_NOTIFICATION_KEY].ToLower() == "true";
        }
        return false;
    }

    public static OctopusNotification GetOctopusNotification(IDictionary<string, string> data)
    {
        return new OctopusNotification(data);
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
        this.data = data;
    }

    public string Title { get { return GetVal(data, TITLE_KEY, ""); } }
    public string Body { get { return GetVal(data, BODY_KEY, ""); } }
    private string LinkPath { get { return GetVal(data, LINK_PATH_KEY, ""); } }
    public string PostId { get { return GetVal(data, POST_ID_KEY, ""); } }
    public string CommentId { get { return GetVal(data, COMMENT_ID_KEY, ""); } }
    public string ReplyId { get { return GetVal(data, REPLY_ID_KEY, ""); } }
    public string DeepLink { get { return LinkPath.Length > 0 ? INTERNAL_DEEP_LINK_BASE_PATH + LinkPath : ""; } }

    private string GetVal(IDictionary<string, string> data, string key, string defaultValue)
    {
        if(data.ContainsKey(key))
        {
            return data[key];
        }
        return defaultValue;
    }
}
