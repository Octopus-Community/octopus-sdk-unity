using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// The sample must initialise the SDK against the backend its demo keys live on. Without a host
/// the native SDKs target production, where the demo key resolves to a community with no
/// bridge-post setup, and every Bridge preset fails with "Bridge posts are not configured for this
/// community".
/// </summary>
public class OctopusSampleApiServerHostTests
{
    private const string PreferenceKey = "OctopusSample.Tests.ApiServerHost";
    private OctopusRecordingScenarioSdk _sdk;
    private OctopusExampleConfig _config;

    [SetUp]
    public void SetUp()
    {
        PlayerPrefs.DeleteKey(PreferenceKey);
        OctopusSampleFeatureToggles.Reset();
        OctopusSampleLog.Current = OctopusSampleLog.None;
    }

    [TearDown]
    public void TearDown()
    {
        OctopusScenarioSdk.Use(null, null);
        OctopusSampleFeatureToggles.Reset();
        PlayerPrefs.DeleteKey(PreferenceKey);
        PlayerPrefs.Save();
        if (_config != null) Object.DestroyImmediate(_config);
    }

    [Test]
    public void InitializeTargetsTheDemoBackendByDefault()
    {
        Use(new OctopusExampleConfig.ExampleProfile { apiKey = "not-a-real-key" });

        Initialize();

        Assert.AreEqual("api-demo2.8pus.io", InitializeHost(),
            "An empty host sends the demo key to production, where bridge posts are not configured.");
    }

    [Test]
    public void InitializeUsesTheProfileHostOverride()
    {
        Use(new OctopusExampleConfig.ExampleProfile { apiKey = "not-a-real-key", apiServerHost = "  api.example.test " });

        Initialize();

        Assert.AreEqual("api.example.test", InitializeHost());
    }

    [Test]
    public void ConfigProfilesCarryTheDemoHostWhenTheAssetLeavesItEmpty()
    {
        _config = ScriptableObject.CreateInstance<OctopusExampleConfig>();

        Assert.AreEqual(OctopusExampleConfig.DemoApiServerHost, _config.ApiServerHost);
        Assert.AreEqual(OctopusExampleConfig.DemoApiServerHost, _config.Default.apiServerHost);
        Assert.AreEqual(OctopusExampleConfig.DemoApiServerHost, _config.ForcedLogin.apiServerHost);
        Assert.AreEqual(OctopusExampleConfig.DemoApiServerHost, _config.ManagedFields.apiServerHost);
    }

    [Test]
    public void ConfigProfilesCarryTheAssetHostOverride()
    {
        _config = ScriptableObject.CreateInstance<OctopusExampleConfig>();
        var serialized = new SerializedObject(_config);
        serialized.FindProperty("apiServerHost").stringValue = "api.example.test";
        serialized.ApplyModifiedPropertiesWithoutUndo();

        Assert.AreEqual("api.example.test", _config.Default.apiServerHost);
        Assert.AreEqual("api.example.test", _config.ManagedFields.apiServerHost);
    }

    [Test]
    public void HomeNamesTheHostInitializeReceives()
    {
        Use(new OctopusExampleConfig.ExampleProfile { apiKey = "not-a-real-key" });

        var lines = OctopusSampleHomeView.ConfigurationLines();

        Assert.AreEqual("Server environment", lines[0].Key);
        Assert.AreEqual(OctopusExampleConfig.DemoApiServerHost, lines[0].Value);
    }

    [Test]
    public void HomeNamesTheHostTheLastSwitchLandedOn()
    {
        Use(new OctopusExampleConfig.ExampleProfile { apiKey = "not-a-real-key" });
        Initialize();

        OctopusScenarioSdk.CommunitySwitched(new OctopusExampleConfig.ExampleProfile
            { apiKey = "not-a-real-alternate-key", apiServerHost = "api.example.test" }, 2);

        Assert.AreEqual("api.example.test", OctopusSampleHomeView.ConfigurationLines()[0].Value,
            "Home kept showing the startup host after a switch (#391).");
    }

    [Test]
    public void StopForgetsTheRunningHost()
    {
        Use(new OctopusExampleConfig.ExampleProfile { apiKey = "not-a-real-key" });
        Initialize();
        Assert.AreEqual(OctopusExampleConfig.DemoApiServerHost, OctopusScenarioSdk.RunningServerHost);

        OctopusScenarioSdk.LifecycleStopped();

        Assert.IsNull(OctopusScenarioSdk.RunningServerHost);
    }

    private void Use(OctopusExampleConfig.ExampleProfile profile)
    {
        _sdk = new OctopusRecordingScenarioSdk { Profile = profile };
        OctopusScenarioSdk.Use(_sdk, PreferenceKey);
    }

    private static void Initialize()
    {
        string reason;
        Assert.IsNotNull(OctopusScenarioSdk.EnsurePilotInitialized(out reason), reason);
    }

    private string InitializeHost()
    {
        foreach (var call in _sdk.Calls)
            if (call.Method == "Initialize") return (string)call.Args[2];
        Assert.Fail("Initialize was not called.");
        return null;
    }
}
