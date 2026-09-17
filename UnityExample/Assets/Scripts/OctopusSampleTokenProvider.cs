using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

// Demo only. Production apps fetch SSO and bridge tokens from a backend, never embed its signing secret.
public sealed class OctopusSampleTokenProvider
{
    private readonly OctopusExampleConfig.ExampleProfile _profile;
    private readonly string _secret;
    private readonly Func<DateTimeOffset> _clock;

    public OctopusSampleTokenProvider(OctopusExampleConfig.ExampleProfile profile,
        Func<DateTimeOffset> clock = null, string secret = null)
    {
        _profile = profile;
        _secret = secret ?? profile?.signingSecret;
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
    }

    public static bool CanConnect(OctopusExampleConfig.ExampleProfile profile, out string reason, string secret = null)
    {
        if (profile == null || (string.IsNullOrWhiteSpace(secret ?? profile.signingSecret) &&
                                string.IsNullOrWhiteSpace(profile.authToken)))
        {
            reason = "OctopusExampleConfig has no SSO credentials — fill ssoTokenSecret or authToken " +
                     "on the asset, then build again.";
            return false;
        }
        reason = null;
        return true;
    }

    public string GetToken(string userId, IEnumerable<string> entitlements = null)
    {
        string reason;
        // A token callback must complete even without credentials: the SDK treats an empty
        // token as a recoverable error. Sample entry points also show CanConnect's reason.
        if (!CanConnect(_profile, out reason, _secret)) return string.Empty;
        if (string.IsNullOrWhiteSpace(_secret)) return _profile.authToken;

        var payload = new StringBuilder("{\"sub\":").Append(Quote(userId))
            .Append(",\"exp\":")
            .Append((_clock().ToUnixTimeSeconds() + 365L * 24 * 60 * 60).ToString(CultureInfo.InvariantCulture));
        var claims = new List<string>();
        if (entitlements != null)
            foreach (var entitlement in entitlements) claims.Add(Quote(entitlement));
        if (claims.Count > 0) payload.Append(",\"entitlements\":[").Append(string.Join(",", claims)).Append(']');
        payload.Append('}');
        return Sign(payload.ToString());
    }

    public string GetBridgeSignature(string bridgeFingerprint)
    {
        if (string.IsNullOrWhiteSpace(_secret))
            throw new InvalidOperationException(
                "OctopusExampleConfig has no bridge signing secret — fill ssoTokenSecret on the asset, " +
                "then build again, or configure OctopusScenarioSdk.BridgeShareSigner with a backend provider. " +
                "A static authToken cannot sign bridge posts. No bridge token was generated.");

        // No iat: a device clock ahead of the server would make the token 'issued in the future'.
        var payload = "{\"bridge_fingerprint\":" + Quote(bridgeFingerprint) + ",\"exp\":" +
                      (_clock().ToUnixTimeSeconds() + 3600L).ToString(CultureInfo.InvariantCulture) + "}";
        return Sign(payload);
    }

    private string Sign(string payload)
    {
        var data = Encode(Encoding.UTF8.GetBytes("{\"alg\":\"HS256\",\"typ\":\"JWT\"}")) + "." +
                   Encode(Encoding.UTF8.GetBytes(payload));
        using (var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_secret)))
            return data + "." + Encode(hmac.ComputeHash(Encoding.UTF8.GetBytes(data)));
    }

    private static string Encode(byte[] bytes)
    {
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static string Quote(string value)
    {
        var result = new StringBuilder("\"");
        foreach (var character in value ?? string.Empty)
        {
            switch (character)
            {
                case '"': result.Append("\\\""); break;
                case '\\': result.Append("\\\\"); break;
                case '\b': result.Append("\\b"); break;
                case '\f': result.Append("\\f"); break;
                case '\n': result.Append("\\n"); break;
                case '\r': result.Append("\\r"); break;
                case '\t': result.Append("\\t"); break;
                default:
                    if (character < 0x20) result.Append("\\u").Append(((int)character).ToString("x4"));
                    else result.Append(character);
                    break;
            }
        }
        return result.Append('"').ToString();
    }
}
