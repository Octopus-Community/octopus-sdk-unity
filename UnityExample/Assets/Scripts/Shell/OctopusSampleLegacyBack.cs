using UnityEngine;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// The system back inside the legacy example scenes: they have no handler of their own, so one
/// press returns to the shell, on About, where "Legacy examples" opened them. Lives across the
/// legacy scenes and removes itself once the shell scene is active again.
/// </summary>
public sealed class OctopusSampleLegacyBack : MonoBehaviour
{
    public const string HostName = "OctopusSampleLegacyBack";

    /// <summary>Creates the listener once; a no-op while it already exists.</summary>
    public static void Install()
    {
        if (GameObject.Find(HostName) != null) return;
        var host = new GameObject(HostName);
        if (Application.isPlaying) DontDestroyOnLoad(host);
        host.AddComponent<OctopusSampleLegacyBack>();
    }

    /// <summary>Loads the shell and asks it to land on About.</summary>
    public static void ReturnToShell()
    {
        OctopusSampleShell.OpenAboutOnStart = true;
        SceneManager.LoadScene(OctopusSampleShell.SceneName);
    }

#if ENABLE_INPUT_SYSTEM
    private InputAction _back;
    private bool _pending;

    private void OnEnable()
    {
        if (!OctopusSampleBackHandler.ListensForHardwareBack) return;
        _back = new InputAction("OctopusSampleLegacyBack", InputActionType.Button, "<Keyboard>/escape");
        _back.performed += OnBack;
        _back.Enable();
    }

    private void OnDisable()
    {
        if (_back == null) return;
        _back.performed -= OnBack;
        _back.Dispose();
        _back = null;
    }

    private void OnBack(InputAction.CallbackContext context) { _pending = true; }
#endif

    private void Update()
    {
        if (SceneManager.GetActiveScene().name == OctopusSampleShell.SceneName)
        {
            Destroy(gameObject);
            return;
        }
#if ENABLE_INPUT_SYSTEM
        if (!_pending) return;
        _pending = false;
        ReturnToShell();
#endif
    }
}
