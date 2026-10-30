using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Build;

public class BuildScriptDefineTests
{
    [TestCase(null, false)]
    [TestCase("", false)]
    [TestCase(" \t\n", true)]
    [TestCase("unit-test-only-secret", true)]
    public void ReleaseBuildRefusesEveryNonEmptySsoTokenSecret(string secret, bool shouldRefuse)
    {
        var type = Type.GetType("BuildScript, Assembly-CSharp-Editor");
        Assert.IsNotNull(type, "BuildScript is missing from Assembly-CSharp-Editor.");
        var method = type.GetMethod("RequireNoSsoTokenSecret", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.IsNotNull(method, "The release signing-secret guard is missing or renamed.");
        if (shouldRefuse)
        {
            var error = Assert.Throws<TargetInvocationException>(() => method.Invoke(null, new object[] { secret }));
            Assert.IsInstanceOf<BuildFailedException>(error.InnerException);
            StringAssert.Contains("Clear it in OctopusExampleConfig", error.InnerException.Message);
            StringAssert.DoesNotContain(secret, error.InnerException.Message);
        }
        else
        {
            Assert.DoesNotThrow(() => method.Invoke(null, new object[] { secret }));
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void BuildSnapshotRestoresExactBytesIncludingAfterFailure(bool fail)
    {
        var path = System.IO.Path.GetTempFileName();
        var original = new byte[] { 0, 1, 13, 10, 255, 42 };
        try
        {
            System.IO.File.WriteAllBytes(path, original);
            var type = Type.GetType("BuildScript, Assembly-CSharp-Editor");
            var method = type.GetMethod("WithFileSnapshot", BindingFlags.NonPublic | BindingFlags.Static);
            Action mutate = () =>
            {
                System.IO.File.WriteAllText(path, "mutated settings");
                if (fail) throw new InvalidOperationException("test build failure");
            };
            if (fail) Assert.Throws<TargetInvocationException>(() => method.Invoke(null, new object[] { path, mutate }));
            else Assert.DoesNotThrow(() => method.Invoke(null, new object[] { path, mutate }));
            CollectionAssert.AreEqual(original, System.IO.File.ReadAllBytes(path));
        }
        finally { System.IO.File.Delete(path); }
    }

    [Test]
    public void SdkVersionIsSafeForTheIosMarketingVersion()
    {
        StringAssert.IsMatch(@"^[0-9]+(\.[0-9]+){0,2}$", OctopusSDK.Version);
    }

    [TestCase(BuildTarget.iOS, true)]
    [TestCase(BuildTarget.Android, false)]
    [TestCase(BuildTarget.StandaloneOSX, false)]
    public void ActiveIosTargetGuardAcceptsOnlyIos(BuildTarget target, bool expected)
    {
        var type = Type.GetType("BuildScript, Assembly-CSharp-Editor");
        var method = type.GetMethod("IsIOSTarget", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.AreEqual(expected, method.Invoke(null, new object[] { target }));
    }

    [TestCase("defaultProfile")]
    [TestCase("forcedLoginProfile")]
    [TestCase("managedFieldsProfile")]
    [TestCase("someManagedFieldsProfile")]
    [TestCase("ssoTokenSecret")]
    public void EveryCredentialIsRefusedOutsideDevelopmentBuilds(string field)
    {
        var config = UnityEngine.ScriptableObject.CreateInstance<OctopusExampleConfig>();
        try
        {
            if (field == "ssoTokenSecret") config.ssoTokenSecret = "unit-test-only-secret";
            else typeof(OctopusExampleConfig).GetField(field, BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(config, new OctopusExampleConfig.ExampleProfile { authToken = "not-a-real-token" });
            var type = Type.GetType("OctopusSampleReleaseCredentialsGuard, Assembly-CSharp-Editor");
            Assert.IsNotNull(type);
            Assert.IsTrue(typeof(IPreprocessBuildWithReport).IsAssignableFrom(type));
            var validate = type.GetMethod("Validate");
            Assert.DoesNotThrow(() => validate.Invoke(null, new object[] { config, BuildOptions.Development }));
            var error = Assert.Throws<TargetInvocationException>(() =>
                validate.Invoke(null, new object[] { config, BuildOptions.None }));
            Assert.IsInstanceOf<BuildFailedException>(error.InnerException);
            StringAssert.DoesNotContain("not-a-real-token", error.InnerException.Message);
            StringAssert.DoesNotContain("unit-test-only-secret", error.InnerException.Message);
        }
        finally { UnityEngine.Object.DestroyImmediate(config); }
    }

    private static string WithInternalDefine(string defines, string environmentValue)
    {
        // An asmdef cannot reference Unity's predefined Assembly-CSharp-Editor assembly.
        // Resolve the real build helper at runtime; never copy its implementation into tests.
        var type = Type.GetType("BuildScript, Assembly-CSharp-Editor");
        Assert.IsNotNull(type, "BuildScript is missing from Assembly-CSharp-Editor.");
        var method = type.GetMethod("WithInternalDefine", BindingFlags.Public | BindingFlags.Static);
        Assert.IsNotNull(method, "The pure build define helper is missing or renamed.");
        return (string)method.Invoke(null, new object[] { defines, environmentValue });
    }

    [TestCase(null, "true", "OCTOPUS_INTERNAL")]
    [TestCase("", "TRUE", "OCTOPUS_INTERNAL")]
    [TestCase("A;B", "TrUe", "A;B;OCTOPUS_INTERNAL")]
    [TestCase("A;OCTOPUS_INTERNAL;B", "true", "A;B;OCTOPUS_INTERNAL")]
    [TestCase("OCTOPUS_INTERNAL;OCTOPUS_INTERNAL", "true", "OCTOPUS_INTERNAL")]
    [TestCase("; A ;; B; ", "true", "A;B;OCTOPUS_INTERNAL")]
    [TestCase(null, null, "")]
    [TestCase(";; ;", "false", "")]
    [TestCase("OCTOPUS_INTERNAL", null, "")]
    [TestCase("OCTOPUS_INTERNAL;A", "", "A")]
    [TestCase("A;OCTOPUS_INTERNAL;B", "false", "A;B")]
    [TestCase("A;OCTOPUS_INTERNAL", "FALSE", "A")]
    [TestCase(";OCTOPUS_INTERNAL;;OCTOPUS_INTERNAL;", "1", "")]
    [TestCase("A;OCTOPUS_INTERNAL", " true ", "A")]
    [TestCase("A;B", null, "A;B")]
    [TestCase("OCTOPUS_INTERNAL_EXTRA;MY_OCTOPUS_INTERNAL;octopus_internal;OCTOPUS_INTERNAL",
        "false", "OCTOPUS_INTERNAL_EXTRA;MY_OCTOPUS_INTERNAL;octopus_internal")]
    public void SetsOnlyTheExactSymbolAndIsIdempotent(
        string defines, string environmentValue, string expected)
    {
        var actual = WithInternalDefine(defines, environmentValue);
        Assert.AreEqual(expected, actual);
        Assert.AreEqual(expected, WithInternalDefine(actual, environmentValue));
    }

    [TestCase(null, BuildOptions.Development)]
    [TestCase("", BuildOptions.Development)]
    [TestCase("true", BuildOptions.Development)]
    [TestCase("TRUE", BuildOptions.Development)]
    [TestCase("0", BuildOptions.Development)]
    [TestCase(" false ", BuildOptions.Development)]
    [TestCase("false", BuildOptions.None)]
    [TestCase("False", BuildOptions.None)]
    [TestCase("FALSE", BuildOptions.None)]
    public void AndroidDebugBuildIsDevelopmentUnlessTheEnvironmentSaysFalse(
        string environmentValue, BuildOptions expected)
    {
        var type = Type.GetType("BuildScript, Assembly-CSharp-Editor");
        Assert.IsNotNull(type, "BuildScript is missing from Assembly-CSharp-Editor.");
        var method = type.GetMethod("AndroidDebugBuildOptions", BindingFlags.Public | BindingFlags.Static);
        Assert.IsNotNull(method, "The pure Android debug build options helper is missing or renamed.");
        // Compare values, not names: Mono's Enum.ToString() also lists the obsolete aliases equal to 0.
        Assert.AreEqual(expected, (BuildOptions)method.Invoke(null, new object[] { environmentValue }));
    }

    [Test]
    public void StoreBuildFollowingInternalBuildRemovesTheSymbol()
    {
        var internalDefines = WithInternalDefine("A;B", "true");
        var storeDefines = WithInternalDefine(internalDefines, null);
        Assert.AreEqual("A;B", storeDefines);
        Assert.AreEqual(internalDefines, WithInternalDefine(storeDefines, "true"));
    }
}
