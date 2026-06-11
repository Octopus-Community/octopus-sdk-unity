using System;
using UnityEngine;

public partial class OctopusSDK
{
    // Raised when the user taps a CTA on a post linked to one of your own objects
    // (article, product...). The argument is the clientObjectId you associated with it.
    public static event Action<string> OnNavigateToClientObject;

    private static void TriggerOnNavigateToClientObject(string clientObjectId)
    {
        OnNavigateToClientObject?.Invoke(clientObjectId);
    }

    public partial class OctopusChannel : MonoBehaviour
    {
        public void OnNavigateToClientObject(string clientObjectId)
        {
            OctopusSDK.TriggerOnNavigateToClientObject(clientObjectId);
        }
    }
}
