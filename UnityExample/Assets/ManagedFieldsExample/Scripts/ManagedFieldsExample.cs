using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

public class ManagedFieldsExample : MonoBehaviour
{
    OctopusExampleConfig.ExampleProfile config => OctopusExampleConfig.Instance.ManagedFields;

    string[] PICTURES =
    {
        "https://media-content.octocdn.net/misc/81facf09052cffeac7637b62db91511fadbc4f32.png",
        "https://media-content.octocdn.net/misc/3ae2a4d93f8154853306eca8b0f3091bcced5aa5.png"
    };

    int SelectedPicture = 0;
    bool IsLoading = false;
    string MessageContent = "";

    [SerializeField] Image Frame1;
    [SerializeField] Image Frame2;
    [SerializeField] InputField NicknameInput;
    [SerializeField] InputField BioInput;
    [SerializeField] Text MessageText;
    [SerializeField] Button CallToActionButton;

    void Start()
    {
        OctopusSDK.Initialize(
            config.apiKey,
            ConnectionMode.SSO(
                ProfileField.NICKNAME,
                ProfileField.BIO,
                ProfileField.PICTURE
            )
        );
        OctopusSDK.OnModifyUser += OnModifyUser;
        UpdateUI();
    }

    void OnModifyUser(ProfileField? field)
    {
        switch(field)
        {
            case ProfileField.NICKNAME: MessageContent = "Editing Nickname"; break;
            case ProfileField.BIO: MessageContent = "Editing Bio"; break;
            case ProfileField.PICTURE: MessageContent = "Editing Picture"; break;
            default: MessageContent = "Editing All"; break;
        }
        UpdateUI();
    }

    void UpdateUI()
    {
        if(SelectedPicture == 0)
        {
            Frame1.enabled = true;
            Frame2.enabled = false;
        }
        else
        {
            Frame1.enabled = false;
            Frame2.enabled = true;
        }

        if(IsLoading)
        {
            CallToActionButton.GetComponentInChildren<Text>().text = "Loading...";
        }
        else
        {
            CallToActionButton.GetComponentInChildren<Text>().text = "Open octopus";
        }

        MessageText.text = MessageContent;
    }

    public void SelectImage1()
    {
        SelectedPicture = 0;
        UpdateUI();
    }

    public void SelectImage2()
    {
        SelectedPicture = 1;
        UpdateUI();
    }

    public async void OpenOctopus()
    {
        if (IsLoading) return;
        if(NicknameInput.text.Length == 0)
        {
            MessageContent = "Missing Nickname";
            UpdateUI();
            return;
        }

        if (BioInput.text.Length == 0)
        {
            MessageContent = "Missing Bio";
            UpdateUI();
            return;
        }

        IsLoading = true;
        MessageContent = "";
        UpdateUI();

        await OctopusSDK.ConnectUser(
            config.userId,
            NicknameInput.text,
            BioInput.text,
            PICTURES[SelectedPicture],
            GetToken
        );

        IsLoading = false;
        UpdateUI();

        OctopusSDK.Open();
    }

    public async Task<string> GetToken()
    {
        await Task.Delay(1);
        return config.authToken;
    }
}
