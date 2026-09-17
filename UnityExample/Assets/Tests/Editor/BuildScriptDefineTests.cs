using System;
using System.Reflection;
using NUnit.Framework;

public class BuildScriptDefineTests
{
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

    [Test]
    public void StoreBuildFollowingInternalBuildRemovesTheSymbol()
    {
        var internalDefines = WithInternalDefine("A;B", "true");
        var storeDefines = WithInternalDefine(internalDefines, null);
        Assert.AreEqual("A;B", storeDefines);
        Assert.AreEqual(internalDefines, WithInternalDefine(storeDefines, "true"));
    }
}
