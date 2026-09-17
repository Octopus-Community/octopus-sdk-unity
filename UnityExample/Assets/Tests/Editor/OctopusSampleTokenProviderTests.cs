using System;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;

public class OctopusSampleTokenProviderTests
{
    // Deliberately fake fixture data; the bridge golden below uses only this synthetic secret.
    private const string FixtureSecret = "unit-test-only-secret";

    [Test]
    public void BridgeSignatureMatchesIndependentPythonGoldenAndContainsOnlyFingerprintAndExpiry()
    {
        var now = DateTimeOffset.FromUnixTimeSeconds(1700000000);
        var provider = new OctopusSampleTokenProvider(new OctopusExampleConfig.ExampleProfile(), () => now, FixtureSecret);
        var token = provider.GetBridgeSignature("abc123-fingerprint");
        // Derived with Python base64.urlsafe_b64encode + hmac.new(..., hashlib.sha256),
        // using UTF-8 literal JSON, FixtureSecret and exp=1700003600 (now + 3600).
        const string header = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9";
        const string payload = "eyJicmlkZ2VfZmluZ2VycHJpbnQiOiJhYmMxMjMtZmluZ2VycHJpbnQiLCJleHAiOjE3MDAwMDM2MDB9";
        const string signature = "RMAqT38gQJ0d7d2qiSbfqeaNnRpHRh66NfMhSxtG1xU";
        Assert.AreEqual(header + "." + payload + "." + signature, token);
        var parts = token.Split('.');
        Assert.AreEqual(3, parts.Length);
        Assert.AreEqual(header, parts[0]);
        Assert.AreEqual(payload, parts[1]);
        Assert.AreEqual(signature, parts[2]);
        Assert.AreEqual("{\"alg\":\"HS256\",\"typ\":\"JWT\"}", Decode(parts[0]));
        // Exact JSON equality pins the key set/order and excludes iat or any SSO claims.
        Assert.AreEqual("{\"bridge_fingerprint\":\"abc123-fingerprint\",\"exp\":1700003600}", Decode(parts[1]));
        foreach (var part in parts) StringAssert.IsMatch("^[A-Za-z0-9_-]+$", part);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase(" \t\n")]
    public void BridgeSignatureWithoutSecretRefusesEvenWithStaticAuthToken(string secret)
    {
        var profile = new OctopusExampleConfig.ExampleProfile { authToken = "not-a-real-token" };
        var provider = new OctopusSampleTokenProvider(profile,
            () => { throw new Exception("Clock must not run without a secret"); }, secret);
        string token = null;
        var error = Assert.Throws<InvalidOperationException>(() => token = provider.GetBridgeSignature("fingerprint"));
        Assert.IsNull(token);
        StringAssert.Contains("fill ssoTokenSecret", error.Message);
        StringAssert.Contains("OctopusScenarioSdk.BridgeShareSigner", error.Message);
        StringAssert.Contains("static authToken cannot sign", error.Message);
    }

    [Test]
    public void BridgeSignatureWithoutProfileRefusesReadably()
    {
        var error = Assert.Throws<InvalidOperationException>(() =>
            new OctopusSampleTokenProvider(null).GetBridgeSignature("fingerprint"));
        StringAssert.Contains("OctopusExampleConfig", error.Message);
        StringAssert.Contains("No bridge token was generated", error.Message);
    }

    [Test]
    public void BridgeFingerprintEscapesJsonAndRefreshesExpiryFromTheClock()
    {
        var now = DateTimeOffset.FromUnixTimeSeconds(0);
        var provider = new OctopusSampleTokenProvider(null, () => now, FixtureSecret);
        var first = provider.GetBridgeSignature("a\"\\\n\t\u0001é");
        Assert.AreEqual("{\"bridge_fingerprint\":\"a\\\"\\\\\\n\\t\\u0001é\",\"exp\":3600}", Decode(first.Split('.')[1]));
        now = now.AddSeconds(1);
        var second = provider.GetBridgeSignature("a\"\\\n\t\u0001é");
        Assert.AreNotEqual(first, second);
        StringAssert.Contains("\"exp\":3601", Decode(second.Split('.')[1]));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void MatchesIndependentHs256EncodingAtFixedExpiration(bool withEntitlements)
    {
        var profile = new OctopusExampleConfig.ExampleProfile();
        var now = DateTimeOffset.FromUnixTimeSeconds(1700000000);
        var provider = new OctopusSampleTokenProvider(profile, () => now, FixtureSecret);
        var claims = withEntitlements ? new[] { "customer:premium", "customer:moderator" } : new string[0];
        var parts = provider.GetToken("fixture-user", claims).Split('.');
        Assert.AreEqual(3, parts.Length);
        var header = "{\"alg\":\"HS256\",\"typ\":\"JWT\"}";
        var payload = "{\"sub\":\"fixture-user\",\"exp\":1731536000" +
            (withEntitlements ? ",\"entitlements\":[\"customer:premium\",\"customer:moderator\"]" : "") + "}";
        Assert.AreEqual(header, Decode(parts[0]));
        Assert.AreEqual(payload, Decode(parts[1]));
        foreach (var part in parts) StringAssert.IsMatch("^[A-Za-z0-9_-]+$", part);
        // Expected signing input is built from literal JSON, independently of provider output.
        var input = Base64Url(header) + "." + Base64Url(payload);
        using (var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(FixtureSecret)))
            CollectionAssert.AreEqual(hmac.ComputeHash(Encoding.UTF8.GetBytes(input)), DecodeBytes(parts[2]));
    }

    [Test]
    public void JsonEscapesControlCharactersAndPreservesUnicode()
    {
        var profile = new OctopusExampleConfig.ExampleProfile();
        var token = new OctopusSampleTokenProvider(profile, () => DateTimeOffset.FromUnixTimeSeconds(0), FixtureSecret)
            .GetToken("a\"\\\n\t\u0001é", new[] { "quote\"\n" });
        Assert.AreEqual("{\"sub\":\"a\\\"\\\\\\n\\t\\u0001é\",\"exp\":31536000,\"entitlements\":[\"quote\\\"\\n\"]}",
            Decode(token.Split('.')[1]));
    }

    [Test]
    public void OldProfilesDefaultToEmptySecretAndUseTheirStaticToken()
    {
        var profile = UnityEngine.JsonUtility.FromJson<OctopusExampleConfig.ExampleProfile>("{\"userId\":\"fixture-user\"}");
        profile.authToken = "not-a-real-token";
        var provider = new OctopusSampleTokenProvider(profile, () => { throw new Exception("Clock must not run"); });
        Assert.AreEqual(profile.authToken, provider.GetToken(profile.userId, new[] { "customer:premium" }));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase(" ")]
    public void MissingCredentialsHaveAnActionableRefusal(string emptyCredential)
    {
        var profile = new OctopusExampleConfig.ExampleProfile { authToken = emptyCredential };
        string reason;
        Assert.IsFalse(OctopusSampleTokenProvider.CanConnect(profile, out reason));
        StringAssert.Contains("ssoTokenSecret or authToken", reason);
        var provider = new OctopusSampleTokenProvider(profile, secret: emptyCredential);
        Assert.DoesNotThrow(() => Assert.AreEqual(string.Empty, provider.GetToken("fixture-user")));
    }

    [Test]
    public void RefreshUsesTheInjectedClockAgainAndSecretWinsOverStaticToken()
    {
        var now = DateTimeOffset.FromUnixTimeSeconds(0);
        var profile = new OctopusExampleConfig.ExampleProfile { authToken = "unused-fixture" };
        var provider = new OctopusSampleTokenProvider(profile, () => now, FixtureSecret);
        var first = provider.GetToken("fixture-user");
        now = now.AddSeconds(1);
        Assert.AreNotEqual(first, provider.GetToken("fixture-user"));
        StringAssert.Contains("\"exp\":31536001", Decode(provider.GetToken("fixture-user").Split('.')[1]));
    }

    public static string Decode(string value) { return Encoding.UTF8.GetString(DecodeBytes(value)); }

    private static byte[] DecodeBytes(string value)
    {
        var standard = value.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(standard.PadRight((standard.Length + 3) / 4 * 4, '='));
    }

    private static string Base64Url(string value)
    {
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(value)).Replace("=", "").Replace("/", "_").Replace("+", "-");
    }
}
