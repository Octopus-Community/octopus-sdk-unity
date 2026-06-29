using System;
using System.Collections.Generic;

// Parses the flat-JSON event envelope emitted by the native bridges (Kotlin eventToJson /
// future iOS serializer) into the typed OctopusEvent model. Pure + Editor-testable.
// Wire tokens are the C# enum member names; parsing is case-insensitive so a native
// `.name` token like "TEXT" still maps to OctopusPostContent.Text.
internal static class OctopusEventParsing
{
    internal static OctopusEvent FromJson(string json)
    {
        var d = OctopusJson.ParseObject(json);
        string type = S(d, "type");
        switch (type)
        {
            case "PostCreated":
                return new PostCreatedEvent {
                    PostId = S(d, "postId"), GroupId = S(d, "groupId"),
                    TextLength = I(d, "textLength"), Content = ContentFlags(d, "content")
                };
            case "CommentCreated":
                return new CommentCreatedEvent {
                    CommentId = S(d, "commentId"), PostId = S(d, "postId"), TextLength = I(d, "textLength")
                };
            case "ReplyCreated":
                return new ReplyCreatedEvent {
                    ReplyId = S(d, "replyId"), CommentId = S(d, "commentId"), TextLength = I(d, "textLength")
                };
            case "ContentDeleted":
                return new ContentDeletedEvent {
                    ContentId = S(d, "contentId"), ContentKind = Enum(d, "contentKind", OctopusContentKind.Unknown),
                    ParentId = S(d, "parentId")
                };
            case "ReactionModified":
                return new ReactionModifiedEvent {
                    ContentId = S(d, "contentId"), ContentKind = Enum(d, "contentKind", OctopusContentKind.Unknown),
                    PreviousReaction = Reaction(d, "previousReaction"), NewReaction = Reaction(d, "newReaction")
                };
            case "PollVoted":
                return new PollVotedEvent { ContentId = S(d, "contentId"), OptionId = S(d, "optionId") };
            case "ContentReported":
                return new ContentReportedEvent {
                    ContentId = S(d, "contentId"), ContentKind = Enum(d, "contentKind", OctopusContentKind.Unknown),
                    Reasons = Csv(d, "reasons")
                };
            case "ProfileReported":
                return new ProfileReportedEvent { ProfileId = S(d, "profileId"), Reasons = Csv(d, "reasons") };
            case "GroupFollowingChanged":
                return new GroupFollowingChangedEvent { GroupId = S(d, "groupId"), Followed = B(d, "followed") };
            case "GamificationPointsGained":
                return new GamificationPointsGainedEvent { Points = I(d, "points"), Action = Enum(d, "action", OctopusGamificationAction.Unknown) };
            case "GamificationPointsRemoved":
                return new GamificationPointsRemovedEvent { Points = I(d, "points"), Action = Enum(d, "action", OctopusGamificationAction.Unknown) };
            case "ScreenDisplayed":
                return new ScreenDisplayedEvent {
                    Screen = Enum(d, "screen", OctopusScreen.Unknown),
                    FeedId = S(d, "feedId"), PostId = S(d, "postId"), CommentId = S(d, "commentId"),
                    GroupId = S(d, "groupId"), ProfileId = S(d, "profileId"),
                    Source = Enum(d, "source", OctopusGroupDetailSource.Unknown)
                };
            case "NotificationClicked":
                return new NotificationClickedEvent { NotificationId = S(d, "notificationId"), ContentId = S(d, "contentId") };
            case "PostClicked":
                return new PostClickedEvent { PostId = S(d, "postId"), Source = Enum(d, "source", OctopusPostClickedSource.Unknown) };
            case "TranslationButtonClicked":
                return new TranslationButtonClickedEvent {
                    ContentId = S(d, "contentId"), ViewTranslated = B(d, "viewTranslated"),
                    ContentKind = Enum(d, "contentKind", OctopusContentKind.Unknown)
                };
            case "CommentButtonClicked":
                return new CommentButtonClickedEvent { PostId = S(d, "postId") };
            case "ReplyButtonClicked":
                return new ReplyButtonClickedEvent { CommentId = S(d, "commentId") };
            case "SeeRepliesButtonClicked":
                return new SeeRepliesButtonClickedEvent { CommentId = S(d, "commentId") };
            case "ProfileModified":
                return new ProfileModifiedEvent {
                    NicknameChanged = B(d, "nicknameChanged"), BioChanged = B(d, "bioChanged"),
                    PictureChanged = B(d, "pictureChanged"), BioLength = I(d, "bioLength"), HasPicture = B(d, "hasPicture")
                };
            case "SessionStarted":
                return new SessionStartedEvent { SessionId = S(d, "sessionId") };
            case "SessionStopped":
                return new SessionStoppedEvent { SessionId = S(d, "sessionId") };
            default:
                return new UnknownEvent { RawType = type };
        }
    }

    static string S(Dictionary<string, string> d, string k) => d.TryGetValue(k, out var v) ? v : "";

    static int I(Dictionary<string, string> d, string k) =>
        d.TryGetValue(k, out var v) && int.TryParse(v, out var n) ? n : 0;

    static bool B(Dictionary<string, string> d, string k) => d.TryGetValue(k, out var v) && v == "true";

    static string[] Csv(Dictionary<string, string> d, string k) =>
        d.TryGetValue(k, out var v) && !string.IsNullOrEmpty(v) ? v.Split(',') : Array.Empty<string>();

    // Case-insensitive enum token → value, with a fallback for unrecognised/absent tokens.
    static T Enum<T>(Dictionary<string, string> d, string k, T fallback) where T : struct =>
        d.TryGetValue(k, out var v) && System.Enum.TryParse<T>(v, true, out var e) ? e : fallback;

    // Reaction keys are omitted by the bridge when null → None; a present-but-unknown token → Unknown.
    static OctopusReactionKind Reaction(Dictionary<string, string> d, string k) =>
        d.ContainsKey(k) ? Enum(d, k, OctopusReactionKind.Unknown) : OctopusReactionKind.None;

    static OctopusPostContent ContentFlags(Dictionary<string, string> d, string k)
    {
        var result = OctopusPostContent.None;
        foreach (var tok in Csv(d, k))
            if (System.Enum.TryParse<OctopusPostContent>(tok.Trim(), true, out var flag)) result |= flag;
        return result;
    }
}
