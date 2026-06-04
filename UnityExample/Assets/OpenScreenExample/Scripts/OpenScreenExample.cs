using UnityEngine;

// Demonstrates OctopusSDK.OpenPost and OctopusSDK.OpenCreatePost.
// Wire the public methods below to UI buttons in the example scene.
public class OpenScreenExample : MonoBehaviour
{
    [SerializeField] private string postId = "";
    [SerializeField] private string prefilledText = "Hello from Unity!";
    [SerializeField] private string prefilledTopicId = "";
    [SerializeField] private string prefilledImagePath = "";

    void Start()
    {
        OctopusSDK.Initialize(OctopusExampleConfig.Instance.Default.apiKey, ConnectionMode.OctopusAuth());
    }

    public void OnOpenPostClicked()
    {
        OctopusSDK.OpenPost(postId);
    }

    public void OnOpenCreatePostClicked()
    {
        OctopusSDK.OpenCreatePost(new OctopusPrefilledPost
        {
            Text = prefilledText,
            TopicId = string.IsNullOrEmpty(prefilledTopicId) ? null : prefilledTopicId,
            ImagePath = string.IsNullOrEmpty(prefilledImagePath) ? null : prefilledImagePath
        });
    }
}
