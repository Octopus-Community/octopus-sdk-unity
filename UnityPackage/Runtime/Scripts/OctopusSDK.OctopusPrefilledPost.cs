using System;
using System.Threading.Tasks;

// Prefilled content for the post editor opened via OctopusSDK.OpenCreatePost.
// All fields are optional; leave any unset to omit it from the editor.
public class OctopusPrefilledPost
{
    public string Text;       // initial editor body
    public string TopicId;    // target group/topic id
    public string ImagePath;  // local file path to an image
    // CtaLabel and CtaUrl must both be set, or the CTA is omitted from the post.
    public string CtaLabel;   // CTA button text
    public string CtaUrl;     // CTA target URL

    // Signs a prefilled-share IMAGE for a community that restricts member pictures.
    // Invoked ONLY when this share carries an image AND the community gates pictures:
    // receives the SDK-computed bridge fingerprint, returns the compact HS256 JWT your
    // BACKEND mints (bridge_fingerprint claim). Leave null if you never share images to
    // picture-restricted communities.
    //
    // Return a valid JWT to authorize the image. If your callback throws or returns
    // null/empty, the share is aborted with an error (the image is not posted) — so a
    // backend outage surfaces cleanly instead of silently posting an unsigned image.
    //
    // IMPORTANT: runs on a BACKGROUND thread while the game loop is suspended (the Octopus
    // UI is treated like backgrounding the game). It must complete WITHOUT the Unity player
    // loop: use loop-independent I/O (System.Net.Http), never UnityWebRequest, and do not
    // await continuations that marshal back to the main thread. Never ship your signing
    // secret in the app — sign on your backend.
    public Func<string, Task<string>> SignBridgeShare;
}

// Pure, testable normalization shared by both platform bridges:
// flattens a (possibly null) prefilled post into the non-null string
// arguments the native bridge expects ("" == not set). A CTA is emitted
// only when both label and url are present; a half-set CTA is dropped.
internal static class OctopusPrefilledPostMarshal
{
    public static void ToArgs(OctopusPrefilledPost p,
        out string text, out string topicId, out string imagePath,
        out string ctaLabel, out string ctaUrl)
    {
        text = p?.Text ?? "";
        topicId = p?.TopicId ?? "";
        imagePath = p?.ImagePath ?? "";

        var label = p?.CtaLabel ?? "";
        var url = p?.CtaUrl ?? "";
        bool hasCta = label.Length > 0 && url.Length > 0;
        ctaLabel = hasCta ? label : "";
        ctaUrl = hasCta ? url : "";
    }
}
