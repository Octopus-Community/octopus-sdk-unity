using NUnit.Framework;

/// <summary>
/// Covers the public half of the Debug-console seam only: <see cref="IOctopusSampleLog"/> and
/// <see cref="OctopusSampleLog"/> live in `Assets/Scripts/`, so this file must never reference a
/// `Assets/Debug/` type — it ships to the public mirror exactly as this test file does. The real
/// recorder is covered separately, from inside `Assets/Debug/Tests/Editor/`, which is
/// export-ignored as a unit with the rest of `Debug/` (this file stays clean of `Assets/Debug/`
/// type names on purpose — see `ci/mirror-export-guard/check-mirror-export.sh`'s backstop).
/// </summary>
public class OctopusSampleLogSeamTests
{
    [TearDown]
    public void TearDown()
    {
        // Never leave a real implementation installed for the next test in the same domain.
        OctopusSampleLog.Current = OctopusSampleLog.None;
    }

    [Test]
    public void CurrentDefaultsToTheNoopImplementation()
    {
        Assert.AreSame(OctopusSampleLog.None, OctopusSampleLog.Current);
    }

    [Test]
    public void NoopImplementationLogsSilentlyWithoutThrowing()
    {
        Assert.DoesNotThrow(() => OctopusSampleLog.None.LogApiCall("OctopusSDK.Open", "screen=Home"));
        Assert.DoesNotThrow(() => OctopusSampleLog.None.LogApiCall("OctopusSDK.Open"));
    }

    [Test]
    public void CurrentIsSettableByAnyImplementation()
    {
        var probe = new ProbeLog();
        OctopusSampleLog.Current = probe;

        OctopusSampleLog.Current.LogApiCall("OctopusSDK.Track", "eventName=test");

        Assert.AreEqual(1, probe.Calls.Count);
        Assert.AreEqual("OctopusSDK.Track", probe.Calls[0].method);
        Assert.AreEqual("eventName=test", probe.Calls[0].detail);
    }

    private sealed class ProbeLog : IOctopusSampleLog
    {
        public readonly System.Collections.Generic.List<(string method, string detail)> Calls =
            new System.Collections.Generic.List<(string, string)>();

        public void LogApiCall(string method, string detail = null) => Calls.Add((method, detail));

        public readonly System.Collections.Generic.List<(string headline, string detail)> States =
            new System.Collections.Generic.List<(string, string)>();

        public void LogStateChange(string headline, string detail = null) =>
            States.Add((headline, detail));
    }
}
