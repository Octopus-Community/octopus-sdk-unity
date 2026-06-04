// Prefilled content for the post editor opened via OctopusSDK.OpenCreatePost.
// All fields are optional; leave any unset to omit it from the editor.
public class OctopusPrefilledPost
{
    public string Text;       // initial editor body
    public string TopicId;    // target group/topic id
    public string ImagePath;  // local file path to an image
}

// Pure, testable normalization shared by both platform bridges:
// flattens a (possibly null) prefilled post into the non-null string
// arguments the native bridge expects ("" == not set).
internal static class OctopusPrefilledPostMarshal
{
    public static void ToArgs(OctopusPrefilledPost p, out string text, out string topicId, out string imagePath)
    {
        text = p?.Text ?? "";
        topicId = p?.TopicId ?? "";
        imagePath = p?.ImagePath ?? "";
    }
}
