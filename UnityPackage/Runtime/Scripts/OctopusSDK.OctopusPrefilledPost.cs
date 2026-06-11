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
