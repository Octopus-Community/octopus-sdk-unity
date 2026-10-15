using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

public sealed class ConnectionMode
{
    public string Mode { get; }
    public IReadOnlyList<ProfileField> AppManagedFields { get; }

    private ConnectionMode(string mode, IReadOnlyList<ProfileField> fields)
    {
        Mode = mode;
        AppManagedFields = fields;
    }

    /// <summary>Uses your app's sign-in flow, with the profile fields your app manages.</summary>
    public static ConnectionMode SSO(params ProfileField[] fields)
        => new ConnectionMode("sso", fields ?? Array.Empty<ProfileField>());

    public int[] AppManagedFieldsAsIntArray
    {
        get
        {
            int[] intArray = new int[AppManagedFields.Count];
            for (int i = 0; i < AppManagedFields.Count; i++)
            { intArray[i] = (int)AppManagedFields[i]; }
            return intArray;
        }
    }
}

public enum ProfileField
{
    NICKNAME,
    BIO,
    PICTURE,
}
