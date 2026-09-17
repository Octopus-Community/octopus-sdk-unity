using System;

// Public, synthetic sample data only. Unity-specific ids avoid the shared cross-platform QA
// identities. RN's sample supplies John Doe/pravatar; other names and bios are placeholders.
public static class OctopusSampleFixtures
{
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
