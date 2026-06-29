// OctopusEvent.cs — pure data model for Octopus SDK events.
// No parsing, no platform conditionals. Compile-safe in the Unity Editor.

public abstract class OctopusEvent
{
    public OctopusEventKind Kind { get; protected set; }
}

// ---------------------------------------------------------------------------
// Enums
// ---------------------------------------------------------------------------

public enum OctopusEventKind
{
    PostCreated,
    CommentCreated,
    ReplyCreated,
    ContentDeleted,
    ReactionModified,
    PollVoted,
    ContentReported,
    ProfileReported,
    GroupFollowingChanged,
    GamificationPointsGained,
    GamificationPointsRemoved,
    ScreenDisplayed,
    NotificationClicked,
    PostClicked,
    TranslationButtonClicked,
    CommentButtonClicked,
    ReplyButtonClicked,
    SeeRepliesButtonClicked,
    ProfileModified,
    SessionStarted,
    SessionStopped,
    Unknown
}

public enum OctopusContentKind
{
    Post,
    Comment,
    Reply,
    Unknown
}

public enum OctopusReactionKind
{
    None,
    Heart,
    Joy,
    MouthOpen,
    Clap,
    Cry,
    Rage,
    Unknown
}

[System.Flags]
public enum OctopusPostContent
{
    None  = 0,
    Text  = 1,
    Image = 2,
    Poll  = 4
}

public enum OctopusScreen
{
    MainFeed,
    PostsFeed,
    PostDetail,
    CommentDetail,
    Groups,
    GroupDetail,
    CreatePost,
    Profile,
    OtherUserProfile,
    EditProfile,
    ReportContent,
    ReportProfile,
    ValidateNickname,
    SettingsList,
    SettingsAccount,
    SettingsAbout,
    ReportExplanation,
    DeleteAccount,
    Unknown
}

public enum OctopusGroupDetailSource
{
    ClientApp,
    Community,
    Unknown
}

public enum OctopusPostClickedSource
{
    Feed,
    Profile,
    Unknown
}

public enum OctopusGamificationAction
{
    Reaction,
    Comment,
    Reply,
    Post,
    Vote,
    PostCommented,
    ProfileCompleted,
    DailySession,
    Unknown
}

// ---------------------------------------------------------------------------
// Concrete event classes
// ---------------------------------------------------------------------------

public sealed class PostCreatedEvent : OctopusEvent
{
    public string PostId;
    public string GroupId;
    public int TextLength;
    public OctopusPostContent Content;

    public PostCreatedEvent() { Kind = OctopusEventKind.PostCreated; }
}

public sealed class CommentCreatedEvent : OctopusEvent
{
    public string CommentId;
    public string PostId;
    public int TextLength;

    public CommentCreatedEvent() { Kind = OctopusEventKind.CommentCreated; }
}

public sealed class ReplyCreatedEvent : OctopusEvent
{
    public string ReplyId;
    public string CommentId;
    public int TextLength;

    public ReplyCreatedEvent() { Kind = OctopusEventKind.ReplyCreated; }
}

public sealed class ContentDeletedEvent : OctopusEvent
{
    public string ContentId;
    public OctopusContentKind ContentKind;
    public string ParentId;

    public ContentDeletedEvent() { Kind = OctopusEventKind.ContentDeleted; }
}

public sealed class ReactionModifiedEvent : OctopusEvent
{
    public string ContentId;
    public OctopusContentKind ContentKind;
    public OctopusReactionKind PreviousReaction;
    public OctopusReactionKind NewReaction;

    public ReactionModifiedEvent() { Kind = OctopusEventKind.ReactionModified; }
}

public sealed class PollVotedEvent : OctopusEvent
{
    public string ContentId;
    public string OptionId;

    public PollVotedEvent() { Kind = OctopusEventKind.PollVoted; }
}

public sealed class ContentReportedEvent : OctopusEvent
{
    public string ContentId;
    public OctopusContentKind ContentKind;
    public string[] Reasons;

    public ContentReportedEvent() { Kind = OctopusEventKind.ContentReported; }
}

public sealed class ProfileReportedEvent : OctopusEvent
{
    public string ProfileId;
    public string[] Reasons;

    public ProfileReportedEvent() { Kind = OctopusEventKind.ProfileReported; }
}

public sealed class GroupFollowingChangedEvent : OctopusEvent
{
    public string GroupId;
    public bool Followed;

    public GroupFollowingChangedEvent() { Kind = OctopusEventKind.GroupFollowingChanged; }
}

public sealed class GamificationPointsGainedEvent : OctopusEvent
{
    public int Points;
    public OctopusGamificationAction Action;

    public GamificationPointsGainedEvent() { Kind = OctopusEventKind.GamificationPointsGained; }
}

public sealed class GamificationPointsRemovedEvent : OctopusEvent
{
    public int Points;
    public OctopusGamificationAction Action;

    public GamificationPointsRemovedEvent() { Kind = OctopusEventKind.GamificationPointsRemoved; }
}

public sealed class ScreenDisplayedEvent : OctopusEvent
{
    public OctopusScreen Screen;
    public string FeedId;
    public string PostId;
    public string CommentId;
    public string GroupId;
    public string ProfileId;
    public OctopusGroupDetailSource Source;

    public ScreenDisplayedEvent() { Kind = OctopusEventKind.ScreenDisplayed; }
}

public sealed class NotificationClickedEvent : OctopusEvent
{
    public string NotificationId;
    public string ContentId;

    public NotificationClickedEvent() { Kind = OctopusEventKind.NotificationClicked; }
}

public sealed class PostClickedEvent : OctopusEvent
{
    public string PostId;
    public OctopusPostClickedSource Source;

    public PostClickedEvent() { Kind = OctopusEventKind.PostClicked; }
}

public sealed class TranslationButtonClickedEvent : OctopusEvent
{
    public string ContentId;
    public bool ViewTranslated;
    public OctopusContentKind ContentKind;

    public TranslationButtonClickedEvent() { Kind = OctopusEventKind.TranslationButtonClicked; }
}

public sealed class CommentButtonClickedEvent : OctopusEvent
{
    public string PostId;

    public CommentButtonClickedEvent() { Kind = OctopusEventKind.CommentButtonClicked; }
}

public sealed class ReplyButtonClickedEvent : OctopusEvent
{
    public string CommentId;

    public ReplyButtonClickedEvent() { Kind = OctopusEventKind.ReplyButtonClicked; }
}

public sealed class SeeRepliesButtonClickedEvent : OctopusEvent
{
    public string CommentId;

    public SeeRepliesButtonClickedEvent() { Kind = OctopusEventKind.SeeRepliesButtonClicked; }
}

public sealed class ProfileModifiedEvent : OctopusEvent
{
    public bool NicknameChanged;
    public bool BioChanged;
    public bool PictureChanged;
    public int BioLength;
    public bool HasPicture;

    public ProfileModifiedEvent() { Kind = OctopusEventKind.ProfileModified; }
}

public sealed class SessionStartedEvent : OctopusEvent
{
    public string SessionId;

    public SessionStartedEvent() { Kind = OctopusEventKind.SessionStarted; }
}

public sealed class SessionStoppedEvent : OctopusEvent
{
    public string SessionId;

    public SessionStoppedEvent() { Kind = OctopusEventKind.SessionStopped; }
}

public sealed class UnknownEvent : OctopusEvent
{
    public string RawType;

    public UnknownEvent() { Kind = OctopusEventKind.Unknown; }
}
