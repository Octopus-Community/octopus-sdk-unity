using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

public class OctopusDebugTests
{
    private const string CONFIG = "{\"exposeClientUserId\":true,\"forceLoginOnStrongActions\":false,\"displayAccountAge\":true,\"termsAcceptanceMode\":\"explicitMultiCheckbox\"}";
    private GameObject _object;
    private OctopusMainThread _drainer;
    private OctopusSDK.OctopusChannel _channel;

    [SetUp]
    public void SetUp()
    {
        OctopusSDK.Mock.Reset();
        OctopusSDK.Mock.Enabled = true;
        _object = new GameObject("DebugConfigTest");
        _drainer = _object.AddComponent<OctopusMainThread>();
        _channel = _object.AddComponent<OctopusSDK.OctopusChannel>();
        Drain();
    }

    [TearDown]
    public void TearDown()
    {
        Drain();
        UnityEngine.Object.DestroyImmediate(_object);
        OctopusSDK.Mock.Reset();
        OctopusSDK.Mock.Enabled = true;
    }

    private void Drain()
    {
        typeof(OctopusMainThread).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(_drainer, null);
    }

    [TestCase("implicit", OctopusTermsAcceptanceMode.Implicit)]
    [TestCase("IMPLICIT", OctopusTermsAcceptanceMode.Implicit)]
    [TestCase("explicitMultiCheckbox", OctopusTermsAcceptanceMode.ExplicitMultiCheckbox)]
    [TestCase("EXPLICIT_MULTI_CHECKBOX", OctopusTermsAcceptanceMode.ExplicitMultiCheckbox)]
    [TestCase("explicitSingleCheckbox", OctopusTermsAcceptanceMode.ExplicitSingleCheckbox)]
    [TestCase("EXPLICIT_SINGLE_CHECKBOX", OctopusTermsAcceptanceMode.ExplicitSingleCheckbox)]
    public void ConfigParsing_MapsBothNativeSpellings(string wire, OctopusTermsAcceptanceMode expected)
    {
        var config = OctopusDebugParsing.ConfigFromJson(CONFIG.Replace("explicitMultiCheckbox", wire));
        Assert.IsTrue(config.ExposeClientUserId);
        Assert.IsFalse(config.ForceLoginOnStrongActions);
        Assert.IsTrue(config.DisplayAccountAge);
        Assert.AreEqual(expected, config.TermsAcceptanceMode);
    }

    [TestCase(OctopusProfileFieldLockState.Editable, "editable")]
    [TestCase(OctopusProfileFieldLockState.ReadOnly, "readOnly")]
    [TestCase(OctopusProfileFieldLockState.Disabled, "disabled")]
    public void ProfileLockSerialization_MapsEveryState(OctopusProfileFieldLockState state, string wire)
    {
        var row = OctopusJson.ParseObject(OctopusDebugParsing.ProfileLockToJson(new OctopusProfileFieldsLock(bio: state)));
        Assert.AreEqual("editable", row["nickname"]);
        Assert.AreEqual("editable", row["avatar"]);
        Assert.AreEqual(wire, row["bio"]);
    }

    [Test]
    public void Serialization_DefaultFlagsAndNullResets()
    {
        Assert.AreEqual("null", OctopusDebugParsing.ProfileLockToJson(null));
        Assert.AreEqual("null", OctopusDebugParsing.ContentOptionsToJson(null));
        var defaults = OctopusJson.ParseRawObject(OctopusDebugParsing.ContentOptionsToJson(new OctopusContentOptions()));
        Assert.AreEqual(5, defaults.Count);
        foreach (var value in defaults.Values) Assert.AreEqual("true", value);
        var disabled = OctopusJson.ParseRawObject(OctopusDebugParsing.ContentOptionsToJson(new OctopusContentOptions(
            new OctopusPostOptions(false, false, false), new OctopusCommentOptions(false), new OctopusReplyOptions(false))));
        foreach (var value in disabled.Values) Assert.AreEqual("false", value);
        var mixed = OctopusJson.ParseRawObject(OctopusDebugParsing.ContentOptionsToJson(new OctopusContentOptions(
            new OctopusPostOptions(false, true), new OctopusCommentOptions(true), new OctopusReplyOptions(false))));
        Assert.AreEqual("false", mixed["postEnablePictures"]);
        Assert.AreEqual("true", mixed["postEnablePolls"]);
        Assert.AreEqual("true", mixed["commentEnablePictures"]);
        Assert.AreEqual("false", mixed["replyEnablePictures"]);
    }

    [Test]
    public void TextRequirement_PreservesOldConstructorAndMockClear()
    {
        Assert.IsTrue(new OctopusPostOptions().TextRequired);
        Assert.IsTrue(new OctopusPostOptions(false, false).TextRequired);
        var options = new OctopusContentOptions(new OctopusPostOptions(true, true, false));
        Assert.IsFalse(options.Post.TextRequired);
        var row = OctopusJson.ParseRawObject(OctopusDebugParsing.ContentOptionsToJson(options));
        Assert.AreEqual("false", row["postTextRequired"]);
        OctopusSDK.DebugOverrideContentOptions(options);
        Assert.AreSame(options, OctopusSDK.Mock.DebugOverrides.ContentOptions);
        Assert.IsFalse(OctopusSDK.Mock.DebugOverrides.ContentOptions.Post.TextRequired);
        OctopusSDK.DebugOverrideContentOptions(null);
        Assert.IsNull(OctopusSDK.Mock.DebugOverrides.ContentOptions);
    }

    [Test]
    public void NicknameOverride_DistinguishesTrueFalseAndBackendAndReset()
    {
        Assert.AreEqual("true", OctopusDebugParsing.NullableBoolToWire(true));
        Assert.AreEqual("false", OctopusDebugParsing.NullableBoolToWire(false));
        Assert.AreEqual("null", OctopusDebugParsing.NullableBoolToWire(null));
        foreach (bool? enabled in new bool?[] { true, false, null })
        {
            OctopusSDK.DebugOverrideForceNicknameChoice(enabled);
            Assert.AreEqual(enabled, OctopusSDK.Mock.DebugOverrides.ForceNicknameChoice);
        }
        OctopusSDK.Mock.Enabled = false;
        OctopusSDK.DebugOverrideForceNicknameChoice(false);
        Assert.AreEqual(false, OctopusSDK.Mock.DebugOverrides.ForceNicknameChoice);
        OctopusSDK.Mock.Reset();
        Assert.IsNull(OctopusSDK.Mock.DebugOverrides.ForceNicknameChoice);
    }

    [Test]
    public void NicknameSnapshot_AbsentIsFalseAndMalformedIsRejected()
    {
        Assert.IsFalse(OctopusDebugParsing.ConfigFromJson(CONFIG).ForceNicknameChoice);
        foreach (bool enabled in new[] { true, false })
        {
            var json = CONFIG.TrimEnd('}') + ",\"forceNicknameChoice\":" + (enabled ? "true" : "false") + "}";
            Assert.AreEqual(enabled, OctopusDebugParsing.ConfigFromJson(json).ForceNicknameChoice);
        }
        Assert.Throws<FormatException>(() => OctopusDebugParsing.ConfigFromJson(
            CONFIG.TrimEnd('}') + ",\"forceNicknameChoice\":null}"));
    }

    [Test]
    public void ConsentFactoriesAreImmutableAndEnumOrdinalsAreStable()
    {
        Assert.AreEqual(0, (int)OctopusConsentTextsMode.Implicit);
        Assert.AreEqual(1, (int)OctopusConsentTextsMode.MultiCheckbox);
        Assert.AreEqual(2, (int)OctopusConsentTextsMode.SingleCheckbox);
        Assert.AreEqual(3, Enum.GetValues(typeof(OctopusConsentTextsMode)).Length);
        Assert.AreEqual(OctopusConsentTextsMode.Implicit, OctopusConsentTexts.Implicit().Mode);
        Assert.AreEqual(OctopusConsentTextsMode.MultiCheckbox, OctopusConsentTexts.MultiCheckbox().Mode);
        Assert.AreEqual(OctopusConsentTextsMode.SingleCheckbox, OctopusConsentTexts.SingleCheckbox().Mode);
        foreach (var property in typeof(OctopusConsentTexts).GetProperties())
            Assert.IsNull(property.GetSetMethod(), property.Name + " must not have a public setter");
        Assert.IsEmpty(typeof(OctopusConsentTexts).GetConstructors());
    }

    [Test]
    public void ConsentJsonRoundTripsAllVariantsNullBlankUnicodeAndControlCharacters()
    {
        string wording = "A \"quote\" and \\ slash\nDeuxième ligne ☃ 😀\r\t\b\f\u0001 [link](https://example.com)";
        var variants = new[]
        {
            OctopusConsentTexts.Implicit(wording),
            OctopusConsentTexts.MultiCheckbox(null, "", "   "),
            OctopusConsentTexts.SingleCheckbox(wording, "null")
        };
        foreach (var value in variants)
        {
            string json = OctopusDebugParsing.ConsentTextsToJson(value);
            var copy = OctopusDebugParsing.ConsentTextsFromJson(json);
            Assert.AreEqual(value.Mode, copy.Mode);
            var fields = OctopusJson.ParseRawObject(json);
            Assert.AreEqual(value.Mode == OctopusConsentTextsMode.Implicit ? "\"implicit\"" :
                value.Mode == OctopusConsentTextsMode.MultiCheckbox ? "\"multiCheckbox\"" : "\"singleCheckbox\"", fields["mode"]);
            Assert.AreEqual(value.Notice, copy.Notice);
            Assert.AreEqual(value.TermsLabel, copy.TermsLabel);
            Assert.AreEqual(value.PrivacyLabel, copy.PrivacyLabel);
            Assert.AreEqual(value.RulesLabel, copy.RulesLabel);
            Assert.AreEqual(value.Label, copy.Label);
            Assert.AreEqual(value.BelowText, copy.BelowText);
            Assert.AreEqual(value.Mode == OctopusConsentTextsMode.Implicit ? 2 :
                value.Mode == OctopusConsentTextsMode.MultiCheckbox ? 4 : 3, OctopusJson.ParseRawObject(json).Count);
            foreach (char c in json) Assert.GreaterOrEqual((int)c, 0x20, "JSON must escape control characters");
            var snapshot = OctopusDebugParsing.ConfigFromJson(CONFIG.TrimEnd('}') + ",\"consentTexts\":" + json + "}");
            Assert.AreEqual(copy.Mode, snapshot.ConsentTexts.Mode);
        }
        var nullableFields = OctopusJson.ParseRawObject(OctopusDebugParsing.ConsentTextsToJson(variants[1]));
        Assert.AreEqual("null", nullableFields["termsLabel"]);
        Assert.AreEqual("\"\"", nullableFields["privacyLabel"]);
        Assert.AreEqual("\"   \"", nullableFields["rulesLabel"]);
        Assert.IsNull(OctopusDebugParsing.ConsentTextsFromJson(" null "));
        Assert.AreEqual("null", OctopusDebugParsing.ConsentTextsToJson(null));
        Assert.IsNull(OctopusDebugParsing.ConfigFromJson(CONFIG).ConsentTexts);
        Assert.IsNull(OctopusDebugParsing.ConfigFromJson(CONFIG.TrimEnd('}') + ",\"consentTexts\":null}").ConsentTexts);
        Assert.IsNull(OctopusDebugParsing.ConsentTextsFromJson("{\"mode\":\"implicit\"}").Notice);
        Assert.AreEqual("", OctopusDebugParsing.ConsentTextsFromJson("{\"mode\":\"implicit\",\"notice\":\"\"}").Notice);
    }

    [TestCase("{}")]
    [TestCase("[]")]
    [TestCase("")]
    [TestCase("{\"mode\":\"future\"}")]
    [TestCase("{\"mode\":\"implicit\",\"notice\":false}")]
    public void InvalidConsentPayloadsAreRejected(string json)
    {
        Assert.Throws<FormatException>(() => OctopusDebugParsing.ConsentTextsFromJson(json));
    }

    [Test]
    public void ConsentMockKeepsModeIndependentAndClearsBothOverridesAndReset()
    {
        var texts = OctopusConsentTexts.Implicit("**notice**");
        OctopusSDK.DebugOverrideTermsAcceptanceMode(OctopusTermsAcceptanceMode.ExplicitMultiCheckbox);
        OctopusSDK.DebugOverrideConsentTexts(texts);
        Assert.AreSame(texts, OctopusSDK.Mock.DebugOverrides.ConsentTexts);
        Assert.AreEqual(OctopusTermsAcceptanceMode.ExplicitMultiCheckbox, OctopusSDK.Mock.DebugOverrides.TermsAcceptanceMode);
        OctopusSDK.DebugOverrideConsentTexts(null);
        Assert.IsNull(OctopusSDK.Mock.DebugOverrides.ConsentTexts);
        Assert.AreEqual(OctopusTermsAcceptanceMode.ExplicitMultiCheckbox, OctopusSDK.Mock.DebugOverrides.TermsAcceptanceMode);
        OctopusSDK.DebugOverrideConsentTexts(OctopusConsentTexts.SingleCheckbox("", "   "));
        OctopusSDK.DebugOverrideTermsAcceptanceMode(null);
        Assert.IsNotNull(OctopusSDK.Mock.DebugOverrides.ConsentTexts);
        OctopusSDK.DebugOverrideConsentTexts(null);
        Assert.IsNull(OctopusSDK.Mock.DebugOverrides.TermsAcceptanceMode);
        Assert.IsNull(OctopusSDK.Mock.DebugOverrides.ConsentTexts);
        OctopusSDK.Mock.Enabled = false;
        OctopusSDK.DebugOverrideConsentTexts(texts);
        Assert.AreSame(texts, OctopusSDK.Mock.DebugOverrides.ConsentTexts);
        OctopusSDK.Mock.Reset();
        Assert.IsNull(OctopusSDK.Mock.DebugOverrides.ConsentTexts);
    }

    [Test]
    public void InvalidEnums_DoNotRecordOrReplaceOverrides()
    {
        OctopusSDK.DebugOverrideTermsAcceptanceMode(OctopusTermsAcceptanceMode.Implicit);
        Assert.Throws<ArgumentOutOfRangeException>(() => OctopusSDK.DebugOverrideTermsAcceptanceMode((OctopusTermsAcceptanceMode)99));
        Assert.Throws<ArgumentOutOfRangeException>(() => OctopusSDK.DebugOverrideProfileFieldsLock(
            new OctopusProfileFieldsLock(bio: (OctopusProfileFieldLockState)99)));
        Assert.AreEqual(1, OctopusSDK.Mock.Calls.Count);
        Assert.AreEqual(OctopusTermsAcceptanceMode.Implicit, OctopusSDK.Mock.DebugOverrides.TermsAcceptanceMode);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("{}")]
    [TestCase("[]")]
    [TestCase("invalid")]
    public void InvalidConfig_UsesErrorCallback(string json)
    {
        string error = null;
        bool succeeded = false;
        OctopusSDK.Mock.DebugCommunityConfigJson = json;
        OctopusSDK.DebugGetCommunityConfig(c => succeeded = true, e => error = e);
        Assert.IsNull(error);
        Drain();
        Assert.IsFalse(succeeded);
        StringAssert.Contains("Malformed community config", error);
    }

    [Test]
    public void InvalidConfigTypesAndUnknownMode_AreRejected()
    {
        Assert.Throws<FormatException>(() => OctopusDebugParsing.ConfigFromJson(CONFIG.Replace("true", "\"true\"")));
        Assert.Throws<FormatException>(() => OctopusDebugParsing.ConfigFromJson(CONFIG.Replace("explicitMultiCheckbox", "futureMode")));
        Assert.Throws<FormatException>(() => OctopusDebugParsing.ConfigFromJson(CONFIG.TrimEnd('}')));
        Assert.IsNull(OctopusDebugParsing.ConfigFromJson(" null "));
    }

    [Test]
    public void NativeEnvelopes_CorrelateOutOfOrderOnceAndDispatchOnMainThread()
    {
        var calls = new List<string>();
        int callerThread = Thread.CurrentThread.ManagedThreadId;
        int callbackThread = -1;
        int first = OctopusSDK.RegisterDebugConfigCallbacks(c => calls.Add(c.TermsAcceptanceMode.ToString()), e => calls.Add("wrong"));
        int second = OctopusSDK.RegisterDebugConfigCallbacks(c => calls.Add("wrong"), e =>
        {
            callbackThread = Thread.CurrentThread.ManagedThreadId;
            calls.Add(e);
        });
        Task.Run(() =>
        {
            _channel.OnDebugGetCommunityConfigError(second + "\nline one\nline two");
            _channel.OnDebugGetCommunityConfigResult(first + "\n" + CONFIG);
            _channel.OnDebugGetCommunityConfigError(first + "\nduplicate");
            _channel.OnDebugGetCommunityConfigResult(second + "\nnull");
            _channel.OnDebugGetCommunityConfigResult(null);
            _channel.OnDebugGetCommunityConfigResult("invalid\n{}");
            _channel.OnDebugGetCommunityConfigError("missing separator");
        }).GetAwaiter().GetResult();
        Assert.AreEqual(0, calls.Count);
        Drain();
        CollectionAssert.AreEqual(new[] { "line one\nline two", "ExplicitMultiCheckbox" }, calls);
        Assert.AreEqual(callerThread, callbackThread);
    }

    [Test]
    public void Mock_StoresImmutableOverridesAndClearsThemWithNullAndReset()
    {
        var fields = new OctopusProfileFieldsLock(OctopusProfileFieldLockState.ReadOnly, bio: OctopusProfileFieldLockState.Disabled);
        var content = new OctopusContentOptions(post: new OctopusPostOptions(enablePolls: false));
        OctopusSDK.DebugOverrideProfileFieldsLock(fields);
        OctopusSDK.DebugOverrideContentOptions(content);
        OctopusSDK.DebugOverrideTermsAcceptanceMode(OctopusTermsAcceptanceMode.ExplicitSingleCheckbox);
        OctopusSDK.DebugOverrideExposeClientUserId(true);
        Assert.AreSame(fields, OctopusSDK.Mock.DebugOverrides.ProfileFieldsLock);
        Assert.AreSame(content, OctopusSDK.Mock.DebugOverrides.ContentOptions);
        Assert.AreEqual(OctopusTermsAcceptanceMode.ExplicitSingleCheckbox, OctopusSDK.Mock.DebugOverrides.TermsAcceptanceMode);
        Assert.AreEqual(true, OctopusSDK.Mock.DebugOverrides.ExposeClientUserId);
        Assert.AreEqual(4, OctopusSDK.Mock.Calls.Count);
        OctopusSDK.DebugOverrideProfileFieldsLock(null);
        OctopusSDK.DebugOverrideContentOptions(null);
        OctopusSDK.DebugOverrideTermsAcceptanceMode(null);
        OctopusSDK.DebugOverrideExposeClientUserId(false);
        Assert.AreEqual(false, OctopusSDK.Mock.DebugOverrides.ExposeClientUserId);
        OctopusSDK.DebugOverrideExposeClientUserId(null);
        Assert.IsNull(OctopusSDK.Mock.DebugOverrides.ProfileFieldsLock);
        Assert.IsNull(OctopusSDK.Mock.DebugOverrides.ContentOptions);
        Assert.IsNull(OctopusSDK.Mock.DebugOverrides.TermsAcceptanceMode);
        Assert.IsNull(OctopusSDK.Mock.DebugOverrides.ExposeClientUserId);
        OctopusSDK.DebugOverrideExposeClientUserId(true);
        OctopusSDK.Mock.DebugCommunityConfigJson = CONFIG;
        OctopusSDK.Mock.DebugCommunityConfigError = "forced";
        OctopusSDK.Mock.Reset();
        Assert.IsNull(OctopusSDK.Mock.DebugOverrides.ExposeClientUserId);
        Assert.AreEqual("null", OctopusSDK.Mock.DebugCommunityConfigJson);
        Assert.IsNull(OctopusSDK.Mock.DebugCommunityConfigError);
        Assert.AreEqual(0, OctopusSDK.Mock.Calls.Count);
    }

    [Test]
    public void Mock_ConfigCompletesWhileDisabledAndReportsConfiguredError()
    {
        OctopusSDK.Mock.Enabled = false;
        OctopusSDK.DebugOverrideExposeClientUserId(true);
        Assert.AreEqual(true, OctopusSDK.Mock.DebugOverrides.ExposeClientUserId);
        int nullResults = 0;
        OctopusSDK.DebugGetCommunityConfig(c => { if (c == null) nullResults++; }, e => Assert.Fail(e));
        OctopusSDK.Mock.DebugCommunityConfigJson = CONFIG;
        OctopusCommunityConfig result = null;
        OctopusSDK.DebugGetCommunityConfig(c => result = c, e => Assert.Fail(e));
        OctopusSDK.Mock.DebugCommunityConfigError = "configured error";
        string error = null;
        OctopusSDK.DebugGetCommunityConfig(c => Assert.Fail("Unexpected result"), e => error = e);
        Drain();
        Assert.AreEqual(1, nullResults);
        Assert.IsTrue(result.ExposeClientUserId);
        Assert.AreEqual("configured error", error);
        Assert.AreEqual(0, OctopusSDK.Mock.Calls.Count);
    }
}
