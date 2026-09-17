using System;
using System.Collections.Generic;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public class OctopusLegacyTokenPathTests
{
    private const string FixtureSecret = "unit-test-only-secret";
    private const string StaticToken = "not-a-real-token";
    private readonly List<GameObject> _objects = new List<GameObject>();
    private OctopusExampleConfig _config;
    private object _previousConfig;
    private IOctopusSampleLog _previousLog;
    private ProbeLog _log;
    private static readonly FieldInfo InstanceField = typeof(OctopusExampleConfig)
        .GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic);

    [SetUp]
    public void SetUp()
    {
        // Inject synthetic config before any scene access; never load the machine-local asset.
        _previousConfig = InstanceField.GetValue(null);
        _config = ScriptableObject.CreateInstance<OctopusExampleConfig>();
        InstanceField.SetValue(null, _config);
        _previousLog = OctopusSampleLog.Current;
        _log = new ProbeLog();
        OctopusSampleLog.Current = _log;
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var go in _objects) Object.DestroyImmediate(go);
        _objects.Clear();
        InstanceField.SetValue(null, _previousConfig);
        Object.DestroyImmediate(_config);
        OctopusSampleLog.Current = _previousLog;
    }

    [TestCase("ForcedLoginExample", "missing")]
    [TestCase("ManagedFieldsExample", "missing")]
    [TestCase("SSOExample", "missing")]
    [TestCase("ForcedLoginExample", "signed")]
    [TestCase("ManagedFieldsExample", "signed")]
    [TestCase("SSOExample", "signed")]
    [TestCase("ForcedLoginExample", "static")]
    [TestCase("ManagedFieldsExample", "static")]
    [TestCase("SSOExample", "static")]
    public void RealSceneTokenCallbackCompletesForEveryCredentialMode(string sceneName, string mode)
    {
        var profile = Configure(sceneName, mode);
        var scene = CreateScene(sceneName);
        Task<string> task = null;
        var previousContext = SynchronizationContext.Current;
        try
        {
            // Only the synthetic delay's continuation runs off-thread; config is read here.
            // Do not block the Unity context that the callback would otherwise capture.
            SynchronizationContext.SetSynchronizationContext(null);
            Assert.DoesNotThrow(() => task = (Task<string>)scene.GetType().GetMethod("GetToken").Invoke(scene, null));
        }
        finally { SynchronizationContext.SetSynchronizationContext(previousContext); }
        Assert.IsTrue(task.Wait(TimeSpan.FromSeconds(5)), "Token callback did not complete.");
        var token = task.GetAwaiter().GetResult();
        if (mode == "missing") Assert.AreEqual(string.Empty, token);
        else if (mode == "static") Assert.AreEqual(StaticToken, token);
        else
        {
            var parts = token.Split('.');
            Assert.AreEqual(3, parts.Length);
            Assert.AreEqual("{\"alg\":\"HS256\",\"typ\":\"JWT\"}", OctopusSampleTokenProviderTests.Decode(parts[0]));
            var payload = JsonUtility.FromJson<Claims>(OctopusSampleTokenProviderTests.Decode(parts[1]));
            Assert.AreEqual(profile.userId, payload.sub);
            CollectionAssert.AreEqual(profile.entitlements, payload.entitlements);
            Assert.Greater(payload.exp, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            using (var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(FixtureSecret)))
            {
                var signature = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(parts[0] + "." + parts[1])))
                    .TrimEnd('=').Replace('+', '-').Replace('/', '_');
                Assert.AreEqual(signature, parts[2]);
            }
        }
    }

    [TestCase("ForcedLoginExample")]
    [TestCase("ManagedFieldsExample")]
    [TestCase("SSOExample")]
    public void MissingCredentialsLeaveVisibleRefusalAndUsableButton(string sceneName)
    {
        Configure(sceneName, "missing");
        var scene = CreateScene(sceneName);
        var button = Child<Button>(scene.transform, "Login");
        var caption = Child<Text>(button.transform, "Caption");
        var message = Child<Text>(scene.transform, "Message");
        if (sceneName == "ManagedFieldsExample")
        {
            Set(scene, "CallToActionButton", button);
            Set(scene, "MessageText", message);
            Set(scene, "Frame1", Child<Image>(scene.transform, "Frame1"));
            Set(scene, "Frame2", Child<Image>(scene.transform, "Frame2"));
            Set(scene, "NicknameInput", Child<InputField>(scene.transform, "Nickname"));
            Set(scene, "BioInput", Child<InputField>(scene.transform, "Bio"));
        }
        else
        {
            Set(scene, "loginButton", button);
            if (sceneName == "ForcedLoginExample") Set(scene, "message", message);
        }
        var method = scene.GetType().GetMethod(sceneName == "ManagedFieldsExample" ? "OpenOctopus" : "OnLoginButtonClicked");
        // Repeated taps must also refuse without entering a loading state or calling the SDK.
        for (var i = 0; i < 2; i++)
        {
            Assert.DoesNotThrow(() => method.Invoke(scene, null));
            var visible = sceneName == "SSOExample"
                ? scene.GetComponentInChildren<TMP_Text>(true).text : message.text;
            StringAssert.Contains("ssoTokenSecret or authToken", visible);
            Assert.IsTrue(button.enabled);
            Assert.AreEqual(sceneName == "ManagedFieldsExample" ? "Open octopus" : "Connect user", caption.text);
            Assert.AreEqual(visible, _log.Details[i]);
            StringAssert.StartsWith("[OctopusQA] scene=", _log.Headlines[i]);
            StringAssert.EndsWith("state=refused", _log.Headlines[i]);
            StringAssert.DoesNotContain("test-key", visible);
        }
        Assert.AreEqual(0, _log.ApiCalls);
    }

    private OctopusExampleConfig.ExampleProfile Configure(string sceneName, string mode)
    {
        var profile = new OctopusExampleConfig.ExampleProfile
        {
            apiKey = "test-key", userId = "fixture-user", nickname = "Fixture", bio = "Fixture bio",
            entitlements = new[] { "customer:premium" }, authToken = mode == "missing" ? "" : StaticToken
        };
        _config.ssoTokenSecret = mode == "signed" ? FixtureSecret : "";
        Set(_config, sceneName == "ForcedLoginExample" ? "forcedLoginProfile" :
            sceneName == "ManagedFieldsExample" ? "managedFieldsProfile" : "defaultProfile", profile);
        return profile;
    }

    private Component CreateScene(string sceneName)
    {
        // Legacy scenes live in the predefined assembly, which an asmdef cannot reference.
        var type = Type.GetType(sceneName + ", Assembly-CSharp");
        Assert.IsNotNull(type);
        var go = new GameObject(sceneName, typeof(RectTransform));
        go.SetActive(false);
        _objects.Add(go);
        var scene = (MonoBehaviour)go.AddComponent(type);
        scene.enabled = false; // Skip Start/Initialize, but keep the UI hierarchy active.
        go.SetActive(true);
        return scene;
    }

    private static T Child<T>(Transform parent, string name) where T : Component
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(T));
        go.transform.SetParent(parent, false);
        return go.GetComponent<T>();
    }

    private static void Set(object target, string field, object value)
    {
        target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    }

    [Serializable]
    private class Claims
    {
        public string sub;
        public long exp;
        public string[] entitlements;
    }

    private sealed class ProbeLog : IOctopusSampleLog
    {
        public readonly List<string> Headlines = new List<string>();
        public readonly List<string> Details = new List<string>();
        public int ApiCalls;
        public void LogApiCall(string method, string detail = null) { ApiCalls++; }
        public void LogStateChange(string headline, string detail = null)
        {
            Headlines.Add(headline);
            Details.Add(detail);
        }
    }
}
