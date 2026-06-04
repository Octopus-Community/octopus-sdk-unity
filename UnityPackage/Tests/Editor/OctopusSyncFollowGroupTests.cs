using NUnit.Framework;
using System;
using System.Collections.Generic;

public class OctopusSyncFollowGroupTests
{
    [Test]
    public void StatusFromWire_MapsAllValues()
    {
        Assert.AreEqual(OctopusSyncFollowGroupStatus.Applied, OctopusSyncFollowGroupParsing.StatusFromWire("applied"));
        Assert.AreEqual(OctopusSyncFollowGroupStatus.Skipped, OctopusSyncFollowGroupParsing.StatusFromWire("skipped"));
        Assert.AreEqual(OctopusSyncFollowGroupStatus.GroupNotFound, OctopusSyncFollowGroupParsing.StatusFromWire("groupNotFound"));
        Assert.AreEqual(OctopusSyncFollowGroupStatus.NotFollowable, OctopusSyncFollowGroupParsing.StatusFromWire("notFollowable"));
        Assert.AreEqual(OctopusSyncFollowGroupStatus.NotUnfollowable, OctopusSyncFollowGroupParsing.StatusFromWire("notUnfollowable"));
        Assert.AreEqual(OctopusSyncFollowGroupStatus.AlreadyFollowed, OctopusSyncFollowGroupParsing.StatusFromWire("alreadyFollowed"));
        Assert.AreEqual(OctopusSyncFollowGroupStatus.AlreadyUnfollowed, OctopusSyncFollowGroupParsing.StatusFromWire("alreadyUnfollowed"));
        Assert.AreEqual(OctopusSyncFollowGroupStatus.UnknownError, OctopusSyncFollowGroupParsing.StatusFromWire("anything-else"));
    }

    [Test]
    public void ActionsToJson_EmitsRawBoolAndMillis()
    {
        var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var actions = new List<OctopusSyncFollowGroupAction>
        {
            new OctopusSyncFollowGroupAction { GroupId = "g1", Followed = true, ActionDate = epoch.AddMilliseconds(1717000000000) },
        };
        string json = OctopusSyncFollowGroupParsing.ActionsToJson(actions);
        Assert.AreEqual("[{\"groupId\":\"g1\",\"followed\":true,\"actionDateMillis\":1717000000000}]", json);
    }

    [Test]
    public void ResultsFromJson_ParsesRows()
    {
        var json = "[{\"groupId\":\"g1\",\"status\":\"applied\"},{\"groupId\":\"g2\",\"status\":\"skipped\"}]";
        var results = OctopusSyncFollowGroupParsing.ResultsFromJson(json);
        Assert.AreEqual(2, results.Count);
        Assert.AreEqual("g1", results[0].GroupId);
        Assert.AreEqual(OctopusSyncFollowGroupStatus.Applied, results[0].Status);
        Assert.AreEqual(OctopusSyncFollowGroupStatus.Skipped, results[1].Status);
    }

    [Test]
    public void GroupsFromJson_ParsesBoolFields()
    {
        var json = "[{\"id\":\"g1\",\"name\":\"Tech\",\"isFollowed\":true,\"canChangeFollowStatus\":false}]";
        var groups = OctopusSyncFollowGroupParsing.GroupsFromJson(json);
        Assert.AreEqual(1, groups.Count);
        Assert.AreEqual("g1", groups[0].Id);
        Assert.AreEqual("Tech", groups[0].Name);
        Assert.IsTrue(groups[0].IsFollowed);
        Assert.IsFalse(groups[0].CanChangeFollowStatus);
    }
}
