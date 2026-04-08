using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using UnityEngine;

public partial class OctopusSDK
{
    public static event Action<ProfileField?> OnModifyUser;

    private static void TriggerOnModifyUser(ProfileField? field)
    {
        OnModifyUser?.Invoke(field);
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
}
