using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

public class OctopusSignInThemeScenarioTests
{
    private OctopusRecordingScenarioSdk _sdk;

    [SetUp]
    public void SetUp()
    {
        _sdk = new OctopusRecordingScenarioSdk
        {
            Profile = new OctopusExampleConfig.ExampleProfile
            {
                apiKey = "qa-example-api-key", userId = "qa-example-user"
            }
        };
        OctopusScenarioSdk.Use(_sdk);
    }

    [TearDown]
    public void TearDown()
    {
        OctopusScenarioSdk.Use(null);
        OctopusSampleLog.Current = OctopusSampleLog.None;
    }

    private static void Tap(OctopusScenarioPilot pilot, int index)
    {
        pilot.Presets[index].Fill(pilot.Fields);
        pilot.Execute(() => pilot.Presets[index].Run(pilot.Fields));
    }

    [TestCase("theme")]
    [TestCase("refreshEntitlements")]
    [TestCase("termsAcceptance")]
    [TestCase("profileFieldsLock")]
    [TestCase("communityData")]
    public void EverySeamCallIsAnnouncedAndFillDoesNotLog(string id)
    {
        var log = new ProbeLog();
        OctopusSampleLog.Current = log;
        var pilot = OctopusScenarioPilots.Create(id);
        foreach (var preset in pilot.Presets) preset.Fill(pilot.Fields);
        Assert.IsEmpty(log.Methods);
        Assert.IsEmpty(_sdk.Calls);
        _sdk.CommunityDataResult = new OctopusCommunityData("fixture-profile");
        for (var i = 0; i < pilot.Presets.Count; i++)
        {
            log.Methods.Clear();
            _sdk.Clear();
            Tap(pilot, i);
            foreach (var method in _sdk.Methods)
            {
                if (method == "ApplyTheme")
                {
                    CollectionAssert.Contains(log.Methods, "OctopusSDK.SetLightColorScheme");
                    CollectionAssert.Contains(log.Methods, "OctopusSDK.SetDarkColorScheme");
                }
                else CollectionAssert.Contains(log.Methods, "OctopusSDK." + method);
            }
        }
        pilot.Dispose();
    }

    private sealed class ProbeLog : IOctopusSampleLog
    {
        public readonly System.Collections.Generic.List<string> Methods = new System.Collections.Generic.List<string>();
        public void LogApiCall(string method, string detail = null) { Methods.Add(method); }
        public void LogStateChange(string headline, string detail = null) { }
    }

    [Test]
    public void ThemeDefaultsClearPreviousSurfaceFontAndLogoOverrides()
    {
        var pilot = new ThemeScenario();
        Tap(pilot, 2);
        Tap(pilot, 4);
        var call = _sdk.Calls.Last(c => c.Method == "ApplyTheme");
        foreach (OctopusColorScheme scheme in call.Args)
        {
            Assert.AreEqual(Color.clear, scheme.Primary);
            Assert.AreEqual(Color.clear, scheme.PrimaryLow);
            Assert.AreEqual(Color.clear, scheme.PrimaryHigh);
            Assert.AreEqual(Color.clear, scheme.OnPrimary);
            Assert.AreEqual(Color.clear, scheme.Background);
            Assert.AreEqual(Color.clear, scheme.Link);
        }
        Assert.IsNull(_sdk.Calls.Last(c => c.Method == "SetLogo").Args[0]);
        Assert.IsNull(_sdk.Calls.Last(c => c.Method == "SetFonts").Args[0]);
    }

    [Test]
    public void TermsResultShowsTheEffectiveConfiguration()
    {
        var pilot = new TermsAcceptanceScenario();
        _sdk.DeferCompletions = true;
        Tap(pilot, 1);
        var config = (OctopusCommunityConfig)Activator.CreateInstance(typeof(OctopusCommunityConfig), true);
        typeof(OctopusCommunityConfig).GetProperty("TermsAcceptanceMode").SetValue(config,
            OctopusTermsAcceptanceMode.ExplicitSingleCheckbox, null);
        _sdk.ConfigResult(config);
        StringAssert.Contains("Applied: ExplicitMultiCheckbox", pilot.Result);
        StringAssert.Contains("Effective TermsAcceptanceMode: ExplicitSingleCheckbox", pilot.Result);
    }

    [TestCase(0, "fetch-client", "current-or-configured")]
    [TestCase(1, "fetch-profile", "last-lookup")]
    [TestCase(2, "observe", "current-or-configured")]
    [TestCase(3, "stop", "observed-member")]
    [TestCase(4, "contract", "local-contract")]
    [TestCase(5, "host-profile", "current-or-configured")]
    public void CommunityDataFillIsPure(int index, string action, string member)
    {
        var pilot = new CommunityDataScenario();
        pilot.Presets[index].Fill(pilot.Fields);
        CollectionAssert.AreEqual(new[] { action, member }, pilot.Fields.All.Select(f => f.Value).ToArray());
        Assert.IsEmpty(_sdk.Calls);
        Assert.AreEqual(0, _sdk.CommunityDataListenerCount);
    }

    [Test]
    public void CommunityDataFetchUsesCurrentClientIdThenTheLearnedProfileId()
    {
        var pilot = new CommunityDataScenario();
        _sdk.CurrentProfile = new OctopusProfile(clientUserId: "fixture-current");
        _sdk.CommunityDataResult = new OctopusCommunityData("fixture-profile", 3, new OctopusGamification(2));
        Tap(pilot, 0);
        Assert.AreEqual("FetchCommunityData", _sdk.Last.Method);
        Assert.AreEqual("fixture-current", ((OctopusCommunityMemberId)_sdk.Last.Args[0]).ClientUserId);
        StringAssert.Contains("messageCount=3", pilot.Result);
        StringAssert.Contains("gamification.level=2", pilot.Result);
        StringAssert.Contains("gamification.score=null", pilot.Result);
        _sdk.Clear();
        Tap(pilot, 1);
        CollectionAssert.AreEqual(new[] { "FetchCommunityData" }, _sdk.ScenarioMethods);
        var member = (OctopusCommunityMemberId)_sdk.Last.Args[0];
        Assert.AreEqual("fixture-profile", member.ProfileId);
        Assert.IsNull(member.ClientUserId);
    }

    [Test]
    public void CommunityDataMissingPriorLookupAndContractNeedNoSdk()
    {
        var pilot = new CommunityDataScenario();
        Tap(pilot, 1);
        StringAssert.Contains("run Preset 1 or 3 first", pilot.Result);
        Tap(pilot, 4);
        StringAssert.Contains("Exactly-one-id contract enforced locally", pilot.Result);
        Assert.IsEmpty(_sdk.Calls);
    }

    [Test]
    public void CommunityDataObservationReplacesAndStopsWithoutLeakingListeners()
    {
        var pilot = new CommunityDataScenario();
        Tap(pilot, 2);
        Assert.AreEqual("StartObservingCommunityData", _sdk.Last.Method);
        Assert.AreEqual("qa-example-user", ((OctopusCommunityMemberId)_sdk.Last.Args[0]).ClientUserId);
        Assert.AreEqual(1, _sdk.CommunityDataListenerCount);
        _sdk.EmitCommunityData(new OctopusCommunityData("fixture-flow", 4));
        StringAssert.Contains("flow(clientUserId)", pilot.Result);
        Tap(pilot, 1);
        Assert.AreEqual("fixture-flow", ((OctopusCommunityMemberId)_sdk.Last.Args[0]).ProfileId);
        _sdk.Clear();
        Tap(pilot, 2);
        CollectionAssert.AreEqual(new[] { "StopObservingCommunityData", "StartObservingCommunityData" }, _sdk.ScenarioMethods);
        Assert.AreEqual(1, _sdk.CommunityDataListenerCount);
        Tap(pilot, 3);
        Assert.AreEqual("StopObservingCommunityData", _sdk.Last.Method);
        Assert.AreEqual(0, _sdk.CommunityDataListenerCount);
        var result = pilot.Result;
        _sdk.EmitCommunityData(new OctopusCommunityData("ignored"));
        Assert.AreEqual(result, pilot.Result);
        _sdk.Clear();
        Tap(pilot, 3);
        Assert.IsEmpty(_sdk.Calls);
        Tap(pilot, 2);
        Action<string> listener = line => { };
        pilot.ResultChanged += listener;
        pilot.ResultChanged -= listener;
        Assert.AreEqual(0, _sdk.CommunityDataListenerCount);
        Assert.AreEqual("StopObservingCommunityData", _sdk.Last.Method);
    }

    [Test]
    public void ANewCommunityPilotTakesOwnershipOfTheSingleNativeObservation()
    {
        var first = new CommunityDataScenario();
        var second = new CommunityDataScenario();
        Tap(first, 2);
        Tap(second, 2);
        Assert.AreEqual(1, _sdk.CommunityDataListenerCount);
        _sdk.Clear();
        first.Dispose();
        Assert.IsEmpty(_sdk.Calls);
        second.Dispose();
        Assert.AreEqual("StopObservingCommunityData", _sdk.Last.Method);
    }

    [Test]
    public void CommunityDataHostDestinationCarriesDataUnknownAndErrorStates()
    {
        var pilot = new CommunityDataScenario();
        OctopusScenarioHostProfile destination = null;
        pilot.HostProfileRequested += request => destination = request;
        _sdk.DeferCompletions = true;
        Tap(pilot, 5);
        Assert.AreEqual("FetchCommunityData", _sdk.Last.Method);
        Assert.IsTrue(destination.IsLoading);
        Assert.AreEqual("qa-example-user", destination.ClientUserId);
        _sdk.FetchResult(new OctopusCommunityData("fixture-profile"));
        Assert.AreEqual("clientProfile-data", destination.ResultTestId);
        Assert.IsFalse(destination.IsLoading);
        Tap(pilot, 5);
        _sdk.FetchResult(null);
        Assert.AreEqual("clientProfile-unknown", destination.ResultTestId);
        Tap(pilot, 5);
        _sdk.FetchError("fixture failure");
        Assert.AreEqual("clientProfile-error", destination.ResultTestId);
        Assert.AreEqual("fixture failure", destination.Error);
        Assert.IsFalse(pilot.IsRunning);
    }

    [Test]
    public void CommunityDataWithoutHostRendererReportsTheNavigationGap()
    {
        var pilot = new CommunityDataScenario();
        Tap(pilot, 5);
        StringAssert.Contains("navigation unavailable", pilot.Result);
    }

    [Test]
    public void CommunityDataLateFetchAfterDisposalDoesNotChangeTheScenarioResult()
    {
        var pilot = new CommunityDataScenario();
        _sdk.DeferCompletions = true;
        Tap(pilot, 0);
        pilot.Dispose();
        var result = pilot.Result;
        _sdk.FetchError("late failure");
        Assert.AreEqual(result, pilot.Result);
    }

    [TestCase(0, "Editable", "Editable", "Editable")]
    [TestCase(1, "ReadOnly", "ReadOnly", "Disabled")]
    [TestCase(2, "ReadOnly", "ReadOnly", "Editable")]
    [TestCase(3, "backend", "backend", "backend")]
    public void ProfileLockFillIsPureAndRunSendsAllFields(int index, string nickname, string avatar, string bio)
    {
        var pilot = new ProfileFieldsLockScenario();
        pilot.Presets[index].Fill(pilot.Fields);
        CollectionAssert.AreEqual(new[] { nickname, avatar, bio }, pilot.Fields.All.Select(f => f.Value).ToArray());
        Assert.IsEmpty(_sdk.Calls);
        Tap(pilot, index);
        CollectionAssert.AreEqual(new[] { "Initialize", "DebugOverrideProfileFieldsLock" }, _sdk.ScenarioMethods);
        var fieldsLock = (OctopusProfileFieldsLock)_sdk.Last.Args[0];
        if (nickname == "backend") Assert.IsNull(fieldsLock);
        else
        {
            Assert.AreEqual(nickname, fieldsLock.Nickname.ToString());
            Assert.AreEqual(avatar, fieldsLock.Avatar.ToString());
            Assert.AreEqual(bio, fieldsLock.Bio.ToString());
        }
        StringAssert.StartsWith("Applied:", pilot.Result);
        Assert.IsFalse(pilot.IsRunning);
    }

    [TestCase(0, "Implicit", OctopusTermsAcceptanceMode.Implicit)]
    [TestCase(1, "ExplicitMultiCheckbox", OctopusTermsAcceptanceMode.ExplicitMultiCheckbox)]
    [TestCase(2, "ExplicitSingleCheckbox", OctopusTermsAcceptanceMode.ExplicitSingleCheckbox)]
    [TestCase(3, "backend", null)]
    public void TermsFillIsPureAndRunOverridesThenReadsConfig(int index, string value,
        OctopusTermsAcceptanceMode? expected)
    {
        var pilot = new TermsAcceptanceScenario();
        pilot.Presets[index].Fill(pilot.Fields);
        Assert.AreEqual(value, pilot.Fields.Get("mode"));
        Assert.IsEmpty(_sdk.Calls);
        Tap(pilot, index);
        CollectionAssert.AreEqual(new[] { "Initialize", "DebugOverrideTermsAcceptanceMode",
            "DebugGetCommunityConfig" }, _sdk.ScenarioMethods);
        Assert.AreEqual(expected, _sdk.Calls.First(c => c.Method == "DebugOverrideTermsAcceptanceMode").Args[0]);
        StringAssert.StartsWith("Applied:", pilot.Result);
        StringAssert.Contains("Effective config unavailable", pilot.Result);
        Assert.IsFalse(pilot.IsRunning);
    }

    [Test]
    public void TermsReadFailureDoesNotClaimTheOverrideFailed()
    {
        var pilot = new TermsAcceptanceScenario();
        _sdk.DeferCompletions = true;
        Tap(pilot, 1);
        _sdk.ConfigError("fixture failure");
        StringAssert.Contains("Applied: ExplicitMultiCheckbox", pilot.Result);
        StringAssert.Contains("Config read failed", pilot.Result);
        Assert.IsFalse(pilot.IsRunning);
    }

    [Test]
    public void RefreshFillIsPureAndProfileStreamCanArriveAfterSuccess()
    {
        var pilot = new RefreshEntitlementsScenario();
        pilot.Presets[0].Fill(pilot.Fields);
        Assert.AreEqual("refresh", pilot.Fields.Get("action"));
        Assert.IsEmpty(_sdk.Calls);
        _sdk.CurrentProfile = new OctopusProfile(new[] { "old" });
        _sdk.DeferCompletions = true;
        Tap(pilot, 0);
        Assert.AreEqual("RefreshEntitlements", _sdk.Last.Method);
        Assert.IsTrue(pilot.IsRunning);
        _sdk.RefreshSuccess();
        Assert.IsFalse(pilot.IsRunning);
        StringAssert.Contains("old", pilot.Result);
        _sdk.EmitProfile(new OctopusProfile(new[] { "premium" }));
        StringAssert.Contains("succeeded", pilot.Result);
        StringAssert.Contains("premium", pilot.Result);
        pilot.Dispose();
        var result = pilot.Result;
        _sdk.EmitProfile(null);
        Assert.AreEqual(result, pilot.Result);
    }

    [TestCase(OctopusRefreshEntitlementsErrorKind.NoClientTokenProvider)]
    [TestCase(OctopusRefreshEntitlementsErrorKind.UserNotConnected)]
    [TestCase(OctopusRefreshEntitlementsErrorKind.NoNetwork)]
    [TestCase(OctopusRefreshEntitlementsErrorKind.UserBanned)]
    [TestCase(OctopusRefreshEntitlementsErrorKind.ServerError)]
    public void RefreshReportsTypedFailures(OctopusRefreshEntitlementsErrorKind kind)
    {
        var pilot = new RefreshEntitlementsScenario();
        _sdk.DeferCompletions = true;
        Tap(pilot, 0);
        _sdk.RefreshError(new OctopusRefreshEntitlementsError(kind, "fixture failure"));
        StringAssert.Contains(kind.ToString(), pilot.Result);
        Assert.IsFalse(pilot.IsRunning);
        pilot.Dispose();
    }

    [TestCase(0, "brand", "unset", "unset", "unset", "theme_blueberry_logo")]
    [TestCase(1, "brand", "unset", "unset", "unset", "theme_blueberry_logo")]
    [TestCase(2, "brand", "#1B1035", "#FFC857", "22", "theme_blueberry_logo")]
    [TestCase(3, "unset", "#1B1035", "#FFC857", "unset", "unset")]
    [TestCase(4, "unset", "unset", "unset", "unset", "unset")]
    public void ThemeFillIsPureAndRunAppliesEverySlot(int index, string primary,
        string background, string link, string size, string logo)
    {
        var pilot = new ThemeScenario();
        pilot.Presets[index].Fill(pilot.Fields);
        CollectionAssert.AreEqual(new[] { primary, background, link, size, logo },
            pilot.Fields.All.Select(f => f.Value).ToArray());
        Assert.IsEmpty(_sdk.Calls);
        Tap(pilot, index);
        CollectionAssert.AreEqual(new[] { "Initialize", "ApplyTheme", "SetColorSchemeType",
            "ApplyTheme", "SetLogo", "SetFonts" }, _sdk.Methods);
        var schemes = _sdk.Calls.Last(c => c.Method == "ApplyTheme");
        for (var i = 0; i < 2; i++)
        {
            var scheme = (OctopusColorScheme)schemes.Args[i];
            Assert.AreEqual(primary == "unset" ? Color.clear :
                (i == 0 ? OctopusSampleNativeTheme.LightScheme.Primary :
                 OctopusSampleNativeTheme.DarkScheme.Primary), scheme.Primary);
            Assert.AreEqual(background == "unset" ? Color.clear : (Color)new Color32(27, 16, 53, 255), scheme.Background);
            Assert.AreEqual(link == "unset" ? Color.clear : (Color)new Color32(255, 200, 87, 255), scheme.Link);
        }
        var actualLogo = (OctopusLogo)_sdk.Calls.Last(c => c.Method == "SetLogo").Args[0];
        Assert.AreEqual(logo == "unset" ? null : logo, actualLogo == null ? null : actualLogo.AndroidDrawableName);
        var fonts = (OctopusFonts)_sdk.Last.Args[0];
        if (size == "unset") Assert.IsNull(fonts);
        else Assert.AreEqual(22, fonts.NavBarItem.Size);
        Assert.IsFalse(pilot.IsRunning);
    }
}
