using NUnit.Framework;
using System.Collections.Generic;

public class OctopusJsonTests
{
    [Test]
    public void ParseObject_ReadsStringBoolNumber_AsStrings()
    {
        var json = "{\"is_octopus_notification\":\"true\",\"followed\":true,\"count\":3,\"link_path\":\"post/abc\"}";
        Dictionary<string, string> d = OctopusJson.ParseObject(json);
        Assert.AreEqual("true", d["is_octopus_notification"]);
        Assert.AreEqual("true", d["followed"]);   // JSON bool -> "true"
        Assert.AreEqual("3", d["count"]);          // JSON number -> "3"
        Assert.AreEqual("post/abc", d["link_path"]);
    }

    [Test]
    public void ParseObject_HandlesEscapesAndSlashes()
    {
        var json = "{\"link_path\":\"comment\\/x\\/reply\\/y\",\"title\":\"a \\\"b\\\"\"}";
        var d = OctopusJson.ParseObject(json);
        Assert.AreEqual("comment/x/reply/y", d["link_path"]);
        Assert.AreEqual("a \"b\"", d["title"]);
    }

    [Test]
    public void ParseObject_NullOrEmpty_ReturnsEmptyDict()
    {
        Assert.AreEqual(0, OctopusJson.ParseObject(null).Count);
        Assert.AreEqual(0, OctopusJson.ParseObject("").Count);
        Assert.AreEqual(0, OctopusJson.ParseObject("{}").Count);
    }

    [Test]
    public void WriteArray_EmitsCompactObjects()
    {
        var rows = new List<Dictionary<string, string>>
        {
            new Dictionary<string, string> { {"groupId","g1"}, {"followed","true"}, {"actionDateMillis","1717000000000"} },
        };
        var raw = new HashSet<string> { "followed", "actionDateMillis" };
        string json = OctopusJson.WriteArray(rows, raw);
        Assert.AreEqual("[{\"groupId\":\"g1\",\"followed\":true,\"actionDateMillis\":1717000000000}]", json);
    }

    [Test]
    public void WriteArray_EscapesStrings()
    {
        var rows = new List<Dictionary<string, string>>
        {
            new Dictionary<string, string> { {"name","a \"b\" /c"} },
        };
        string json = OctopusJson.WriteArray(rows, new HashSet<string>());
        Assert.AreEqual("[{\"name\":\"a \\\"b\\\" /c\"}]", json);
    }
}
