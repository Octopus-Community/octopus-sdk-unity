using System.Collections.Generic;
using System.Threading;
using NUnit.Framework;

/// <summary>
/// Covers the half of a scenario preset that `OctopusScenarioPilotTests` deliberately cannot: the
/// <see cref="OctopusScenarioPreset.Run"/> that talks to the SDK.
///
/// Those tests observe the sample's own log seam, i.e. the calls a pilot SAYS it makes. These
/// observe <see cref="IOctopusScenarioSdk"/>, i.e. the entry point it actually reaches, with the
/// values the preset filled in — the two can drift, and only this side is the contract a QA pass
/// depends on.
///
/// Two mechanics make that testable without an initialised SDK:
/// - <see cref="OctopusScenarioSdk.Use"/> installs <see cref="OctopusRecordingScenarioSdk"/> and
///   resets the process-wide "already initialised / operation in flight" caches, so each test
///   starts from a cold sample.
/// - <see cref="DrainableSynchronizationContext"/> replaces the editor's synchronization context
///   for the duration of a test, so the continuation of a deferred `await` runs when the test
///   drains it rather than on some later editor tick. `ConnectAsync` is `async void`: without a
///   context under our control, "the slot is released after the call completes" would be a
///   timing assertion.
/// </summary>
public class OctopusScenarioSdkFacadeTests
{
    // Placeholder values only. The real ones live in the gitignored OctopusExampleConfig asset;
    // nothing here may look like a credential — `UnityExample/` is mirrored publicly and
    // ci/mirror-export-guard scans the archive for credential shapes.
    private const string ApiKey = "qa-example-api-key";
    private const string UserId = "qa-example-user";
    private const string Nickname = "QA example user";
    private const string Bio = "QA example bio";
    private const string Picture = "https://example.invalid/avatar.png";
    private const string AuthToken = "not-a-real-token";

    private OctopusRecordingScenarioSdk _sdk;
    private DrainableSynchronizationContext _context;
    private SynchronizationContext _previousContext;

    [SetUp]
    public void SetUp()
    {
        _sdk = new OctopusRecordingScenarioSdk { Profile = NewProfile() };
        OctopusScenarioSdk.Use(_sdk);

        _previousContext = SynchronizationContext.Current;
        _context = new DrainableSynchronizationContext();
        SynchronizationContext.SetSynchronizationContext(_context);
    }

    [TearDown]
    public void TearDown()
    {
        SynchronizationContext.SetSynchronizationContext(_previousContext);
        // Restores the live SDK and clears the process-wide caches, so nothing here leaks into
        // another fixture (`OctopusScenarioPilotTests` asserts the sample starts uninitialised).
        OctopusScenarioSdk.Use(null);
        OctopusSampleLog.Current = OctopusSampleLog.None;
    }

    [Test]
    public void TheFirstPresetTapInitialisesOnceInTheSharedSsoMode()
    {
        var pilot = new ConnectionScenario();

        Tap(pilot, 0);
        Tap(pilot, 0);

        CollectionAssert.AreEqual(new[] { "Initialize", "ConnectUser", "ConnectUser" }, _sdk.ScenarioMethods);
        var initialize = _sdk.Calls[0];
        Assert.AreEqual(ApiKey, initialize.Args[0]);
        Assert.AreEqual("sso", ((ConnectionMode)initialize.Args[1]).Mode);
        Assert.IsTrue(OctopusScenarioSdk.IsInPilotMode);
    }

    [Test]
    public void EveryConnectPresetReachesConnectUserWithTheConfiguredProfile()
    {
        var pilot = new ConnectionScenario();

        // Warm the one-per-process initialisation, so the loop below sees the ConnectUser call on
        // its own. That the first tap initialises is asserted by the test above.
        Tap(pilot, 0);

        // All presets use the configured identity; their provider carries the token claims.
        for (var index = 0; index < 4; index++)
        {
            _sdk.Clear();

            Tap(pilot, index);

            CollectionAssert.AreEqual(new[] { "ConnectUser" }, _sdk.ScenarioMethods,
                "Preset " + (index + 1) + " of `connection` reached " + Describe(_sdk));
            var call = _sdk.Last;
            Assert.AreEqual(UserId, call.Args[0]);
            Assert.AreEqual(Nickname, call.Args[1]);
            Assert.AreEqual(Bio, call.Args[2]);
            Assert.AreEqual(Picture, call.Args[3]);
            Assert.AreEqual(true, call.Args[4], "No SSO token provider was passed.");
        }
    }

    [Test]
    public void StaticTokenFallbackExplainsThatPresetEntitlementsWereNotSent()
    {
        var pilot = new ConnectionScenario();

        Tap(pilot, 0);
        StringAssert.DoesNotContain("NOT sent", pilot.Result);

        for (var index = 1; index < 4; index++)
        {
            Tap(pilot, index);
            StringAssert.Contains("NOT sent", pilot.Result,
                "Preset " + (index + 1) + " uses a static token and must say so: " + pilot.Result);
        }
    }

    [Test]
    public void ConnectPresetsSendTheNativeSampleEntitlementIds()
    {
        var config = UnityEngine.ScriptableObject.CreateInstance<OctopusExampleConfig>();
        try
        {
            config.ssoTokenSecret = "unit-test-only-secret";
            typeof(OctopusExampleConfig).GetField("defaultProfile", System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance).SetValue(config, _sdk.Profile);
            _sdk.Profile = config.Default;
        }
        finally { UnityEngine.Object.DestroyImmediate(config); }
        _sdk.Profile.authToken = "";
        var pilot = new ConnectionScenario();
        var expected = new[]
        {
            new string[0], new[] { "customer:premium" }, new[] { "customer:moderator" },
            new[] { "customer:premium", "customer:moderator" }
        };
        for (var index = 0; index < expected.Length; index++)
        {
            Tap(pilot, index);
            Assert.NotNull(_sdk.LastTokenProvider);
            var token = _sdk.LastTokenProvider().GetAwaiter().GetResult();
            var payload = UnityEngine.JsonUtility.FromJson<Claims>(OctopusSampleTokenProviderTests.Decode(token.Split('.')[1]));
            CollectionAssert.AreEqual(expected[index], payload.entitlements ?? new string[0]);
            Assert.AreEqual(UserId, payload.sub);
            StringAssert.DoesNotContain("NOT sent", pilot.Result);
        }
    }

    [System.Serializable]
    private class Claims
    {
        public string sub = null;
        public string[] entitlements = null;
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    public void MissingSsoCredentialsRefuseEveryConnectPreset(int index)
    {
        _sdk.Profile.authToken = "";
        var pilot = new ConnectionScenario();
        Tap(pilot, index);
        CollectionAssert.DoesNotContain(_sdk.ScenarioMethods, "ConnectUser");
        StringAssert.Contains("ssoTokenSecret or authToken", pilot.Result);
        string busy;
        Assert.IsTrue(OctopusScenarioSdk.TryBeginOperation("RefusalCheck", out busy));
        OctopusScenarioSdk.EndOperation(OctopusScenarioSdk.OperationToken);
    }

    [Test]
    public void TheDisconnectPresetReachesDisconnectUserAndNothingElse()
    {
        var pilot = new ConnectionScenario();

        Tap(pilot, 4);

        CollectionAssert.AreEqual(new[] { "Initialize", "DisconnectUser" }, _sdk.ScenarioMethods);
    }

    [Test]
    public void TheCustomEventsPresetsReachTrackWithTheFilledValues()
    {
        var pilot = new CustomEventsScenario();

        Tap(pilot, 0);
        var noProps = _sdk.Last;
        Assert.AreEqual("Track", noProps.Method);
        Assert.AreEqual("qa_sample_event", noProps.Args[0]);
        var empty = (IDictionary<string, string>)noProps.Args[1];
        Assert.IsNotNull(empty, "Track must receive an empty dictionary, never null.");
        Assert.AreEqual(0, empty.Count);

        _sdk.Clear();
        Tap(pilot, 1);
        CollectionAssert.AreEqual(new[] { "Track" }, _sdk.ScenarioMethods);
        var withProps = (IDictionary<string, string>)_sdk.Last.Args[1];
        Assert.AreEqual(3, withProps.Count);
        Assert.AreEqual("59.90", withProps["price"]);
        Assert.AreEqual("USD", withProps["currency"]);
        Assert.AreEqual("unity-sample", withProps["source"]);
    }

    [Test]
    public void TheLocalePresetsReachOverrideDefaultLocaleWithTheFilledCode()
    {
        var pilot = new LocaleScenario();

        Tap(pilot, 0);
        Assert.AreEqual("OverrideDefaultLocale", _sdk.Last.Method);
        Assert.AreEqual("fr", _sdk.Last.Args[0]);

        Tap(pilot, 1);
        Assert.AreEqual("en", _sdk.Last.Args[0]);

        // Preset 3 is "Reset to system": the package has no reset overload, so it overrides with
        // the device's own code. Which code that is depends on the machine — that it is a real,
        // non-empty one is the contract.
        _sdk.Clear();
        Tap(pilot, 2);
        CollectionAssert.AreEqual(new[] { "OverrideDefaultLocale" }, _sdk.ScenarioMethods);
        Assert.IsNotEmpty((string)_sdk.Last.Args[0]);
    }

    [Test]
    public void ThePresetsFillTheFieldTheCallReads()
    {
        // The values the SDK receives come from the fields, not from a second copy inside the
        // pilot: edit the field a preset filled and the next tap sends the edited value.
        var pilot = new LocaleScenario();
        pilot.Presets[0].Fill(pilot.Fields);
        pilot.Fields.Set("locale", "es");

        pilot.Presets[0].Run(pilot.Fields);

        Assert.AreEqual("es", _sdk.Last.Args[0]);
    }

    [Test]
    public void NoPresetReachesTheSdkWhenTheConfigAssetIsMissing()
    {
        _sdk.Profile = null;

        foreach (var pilot in AllPilots())
        {
            // events: its only preset clears the local log and never reaches the SDK.
            // lifecycle: refuses an uninitialised SDK; once initialised, reset/stop need no config
            // — covered by LifecycleResetAndStopReachTheInitializedSdkWithoutAConfig below.
            if (pilot.Id == "events" || pilot.Id == "lifecycle") continue;
            foreach (var preset in pilot.Presets)
            {
                preset.Fill(pilot.Fields);
                preset.Run(pilot.Fields);
                // communityData presets 2, 4 and 5 are local (last-lookup / stop / contract)
                // and never reach the SDK, so they cannot report the missing config.
                var localPreset = pilot.Id == "communityData" &&
                    (preset.TestId == "qa-preset-communityData-2" ||
                     preset.TestId == "qa-preset-communityData-4" ||
                     preset.TestId == "qa-preset-communityData-5");
                // initialScreen presets 8 and 9 open a member's activity by id, which Unity
                // does not expose: they report that unavailability and never reach the SDK,
                // so they cannot report the missing config either.
                localPreset = localPreset || (pilot.Id == "initialScreen" &&
                    (preset.TestId == "qa-preset-initialScreen-8" ||
                     preset.TestId == "qa-preset-initialScreen-9"));
                if (!localPreset)
                    StringAssert.Contains("OctopusExampleConfig", pilot.Result);
            }
        }

        CollectionAssert.IsEmpty(_sdk.ScenarioMethods, "Reached " + Describe(_sdk) + " without a config.");
    }

    [TestCase(1, "Reset")]
    [TestCase(2, "Stop")]
    public void LifecycleResetAndStopReachTheInitializedSdkWithoutAConfig(int presetIndex, string method)
    {
        _sdk.Profile = null;
        OctopusSampleState.ReportInitialized("SSO");
        var pilot = new LifecycleScenario();
        Tap(pilot, presetIndex);
        CollectionAssert.AreEqual(new[] { method }, _sdk.ScenarioMethods);
        StringAssert.Contains("completed", pilot.Result);
    }

    [Test]
    public void StaleInFlightOperationIsReleasedAfterTheTimeout()
    {
        var now = new System.DateTime(2026, 9, 14, 12, 0, 0, System.DateTimeKind.Utc);
        OctopusScenarioSdk.UtcNow = () => now;
        string busy;
        Assert.IsTrue(OctopusScenarioSdk.TryBeginOperation("Lifecycle.stop", out busy));
        Assert.IsFalse(OctopusScenarioSdk.TryBeginOperation("ConnectUser", out busy));
        StringAssert.Contains("Lifecycle.stop", busy);

        now = now.Add(OctopusScenarioSdk.OperationTimeout);
        Assert.IsTrue(OctopusScenarioSdk.TryBeginOperation("ConnectUser", out busy),
            "a callback that never fires must not block the sample until restart");
        Assert.IsNull(busy);
        OctopusScenarioSdk.ResetOperationSlot();
    }

    [Test]
    public void ALateCallbackOfATimedOutOperationDoesNotReleaseItsSuccessor()
    {
        var now = new System.DateTime(2026, 9, 14, 12, 0, 0, System.DateTimeKind.Utc);
        OctopusScenarioSdk.UtcNow = () => now;
        string busy;
        Assert.IsTrue(OctopusScenarioSdk.TryBeginOperation("Lifecycle.stop", out busy));
        var staleToken = OctopusScenarioSdk.OperationToken;

        now = now.Add(OctopusScenarioSdk.OperationTimeout);
        Assert.IsTrue(OctopusScenarioSdk.TryBeginOperation("ConnectUser", out busy));
        var currentToken = OctopusScenarioSdk.OperationToken;
        Assert.AreNotEqual(staleToken, currentToken);

        OctopusScenarioSdk.EndOperation(staleToken);
        Assert.IsFalse(OctopusScenarioSdk.TryBeginOperation("DisconnectUser", out busy),
            "the stale Lifecycle.stop callback must not free the slot ConnectUser now owns");
        StringAssert.Contains("ConnectUser", busy);

        OctopusScenarioSdk.EndOperation(currentToken);
        Assert.IsTrue(OctopusScenarioSdk.TryBeginOperation("DisconnectUser", out busy));
        OctopusScenarioSdk.ResetOperationSlot();
    }

    [Test]
    public void ASecondTapIsRefusedWhileTheFirstCallIsStillInFlight()
    {
        _sdk.DeferCompletions = true;
        var pilot = new ConnectionScenario();

        Tap(pilot, 0);
        Assert.IsNotNull(_sdk.Pending, "ConnectUser should still be in flight.");
        CollectionAssert.AreEqual(new[] { "Initialize", "ConnectUser" }, _sdk.ScenarioMethods);

        // Second tap, on the preset that makes the OTHER call: the package keeps one completion
        // source per call, so letting this through would complete the wrong task.
        Tap(pilot, 4);

        CollectionAssert.AreEqual(new[] { "Initialize", "ConnectUser" }, _sdk.ScenarioMethods,
            "The second tap reached the SDK: " + Describe(_sdk));
        StringAssert.Contains("ConnectUser", pilot.Result);
        StringAssert.Contains("DisconnectUser", pilot.Result);
        StringAssert.Contains("still running", pilot.Result);

        // …and the slot is released once the first call completes, so the screen is usable again.
        _sdk.CompletePending();
        _context.Drain();
        StringAssert.Contains("completed", pilot.Result);

        Tap(pilot, 4);
        CollectionAssert.AreEqual(new[] { "Initialize", "ConnectUser", "DisconnectUser" }, _sdk.ScenarioMethods);
    }

    [Test]
    public void TheSerialisationSlotIsReleasedWhenTheCallFails()
    {
        _sdk.DeferCompletions = true;
        var pilot = new ConnectionScenario();

        Tap(pilot, 0);
        _sdk.FailPending("bridge unavailable");
        _context.Drain();

        StringAssert.Contains("ConnectUser failed", pilot.Result);
        StringAssert.Contains("bridge unavailable", pilot.Result);

        // A failure that left the slot taken would refuse every later tap for the rest of the run.
        _sdk.Clear();
        Tap(pilot, 4);
        CollectionAssert.AreEqual(new[] { "DisconnectUser" }, _sdk.ScenarioMethods,
            "The slot was not released by the failure: " + pilot.Result);
    }

    private static void Tap(OctopusScenarioPilot pilot, int presetIndex)
    {
        var preset = pilot.Presets[presetIndex];
        preset.Fill(pilot.Fields);
        preset.Run(pilot.Fields);
    }

    private static string Describe(OctopusRecordingScenarioSdk sdk)
    {
        var rendered = new List<string>();
        foreach (var call in sdk.Calls) rendered.Add(call.ToString());
        return rendered.Count == 0 ? "no SDK call" : string.Join(" | ", rendered.ToArray());
    }

    private static List<OctopusScenarioPilot> AllPilots()
    {
        var pilots = new List<OctopusScenarioPilot>();
        foreach (var id in OctopusScenarioPilots.Ids) pilots.Add(OctopusScenarioPilots.Create(id));
        return pilots;
    }

    private static OctopusExampleConfig.ExampleProfile NewProfile()
    {
        return new OctopusExampleConfig.ExampleProfile
        {
            apiKey = ApiKey,
            authToken = AuthToken,
            userId = UserId,
            nickname = Nickname,
            bio = Bio,
            picture = Picture,
        };
    }

    /// <summary>
    /// A synchronization context that queues posted continuations until the test drains them.
    /// </summary>
    private sealed class DrainableSynchronizationContext : SynchronizationContext
    {
        private readonly Queue<KeyValuePair<SendOrPostCallback, object>> _queue =
            new Queue<KeyValuePair<SendOrPostCallback, object>>();

        public override void Post(SendOrPostCallback d, object state)
        {
            _queue.Enqueue(new KeyValuePair<SendOrPostCallback, object>(d, state));
        }

        public override void Send(SendOrPostCallback d, object state)
        {
            d(state);
        }

        /// <summary>Runs every queued continuation, including those they queue in turn.</summary>
        public void Drain()
        {
            while (_queue.Count > 0)
            {
                var item = _queue.Dequeue();
                item.Key(item.Value);
            }
        }
    }
}
