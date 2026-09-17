using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class OctopusSampleFixturesTests
{
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
