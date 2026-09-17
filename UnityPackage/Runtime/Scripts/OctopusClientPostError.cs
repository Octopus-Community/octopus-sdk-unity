/// <summary>ClientPostError cases matching Flutter. iOS validation details are not public and map to Other.</summary>
public enum OctopusClientPostErrorCode
{
    /// <summary>Unclassified validation, connection, authentication, cancellation or bridge failure.</summary>
    Other,
    /// <summary>Post text is required.</summary>
    TextMissing,
    /// <summary>Post text exceeds the maximum length.</summary>
    TextTooLong,
    /// <summary>The image is empty.</summary>
    FileEmpty,
    /// <summary>The image exceeds the size limit.</summary>
    FileTooLarge,
    /// <summary>The image format is unsupported.</summary>
    FileBadFormat,
    /// <summary>The image could not be uploaded.</summary>
    FileUpload,
    /// <summary>The remote image could not be downloaded.</summary>
    FileDownload,
    /// <summary>The client object identifier is required.</summary>
    MissingObjectId,
    /// <summary>The call-to-action text is required.</summary>
    MissingCta,
    /// <summary>The related post is unavailable.</summary>
    PostUnavailable,
    /// <summary>The related post was not found.</summary>
    PostNotFound,
    /// <summary>A related post already exists.</summary>
    PostAlreadyExists,
    /// <summary>The group identifier is invalid.</summary>
    InvalidGroupId,
    /// <summary>The author is not authorized.</summary>
    InvalidAuthor,
    /// <summary>The bridge signature is invalid.</summary>
    TokenInvalid,
    /// <summary>The bridge signature has expired.</summary>
    TokenExpired
}

/// <summary>A typed client-post failure. Android reports the first validation error; iOS reports Other.</summary>
public sealed class OctopusClientPostError
{
    /// <summary>The classified failure; unfamiliar native cases map to Other.</summary>
    public OctopusClientPostErrorCode Code { get; private set; }
    /// <summary>A native diagnostic or fallback message.</summary>
    public string Message { get; private set; }
    /// <summary>Creates a failure, including for an Editor mock response.</summary>
    public OctopusClientPostError(OctopusClientPostErrorCode code, string message)
    {
        Code = code;
        Message = string.IsNullOrEmpty(message) ? "Client post error" : message;
    }
}
