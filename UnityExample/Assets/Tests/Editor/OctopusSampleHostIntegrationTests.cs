using System;
using NUnit.Framework;
using UnityEngine;

public class OctopusSampleHostIntegrationTests
{
    private string _previousPrefix;
    private Func<string, UrlOpeningStrategy> _previousHandler;

    [SetUp]
    public void SetUp()
    {
        _previousPrefix = OctopusSampleHostIntegration.Prefix;
        _previousHandler = OctopusSDK.NavigateToUrlHandler;
        OctopusSampleHostIntegration.Prefix = "OctopusSample.Tests.Host." + Guid.NewGuid().ToString("N");
        OctopusScenarioSdk.Use(new OctopusRecordingScenarioSdk
        {
            Profile = new OctopusExampleConfig.ExampleProfile { apiKey = "test-key" }
        });
        OctopusSampleHostIntegration.Clear();
        OctopusSampleState.ReportInitialized("SSO");
    }

    [TearDown]
    public void TearDown()
    {
        OctopusSampleHostIntegration.Clear();
        OctopusSampleHostIntegration.Prefix = _previousPrefix;
        OctopusScenarioSdk.Use(null);
        OctopusSDK.NavigateToUrlHandler = _previousHandler;
    }

    [Test]
    public void DetachingRestoresThePreviousHandlerAndPreservesANewerOwner()
    {
        Func<string, UrlOpeningStrategy> legacy = url => UrlOpeningStrategy.HandledByApp;
        Func<string, UrlOpeningStrategy> newer = url => UrlOpeningStrategy.HandledByOctopus;
        OctopusSDK.NavigateToUrlHandler = legacy;
        OctopusSampleHostIntegration.Apply();
        Assert.AreNotSame(legacy, OctopusSDK.NavigateToUrlHandler);
        OctopusSampleHostIntegration.Detach();
        Assert.AreSame(legacy, OctopusSDK.NavigateToUrlHandler);
        OctopusSampleHostIntegration.Apply();
        OctopusSDK.NavigateToUrlHandler = newer;
        OctopusSampleHostIntegration.Detach();
        Assert.AreSame(newer, OctopusSDK.NavigateToUrlHandler);
    }

    [Test]
    public void GroupScenarioOwnershipEndsWhenThePilotDisposes()
    {
        var pilot = new GroupAccessDeniedScenario();
        try
        {
            pilot.Presets[0].Fill(pilot.Fields);
            pilot.Presets[0].Run(pilot.Fields);
            Assert.IsTrue(GroupAccessDeniedScenario.OwnsCallback);
            typeof(OctopusSampleHostIntegration).GetMethod("GroupDenied",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
                .Invoke(null, new object[] { "test-group" });
            Assert.IsNull(OctopusSampleHostIntegration.LastCallback, "The host must leave this callback to the scenario.");
        }
        finally { pilot.Dispose(); }
        Assert.IsFalse(GroupAccessDeniedScenario.OwnsCallback);
    }
}
