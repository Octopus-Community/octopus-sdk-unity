/// <summary>The ClientUserError business cases shared with Flutter and React Native.</summary>
public enum OctopusClientUserErrorCode
{
    /// <summary>No usable token was supplied.</summary>
    MissingToken,
    /// <summary>The user is banned; Message carries the backend's reason.</summary>
    UserBanned,
    /// <summary>The supplied profile failed validation; Message contains the details.</summary>
    ProfileError,
    /// <summary>The JWT was rejected (reported by iOS).</summary>
    InvalidToken,
    /// <summary>The user cannot access this community (reported by iOS).</summary>
    CommunityAccessDenied,
    /// <summary>An unclassified native, transport, token-provider or bridge failure.</summary>
    Other
}

/// <summary>A typed connection failure. Profile validation details are conveyed in Message,
/// matching the Flutter and React Native wrappers.</summary>
public sealed class OctopusClientUserError
{
    /// <summary>The classified failure; unfamiliar native cases map to Other.</summary>
    public OctopusClientUserErrorCode Code { get; private set; }
    /// <summary>The diagnostic message, or a fallback when none was supplied.</summary>
    public string Message { get; private set; }

    /// <summary>Creates a failure, including an injected Editor Mock response.</summary>
    public OctopusClientUserError(OctopusClientUserErrorCode code, string message)
    {
        Code = code;
        Message = string.IsNullOrEmpty(message) ? "Unknown error" : message;
    }
}

internal static class OctopusClientUserErrorParsing
{
    internal static OctopusClientUserError FromJson(string json)
    {
        try
        {
            var row = OctopusJson.ParseObject(json);
            string value;
            string message;
            row.TryGetValue("code", out value);
            row.TryGetValue("message", out message);
            var code = OctopusClientUserErrorCode.Other;
            switch (value)
            {
                case "missingToken": code = OctopusClientUserErrorCode.MissingToken; break;
                case "userBanned": code = OctopusClientUserErrorCode.UserBanned; break;
                case "profileError": code = OctopusClientUserErrorCode.ProfileError; break;
                case "invalidToken": code = OctopusClientUserErrorCode.InvalidToken; break;
                case "communityAccessDenied": code = OctopusClientUserErrorCode.CommunityAccessDenied; break;
            }
            return new OctopusClientUserError(code, message);
        }
        catch (System.FormatException)
        {
            return new OctopusClientUserError(OctopusClientUserErrorCode.Other,
                "Malformed connect user error payload");
        }
    }
}
