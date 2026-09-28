using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

public partial class OctopusSDK
{
    /// <summary>
    /// Overrides the icons of the native Octopus UI. Every slot left unset keeps the native default,
    /// and passing <c>null</c> (or an empty <see cref="OctopusIcons"/>) restores every default.
    /// Like the other theme setters, it closes an open Octopus UI so the next screen picks it up.
    /// </summary>
    /// <remarks>
    /// Each call replaces the previous set: slots missing from <paramref name="icons"/> go back to
    /// their default. A slot whose resource cannot be found keeps its default and logs a warning.
    /// </remarks>
    /// <param name="icons">The icons to override, or <c>null</c> for the native defaults.</param>
    public static void SetIcons(OctopusIcons icons)
    {
#if UNITY_EDITOR
        Mock.Record("SetIcons", IconsToJson(icons, false), IconsToJson(icons, true));
#elif UNITY_ANDROID
        using (AndroidJavaClass plugin = new AndroidJavaClass("com.octopuscommunity.bridge.Bridge"))
        {
            plugin.CallStatic("setIcons", IconsToJson(icons, false));
        }
#elif UNITY_IOS
        OctopusSdkSetIcons(IconsToJson(icons, true));
#endif
    }

    /// <summary>
    /// Serializes the overrides for one platform as <c>[{"slot":"...","resource":"..."}]</c>,
    /// ordered by slot. Slots without a resource name for that platform are left out.
    /// </summary>
    internal static string IconsToJson(OctopusIcons icons, bool ios)
    {
        var rows = new List<Dictionary<string, string>>();
        if (icons != null)
        {
            foreach (OctopusIconSlot slot in icons.Slots)
            {
                OctopusIcon icon = icons.Get(slot);
                string resource = ios ? icon.IOSResourceName : icon.AndroidDrawableName;
                if (string.IsNullOrEmpty(resource)) continue;
                rows.Add(new Dictionary<string, string>
                {
                    { "slot", IconSlotKey(slot) },
                    { "resource", resource }
                });
            }
        }
        return OctopusJson.WriteArray(rows, new HashSet<string>());
    }

    /// <summary>The bridge key of a slot, shared by the Kotlin bridge and the Swift plugin.</summary>
    internal static string IconSlotKey(OctopusIconSlot slot)
    {
        switch (slot)
        {
            case OctopusIconSlot.GroupsOpenList: return "groups.openList";
            case OctopusIconSlot.GroupsSelected: return "groups.selected";
            case OctopusIconSlot.GroupsViewGroup: return "groups.viewGroup";
            case OctopusIconSlot.ContentPostCreationOpen: return "content.post.creation.open";
            case OctopusIconSlot.ContentPostCreationTopicSelection: return "content.post.creation.topicSelection";
            case OctopusIconSlot.ContentPostCreationAddPicture: return "content.post.creation.addPicture";
            case OctopusIconSlot.ContentPostCreationDeletePicture: return "content.post.creation.deletePicture";
            case OctopusIconSlot.ContentPostCreationAddPoll: return "content.post.creation.addPoll";
            case OctopusIconSlot.ContentPostCreationAddPollOption: return "content.post.creation.addPollOption";
            case OctopusIconSlot.ContentPostCreationDeletePoll: return "content.post.creation.deletePoll";
            case OctopusIconSlot.ContentPostCreationDeletePollOption: return "content.post.creation.deletePollOption";
            case OctopusIconSlot.ContentPostEmptyFeedInGroups: return "content.post.emptyFeedInGroups";
            case OctopusIconSlot.ContentPostEmptyFeedInCurrentUserProfile: return "content.post.emptyFeedInCurrentUserProfile";
            case OctopusIconSlot.ContentPostEmptyFeedInOtherUserProfile: return "content.post.emptyFeedInOtherUserProfile";
            case OctopusIconSlot.ContentPostNotAvailable: return "content.post.notAvailable";
            case OctopusIconSlot.ContentPostCommentCount: return "content.post.commentCount";
            case OctopusIconSlot.ContentPostViewCount: return "content.post.viewCount";
            case OctopusIconSlot.ContentPostMoreReactions: return "content.post.moreReactions";
            case OctopusIconSlot.ContentPostLikeNotSelected: return "content.post.likeNotSelected";
            case OctopusIconSlot.ContentPostModerated: return "content.post.moderated";
            case OctopusIconSlot.ContentCommentCreationOpen: return "content.comment.creation.open";
            case OctopusIconSlot.ContentCommentCreationCreate: return "content.comment.creation.create";
            case OctopusIconSlot.ContentCommentCreationAddPicture: return "content.comment.creation.addPicture";
            case OctopusIconSlot.ContentCommentCreationDeletePicture: return "content.comment.creation.deletePicture";
            case OctopusIconSlot.ContentCommentEmptyFeed: return "content.comment.emptyFeed";
            case OctopusIconSlot.ContentCommentNotAvailable: return "content.comment.notAvailable";
            case OctopusIconSlot.ContentCommentSeeReplies: return "content.comment.seeReplies";
            case OctopusIconSlot.ContentCommentLikeNotSelected: return "content.comment.likeNotSelected";
            case OctopusIconSlot.ContentReplyCreationOpen: return "content.reply.creation.open";
            case OctopusIconSlot.ContentReplyCreationCreate: return "content.reply.creation.create";
            case OctopusIconSlot.ContentReplyCreationAddPicture: return "content.reply.creation.addPicture";
            case OctopusIconSlot.ContentReplyCreationDeletePicture: return "content.reply.creation.deletePicture";
            case OctopusIconSlot.ContentReplyLikeNotSelected: return "content.reply.likeNotSelected";
            case OctopusIconSlot.ContentVideoMuted: return "content.video.muted";
            case OctopusIconSlot.ContentVideoNotMuted: return "content.video.notMuted";
            case OctopusIconSlot.ContentVideoPause: return "content.video.pause";
            case OctopusIconSlot.ContentVideoPlay: return "content.video.play";
            case OctopusIconSlot.ContentVideoReplay: return "content.video.replay";
            case OctopusIconSlot.ContentPollSelectedOption: return "content.poll.selectedOption";
            case OctopusIconSlot.ContentReactionHeart: return "content.reaction.heart";
            case OctopusIconSlot.ContentReactionJoy: return "content.reaction.joy";
            case OctopusIconSlot.ContentReactionMouthOpen: return "content.reaction.mouthOpen";
            case OctopusIconSlot.ContentReactionClap: return "content.reaction.clap";
            case OctopusIconSlot.ContentReactionCry: return "content.reaction.cry";
            case OctopusIconSlot.ContentReactionRage: return "content.reaction.rage";
            case OctopusIconSlot.ContentDelete: return "content.delete";
            case OctopusIconSlot.ContentReport: return "content.report";
            case OctopusIconSlot.ContentViews: return "content.views";
            case OctopusIconSlot.ContentLike: return "content.like";
            case OctopusIconSlot.ProfileDefaultAvatar: return "profile.defaultAvatar";
            case OctopusIconSlot.ProfileAddPicture: return "profile.addPicture";
            case OctopusIconSlot.ProfileEditPicture: return "profile.editPicture";
            case OctopusIconSlot.ProfileAddBio: return "profile.addBio";
            case OctopusIconSlot.ProfileEmptyNotifications: return "profile.emptyNotifications";
            case OctopusIconSlot.ProfileReport: return "profile.report";
            case OctopusIconSlot.ProfileNotConnected: return "profile.notConnected";
            case OctopusIconSlot.ProfileBlockUser: return "profile.blockUser";
            case OctopusIconSlot.GamificationBadge: return "gamification.badge";
            case OctopusIconSlot.GamificationInfo: return "gamification.info";
            case OctopusIconSlot.GamificationRulesHeader: return "gamification.rulesHeader";
            case OctopusIconSlot.SettingsAccount: return "settings.account";
            case OctopusIconSlot.SettingsHelp: return "settings.help";
            case OctopusIconSlot.SettingsInfo: return "settings.info";
            case OctopusIconSlot.SettingsLogout: return "settings.logout";
            case OctopusIconSlot.SettingsDeleteAccountWarning: return "settings.deleteAccountWarning";
            case OctopusIconSlot.CommonRadioOn: return "common.radio.on";
            case OctopusIconSlot.CommonRadioOff: return "common.radio.off";
            case OctopusIconSlot.CommonCheckboxOn: return "common.checkbox.on";
            case OctopusIconSlot.CommonCheckboxOff: return "common.checkbox.off";
            case OctopusIconSlot.CommonToggleOn: return "common.toggle.on";
            case OctopusIconSlot.CommonToggleOff: return "common.toggle.off";
            case OctopusIconSlot.CommonMoreActions: return "common.moreActions";
            case OctopusIconSlot.CommonActivityButton: return "common.activityButton";
            case OctopusIconSlot.CommonListCellNavIndicator: return "common.listCellNavIndicator";
            case OctopusIconSlot.CommonClose: return "common.close";
            default: return null;
        }
    }

#if UNITY_IOS && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void OctopusSdkSetIcons(string json);
#endif
}

/// <summary>
/// One icon override: the image to use on each platform, resolved like <see cref="OctopusLogo"/>.
/// </summary>
public sealed class OctopusIcon
{
    /// <summary>
    /// Name of an Android drawable resource (for example <c>my_icon</c> for
    /// <c>res/drawable/my_icon.xml</c> or <c>.png</c>), shipped by the app or an Android library.
    /// Null or empty keeps the native default on Android.
    /// </summary>
    public readonly string AndroidDrawableName;

    /// <summary>
    /// iOS image: a path inside the app bundle when it contains a <c>/</c> (for example
    /// <c>Data/Raw/my_icon.png</c> for <c>Assets/StreamingAssets/my_icon.png</c>), otherwise the name
    /// of an image in the app's asset catalog. Null or empty keeps the native default on iOS.
    /// iOS renders these icons as templates tinted by the theme colors.
    /// </summary>
    public readonly string IOSResourceName;

    /// <summary>Creates an icon override.</summary>
    /// <param name="androidDrawableName">Android drawable resource name, or null.</param>
    /// <param name="iOSResourceName">iOS bundle path or asset name, or null.</param>
    public OctopusIcon(string androidDrawableName, string iOSResourceName)
    {
        AndroidDrawableName = androidDrawableName;
        IOSResourceName = iOSResourceName;
    }
}

/// <summary>
/// A set of icon overrides for <see cref="OctopusSDK.SetIcons"/>, one <see cref="OctopusIcon"/> per
/// <see cref="OctopusIconSlot"/>. Unset slots keep the native default.
/// </summary>
public sealed class OctopusIcons
{
    private readonly SortedDictionary<OctopusIconSlot, OctopusIcon> _icons =
        new SortedDictionary<OctopusIconSlot, OctopusIcon>();

    /// <summary>
    /// Overrides <paramref name="slot"/> with <paramref name="icon"/>; a null icon clears the slot.
    /// Returns this instance, so calls can be chained.
    /// </summary>
    /// <param name="slot">The icon to override.</param>
    /// <param name="icon">The replacement image, or null to keep the native default.</param>
    public OctopusIcons Set(OctopusIconSlot slot, OctopusIcon icon)
    {
        if (!Enum.IsDefined(typeof(OctopusIconSlot), slot))
        {
            throw new ArgumentOutOfRangeException(nameof(slot), slot, "Unknown icon slot.");
        }
        if (icon == null) _icons.Remove(slot);
        else _icons[slot] = icon;
        return this;
    }

    /// <summary>The override set for <paramref name="slot"/>, or null when it keeps the default.</summary>
    /// <param name="slot">The icon slot.</param>
    public OctopusIcon Get(OctopusIconSlot slot)
    {
        OctopusIcon icon;
        return _icons.TryGetValue(slot, out icon) ? icon : null;
    }

    /// <summary>Clears the override of <paramref name="slot"/>. Returns true if one was set.</summary>
    /// <param name="slot">The icon slot.</param>
    public bool Remove(OctopusIconSlot slot)
    {
        return _icons.Remove(slot);
    }

    /// <summary>The number of overridden slots.</summary>
    public int Count
    {
        get { return _icons.Count; }
    }

    /// <summary>The overridden slots, in ascending order.</summary>
    public IEnumerable<OctopusIconSlot> Slots
    {
        get { return _icons.Keys; }
    }
}

/// <summary>
/// The overridable icons of the native Octopus UI, named after the native theme groups (iOS
/// <c>OctopusTheme.Assets.Icons</c>, Android <c>OctopusIcons</c>) of the pinned native SDKs.
/// Values are stable: new slots are only ever appended.
/// </summary>
/// <remarks>
/// A few slots exist on one native SDK only and are ignored on the other; their documentation says so.
/// The <c>Common*On</c> / <c>Common*Off</c> pairs replace the native control only when both halves
/// are set for the platform.
/// </remarks>
public enum OctopusIconSlot
{
    /// <summary>Button that opens the list of groups.</summary>
    GroupsOpenList = 0,
    /// <summary>Check mark on the selected group.</summary>
    GroupsSelected = 1,
    /// <summary>Button that opens a group.</summary>
    GroupsViewGroup = 2,
    /// <summary>Button that opens the post creation screen.</summary>
    ContentPostCreationOpen = 3,
    /// <summary>Topic (group) selection in the post creation screen.</summary>
    ContentPostCreationTopicSelection = 4,
    /// <summary>Button that attaches a picture to a post.</summary>
    ContentPostCreationAddPicture = 5,
    /// <summary>Button that removes the picture attached to a post.</summary>
    ContentPostCreationDeletePicture = 6,
    /// <summary>Button that adds a poll to a post.</summary>
    ContentPostCreationAddPoll = 7,
    /// <summary>Button that adds an option to a poll.</summary>
    ContentPostCreationAddPollOption = 8,
    /// <summary>Button that removes the poll from a post.</summary>
    ContentPostCreationDeletePoll = 9,
    /// <summary>Button that removes one poll option.</summary>
    ContentPostCreationDeletePollOption = 10,
    /// <summary>Illustration of an empty group feed.</summary>
    ContentPostEmptyFeedInGroups = 11,
    /// <summary>Illustration of an empty feed on the connected user's profile.</summary>
    ContentPostEmptyFeedInCurrentUserProfile = 12,
    /// <summary>Illustration of an empty feed on another member's profile.</summary>
    ContentPostEmptyFeedInOtherUserProfile = 13,
    /// <summary>Illustration of a post that is no longer available.</summary>
    ContentPostNotAvailable = 14,
    /// <summary>Comment count indicator on a post.</summary>
    ContentPostCommentCount = 15,
    /// <summary>View count indicator on a post.</summary>
    ContentPostViewCount = 16,
    /// <summary>Button that opens the full reaction picker on a post.</summary>
    ContentPostMoreReactions = 17,
    /// <summary>Like button on a post, not selected.</summary>
    ContentPostLikeNotSelected = 18,
    /// <summary>Illustration of a moderated post.</summary>
    ContentPostModerated = 19,
    /// <summary>Button that opens the comment creation.</summary>
    ContentCommentCreationOpen = 20,
    /// <summary>Button that sends a comment.</summary>
    ContentCommentCreationCreate = 21,
    /// <summary>Button that attaches a picture to a comment.</summary>
    ContentCommentCreationAddPicture = 22,
    /// <summary>Button that removes the picture attached to a comment.</summary>
    ContentCommentCreationDeletePicture = 23,
    /// <summary>Illustration of a post without comments.</summary>
    ContentCommentEmptyFeed = 24,
    /// <summary>Illustration of a comment that is no longer available.</summary>
    ContentCommentNotAvailable = 25,
    /// <summary>Button that shows the replies to a comment.</summary>
    ContentCommentSeeReplies = 26,
    /// <summary>Like button on a comment, not selected.</summary>
    ContentCommentLikeNotSelected = 27,
    /// <summary>Button that opens the reply creation.</summary>
    ContentReplyCreationOpen = 28,
    /// <summary>Button that sends a reply.</summary>
    ContentReplyCreationCreate = 29,
    /// <summary>Button that attaches a picture to a reply.</summary>
    ContentReplyCreationAddPicture = 30,
    /// <summary>Button that removes the picture attached to a reply.</summary>
    ContentReplyCreationDeletePicture = 31,
    /// <summary>Like button on a reply, not selected.</summary>
    ContentReplyLikeNotSelected = 32,
    /// <summary>Video sound control, muted state.</summary>
    ContentVideoMuted = 33,
    /// <summary>Video sound control, sound on.</summary>
    ContentVideoNotMuted = 34,
    /// <summary>Video pause button.</summary>
    ContentVideoPause = 35,
    /// <summary>Video play button.</summary>
    ContentVideoPlay = 36,
    /// <summary>Video replay button.</summary>
    ContentVideoReplay = 37,
    /// <summary>Check mark on the poll option the user voted for.</summary>
    ContentPollSelectedOption = 38,
    /// <summary>Heart reaction.</summary>
    ContentReactionHeart = 39,
    /// <summary>Joy reaction.</summary>
    ContentReactionJoy = 40,
    /// <summary>Mouth-open reaction.</summary>
    ContentReactionMouthOpen = 41,
    /// <summary>Clap reaction.</summary>
    ContentReactionClap = 42,
    /// <summary>Cry reaction.</summary>
    ContentReactionCry = 43,
    /// <summary>Rage reaction.</summary>
    ContentReactionRage = 44,
    /// <summary>Delete action on a post, comment or reply.</summary>
    ContentDelete = 45,
    /// <summary>Report action on a post, comment or reply.</summary>
    ContentReport = 46,
    /// <summary>Generic view count indicator. Android only: ignored on iOS.</summary>
    ContentViews = 47,
    /// <summary>Generic like indicator. Android only: ignored on iOS.</summary>
    ContentLike = 48,
    /// <summary>Avatar shown when a member has no picture. Android only: ignored on iOS.</summary>
    ProfileDefaultAvatar = 49,
    /// <summary>Button that adds a profile picture.</summary>
    ProfileAddPicture = 50,
    /// <summary>Button that changes the profile picture.</summary>
    ProfileEditPicture = 51,
    /// <summary>Button that adds a bio.</summary>
    ProfileAddBio = 52,
    /// <summary>Illustration of an empty notification list.</summary>
    ProfileEmptyNotifications = 53,
    /// <summary>Report action in a profile menu.</summary>
    ProfileReport = 54,
    /// <summary>Illustration shown when the user is not connected.</summary>
    ProfileNotConnected = 55,
    /// <summary>Block action in a profile menu.</summary>
    ProfileBlockUser = 56,
    /// <summary>Gamification badge.</summary>
    GamificationBadge = 57,
    /// <summary>Button that opens the gamification rules.</summary>
    GamificationInfo = 58,
    /// <summary>Header illustration of the gamification rules.</summary>
    GamificationRulesHeader = 59,
    /// <summary>Account entry in the settings.</summary>
    SettingsAccount = 60,
    /// <summary>Help entry in the settings.</summary>
    SettingsHelp = 61,
    /// <summary>Information entry in the settings.</summary>
    SettingsInfo = 62,
    /// <summary>Logout entry in the settings.</summary>
    SettingsLogout = 63,
    /// <summary>Warning illustration on the account deletion screen.</summary>
    SettingsDeleteAccountWarning = 64,
    /// <summary>Radio button, selected. Applied only together with CommonRadioOff.</summary>
    CommonRadioOn = 65,
    /// <summary>Radio button, not selected. Applied only together with CommonRadioOn.</summary>
    CommonRadioOff = 66,
    /// <summary>Checkbox, checked. Applied only together with CommonCheckboxOff.</summary>
    CommonCheckboxOn = 67,
    /// <summary>Checkbox, unchecked. Applied only together with CommonCheckboxOn.</summary>
    CommonCheckboxOff = 68,
    /// <summary>Toggle, on. Applied only together with CommonToggleOff.</summary>
    CommonToggleOn = 69,
    /// <summary>Toggle, off. Applied only together with CommonToggleOn.</summary>
    CommonToggleOff = 70,
    /// <summary>Button that opens the more-actions menu.</summary>
    CommonMoreActions = 71,
    /// <summary>Button that opens the activity screen.</summary>
    CommonActivityButton = 72,
    /// <summary>Navigation indicator (arrow) at the end of a list cell.</summary>
    CommonListCellNavIndicator = 73,
    /// <summary>Close button. iOS only: ignored on Android.</summary>
    CommonClose = 74,
}
