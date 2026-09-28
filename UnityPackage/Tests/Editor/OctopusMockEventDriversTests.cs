using NUnit.Framework;

public class OctopusMockEventDriversTests
{
    [SetUp]
    public void SetUp() { OctopusSDK.Mock.Enabled = true; OctopusSDK.Mock.Reset(); }

    [Test]
    public void EmitLoginRequired_RaisesEvent()
    {
        bool raised = false;
        System.Action h = () => raised = true;
        OctopusSDK.OnLoginRequired += h;
        try { OctopusSDK.Mock.EmitLoginRequired(); }
        finally { OctopusSDK.OnLoginRequired -= h; }
        Assert.IsTrue(raised);
    }

    [Test]
    public void EmitNavigateToClientObject_RaisesEventWithId()
    {
        string seen = null;
        System.Action<string> h = id => seen = id;
        OctopusSDK.OnNavigateToClientObject += h;
        try { OctopusSDK.Mock.EmitNavigateToClientObject("obj_9"); }
        finally { OctopusSDK.OnNavigateToClientObject -= h; }
        Assert.AreEqual("obj_9", seen);
    }

    [Test]
    public void EmitModifyUser_RaisesEventWithField()
    {
        ProfileField? seen = null;
        System.Action<ProfileField?> h = f => seen = f;
        OctopusSDK.OnModifyUser += h;
        try { OctopusSDK.Mock.EmitModifyUser(ProfileField.NICKNAME); }
        finally { OctopusSDK.OnModifyUser -= h; }
        Assert.AreEqual(ProfileField.NICKNAME, seen);
    }

    // The Activity screen's "Edit my profile" item is gated on this flag on iOS (#280): the
    // native SDK hides the item while the callback is nil, so it must only be wired while a
    // host handler exists, or the item would dismiss the community and deliver to nobody.
    [Test]
    public void OnModifyUserSubscription_TogglesNativeHandlerFlag()
    {
        Assert.IsFalse(OctopusSDK.Mock.LastCall("SetHasModifyUserHandler").HasValue);
        System.Action<ProfileField?> first = _ => { };
        System.Action<ProfileField?> second = _ => { };
        OctopusSDK.OnModifyUser += first;
        Assert.AreEqual(true, OctopusSDK.Mock.LastCall("SetHasModifyUserHandler").Value.Args[0]);
        OctopusSDK.OnModifyUser += second;
        Assert.AreEqual(true, OctopusSDK.Mock.LastCall("SetHasModifyUserHandler").Value.Args[0]);
        // Still one subscriber left: the item must stay available.
        OctopusSDK.OnModifyUser -= first;
        Assert.AreEqual(true, OctopusSDK.Mock.LastCall("SetHasModifyUserHandler").Value.Args[0]);
        OctopusSDK.OnModifyUser -= second;
        Assert.AreEqual(false, OctopusSDK.Mock.LastCall("SetHasModifyUserHandler").Value.Args[0]);
    }

    [Test]
    public void EmitModifyUser_WithNull_RaisesEventWithNull()
    {
        ProfileField? seen = ProfileField.NICKNAME;
        bool raised = false;
        System.Action<ProfileField?> h = f => { seen = f; raised = true; };
        OctopusSDK.OnModifyUser += h;
        try { OctopusSDK.Mock.EmitModifyUser(null); }
        finally { OctopusSDK.OnModifyUser -= h; }
        Assert.IsTrue(raised);
        Assert.IsNull(seen);
    }
}
