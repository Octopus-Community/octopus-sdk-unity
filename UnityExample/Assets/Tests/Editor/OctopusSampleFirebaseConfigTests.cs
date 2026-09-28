using System;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class OctopusSampleFirebaseConfigTests
{
    [SetUp]
    public void SetUp() => OctopusSampleFirebaseConfig.Reset();

    [TearDown]
    public void TearDown() => OctopusSampleFirebaseConfig.Reset();

    [Test]
    public void MissingConfigIsOneWarningNotAnError()
    {
        OctopusSampleFirebaseConfig.Probe = () => false;
        LogAssert.Expect(LogType.Warning, OctopusSampleFirebaseConfig.MissingConfigWarning);

        Assert.IsFalse(OctopusSampleFirebaseConfig.IsConfigured());
        LogAssert.NoUnexpectedReceived();
    }

    [Test]
    public void MissingConfigWarnsOncePerProcess()
    {
        var probes = 0;
        OctopusSampleFirebaseConfig.Probe = () => { probes++; return false; };
        LogAssert.Expect(LogType.Warning, OctopusSampleFirebaseConfig.MissingConfigWarning);

        Assert.IsFalse(OctopusSampleFirebaseConfig.IsConfigured());
        Assert.IsFalse(OctopusSampleFirebaseConfig.IsConfigured());
        Assert.AreEqual(1, probes);
        LogAssert.NoUnexpectedReceived();
    }

    [Test]
    public void PresentConfigLogsNothing()
    {
        OctopusSampleFirebaseConfig.Probe = () => true;

        Assert.IsTrue(OctopusSampleFirebaseConfig.IsConfigured());
        LogAssert.NoUnexpectedReceived();
    }

    [Test]
    public void ProbeFailureLetsFirebaseReportTheRealError()
    {
        OctopusSampleFirebaseConfig.Probe = () => throw new InvalidOperationException("no activity");

        Assert.IsTrue(OctopusSampleFirebaseConfig.IsConfigured());
        LogAssert.NoUnexpectedReceived();
    }

    [Test]
    public void EditorCountsAsConfigured()
    {
        Assert.IsTrue(OctopusSampleFirebaseConfig.IsConfigured());
    }
}
