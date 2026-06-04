using NUnit.Framework;
using System.Collections.Generic;

public class OctopusNotificationTests
{
    [Test]
    public void FlatDict_Android_ParsesKeysAndDeepLink()
    {
        var data = new Dictionary<string, string>
        {
            {"is_octopus_notification","true"},
            {"title","Hi"}, {"body","there"},
            {"link_path","post/abc/comment/def"},
            {"post_id","abc"}, {"comment_id","def"},
        };
        Assert.IsTrue(OctopusSDK.IsOctopusNotification(data));
        var n = OctopusSDK.GetOctopusNotification(data);
        Assert.AreEqual("Hi", n.Title);
        Assert.AreEqual("abc", n.PostId);
        Assert.AreEqual("def", n.CommentId);
        Assert.AreEqual("octopus-sdk://post/abc/comment/def", n.DeepLink);
    }

    [Test]
    public void NestedDataJson_iOS_ParsesKeysAndDeepLink()
    {
        var userInfo = new Dictionary<string, string>
        {
            {"aps","{...}"},
            {"data","{\"is_octopus_notification\":\"true\",\"title\":\"Hi\",\"link_path\":\"post/abc\",\"post_id\":\"abc\"}"},
        };
        Assert.IsTrue(OctopusSDK.IsOctopusNotification(userInfo));
        var n = OctopusSDK.GetOctopusNotification(userInfo);
        Assert.AreEqual("Hi", n.Title);
        Assert.AreEqual("abc", n.PostId);
        Assert.AreEqual("octopus-sdk://post/abc", n.DeepLink);
    }

    [Test]
    public void NestedDataObject_iOS_PrettyPrintedSlashEscaped_ParsesDeepLink()
    {
        // When the APNs payload carries `data` as a JSON OBJECT (not a string), Unity Mobile
        // Notifications re-serializes it with NSJSONWritingPrettyPrinted before exposing UserInfo:
        // whitespace/newlines between tokens and forward slashes escaped as \/. This is exactly
        // what arrives in C# for a tapped notification, so the parser must tolerate it.
        var userInfo = new Dictionary<string, string>
        {
            {"data","{\n  \"is_octopus_notification\" : \"true\",\n  \"link_path\" : \"post\\/th8ez21PGohk4aY8scqcsk\"\n}"},
        };
        Assert.IsTrue(OctopusSDK.IsOctopusNotification(userInfo));
        var n = OctopusSDK.GetOctopusNotification(userInfo);
        Assert.AreEqual("octopus-sdk://post/th8ez21PGohk4aY8scqcsk", n.DeepLink);
    }

    [Test]
    public void NotOctopus_ReturnsFalse()
    {
        Assert.IsFalse(OctopusSDK.IsOctopusNotification(new Dictionary<string, string> { {"foo","bar"} }));
        Assert.IsFalse(OctopusSDK.IsOctopusNotification(new Dictionary<string, string> { {"data","{\"x\":\"y\"}"} }));
    }

    [Test]
    public void DataDictJson_RoundTripsOctopusKeys()
    {
        var userInfo = new Dictionary<string, string>
        {
            {"data","{\"is_octopus_notification\":\"true\",\"link_path\":\"post/abc\",\"post_id\":\"abc\"}"},
        };
        var n = OctopusSDK.GetOctopusNotification(userInfo);
        var parsed = OctopusJson.ParseObject(n.DataAsJson);
        Assert.AreEqual("true", parsed["is_octopus_notification"]);
        Assert.AreEqual("post/abc", parsed["link_path"]);
    }
}
