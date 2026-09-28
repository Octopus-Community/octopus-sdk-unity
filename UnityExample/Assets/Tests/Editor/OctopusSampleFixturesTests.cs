using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class OctopusSampleFixturesTests
{
    [Test]
    public void LaunchOverridesCanSupplyMissingTargetsKeepDefaultsAndReset()
    {
        Assert.IsNull(OctopusSampleFixtures.PostImageId);
        Assert.IsNull(OctopusSampleFixtures.PostPollId);
        Assert.IsNull(OctopusSampleFixtures.PostCtaId);
        Assert.IsNull(OctopusSampleFixtures.CommentReportedId);
        Assert.IsNull(OctopusSampleFixtures.GatedTopicId);
        Assert.IsNull(OctopusSampleFixtures.DeepLinkPost);
        var defaults = OctopusSampleFixtures.PostTextId;
        try
        {
            var options = OctopusSampleQaLaunchOptions.Parse(new System.Collections.Generic.Dictionary<string, string>
            {
                { "qaFixture.post.text", " " }, { "qaFixture.post.image", " image-target " },
                { "qaFixture.comment.onPost", "comment-target" }, { "qaFixture.user.other", "profile-target" }
            });
            Assert.IsNull(options.Error);
            new OctopusSampleQaRequest(options);
            Assert.AreEqual(defaults, OctopusSampleFixtures.PostTextId);
            Assert.AreEqual("image-target", OctopusSampleFixtures.PostImageId);
            Assert.AreEqual("comment-target", OctopusSampleFixtures.CommentOnPostId);
            Assert.AreEqual("profile-target", OctopusSampleFixtures.OtherUserId);
            new OctopusSampleQaRequest(OctopusSampleQaLaunchOptions.Parse(null));
            Assert.AreEqual(defaults, OctopusSampleFixtures.PostTextId);
            Assert.IsNull(OctopusSampleFixtures.PostImageId);
        }
        finally { OctopusSampleFixtures.ApplyOverrides(null); }
    }

    [TestCase(false, null, TestName = "MissingExtraKeepsCompiledDefaults")]
    [TestCase(true, "", TestName = "BlankExtraKeepsCompiledDefaultsIncludingNullTargets")]
    [TestCase(true, " \t\r\n ", TestName = "WhitespaceExtraKeepsCompiledDefaultsIncludingNullTargets")]
    [TestCase(true, null, TestName = "NullExtraKeepsCompiledDefaultsIncludingNullTargets")]
    [TestCase(true, " replacement ", TestName = "NonBlankExtraOverridesEveryCompiledDefault")]
    public void LaunchExtraResolutionAppliesToEveryFixture(bool includeExtra, string value)
    {
        OctopusSampleFixtures.ApplyOverrides(null);
        var fixtures = new System.Collections.Generic.Dictionary<string, System.Func<string>>
        {
            { "post.text", () => OctopusSampleFixtures.PostTextId },
            { "post.image", () => OctopusSampleFixtures.PostImageId },
            { "post.poll", () => OctopusSampleFixtures.PostPollId },
            { "post.cta", () => OctopusSampleFixtures.PostCtaId },
            { "post.reactionStack", () => OctopusSampleFixtures.PostReactionStackId },
            { "comment.onPost", () => OctopusSampleFixtures.CommentOnPostId },
            { "comment.reported", () => OctopusSampleFixtures.CommentReportedId },
            { "user.other", () => OctopusSampleFixtures.OtherUserId },
            { "topic.default", () => OctopusSampleFixtures.DefaultTopicId },
            { "topic.gated", () => OctopusSampleFixtures.GatedTopicId },
            { "deeplink.post", () => OctopusSampleFixtures.DeepLinkPost }
        };
        CollectionAssert.AreEquivalent(OctopusSampleFixtures.Names, fixtures.Keys);
        Assert.IsNotNull(OctopusSampleFixtures.PostTextId);
        Assert.IsNull(OctopusSampleFixtures.PostImageId);
        try
        {
            foreach (var fixture in fixtures)
            {
                OctopusSampleFixtures.ApplyOverrides(null);
                var fallback = fixture.Value();
                var extras = new System.Collections.Generic.Dictionary<string, string>();
                if (includeExtra) extras[OctopusSampleFixtures.ExtraPrefix + fixture.Key] = value;
                var options = OctopusSampleQaLaunchOptions.Parse(extras);
                Assert.IsNull(options.Error);
                new OctopusSampleQaRequest(options);
                var expected = includeExtra && !string.IsNullOrWhiteSpace(value) ? value.Trim() : fallback;
                Assert.AreEqual(expected, fixture.Value(), fixture.Key);
            }
        }
        finally { OctopusSampleFixtures.ApplyOverrides(null); }
    }

    [Test]
    public void InvalidLaunchDoesNotApplyItsFixtures()
    {
        var original = OctopusSampleFixtures.PostTextId;
        var options = OctopusSampleQaLaunchOptions.Parse(new System.Collections.Generic.Dictionary<string, string>
        {
            { "qaFixture.post.text", "replacement" }, { "qaTab", "invalid" }
        });
        new OctopusSampleQaRequest(options);
        Assert.IsNotNull(options.Error);
        Assert.AreEqual(original, OctopusSampleFixtures.PostTextId);
        options = OctopusSampleQaLaunchOptions.Parse(new System.Collections.Generic.Dictionary<string, string>
        {
            { "qaFixture.post.text", "TBD" }
        });
        Assert.IsNotNull(options.Error);
    }

    [Test]
    public void ProfilesHaveStableIdsAndCompleteSyntheticIdentities()
    {
        Assert.AreEqual(3, OctopusSampleFixtures.ProfileCount);
        var ids = new[] { "unity-sample-user", "unity-sample-premium", "unity-sample-moderator" };
        var entitlements = new[] { new string[0], new[] { "customer:premium" }, new[] { "customer:moderator" } };
        for (var i = 0; i < ids.Length; i++)
        {
            var profile = OctopusSampleFixtures.CreateProfile(i);
            Assert.AreEqual(ids[i], profile.userId);
            Assert.IsNotEmpty(profile.nickname);
            Assert.IsNotEmpty(profile.bio);
            Assert.AreEqual("https://i.pravatar.cc/150", profile.picture);
            CollectionAssert.AreEqual(entitlements[i], profile.entitlements);
            Assert.IsNull(profile.apiKey);
            Assert.IsNull(profile.authToken);
        }
    }

    [Test]
    public void SingleKeyAndGlobalSecretResolveAllProfilesWithoutChangingSerializedValues()
    {
        var config = ScriptableObject.CreateInstance<OctopusExampleConfig>();
        try
        {
            var original = new OctopusExampleConfig.ExampleProfile { apiKey = "test-key" };
            SetDefault(config, original);
            config.ssoTokenSecret = "unit-test-only-secret";
            var profiles = new[] { config.Default, config.ForcedLogin, config.ManagedFields };
            for (var i = 0; i < profiles.Length; i++)
            {
                Assert.AreEqual("test-key", profiles[i].apiKey);
                Assert.AreEqual(OctopusSampleFixtures.CreateProfile(i).userId, profiles[i].userId);
                var token = new OctopusSampleTokenProvider(profiles[i]).GetToken(profiles[i].userId);
                StringAssert.Contains(profiles[i].userId, OctopusSampleTokenProviderTests.Decode(token.Split('.')[1]));
            }
            Assert.IsNull(original.userId);
            Assert.IsNull(original.nickname);
            StringAssert.DoesNotContain("signingSecret", JsonUtility.ToJson(config));
        }
        finally { Object.DestroyImmediate(config); }
    }

    [Test]
    public void ExistingAssetWithoutSecretKeepsItsIdentityAndStaticToken()
    {
        var config = ScriptableObject.CreateInstance<OctopusExampleConfig>();
        try
        {
            JsonUtility.FromJsonOverwrite("{\"defaultProfile\":{\"apiKey\":\"test-key\",\"userId\":\"existing-user\",\"authToken\":\"not-a-real-token\",\"nickname\":\"Existing\"}}", config);
            Assert.IsTrue(string.IsNullOrEmpty(config.ssoTokenSecret));
            Assert.AreEqual("existing-user", config.Default.userId);
            Assert.AreEqual("Existing", config.Default.nickname);
            Assert.AreEqual("not-a-real-token", new OctopusSampleTokenProvider(config.Default).GetToken(config.Default.userId));
        }
        finally { Object.DestroyImmediate(config); }
    }

    [Test]
    public void FixtureInstancesDoNotShareMutableEntitlements()
    {
        var first = OctopusSampleFixtures.CreateProfile(1);
        first.entitlements[0] = "changed";
        Assert.AreEqual("customer:premium", OctopusSampleFixtures.CreateProfile(1).entitlements[0]);
    }

    private static void SetDefault(OctopusExampleConfig config, OctopusExampleConfig.ExampleProfile profile)
    {
        typeof(OctopusExampleConfig).GetField("defaultProfile", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(config, profile);
    }
}
