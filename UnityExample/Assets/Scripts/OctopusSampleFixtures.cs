using System;
using System.Collections.Generic;

/// <summary>
/// Default content ids in the demo community the sample targets. Seeded demo content can expire:
/// if one no longer resolves, override it with a <c>qaFixture.&lt;name&gt;</c> launch extra.
/// Null means no default target exists yet. No credentials belong here.
/// Synthetic Unity sign-in identities below remain separate from backend profile IDs.
/// </summary>
public static class OctopusSampleFixtures
{
    public const string ExtraPrefix = "qaFixture.";
    public static readonly IReadOnlyList<string> Names = Array.AsReadOnly(new[]
    {
        "post.text", "post.image", "post.poll", "post.cta", "post.reactionStack",
        "comment.onPost", "comment.reported", "user.other", "topic.default", "topic.gated", "deeplink.post"
    });
    private static readonly Dictionary<string, string> _overrides = new Dictionary<string, string>();

    public static string PostTextId { get { return Resolve("post.text", "SfEDTqxLavmbCEcEO6CBRp"); } }
    public static string PostImageId { get { return Resolve("post.image", null); } }
    public static string PostPollId { get { return Resolve("post.poll", null); } }
    public static string PostCtaId { get { return Resolve("post.cta", null); } }
    public static string PostReactionStackId { get { return Resolve("post.reactionStack", "piWm_vjBa43SEOTexuz8ye"); } }
    public static string CommentOnPostId { get { return Resolve("comment.onPost", "v_iqL4I1Scdfv3OfJeINO1"); } }
    public static string CommentReportedId { get { return Resolve("comment.reported", null); } }
    public static string OtherUserId { get { return Resolve("user.other", "FHjwxMQBGmSAD4KJxQk7uP"); } }
    public static string DefaultTopicId { get { return Resolve("topic.default", "PMYFiz0sv6cKEcpMkq5qlT"); } }
    public static string GatedTopicId { get { return Resolve("topic.gated", null); } }
    public static string DeepLinkPost { get { return Resolve("deeplink.post", null); } }

    // Precedence: explicit Customize/Inspector input > launch --es qaFixture.<name>
    // > compiled default. Blank extras keep the default; overrides last for this launch.
    // Example: --es qaScenario initialScreen --es qaPreset 2 --es qaFixture.post.text <post-id>
    public static void ApplyOverrides(IReadOnlyDictionary<string, string> values)
    {
        _overrides.Clear();
        if (values == null) return;
        foreach (var name in Names)
        {
            string value;
            if (values.TryGetValue(ExtraPrefix + name, out value))
                _overrides[name] = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
    }

    private static string Resolve(string name, string fallback)
    {
        string value;
        return _overrides.TryGetValue(name, out value) && !string.IsNullOrWhiteSpace(value) ? value : fallback;
    }

    public const string Premium = "customer:premium";
    public const string Moderator = "customer:moderator";
    public const int ProfileCount = 3;

    // Return fresh values: an Inspector edit or entitlement toggle must not mutate the fixtures.
    public static OctopusExampleConfig.ExampleProfile CreateProfile(int index)
    {
        switch (index)
        {
            case 0:
                return Profile("unity-sample-user", "John Doe", "Exploring the sample community.", new string[0]);
            case 1:
                return Profile("unity-sample-premium", "Sample Premium", "Trying premium community access.", new[] { Premium });
            case 2:
                return Profile("unity-sample-moderator", "Sample Moderator", "Trying community moderation.", new[] { Moderator });
            default:
                throw new ArgumentOutOfRangeException(nameof(index));
        }
    }

    private static OctopusExampleConfig.ExampleProfile Profile(string id, string nickname,
        string bio, string[] entitlements)
    {
        return new OctopusExampleConfig.ExampleProfile
        {
            userId = id,
            nickname = nickname,
            bio = bio,
            picture = "https://i.pravatar.cc/150",
            entitlements = entitlements
        };
    }
}
