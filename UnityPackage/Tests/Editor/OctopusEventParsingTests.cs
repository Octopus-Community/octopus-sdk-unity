using NUnit.Framework;

// Envelopes mirror the exact keys/tokens emitted by the Kotlin bridge eventToJson
// (and, in Phase 3, the iOS serializer). These pin the cross-platform wire parity.
public class OctopusEventParsingTests
{
    static OctopusEvent Parse(string json) => OctopusEventParsing.FromJson(json);

    [Test] public void PostCreated_parses_content_caseInsensitive()
    {
        var e = (PostCreatedEvent)Parse("{\"type\":\"PostCreated\",\"postId\":\"p1\",\"groupId\":\"g1\",\"textLength\":\"42\",\"content\":\"TEXT,IMAGE\"}");
        Assert.AreEqual(OctopusEventKind.PostCreated, e.Kind);
        Assert.AreEqual("p1", e.PostId);
        Assert.AreEqual("g1", e.GroupId);
        Assert.AreEqual(42, e.TextLength);
        Assert.AreEqual(OctopusPostContent.Text | OctopusPostContent.Image, e.Content);
    }

    [Test] public void CommentCreated_parses()
    {
        var e = (CommentCreatedEvent)Parse("{\"type\":\"CommentCreated\",\"commentId\":\"c1\",\"postId\":\"p1\",\"textLength\":\"5\"}");
        Assert.AreEqual("c1", e.CommentId); Assert.AreEqual("p1", e.PostId); Assert.AreEqual(5, e.TextLength);
    }

    [Test] public void ReplyCreated_parses()
    {
        var e = (ReplyCreatedEvent)Parse("{\"type\":\"ReplyCreated\",\"replyId\":\"r1\",\"commentId\":\"c1\",\"textLength\":\"3\"}");
        Assert.AreEqual("r1", e.ReplyId); Assert.AreEqual("c1", e.CommentId); Assert.AreEqual(3, e.TextLength);
    }

    [Test] public void ContentDeleted_post_collapsed()
    {
        var e = (ContentDeletedEvent)Parse("{\"type\":\"ContentDeleted\",\"contentId\":\"p1\",\"contentKind\":\"Post\",\"parentId\":\"g1\"}");
        Assert.AreEqual("p1", e.ContentId); Assert.AreEqual(OctopusContentKind.Post, e.ContentKind); Assert.AreEqual("g1", e.ParentId);
    }

    [Test] public void ReactionModified_withBoth()
    {
        var e = (ReactionModifiedEvent)Parse("{\"type\":\"ReactionModified\",\"contentId\":\"x\",\"contentKind\":\"Comment\",\"previousReaction\":\"Heart\",\"newReaction\":\"Rage\"}");
        Assert.AreEqual(OctopusContentKind.Comment, e.ContentKind);
        Assert.AreEqual(OctopusReactionKind.Heart, e.PreviousReaction);
        Assert.AreEqual(OctopusReactionKind.Rage, e.NewReaction);
    }

    [Test] public void ReactionModified_absentPrevious_isNone()
    {
        var e = (ReactionModifiedEvent)Parse("{\"type\":\"ReactionModified\",\"contentId\":\"x\",\"contentKind\":\"Post\",\"newReaction\":\"Joy\"}");
        Assert.AreEqual(OctopusReactionKind.None, e.PreviousReaction);
        Assert.AreEqual(OctopusReactionKind.Joy, e.NewReaction);
    }

    [Test] public void PollVoted_parses()
    {
        var e = (PollVotedEvent)Parse("{\"type\":\"PollVoted\",\"contentId\":\"p1\",\"optionId\":\"o2\"}");
        Assert.AreEqual("p1", e.ContentId); Assert.AreEqual("o2", e.OptionId);
    }

    [Test] public void ContentReported_rawReasonTokens()
    {
        var e = (ContentReportedEvent)Parse("{\"type\":\"ContentReported\",\"contentId\":\"x\",\"contentKind\":\"Post\",\"reasons\":\"HateSpeech,Other\"}");
        CollectionAssert.AreEqual(new[] { "HateSpeech", "Other" }, e.Reasons);
    }

    [Test] public void ProfileReported_parses()
    {
        var e = (ProfileReportedEvent)Parse("{\"type\":\"ProfileReported\",\"profileId\":\"u1\",\"reasons\":\"Spam\"}");
        Assert.AreEqual("u1", e.ProfileId); CollectionAssert.AreEqual(new[] { "Spam" }, e.Reasons);
    }

    [Test] public void GroupFollowingChanged_parsesBool()
    {
        var e = (GroupFollowingChangedEvent)Parse("{\"type\":\"GroupFollowingChanged\",\"groupId\":\"g1\",\"followed\":true}");
        Assert.AreEqual("g1", e.GroupId); Assert.IsTrue(e.Followed);
    }

    [Test] public void GamificationPointsGained_parses()
    {
        var e = (GamificationPointsGainedEvent)Parse("{\"type\":\"GamificationPointsGained\",\"points\":\"10\",\"action\":\"PostCommented\"}");
        Assert.AreEqual(10, e.Points); Assert.AreEqual(OctopusGamificationAction.PostCommented, e.Action);
    }

    [Test] public void GamificationPointsRemoved_parses()
    {
        var e = (GamificationPointsRemovedEvent)Parse("{\"type\":\"GamificationPointsRemoved\",\"points\":\"5\",\"action\":\"Reaction\"}");
        Assert.AreEqual(5, e.Points); Assert.AreEqual(OctopusGamificationAction.Reaction, e.Action);
    }

    [Test] public void ScreenDisplayed_groupDetail_clientApp()
    {
        var e = (ScreenDisplayedEvent)Parse("{\"type\":\"ScreenDisplayed\",\"screen\":\"GroupDetail\",\"groupId\":\"g1\",\"source\":\"ClientApp\"}");
        Assert.AreEqual(OctopusScreen.GroupDetail, e.Screen);
        Assert.AreEqual("g1", e.GroupId);
        Assert.AreEqual(OctopusGroupDetailSource.ClientApp, e.Source);
    }

    [Test] public void ScreenDisplayed_mainFeed()
    {
        var e = (ScreenDisplayedEvent)Parse("{\"type\":\"ScreenDisplayed\",\"screen\":\"MainFeed\",\"feedId\":\"f1\"}");
        Assert.AreEqual(OctopusScreen.MainFeed, e.Screen); Assert.AreEqual("f1", e.FeedId);
    }

    [Test] public void NotificationClicked_optionalContentIdAbsent()
    {
        var e = (NotificationClickedEvent)Parse("{\"type\":\"NotificationClicked\",\"notificationId\":\"n1\"}");
        Assert.AreEqual("n1", e.NotificationId); Assert.AreEqual("", e.ContentId);
    }

    [Test] public void PostClicked_parses()
    {
        var e = (PostClickedEvent)Parse("{\"type\":\"PostClicked\",\"postId\":\"p1\",\"source\":\"Feed\"}");
        Assert.AreEqual("p1", e.PostId); Assert.AreEqual(OctopusPostClickedSource.Feed, e.Source);
    }

    [Test] public void TranslationButtonClicked_parses()
    {
        var e = (TranslationButtonClickedEvent)Parse("{\"type\":\"TranslationButtonClicked\",\"contentId\":\"x\",\"viewTranslated\":true,\"contentKind\":\"Reply\"}");
        Assert.AreEqual("x", e.ContentId); Assert.IsTrue(e.ViewTranslated); Assert.AreEqual(OctopusContentKind.Reply, e.ContentKind);
    }

    [Test] public void CommentButtonClicked_parses()
    {
        var e = (CommentButtonClickedEvent)Parse("{\"type\":\"CommentButtonClicked\",\"postId\":\"p1\"}");
        Assert.AreEqual("p1", e.PostId);
    }

    [Test] public void ReplyButtonClicked_parses()
    {
        var e = (ReplyButtonClickedEvent)Parse("{\"type\":\"ReplyButtonClicked\",\"commentId\":\"c1\"}");
        Assert.AreEqual("c1", e.CommentId);
    }

    [Test] public void SeeRepliesButtonClicked_parses()
    {
        var e = (SeeRepliesButtonClickedEvent)Parse("{\"type\":\"SeeRepliesButtonClicked\",\"commentId\":\"c1\"}");
        Assert.AreEqual("c1", e.CommentId);
    }

    [Test] public void ProfileModified_parsesFlags()
    {
        var e = (ProfileModifiedEvent)Parse("{\"type\":\"ProfileModified\",\"nicknameChanged\":true,\"bioChanged\":false,\"pictureChanged\":true,\"bioLength\":\"12\",\"hasPicture\":true}");
        Assert.IsTrue(e.NicknameChanged); Assert.IsFalse(e.BioChanged); Assert.IsTrue(e.PictureChanged);
        Assert.AreEqual(12, e.BioLength); Assert.IsTrue(e.HasPicture);
    }

    [Test] public void SessionStarted_parses()
    {
        var e = (SessionStartedEvent)Parse("{\"type\":\"SessionStarted\",\"sessionId\":\"s1\"}");
        Assert.AreEqual("s1", e.SessionId);
    }

    [Test] public void SessionStopped_parses()
    {
        var e = (SessionStoppedEvent)Parse("{\"type\":\"SessionStopped\",\"sessionId\":\"s1\"}");
        Assert.AreEqual("s1", e.SessionId);
    }

    [Test] public void UnknownType_yieldsUnknownEvent_doesNotThrow()
    {
        var e = Parse("{\"type\":\"FutureThing\",\"x\":\"1\"}");
        Assert.AreEqual(OctopusEventKind.Unknown, e.Kind);
        Assert.AreEqual("FutureThing", ((UnknownEvent)e).RawType);
    }

    [Test] public void UnrecognisedEnumToken_fallsBackToUnknown()
    {
        var e = (ScreenDisplayedEvent)Parse("{\"type\":\"ScreenDisplayed\",\"screen\":\"FancyNewScreen\"}");
        Assert.AreEqual(OctopusScreen.Unknown, e.Screen);
    }
}
