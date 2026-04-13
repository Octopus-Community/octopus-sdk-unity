using System;
using UnityEngine;

public partial class OctopusSDK
{
    /// Fired on iOS when the user taps an Octopus notification (cold start, background, or foreground).
    /// The native layer has already captured the UNNotificationResponse.
    /// Call OctopusSDK.Open() in your handler to navigate to the notification's content.
    public static event Action OnNotificationTapped;

    private static void TriggerOnNotificationTapped()
    {
        OnNotificationTapped?.Invoke();
    }

    public partial class OctopusChannel : MonoBehaviour
    {
        public void OnNotificationTapped(string message)
        {
            OctopusSDK.TriggerOnNotificationTapped();
        }
    }
}
