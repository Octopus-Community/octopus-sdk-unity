using System;
using NUnit.Framework;
using UnityEngine;

public class OctopusNavigateToClientObjectTests
{
    [Test]
    public void Channel_OnNavigateToClientObject_RaisesEventWithId()
    {
        string seen = null;
        Action<string> handler = id => seen = id;
        OctopusSDK.OnNavigateToClientObject += handler;
        GameObject go = null;
        try
        {
            go = new GameObject("ch");
            var channel = go.AddComponent<OctopusSDK.OctopusChannel>();
            channel.OnNavigateToClientObject("obj_42");
            Assert.AreEqual("obj_42", seen);
        }
        finally
        {
            OctopusSDK.OnNavigateToClientObject -= handler;
            if (go != null) UnityEngine.Object.DestroyImmediate(go);
        }
    }
}
