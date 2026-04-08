using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    public void OpenOctopusAuthExample()
    {
        SceneManager.LoadScene("OctopusAuthExample");
    }

    public void OpenSSOAuthExample()
    {
        SceneManager.LoadScene("SSOExample");
    }

    public void OpenForcedLoginExample()
    {
        SceneManager.LoadScene("ForcedLoginExample");
    }

    public void OpenCustomThemes()
    {
        SceneManager.LoadScene("CustomThemesExample");
    }

    public void OpenManagedFieldsExample()
    {
        SceneManager.LoadScene("ManagedFieldsExample");
    }

    public void OpenPushNotificationsExample()
    {
        SceneManager.LoadScene("PushNotificationsExample");
    }

    public void OpenEventsExample()
    {
        SceneManager.LoadScene("EventsExample");
    }

    public void OpenLanguageOverrideExample()
    {
        SceneManager.LoadScene("LanguageOverrideExample");
    }
}
