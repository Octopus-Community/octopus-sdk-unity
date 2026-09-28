using System;
using System.Reflection;
using NUnit.Framework;

// Covers the one decision that turns a TestFlight upload into a rejection: `aps-environment`.
// The helper lives outside `#if UNITY_IOS` precisely so this suite runs on any active build
// target — see IOSPushEntitlements.
public class IOSPushEntitlementsTests
{
    private static bool UseDevelopmentEnvironment(string environmentValue)
    {
        // An asmdef cannot reference Unity's predefined Assembly-CSharp-Editor assembly.
        // Resolve the real helper at runtime; never copy its implementation into tests.
        var type = Type.GetType("IOSPushEntitlements, Assembly-CSharp-Editor");
        Assert.IsNotNull(type, "IOSPushEntitlements is missing from Assembly-CSharp-Editor.");
        var method = type.GetMethod("UseDevelopmentEnvironment", BindingFlags.Public | BindingFlags.Static);
        Assert.IsNotNull(method, "The pure aps-environment helper is missing or renamed.");
        try
        {
            return (bool)method.Invoke(null, new object[] { environmentValue });
        }
        catch (TargetInvocationException e)
        {
            throw e.InnerException;
        }
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    [TestCase("development")]
    [TestCase("Development")]
    [TestCase("DEVELOPMENT")]
    [TestCase(" development ")]
    public void DefaultsToDevelopmentSoALocalExportStaysSignable(string environmentValue)
    {
        Assert.IsTrue(UseDevelopmentEnvironment(environmentValue));
    }

    [TestCase("production")]
    [TestCase("Production")]
    [TestCase("PRODUCTION")]
    [TestCase(" production ")]
    public void OptsIntoProductionForAStoreExport(string environmentValue)
    {
        Assert.IsFalse(UseDevelopmentEnvironment(environmentValue));
    }

    [TestCase("prod")]
    [TestCase("dev")]
    [TestCase("true")]
    [TestCase("1")]
    [TestCase("productionn")]
    public void RefusesAnUnknownValueRatherThanGuessing(string environmentValue)
    {
        var error = Assert.Throws<InvalidOperationException>(
            () => UseDevelopmentEnvironment(environmentValue));
        StringAssert.Contains("OCTOPUS_IOS_APS_ENVIRONMENT", error.Message);
    }

    [Test]
    public void NamesTheVariableThePublishScriptExports()
    {
        var type = Type.GetType("IOSPushEntitlements, Assembly-CSharp-Editor");
        Assert.IsNotNull(type, "IOSPushEntitlements is missing from Assembly-CSharp-Editor.");
        var field = type.GetField("EnvironmentVariable", BindingFlags.Public | BindingFlags.Static);
        Assert.IsNotNull(field, "The environment variable name constant is missing or renamed.");
        Assert.AreEqual("OCTOPUS_IOS_APS_ENVIRONMENT", field.GetValue(null));
    }
}
