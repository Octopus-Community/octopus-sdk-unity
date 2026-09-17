using System;
using System.Threading.Tasks;

/// <summary>Content used only when creating the post linked to ObjectId. Existing posts are returned untouched.</summary>
public sealed class OctopusClientObject
{
    /// <summary>Stable object identifier. Must be nonempty and contain no newline or carriage return.</summary>
    public string ObjectId;
    /// <summary>Post text (native limits: 10–5000 characters).</summary>
    public string Text;
    /// <summary>Optional target group; null uses the community's configured default.</summary>
    public string GroupId;
    /// <summary>Optional catch phrase displayed below the post text.</summary>
    public string CatchPhrase;
    /// <summary>Optional button label. Tapping it raises OnNavigateToClientObject with ObjectId.</summary>
    public string ViewObjectButtonText;
    /// <summary>Optional local image file path, using the same convention as OctopusPrefilledPost.ImagePath.
    /// Set at most one of ImagePath and ImageUrl.</summary>
    public string ImagePath;
    /// <summary>Optional absolute HTTP(S) URL pointing directly to an image.</summary>
    public string ImageUrl;
    /// <summary>Optional signature provider scoped to this request. Receives the native fingerprint and
    /// returns a JWT minted by your backend. Runs off the Unity loop; use loop-independent I/O.
    /// Throwing or returning an empty signature fails the request. Never embed a signing secret.</summary>
    public Func<string, Task<string>> SignBridgeShare;
}
