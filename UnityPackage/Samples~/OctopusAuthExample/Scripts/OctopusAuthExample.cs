using UnityEngine;

public class OctopusAuthExample : MonoBehaviour
{
    const string API_KEY = "YOUR_API_KEY";

    void Start()
    {
        OctopusSDK.Initialize(API_KEY, ConnectionMode.OctopusAuth());
    }

    public void OnButtonClicked()
    {
        OctopusSDK.Open();
    }
}
