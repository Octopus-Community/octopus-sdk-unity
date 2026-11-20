using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

// Prefilled content for the post editor opened via OctopusSDK.OpenCreatePost.
// All fields are optional; leave any unset to omit it from the editor.
public class OctopusPrefilledPost
{
    /// <summary>Maximum number of images a prefilled post carries. Mirrors the native post editor,
    /// which accepts up to this many photos per post.</summary>
    public const int MaxImageCount = 10;

    public string Text;       // initial editor body
    public string TopicId;    // target group/topic id
    /// <summary>Deprecated single image: a local file path. Alone, it behaves as an ImagePaths list
    /// holding only this path. Setting it together with a non-empty ImagePaths makes
    /// OctopusSDK.OpenCreatePost throw an ArgumentException.</summary>
    [Obsolete("Use ImagePaths.")]
    public string ImagePath;
    /// <summary>Local file paths of the images the editor opens with, in display order, at most
    /// MaxImageCount. Null and empty entries are ignored. More images make OctopusSDK.OpenCreatePost
    /// throw an OctopusPrefilledPostTooManyImagesException. The SDK does not download remote URLs:
    /// save the images to local files first.</summary>
    public List<string> ImagePaths = new List<string>();
    /// <summary>Label of the CTA button, auto-translated (OctopusLocalizedText.AutoTranslated) or given per
    /// language (OctopusLocalizedText.PerLanguage). Takes precedence over CtaLabel. A label and CtaUrl
    /// are required to include the CTA. Keep each label under 28 characters so that it fits on one line.</summary>
    public OctopusLocalizedText CtaLocalizedLabel;

    /// <summary>Deprecated plain CTA label. Octopus translates a non-empty value automatically
    /// when CtaLocalizedLabel is null.</summary>
    [Obsolete("Use CtaLocalizedLabel: OctopusLocalizedText.AutoTranslated(label) keeps the automatic translation.")]
    public string CtaLabel;

    public string CtaUrl;     // CTA target URL

    // Signs a prefilled-share IMAGE for a community that restricts member pictures.
    // Invoked ONLY when this share carries an image AND the community gates pictures:
    // receives the SDK-computed bridge fingerprint, returns the compact HS256 JWT your
    // BACKEND mints (bridge_fingerprint claim). Leave null if you never share images to
    // picture-restricted communities.
    //
    // Return a valid JWT to authorize the image. Without a callback, or when it throws or
    // returns null/empty, the post is sent unsigned, on iOS and Android alike: the server
    // publishes it, images included, where the community allows member pictures, and refuses
    // it where it does not, the editor then showing an error.
    //
    // IMPORTANT: runs on a BACKGROUND thread while the game loop is suspended (the Octopus
    // UI is treated like backgrounding the game). It must complete WITHOUT the Unity player
    // loop: use loop-independent I/O (System.Net.Http), never UnityWebRequest, and do not
    // await continuations that marshal back to the main thread. Never ship your signing
    // secret in the app — sign on your backend.
    public Func<string, Task<string>> SignBridgeShare;
}

/// <summary>Thrown by OctopusSDK.OpenCreatePost when a prefilled post carries more than
/// OctopusPrefilledPost.MaxImageCount images (null and empty entries are not counted). Mirrors the
/// native OctopusPrefilledPost.TooManyImagesError. It derives from ArgumentException directly, so a
/// generic ArgumentException handler still covers it.</summary>
public sealed class OctopusPrefilledPostTooManyImagesException : ArgumentException
{
    /// <summary>The maximum number of images a post carries (OctopusPrefilledPost.MaxImageCount).</summary>
    public int Max { get; private set; }

    /// <summary>The number of (non-empty) images that were provided.</summary>
    public int Count { get; private set; }

    /// <summary>Creates the error for count images provided where at most max are accepted.</summary>
    public OctopusPrefilledPostTooManyImagesException(int max, int count)
        : base(string.Format("{0} images provided, at most {1} are accepted.", count, max))
    {
        Max = max;
        Count = count;
    }
}

// Pure, testable normalization shared by both platform bridges:
// flattens a (possibly null) prefilled post into the non-null string
// arguments the native bridge expects ("" == not set). A CTA is emitted
// only when both label and url are present; a half-set CTA is dropped.
// The label travels as OctopusLocalizedText JSON, the images as a JSON
// array of paths ("[]" == none).
internal static class OctopusPrefilledPostMarshal
{
    public static void ToArgs(OctopusPrefilledPost p,
        out string text, out string topicId, out string imagePathsJson,
        out string ctaLabelJson, out string ctaUrl)
    {
        var imagePaths = ImagePaths(p);
        text = p?.Text ?? "";
        topicId = p?.TopicId ?? "";
        imagePathsJson = ToJson(imagePaths);

#pragma warning disable CS0618 // the deprecated plain CTA label is still honoured
        var legacy = p?.CtaLabel;
#pragma warning restore CS0618
        var label = p?.CtaLocalizedLabel
            ?? (string.IsNullOrEmpty(legacy) ? null : OctopusLocalizedText.AutoTranslated(legacy));
        var url = p?.CtaUrl ?? "";
        // An empty auto-translated label is no label, as an empty CtaLabel was; a per-language label is
        // always sent, so the native SDK reports what is wrong with it.
        bool hasCta = label != null && (label.IsPerLanguage || label.DefaultText.Length > 0) && url.Length > 0;
        ctaLabelJson = hasCta ? label.ToJson() : "";
        ctaUrl = hasCta ? url : "";
    }

    // The images in display order: the deprecated ImagePath alone, or the non-empty ImagePaths.
    // Throws before any native call, as the native constructors would.
    public static List<string> ImagePaths(OctopusPrefilledPost p)
    {
        var paths = new List<string>();
        if (p == null) return paths;
        if (p.ImagePaths != null)
            foreach (var path in p.ImagePaths)
                if (!string.IsNullOrEmpty(path)) paths.Add(path);
#pragma warning disable 0618 // the deprecated single image is still honoured
        var legacy = p.ImagePath;
#pragma warning restore 0618
        if (!string.IsNullOrEmpty(legacy))
        {
            if (paths.Count > 0) throw new ArgumentException("Set ImagePaths only: ImagePath is deprecated.");
            paths.Add(legacy);
        }
        if (paths.Count > OctopusPrefilledPost.MaxImageCount)
            throw new OctopusPrefilledPostTooManyImagesException(OctopusPrefilledPost.MaxImageCount, paths.Count);
        return paths;
    }

    internal static string ToJson(List<string> paths)
    {
        var json = new StringBuilder("[");
        for (int i = 0; i < paths.Count; i++)
        {
            if (i > 0) json.Append(',');
            json.Append('"').Append(OctopusJson.Escape(paths[i])).Append('"');
        }
        return json.Append(']').ToString();
    }
}
