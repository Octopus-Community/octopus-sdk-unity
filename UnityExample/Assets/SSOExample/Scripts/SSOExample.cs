using UnityEngine;
using System.Threading.Tasks;
using UnityEngine.UI;

public class SSOExample : MonoBehaviour
{
    OctopusExampleConfig.ExampleProfile config => OctopusExampleConfig.Instance.Default;

    [SerializeField] Button loginButton;
    TMPro.TMP_Text refusalText;
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
        string reason;
        if (!isLoggedIn && !OctopusSampleTokenProvider.CanConnect(config, out reason))
        {
            if (refusalText == null)
            {
                refusalText = SampleUi.Label("sso-connection-refusal", loginButton.transform, "",
                    SampleUi.TextBody, OctopusSampleBranding.Palette.Title, TextAnchor.LowerLeft);
                var rect = refusalText.rectTransform;
                rect.anchorMin = new Vector2(0, 1);
                rect.anchorMax = new Vector2(1, 1);
                rect.pivot = new Vector2(0.5f, 0);
                rect.anchoredPosition = new Vector2(0, 16);
                rect.sizeDelta = Vector2.zero;
                refusalText.gameObject.AddComponent<ContentSizeFitter>().verticalFit =
                    ContentSizeFitter.FitMode.PreferredSize;
            }
            refusalText.text = reason;
            UpdateButton();
            OctopusSampleLog.Current.LogStateChange("[OctopusQA] scene=SSO state=refused", reason);
            return;
        }
        if (refusalText != null) refusalText.text = "";
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
        var profile = config;
        var provider = new OctopusSampleTokenProvider(profile);
        await Task.Delay(100);
        return provider.GetToken(profile.userId, profile.entitlements);
    }

    public void OnOpenButtonClicked()
    {
        OctopusSDK.Open();
    }
}
