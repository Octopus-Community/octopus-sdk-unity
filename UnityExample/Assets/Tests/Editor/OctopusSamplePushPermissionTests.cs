using System;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// Covers <see cref="OctopusSamplePushPermission"/> (#233): the OS notification prompt waits for
/// an initialised SDK with Push registration on, as in the Android sample, instead of appearing
/// over the first screen at launch.
/// </summary>
public class OctopusSamplePushPermissionTests
{
    private int _requests;

    [SetUp]
    public void SetUp()
    {
        OctopusSampleLog.Current = OctopusSampleLog.None;
        OctopusSamplePushPermission.Reset();
        OctopusSamplePushRegistration.Reset();
        OctopusSampleFeatureToggles.Reset();
        OctopusSampleState.Reset();
        _requests = 0;
    }

    [TearDown]
    public void TearDown()
    {
        OctopusSamplePushPermission.Reset();
        OctopusSamplePushRegistration.Reset();
        OctopusSampleFeatureToggles.Reset();
        OctopusSampleState.Reset();
    }

    [Test]
    public void ColdLaunchDoesNotAskUntilTheSdkIsInitialized()
    {
        OctopusSamplePushPermission.RequestWhenReady(Count);
        Assert.AreEqual(0, _requests, "The prompt fired before any profile was started.");

        OctopusSampleState.ReportInitialized(OctopusScenarioSdk.PilotModeLabel);
        Assert.AreEqual(1, _requests);
        Assert.IsTrue(OctopusSamplePushPermission.Requested);
    }

    [Test]
    public void AlreadyInitializedAsksImmediately()
    {
        OctopusSampleState.ReportInitialized(OctopusScenarioSdk.PilotModeLabel);
        OctopusSamplePushPermission.RequestWhenReady(Count);
        Assert.AreEqual(1, _requests);
    }

    [Test]
    public void PushRegistrationOffDefersTheRequestUntilItIsTurnedOn()
    {
        OctopusSampleFeatureToggles.SetPushRegistration(false);
        OctopusSamplePushPermission.RequestWhenReady(Count);
        OctopusSampleState.ReportInitialized(OctopusScenarioSdk.PilotModeLabel);
        Assert.AreEqual(0, _requests, "A device whose tokens stay in the app was asked anyway.");

        OctopusSampleFeatureToggles.SetPushRegistration(true);
        Assert.AreEqual(1, _requests);
    }

    [Test]
    public void AsksAtMostOncePerProcess()
    {
        OctopusSamplePushPermission.RequestWhenReady(Count);
        OctopusSampleState.ReportInitialized(OctopusScenarioSdk.PilotModeLabel);
        OctopusSampleState.ReportUnseenNotifications(3);
        OctopusSampleFeatureToggles.SetPushRegistration(false);
        OctopusSampleFeatureToggles.SetPushRegistration(true);
        OctopusSamplePushPermission.RequestWhenReady(Count);
        Assert.AreEqual(1, _requests);
    }

    [Test]
    public void ARequestThatThrowsIsRetriedOnTheNextStateChange()
    {
        var attempts = 0;
        LogAssert.Expect(LogType.Warning, new Regex("notification permission request failed"));
        OctopusSamplePushPermission.RequestWhenReady(() =>
        {
            attempts++;
            if (attempts == 1) throw new InvalidOperationException("no plugin");
        });
        OctopusSampleState.ReportInitialized(OctopusScenarioSdk.PilotModeLabel);
        Assert.AreEqual(1, attempts);
        Assert.IsFalse(OctopusSamplePushPermission.Requested,
            "A request that never reached the OS was counted as made and dropped.");

        OctopusSampleState.ReportUnseenNotifications(1);
        Assert.AreEqual(2, attempts);
        Assert.IsTrue(OctopusSamplePushPermission.Requested);

        OctopusSampleState.ReportUnseenNotifications(2);
        Assert.AreEqual(2, attempts, "A request that went through was made again.");
    }

    [Test]
    public void IosProjectSettingDoesNotRequestAuthorizationAtAppLaunch()
    {
        // The com.unity.mobile.notifications setting prompts natively before any sample code runs,
        // which is what #233 saw; the sample's own request above is the only one.
        var asset = File.ReadAllText(Path.Combine("ProjectSettings", "NotificationsSettings.asset"));
        var keysAt = asset.IndexOf("\"m_iOSNotificationSettingsValues\"", System.StringComparison.Ordinal);
        Assert.GreaterOrEqual(keysAt, 0);
        var ios = asset.Substring(keysAt);
        var keys = Section(ios, "\"m_Keys\"");
        var values = Section(ios, "\"m_Values\"");
        var index = System.Array.IndexOf(keys, "UnityNotificationRequestAuthorizationOnAppLaunch");
        Assert.GreaterOrEqual(index, 0);
        Assert.AreEqual("False", values[index]);
        index = System.Array.IndexOf(keys, "UnityNotificationRequestAuthorizationForRemoteNotificationsOnAppLaunch");
        Assert.GreaterOrEqual(index, 0);
        Assert.AreEqual("False", values[index]);
    }

    private void Count() { _requests++; }

    private static string[] Section(string json, string name)
    {
        var start = json.IndexOf('[', json.IndexOf(name, System.StringComparison.Ordinal)) + 1;
        var end = json.IndexOf(']', start);
        var items = json.Substring(start, end - start).Split(',');
        for (var i = 0; i < items.Length; i++) items[i] = items[i].Trim().Trim('"');
        return items;
    }
}
