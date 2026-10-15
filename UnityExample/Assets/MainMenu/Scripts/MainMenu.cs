using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    void Start()
    {
        // The QA-scenarios entry is built here rather than added to MainMenu.unity: the scene's
        // buttons are serialized YAML, and an entry point that exists only in code is one a
        // reviewer can actually read in a diff. It also keeps this change to two script files.
        OctopusSampleShell.InstallLegacyMenuEntry();
    }

    // Generic legacy scenes browse as guests and use the sample's SSO scene for sign-in.
    public static void RegisterSsoLoginRoute()
    {
        OctopusSDK.OnLoginRequired -= OpenSsoLogin;
        OctopusSDK.OnLoginRequired += OpenSsoLogin;
    }

    public static void UnregisterSsoLoginRoute()
    {
        OctopusSDK.OnLoginRequired -= OpenSsoLogin;
    }

    private static void OpenSsoLogin()
    {
        SceneManager.LoadScene("SSOExample");
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

    public void OpenOpenScreenExample()
    {
        SceneManager.LoadScene("OpenScreenExample");
    }
}
