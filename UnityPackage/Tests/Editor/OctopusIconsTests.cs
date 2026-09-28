using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

public class OctopusIconsTests
{
    [SetUp]
    public void SetUp() { OctopusSDK.Mock.Enabled = true; OctopusSDK.Mock.Reset(); }

    [TearDown]
    public void TearDown() { OctopusSDK.Mock.Enabled = true; OctopusSDK.Mock.Reset(); }

    // Pins every slot's value and bridge key: values are public ABI (append-only) and the keys are
    // matched verbatim by the Kotlin bridge and the Swift plugin.
    [TestCase(OctopusIconSlot.GroupsOpenList, 0, "groups.openList")]
    [TestCase(OctopusIconSlot.GroupsSelected, 1, "groups.selected")]
    [TestCase(OctopusIconSlot.GroupsViewGroup, 2, "groups.viewGroup")]
    [TestCase(OctopusIconSlot.ContentPostCreationOpen, 3, "content.post.creation.open")]
    [TestCase(OctopusIconSlot.ContentPostCreationTopicSelection, 4, "content.post.creation.topicSelection")]
    [TestCase(OctopusIconSlot.ContentPostCreationAddPicture, 5, "content.post.creation.addPicture")]
    [TestCase(OctopusIconSlot.ContentPostCreationDeletePicture, 6, "content.post.creation.deletePicture")]
    [TestCase(OctopusIconSlot.ContentPostCreationAddPoll, 7, "content.post.creation.addPoll")]
    [TestCase(OctopusIconSlot.ContentPostCreationAddPollOption, 8, "content.post.creation.addPollOption")]
    [TestCase(OctopusIconSlot.ContentPostCreationDeletePoll, 9, "content.post.creation.deletePoll")]
    [TestCase(OctopusIconSlot.ContentPostCreationDeletePollOption, 10, "content.post.creation.deletePollOption")]
    [TestCase(OctopusIconSlot.ContentPostEmptyFeedInGroups, 11, "content.post.emptyFeedInGroups")]
    [TestCase(OctopusIconSlot.ContentPostEmptyFeedInCurrentUserProfile, 12, "content.post.emptyFeedInCurrentUserProfile")]
    [TestCase(OctopusIconSlot.ContentPostEmptyFeedInOtherUserProfile, 13, "content.post.emptyFeedInOtherUserProfile")]
    [TestCase(OctopusIconSlot.ContentPostNotAvailable, 14, "content.post.notAvailable")]
    [TestCase(OctopusIconSlot.ContentPostCommentCount, 15, "content.post.commentCount")]
    [TestCase(OctopusIconSlot.ContentPostViewCount, 16, "content.post.viewCount")]
    [TestCase(OctopusIconSlot.ContentPostMoreReactions, 17, "content.post.moreReactions")]
    [TestCase(OctopusIconSlot.ContentPostLikeNotSelected, 18, "content.post.likeNotSelected")]
    [TestCase(OctopusIconSlot.ContentPostModerated, 19, "content.post.moderated")]
    [TestCase(OctopusIconSlot.ContentCommentCreationOpen, 20, "content.comment.creation.open")]
    [TestCase(OctopusIconSlot.ContentCommentCreationCreate, 21, "content.comment.creation.create")]
    [TestCase(OctopusIconSlot.ContentCommentCreationAddPicture, 22, "content.comment.creation.addPicture")]
    [TestCase(OctopusIconSlot.ContentCommentCreationDeletePicture, 23, "content.comment.creation.deletePicture")]
    [TestCase(OctopusIconSlot.ContentCommentEmptyFeed, 24, "content.comment.emptyFeed")]
    [TestCase(OctopusIconSlot.ContentCommentNotAvailable, 25, "content.comment.notAvailable")]
    [TestCase(OctopusIconSlot.ContentCommentSeeReplies, 26, "content.comment.seeReplies")]
    [TestCase(OctopusIconSlot.ContentCommentLikeNotSelected, 27, "content.comment.likeNotSelected")]
    [TestCase(OctopusIconSlot.ContentReplyCreationOpen, 28, "content.reply.creation.open")]
    [TestCase(OctopusIconSlot.ContentReplyCreationCreate, 29, "content.reply.creation.create")]
    [TestCase(OctopusIconSlot.ContentReplyCreationAddPicture, 30, "content.reply.creation.addPicture")]
    [TestCase(OctopusIconSlot.ContentReplyCreationDeletePicture, 31, "content.reply.creation.deletePicture")]
    [TestCase(OctopusIconSlot.ContentReplyLikeNotSelected, 32, "content.reply.likeNotSelected")]
    [TestCase(OctopusIconSlot.ContentVideoMuted, 33, "content.video.muted")]
    [TestCase(OctopusIconSlot.ContentVideoNotMuted, 34, "content.video.notMuted")]
    [TestCase(OctopusIconSlot.ContentVideoPause, 35, "content.video.pause")]
    [TestCase(OctopusIconSlot.ContentVideoPlay, 36, "content.video.play")]
    [TestCase(OctopusIconSlot.ContentVideoReplay, 37, "content.video.replay")]
    [TestCase(OctopusIconSlot.ContentPollSelectedOption, 38, "content.poll.selectedOption")]
    [TestCase(OctopusIconSlot.ContentReactionHeart, 39, "content.reaction.heart")]
    [TestCase(OctopusIconSlot.ContentReactionJoy, 40, "content.reaction.joy")]
    [TestCase(OctopusIconSlot.ContentReactionMouthOpen, 41, "content.reaction.mouthOpen")]
    [TestCase(OctopusIconSlot.ContentReactionClap, 42, "content.reaction.clap")]
    [TestCase(OctopusIconSlot.ContentReactionCry, 43, "content.reaction.cry")]
    [TestCase(OctopusIconSlot.ContentReactionRage, 44, "content.reaction.rage")]
    [TestCase(OctopusIconSlot.ContentDelete, 45, "content.delete")]
    [TestCase(OctopusIconSlot.ContentReport, 46, "content.report")]
    [TestCase(OctopusIconSlot.ContentViews, 47, "content.views")]
    [TestCase(OctopusIconSlot.ContentLike, 48, "content.like")]
    [TestCase(OctopusIconSlot.ProfileDefaultAvatar, 49, "profile.defaultAvatar")]
    [TestCase(OctopusIconSlot.ProfileAddPicture, 50, "profile.addPicture")]
    [TestCase(OctopusIconSlot.ProfileEditPicture, 51, "profile.editPicture")]
    [TestCase(OctopusIconSlot.ProfileAddBio, 52, "profile.addBio")]
    [TestCase(OctopusIconSlot.ProfileEmptyNotifications, 53, "profile.emptyNotifications")]
    [TestCase(OctopusIconSlot.ProfileReport, 54, "profile.report")]
    [TestCase(OctopusIconSlot.ProfileNotConnected, 55, "profile.notConnected")]
    [TestCase(OctopusIconSlot.ProfileBlockUser, 56, "profile.blockUser")]
    [TestCase(OctopusIconSlot.GamificationBadge, 57, "gamification.badge")]
    [TestCase(OctopusIconSlot.GamificationInfo, 58, "gamification.info")]
    [TestCase(OctopusIconSlot.GamificationRulesHeader, 59, "gamification.rulesHeader")]
    [TestCase(OctopusIconSlot.SettingsAccount, 60, "settings.account")]
    [TestCase(OctopusIconSlot.SettingsHelp, 61, "settings.help")]
    [TestCase(OctopusIconSlot.SettingsInfo, 62, "settings.info")]
    [TestCase(OctopusIconSlot.SettingsLogout, 63, "settings.logout")]
    [TestCase(OctopusIconSlot.SettingsDeleteAccountWarning, 64, "settings.deleteAccountWarning")]
    [TestCase(OctopusIconSlot.CommonRadioOn, 65, "common.radio.on")]
    [TestCase(OctopusIconSlot.CommonRadioOff, 66, "common.radio.off")]
    [TestCase(OctopusIconSlot.CommonCheckboxOn, 67, "common.checkbox.on")]
    [TestCase(OctopusIconSlot.CommonCheckboxOff, 68, "common.checkbox.off")]
    [TestCase(OctopusIconSlot.CommonToggleOn, 69, "common.toggle.on")]
    [TestCase(OctopusIconSlot.CommonToggleOff, 70, "common.toggle.off")]
    [TestCase(OctopusIconSlot.CommonMoreActions, 71, "common.moreActions")]
    [TestCase(OctopusIconSlot.CommonActivityButton, 72, "common.activityButton")]
    [TestCase(OctopusIconSlot.CommonListCellNavIndicator, 73, "common.listCellNavIndicator")]
    [TestCase(OctopusIconSlot.CommonClose, 74, "common.close")]
    public void SlotValueAndBridgeKeyArePinned(OctopusIconSlot slot, int value, string key)
    {
        Assert.AreEqual(value, (int)slot);
        Assert.AreEqual(key, OctopusSDK.IconSlotKey(slot));
    }

    [Test]
    public void EverySlotHasAUniqueKeyAndIsPinned()
    {
        var slots = (OctopusIconSlot[])Enum.GetValues(typeof(OctopusIconSlot));
        Assert.AreEqual(75, slots.Length, "a new slot needs a pinned TestCase above");
        var keys = slots.Select(OctopusSDK.IconSlotKey).ToList();
        CollectionAssert.AllItemsAreNotNull(keys);
        CollectionAssert.AllItemsAreUnique(keys);
    }

    [Test]
    public void NullOrEmptyIconsSerializeToAnEmptyArray()
    {
        Assert.AreEqual("[]", OctopusSDK.IconsToJson(null, false));
        Assert.AreEqual("[]", OctopusSDK.IconsToJson(new OctopusIcons(), true));
    }

    [Test]
    public void SerializesOnlyThePlatformResourceInSlotOrder()
    {
        var icons = new OctopusIcons()
            .Set(OctopusIconSlot.SettingsLogout, new OctopusIcon("ic_logout", "Data/Raw/logout.png"))
            .Set(OctopusIconSlot.GroupsOpenList, new OctopusIcon("ic_groups", "Groups"))
            .Set(OctopusIconSlot.CommonClose, new OctopusIcon(null, "Close"))
            .Set(OctopusIconSlot.ProfileDefaultAvatar, new OctopusIcon("ic_avatar", ""));

        var android = OctopusJson.ParseArray(OctopusSDK.IconsToJson(icons, false));
        Assert.AreEqual(3, android.Count);
        Assert.AreEqual("groups.openList", android[0]["slot"]);
        Assert.AreEqual("ic_groups", android[0]["resource"]);
        Assert.AreEqual("profile.defaultAvatar", android[1]["slot"]);
        Assert.AreEqual("settings.logout", android[2]["slot"]);
        Assert.AreEqual("ic_logout", android[2]["resource"]);

        var ios = OctopusJson.ParseArray(OctopusSDK.IconsToJson(icons, true));
        Assert.AreEqual(3, ios.Count);
        Assert.AreEqual("Groups", ios[0]["resource"]);
        Assert.AreEqual("settings.logout", ios[1]["slot"]);
        Assert.AreEqual("Data/Raw/logout.png", ios[1]["resource"]);
        Assert.AreEqual("common.close", ios[2]["slot"]);
    }

    [Test]
    public void EscapesResourceNames()
    {
        var icons = new OctopusIcons().Set(OctopusIconSlot.GroupsSelected, new OctopusIcon("a", "Data/Raw/\"q\".png"));
        var rows = OctopusJson.ParseArray(OctopusSDK.IconsToJson(icons, true));
        Assert.AreEqual("Data/Raw/\"q\".png", rows[0]["resource"]);
    }

    [Test]
    public void SetReplacesAndNullClears()
    {
        var icons = new OctopusIcons();
        icons.Set(OctopusIconSlot.ContentDelete, new OctopusIcon("a", "a"));
        icons.Set(OctopusIconSlot.ContentDelete, new OctopusIcon("b", "b"));
        Assert.AreEqual(1, icons.Count);
        Assert.AreEqual("b", icons.Get(OctopusIconSlot.ContentDelete).AndroidDrawableName);

        icons.Set(OctopusIconSlot.ContentDelete, null);
        Assert.AreEqual(0, icons.Count);
        Assert.IsNull(icons.Get(OctopusIconSlot.ContentDelete));
        Assert.IsFalse(icons.Remove(OctopusIconSlot.ContentDelete));
    }

    [Test]
    public void RejectsAnUndefinedSlot()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new OctopusIcons().Set((OctopusIconSlot)9999, new OctopusIcon("a", "a")));
    }

    [Test]
    public void SetIconsRecordsBothPlatformPayloadsInTheEditor()
    {
        OctopusSDK.SetIcons(new OctopusIcons()
            .Set(OctopusIconSlot.ContentReactionHeart, new OctopusIcon("ic_heart", "Heart")));
        var args = OctopusSDK.Mock.LastCall("SetIcons").Value.Args;
        Assert.AreEqual("[{\"slot\":\"content.reaction.heart\",\"resource\":\"ic_heart\"}]", args[0]);
        Assert.AreEqual("[{\"slot\":\"content.reaction.heart\",\"resource\":\"Heart\"}]", args[1]);

        OctopusSDK.SetIcons(null);
        Assert.AreEqual("[]", OctopusSDK.Mock.LastCall("SetIcons").Value.Args[0]);
    }
}
