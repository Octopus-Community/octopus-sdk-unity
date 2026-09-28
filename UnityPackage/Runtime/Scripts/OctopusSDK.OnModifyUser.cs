using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using UnityEngine;

public partial class OctopusSDK
{
    private static Action<ProfileField?> _onModifyUser;

    /// <summary>Raised on the Unity main thread when the user asks to edit a profile field the
    /// host owns. Two triggers: app-managed fields in SSO mode, and — when Unified Profile is
    /// active — the "Edit my profile" item of another member's Activity screen, which passes
    /// null (no specific field). Octopus is dismissed before delivery in both cases.
    /// On iOS that menu item is offered only while this event has a subscriber; on Android the
    /// native SDK always offers it (see CHANGELOG, "Known divergence").</summary>
    public static event Action<ProfileField?> OnModifyUser
    {
        add
        {
            _onModifyUser += value;
            SetHasModifyUserHandler(_onModifyUser != null);
        }
        remove
        {
            _onModifyUser -= value;
            SetHasModifyUserHandler(_onModifyUser != null);
        }
    }

    private static void TriggerOnModifyUser(ProfileField? field)
    {
        // Capture once: unsubscribing between the null check and the call must not throw.
        var listeners = _onModifyUser;
        if (listeners != null) listeners(field);
    }

    // Tells the native side whether a host handler exists. iOS gates the Activity screen's
    // "Edit my profile" item on it, so the item is never shown without a subscriber to deliver
    // to. No Android branch on purpose: the Android bridge wires its equivalent unconditionally
    // (one native parameter serves both edit paths there, and the native SDK requires it in SSO
    // mode with app-managed fields), so there is nothing for this flag to toggle.
    private static void SetHasModifyUserHandler(bool present)
    {
#if UNITY_EDITOR
        Mock.Record("SetHasModifyUserHandler", present);
#elif UNITY_IOS
        OctopusSdkSetHasModifyUserHandler(present ? 1 : 0);
#endif
    }

    public partial class OctopusChannel : MonoBehaviour
    {
        public void OnModifyUser(string field)
        {
            ProfileField? fieldValue;
            switch (field)
            {
                case "NICKNAME": fieldValue = ProfileField.NICKNAME; break;
                case "BIO": fieldValue = ProfileField.BIO; break;
                case "PICTURE": fieldValue = ProfileField.PICTURE; break;
                default: fieldValue = null; break;
            }
            OctopusSDK.TriggerOnModifyUser(fieldValue);
        }
    }

#if UNITY_IOS && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void OctopusSdkSetHasModifyUserHandler(int present);
#endif
}
