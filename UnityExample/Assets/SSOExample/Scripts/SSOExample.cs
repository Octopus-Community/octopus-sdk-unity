using UnityEngine;
using System.Threading.Tasks;
using UnityEngine.UI;

public class SSOExample : MonoBehaviour
{
    OctopusExampleConfig.ExampleProfile config => OctopusExampleConfig.Instance.Default;

    [SerializeField] Button loginButton;
    bool isLoggedIn = false;

    void Start()
    {
        StartAsync();
    }

    void StartAsync()
    {
        OctopusSDK.Initialize(config.apiKey, ConnectionMode.SSO());
        isLoggedIn = false;
        UpdateButton();
    }

    public async void OnLoginButtonClicked()
    {
        DisableButton();
        if (isLoggedIn)
        {
            await OctopusSDK.DisconnectUser();
        }
        else
        {
            await OctopusSDK.ConnectUser(
                config.userId,
                config.nickname,
                config.bio,
                config.picture,
                GetToken
            );
        }
        isLoggedIn = !isLoggedIn;
        UpdateButton();
    }

    void DisableButton()
    {
        loginButton.enabled = false;
        loginButton.GetComponentInChildren<Text>().text = "...";
    }

    void UpdateButton()
    {
        if (isLoggedIn)
        {
            loginButton.GetComponentInChildren<Text>().text = "Disconnect user";
        }
        else
        {
            loginButton.GetComponentInChildren<Text>().text = "Connect user";
        }
        loginButton.enabled = true;
    }

    public async Task<string> GetToken()
    {
        await Task.Delay(100);
        return config.authToken;
    }

    public void OnOpenButtonClicked()
    {
        OctopusSDK.Open();
    }
}
