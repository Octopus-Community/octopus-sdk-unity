using UnityEngine;

public class OctopusAuthExample : MonoBehaviour
{
    void Start()
    {
        OctopusSDK.Initialize(OctopusExampleConfig.Instance.Default.apiKey, ConnectionMode.OctopusAuth());
    }

    public void OnButtonClicked()
    {
        OctopusSDK.Open();
    }
}
