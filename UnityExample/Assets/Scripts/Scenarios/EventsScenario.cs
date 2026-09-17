using System.Collections.Generic;
using System.Text.RegularExpressions;

public sealed class EventsScenario : OctopusScenarioPilot
{
    private readonly OctopusScenarioFields _fields = new OctopusScenarioFields(
        new OctopusScenarioField("action", "Action", true));
    private readonly List<OctopusScenarioPreset> _presets;

    public EventsScenario() : base("events")
    {
        _presets = new List<OctopusScenarioPreset>
        {
            new OctopusScenarioPreset(PresetTestId(1), PresetLabel(1, "Clear log"),
                fields => fields.Set("action", "clear"), fields =>
                {
                    var count = OctopusScenarioSdk.EventLines.Count;
                    OctopusScenarioSdk.ClearEvents();
                    Report(count == 0 ? "Log was already empty." :
                        "Cleared " + count + " event" + (count == 1 ? "" : "s") + " from the local log.");
                })
        };
        OctopusScenarioSdk.Observe(this);
        if (OctopusScenarioSdk.EventLines.Count > 0) ObservationsChanged();
    }

    public override OctopusScenarioFields Fields { get { return _fields; } }
    public override IReadOnlyList<OctopusScenarioPreset> Presets { get { return _presets; } }
    public override IReadOnlyList<string> ApiSymbols { get { return new[] { "OnOctopusEvent" }; } }
    public override string ParameterNotice
    {
        get { return "Events are recorded from the first scenario SDK initialization, newest first (up to 50). On Android the log fills when you return from the Octopus UI: events are marshalled to the main thread, which Unity does not run while a native Activity is in front. Clear log makes no SDK call."; }
    }

    internal override void ObservationsChanged()
    {
        Report(OctopusScenarioSdk.EventLines.Count == 0 ? "No events yet." :
            string.Join("\n\n", OctopusScenarioSdk.EventLines));
    }

    internal static string Describe(OctopusEvent value)
    {
        var name = Words(value.Kind.ToString());
        var screen = value as ScreenDisplayedEvent;
        if (screen != null)
        {
            name = Words(screen.Screen.ToString()) + " Screen Displayed";
            if (screen.Screen == OctopusScreen.OtherUserProfile) name = "Other Profile Screen Displayed";
            else if (screen.Screen == OctopusScreen.SettingsAccount) name = "Account Settings Screen Displayed";
            else if (screen.Screen == OctopusScreen.ReportProfile) name = "Report Profile Displayed";
        }
        var deleted = value as ContentDeletedEvent;
        if (deleted != null) name = deleted.ContentKind + " Deleted";
        if (value is NotificationClickedEvent) name = "Internal Notification Clicked";
        // Explicit typed fields survive IL2CPP stripping and never expose arbitrary future data.
        var rows = new List<string>();
        if (value is PostCreatedEvent)
        {
            var e = (PostCreatedEvent)value;
            rows.Add("Id: " + e.PostId);
            rows.Add("Topic: " + e.GroupId);
            rows.Add("Text length: " + e.TextLength);
            rows.Add("Content: " + e.Content);
        }
        else if (value is CommentCreatedEvent)
        {
            var e = (CommentCreatedEvent)value;
            rows.Add("Id: " + e.CommentId);
            rows.Add("Post: " + e.PostId);
            rows.Add("Text length: " + e.TextLength);
        }
        else if (value is ReplyCreatedEvent)
        {
            var e = (ReplyCreatedEvent)value;
            rows.Add("Id: " + e.ReplyId);
            rows.Add("Comment: " + e.CommentId);
            rows.Add("Text length: " + e.TextLength);
        }
        else if (deleted != null)
        {
            rows.Add("Content Id: " + deleted.ContentId);
            rows.Add("Content Kind: " + deleted.ContentKind);
        }
        else if (value is ReactionModifiedEvent)
        {
            var e = (ReactionModifiedEvent)value;
            rows.Add("Content Id: " + e.ContentId);
            rows.Add("Previous Reaction: " + e.PreviousReaction);
            rows.Add("New Reaction: " + e.NewReaction);
            rows.Add("Content Kind: " + e.ContentKind);
        }
        else if (value is PollVotedEvent)
        {
            var e = (PollVotedEvent)value;
            rows.Add("Content Id: " + e.ContentId);
            rows.Add("Option Id: " + e.OptionId);
        }
        else if (value is ContentReportedEvent)
        {
            var e = (ContentReportedEvent)value;
            rows.Add("Content Id: " + e.ContentId);
            rows.Add("Reasons: " + string.Join(", ", e.Reasons ?? new string[0]));
        }
        else if (value is ProfileReportedEvent)
        {
            var e = (ProfileReportedEvent)value;
            rows.Add("Profile Id: " + e.ProfileId);
            rows.Add("Reasons: " + string.Join(", ", e.Reasons ?? new string[0]));
        }
        else if (value is GroupFollowingChangedEvent)
        {
            var e = (GroupFollowingChangedEvent)value;
            rows.Add("Group Id: " + e.GroupId);
            rows.Add("Followed: " + e.Followed);
        }
        else if (value is GamificationPointsGainedEvent)
        {
            var e = (GamificationPointsGainedEvent)value;
            rows.Add("Points: " + e.Points);
            rows.Add("Action: " + e.Action);
        }
        else if (value is GamificationPointsRemovedEvent)
        {
            var e = (GamificationPointsRemovedEvent)value;
            rows.Add("Points: " + e.Points);
            rows.Add("Action: " + e.Action);
        }
        else if (screen != null)
        {
            rows.Add("Post: " + screen.PostId);
            rows.Add("Group: " + screen.GroupId);
            rows.Add("Profile: " + screen.ProfileId);
            rows.Add("Comment: " + screen.CommentId);
            rows.Add("Feed: " + screen.FeedId);
        }
        else if (value is NotificationClickedEvent)
        {
            var e = (NotificationClickedEvent)value;
            rows.Add("Notification Id: " + e.NotificationId);
            rows.Add("Content Id: " + e.ContentId);
        }
        else if (value is PostClickedEvent)
        {
            var e = (PostClickedEvent)value;
            rows.Add("Post Id: " + e.PostId);
            rows.Add("Source: " + e.Source);
        }
        else if (value is TranslationButtonClickedEvent)
        {
            var e = (TranslationButtonClickedEvent)value;
            rows.Add("Content Id: " + e.ContentId);
            rows.Add("View Translated: " + e.ViewTranslated);
            rows.Add("Content Kind: " + e.ContentKind);
        }
        else if (value is CommentButtonClickedEvent) rows.Add("Post Id: " + ((CommentButtonClickedEvent)value).PostId);
        else if (value is ReplyButtonClickedEvent) rows.Add("Comment Id: " + ((ReplyButtonClickedEvent)value).CommentId);
        else if (value is SeeRepliesButtonClickedEvent) rows.Add("Comment Id: " + ((SeeRepliesButtonClickedEvent)value).CommentId);
        else if (value is ProfileModifiedEvent)
        {
            var e = (ProfileModifiedEvent)value;
            rows.Add("Nickname changed: " + e.NicknameChanged);
            rows.Add("Bio changed: " + e.BioChanged);
            rows.Add("Picture changed: " + e.PictureChanged);
            rows.Add("Bio length: " + e.BioLength);
            rows.Add("Has picture: " + e.HasPicture);
        }
        else if (value is SessionStartedEvent) rows.Add("Session Id: " + ((SessionStartedEvent)value).SessionId);
        else if (value is SessionStoppedEvent) rows.Add("Session Id: " + ((SessionStoppedEvent)value).SessionId);
        else if (value is UnknownEvent) rows.Add("Type: " + ((UnknownEvent)value).RawType);
        return name + (rows.Count == 0 ? "" : "\n" + string.Join("\n", rows));
    }

    private static string Words(string value) { return Regex.Replace(value, "([a-z])([A-Z])", "$1 $2"); }
}
