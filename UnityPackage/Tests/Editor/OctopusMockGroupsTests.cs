using System.Collections.Generic;
using NUnit.Framework;

public class OctopusMockGroupsTests
{
    [SetUp]
    public void SetUp() { OctopusSDK.Mock.Enabled = true; OctopusSDK.Mock.Reset(); }

    [Test]
    public void FetchGroups_ReturnsSeedGroups()
    {
        IList<OctopusGroup> got = null;
        OctopusSDK.FetchGroups(g => got = g);
        Assert.IsNotNull(got); // empty list if no seed configured
        Assert.IsTrue(OctopusSDK.Mock.LastCall("FetchGroups").HasValue);
    }

    [Test]
    public void SyncFollowGroups_InvokesOnCompletedWithResultPerAction()
    {
        IList<OctopusSyncFollowGroupResult> results = null;
        var actions = new List<OctopusSyncFollowGroupAction>
        {
            new OctopusSyncFollowGroupAction { GroupId = "g1", Followed = true },
        };
        OctopusSDK.SyncFollowGroups(actions, r => results = r);
        Assert.IsNotNull(results);
        Assert.AreEqual(1, results.Count);
        Assert.AreEqual("g1", results[0].GroupId);
    }

    [Test]
    public void EmitGroupsChanged_RaisesEvent()
    {
        IList<OctopusGroup> seen = null;
        System.Action<IList<OctopusGroup>> h = g => seen = g;
        OctopusSDK.OnGroupsChanged += h;
        var groups = new List<OctopusGroup> { new OctopusGroup { Id = "g1", Name = "General" } };
        try { OctopusSDK.Mock.EmitGroupsChanged(groups); }
        finally { OctopusSDK.OnGroupsChanged -= h; }
        Assert.AreEqual(1, seen.Count);
        Assert.AreEqual("General", seen[0].Name);
    }
}
