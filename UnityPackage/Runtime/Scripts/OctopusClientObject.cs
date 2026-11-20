using System;
using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>Content used only when creating the post linked to ObjectId. Existing posts are returned untouched.</summary>
public sealed class OctopusClientObject
{
    /// <summary>Stable object identifier. Must be nonempty and contain no newline or carriage return.</summary>
    public string ObjectId;
    /// <summary>Post text (native limits: 1–5000 characters).</summary>
    public string Text;
    /// <summary>Optional target group; null uses the community's configured default.</summary>
    public string GroupId;
    /// <summary>Optional catch phrase displayed below the post text.</summary>
    public string CatchPhrase;
    /// <summary>Optional button label, auto-translated (OctopusLocalizedText.AutoTranslated) or given per
    /// language (OctopusLocalizedText.PerLanguage). Takes precedence over ViewObjectButtonText. Tapping it raises
    /// OnNavigateToClientObject with ObjectId. Keep each label under 28 characters so that it fits on one
    /// line. Taken when the post is created only: a post that already exists for ObjectId keeps its button.</summary>
    public OctopusLocalizedText ViewObjectButton;

    /// <summary>Deprecated plain button label. Octopus translates a non-empty value automatically
    /// when ViewObjectButton is null.</summary>
    [Obsolete("Use ViewObjectButton: OctopusLocalizedText.AutoTranslated(text) keeps the automatic translation.")]
    public string ViewObjectButtonText;
    /// <summary>Deprecated single image: a local file path, as in OctopusPrefilledPost.ImagePaths.
    /// Set at most one of ImagePath and ImageUrl, and neither together with a non-empty Medias.</summary>
    [Obsolete("Use Medias.")]
    public string ImagePath;
    /// <summary>Deprecated single image: an absolute HTTP(S) URL pointing directly to an image.
    /// Set at most one of ImagePath and ImageUrl, and neither together with a non-empty Medias.</summary>
    [Obsolete("Use Medias.")]
    public string ImageUrl;
    /// <summary>Images of the post, in display order, each a local file or a remote URL; null entries are
    /// ignored. A post accepts at most OctopusPrefilledPost.MaxImageCount images. More, or a non-empty
    /// Medias together with the deprecated ImagePath or ImageUrl, fail the request with
    /// OctopusClientPostErrorCode.Other before any native call.</summary>
    public List<OctopusClientObjectMedia> Medias = new List<OctopusClientObjectMedia>();
    /// <summary>Optional signature provider scoped to this request. Receives the native fingerprint and
    /// returns a JWT minted by your backend. Runs off the Unity loop; use loop-independent I/O.
    /// Throwing or returning an empty signature fails the request. Never embed a signing secret.</summary>
    public Func<string, Task<string>> SignBridgeShare;

    internal OctopusLocalizedText ResolveViewObjectButton()
    {
#pragma warning disable CS0618 // the deprecated plain button label is still honoured
        var legacy = ViewObjectButtonText;
#pragma warning restore CS0618
        return ViewObjectButton
            ?? (string.IsNullOrEmpty(legacy) ? null : OctopusLocalizedText.AutoTranslated(legacy));
    }
}

/// <summary>One image of an OctopusClientObject: a local file (FromPath) or a remote image URL (FromUrl).</summary>
public sealed class OctopusClientObjectMedia
{
    /// <summary>The local file path, or null for a remote image.</summary>
    public string Path { get; private set; }
    /// <summary>The absolute HTTP(S) image URL, or null for a local image.</summary>
    public string Url { get; private set; }

    private OctopusClientObjectMedia(string path, string url)
    {
        Path = path;
        Url = url;
    }

    /// <summary>A local image file. Throws an ArgumentException when path is null or empty.</summary>
    public static OctopusClientObjectMedia FromPath(string path)
    {
        if (string.IsNullOrEmpty(path)) throw new ArgumentException("A local image path is required", "path");
        return new OctopusClientObjectMedia(path, null);
    }

    /// <summary>A remote image at an absolute HTTP(S) URL pointing directly to the image. Throws an
    /// ArgumentException when url is null or empty; the native SDK validates the URL itself.</summary>
    public static OctopusClientObjectMedia FromUrl(string url)
    {
        if (string.IsNullOrEmpty(url)) throw new ArgumentException("An image URL is required", "url");
        return new OctopusClientObjectMedia(null, url);
    }
}
