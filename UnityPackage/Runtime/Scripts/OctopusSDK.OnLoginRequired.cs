using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using UnityEngine;

public partial class OctopusSDK
{
    public static event Action OnLoginRequired;

    private static void TriggerOnLoginRequired()
    {
        OnLoginRequired?.Invoke();
    }

    public partial class OctopusChannel : MonoBehaviour
    {
        public void OnLoginRequired(string message)
        {
            OctopusSDK.TriggerOnLoginRequired();
        }
    }
}
