using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class OctopusLocalizedTextTests
{
    private static Dictionary<string, string> Texts(params string[] pairs)
    {
        var texts = new Dictionary<string, string>();
        for (int i = 0; i < pairs.Length; i += 2) texts.Add(pairs[i], pairs[i + 1]);
        return texts;
    }

    [Test]
    public void AutoTranslated_KeepsTheTextWithoutLanguages()
    {
        var text = OctopusLocalizedText.AutoTranslated("Open");
        Assert.AreEqual("Open", text.DefaultText);
        Assert.IsFalse(text.IsPerLanguage);
        Assert.IsNull(text.DefaultLanguage);
        Assert.IsEmpty(text.Texts);
        Assert.Throws<ArgumentNullException>(() => OctopusLocalizedText.AutoTranslated(null));
    }

    [Test]
    public void PerLanguage_KeepsTheTextsAndTheDefaultLanguageAsGiven()
    {
        var text = OctopusLocalizedText.PerLanguage(Texts("en", "Open", "pt_BR", "Abrir", "zh-Hant-TW", "打開"), "pt_BR");
        Assert.IsTrue(text.IsPerLanguage);
        Assert.AreEqual("Abrir", text.DefaultText);
        Assert.AreEqual("pt_BR", text.DefaultLanguage);
        CollectionAssert.AreEquivalent(Texts("en", "Open", "pt_BR", "Abrir", "zh-Hant-TW", "打開"), text.Texts);
        Assert.Throws<NotSupportedException>(() => ((IDictionary<string, string>)text.Texts).Add("fr", "Ouvrir"));
    }

    [Test]
    public void PerLanguage_DoesNotValidate_TheNativeSdkDoes()
    {
        var empty = OctopusLocalizedText.PerLanguage(new Dictionary<string, string>(), "en");
        Assert.IsEmpty(empty.Texts);
        Assert.IsNull(empty.DefaultText);
        var refusedNatively = OctopusLocalizedText.PerLanguage(Texts("en", " ", "", "x", "pt-BR", "a", "pt_BR", "b"), "en-US");
        Assert.AreEqual(4, refusedNatively.Texts.Count);
        Assert.AreEqual("en-US", refusedNatively.DefaultLanguage);
        Assert.IsNull(refusedNatively.DefaultText);
        Assert.AreEqual("Open", OctopusLocalizedText.PerLanguage(Texts("EN", "Open"), "EN").DefaultText);
    }

    [Test]
    public void PerLanguage_NullArgumentsThrow()
    {
        Assert.AreEqual("texts", Assert.Throws<ArgumentNullException>(() => OctopusLocalizedText.PerLanguage(null, "en")).ParamName);
        Assert.AreEqual("defaultLanguage",
            Assert.Throws<ArgumentNullException>(() => OctopusLocalizedText.PerLanguage(Texts("en", "Open"), null)).ParamName);
    }

    [Test]
    public void PerLanguage_CopiesTheTextsItWasGiven()
    {
        var texts = Texts("en", "Open");
        var text = OctopusLocalizedText.PerLanguage(texts, "en");
        texts["fr"] = "Ouvrir";
        Assert.AreEqual(1, text.Texts.Count);
    }

    [Test]
    public void Equality_FollowsTheTextsAndTheDefaultLanguageAsGiven()
    {
        Assert.AreEqual(OctopusLocalizedText.AutoTranslated("Open"), OctopusLocalizedText.AutoTranslated("Open"));
        Assert.AreNotEqual(OctopusLocalizedText.AutoTranslated("Open"), OctopusLocalizedText.AutoTranslated("Ouvrir"));
        var perLanguage = OctopusLocalizedText.PerLanguage(Texts("en", "Open", "fr", "Ouvrir"), "en");
        var same = OctopusLocalizedText.PerLanguage(Texts("fr", "Ouvrir", "en", "Open"), "en");
        Assert.AreEqual(perLanguage, same);
        Assert.AreEqual(perLanguage.GetHashCode(), same.GetHashCode());
        Assert.AreNotEqual(perLanguage, OctopusLocalizedText.PerLanguage(Texts("EN", "Open", "fr", "Ouvrir"), "EN"));
        Assert.AreNotEqual(perLanguage, OctopusLocalizedText.PerLanguage(Texts("en", "Open", "fr", "Ouvrir"), "fr"));
        Assert.AreNotEqual(perLanguage, OctopusLocalizedText.AutoTranslated("Open"));
        Assert.AreNotEqual(OctopusLocalizedText.AutoTranslated("Open"), OctopusLocalizedText.PerLanguage(Texts("en", "Open"), "en"));
        Assert.AreEqual(OctopusLocalizedText.PerLanguage(Texts("en", null), "de"), OctopusLocalizedText.PerLanguage(Texts("en", null), "de"));
    }

    [Test]
    public void Json_AutoTranslatedAndPerLanguageWithTagsAsGiven()
    {
        Assert.AreEqual("{\"text\":\"Say \\\"hi\\\"\\\\\\u000a\\u0001\"}",
            OctopusLocalizedText.AutoTranslated("Say \"hi\"\\\n\u0001").ToJson());
        Assert.AreEqual("{\"defaultLanguage\":\"PT_br\",\"texts\":{\"PT_br\":\"Abrir\",\"en\":\"Open\",\"zh_hant_tw\":\"打開\"}}",
            OctopusLocalizedText.PerLanguage(Texts("zh_hant_tw", "打開", "PT_br", "Abrir", "en", "Open"), "PT_br").ToJson());
        Assert.AreEqual("{\"defaultLanguage\":\"\",\"texts\":{\"en\":\"\"}}",
            OctopusLocalizedText.PerLanguage(Texts("en", null), "").ToJson());
        Assert.AreEqual("{\"defaultLanguage\":\"en\",\"texts\":{}}",
            OctopusLocalizedText.PerLanguage(new Dictionary<string, string>(), "en").ToJson());
    }

    [Test]
    public void ClientPostError_InvalidLocalizedTextKeepsKindAndLanguage()
    {
        var error = OctopusJson.ClientPostErrorFromJson(
            "{\"type\":\"invalidLocalizedText\",\"message\":\"refused\",\"localizedTextError\":\"duplicateLanguage\",\"languageCode\":\"pt-BR\"}");
        Assert.AreEqual(OctopusClientPostErrorCode.InvalidLocalizedText, error.Code);
        Assert.AreEqual("refused", error.Message);
        Assert.AreEqual(OctopusLocalizedTextValidationErrorKind.DuplicateLanguage, error.LocalizedTextErrorKind);
        Assert.AreEqual("pt-BR", error.LanguageCode);
        var wires = new[] { "emptyTexts", "blankText", "emptyLanguage", "duplicateLanguage", "defaultLanguageMissing" };
        var kinds = (OctopusLocalizedTextValidationErrorKind[])Enum.GetValues(typeof(OctopusLocalizedTextValidationErrorKind));
        Assert.AreEqual(kinds.Length, wires.Length);
        for (int i = 0; i < wires.Length; i++)
            Assert.AreEqual(kinds[i], OctopusJson.LocalizedTextErrorKindFromWire(wires[i]), wires[i]);
        var unknown = OctopusJson.ClientPostErrorFromJson("{\"type\":\"invalidLocalizedText\",\"message\":\"refused\"}");
        Assert.AreEqual(OctopusClientPostErrorCode.InvalidLocalizedText, unknown.Code);
        Assert.IsNull(unknown.LocalizedTextErrorKind);
        Assert.IsNull(unknown.LanguageCode);
        var other = OctopusJson.ClientPostErrorFromJson("{\"type\":\"textMissing\",\"languageCode\":\"en\"}");
        Assert.IsNull(other.LocalizedTextErrorKind);
        Assert.IsNull(other.LanguageCode);
    }

#pragma warning disable 0618 // the deprecated plain button label
    [Test]
    public void ClientObject_DeprecatedButtonTextIsTheAutoTranslatedFallback()
    {
        var content = new OctopusClientObject { ObjectId = "item", ViewObjectButtonText = "Read" };
        var medias = new List<OctopusClientObjectMedia>();
        StringAssert.Contains(",\"viewObjectButton\":{\"text\":\"Read\"},", OctopusJson.ClientObjectToJson(content, medias));

        content.ViewObjectButton = OctopusLocalizedText.PerLanguage(Texts("en", "Read", "fr", "Lire"), "fr");
        StringAssert.Contains(",\"viewObjectButton\":{\"defaultLanguage\":\"fr\",\"texts\":{\"en\":\"Read\",\"fr\":\"Lire\"}},",
            OctopusJson.ClientObjectToJson(content, medias));

        content.ViewObjectButton = null;
        ref string legacy = ref content.ViewObjectButtonText;
        legacy = "";
        StringAssert.Contains(",\"viewObjectButton\":null,", OctopusJson.ClientObjectToJson(content, medias));
    }
#pragma warning restore 0618

    [TestCase(false)]
    [TestCase(true)]
    public void Mock_KeepsTheButtonThePostWasCreatedWith(bool useLegacyLabel)
    {
        OctopusSDK.Mock.Reset();
        OctopusSDK.Mock.Enabled = true;
        var host = new GameObject("LocalizedTextTest");
        var drainer = host.AddComponent<OctopusMainThread>();
        try
        {
            var first = useLegacyLabel
                ? OctopusLocalizedText.AutoTranslated("View recipe")
                : OctopusLocalizedText.PerLanguage(Texts("en", "View recipe", "fr", "Voir la recette"), "en");
            var content = new OctopusClientObject { ObjectId = "recipe", Text = "A recipe" };
#pragma warning disable CS0618
            if (useLegacyLabel) content.ViewObjectButtonText = first.DefaultText;
            else content.ViewObjectButton = first;
#pragma warning restore CS0618
            OctopusSDK.FetchOrCreateClientObjectRelatedPost(content, _ => { }, e => Assert.Fail(e.Message));
            content.ViewObjectButton = OctopusLocalizedText.AutoTranslated("Ignored");
            OctopusSDK.FetchOrCreateClientObjectRelatedPost(content, _ => { }, e => Assert.Fail(e.Message));
            typeof(OctopusMainThread).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(drainer, null);
            Assert.AreEqual(first, OctopusSDK.Mock.ClientObjectRelatedPostButton("recipe"));
            Assert.IsNull(OctopusSDK.Mock.ClientObjectRelatedPostButton("unknown"));
            OctopusSDK.Mock.Reset();
            Assert.IsNull(OctopusSDK.Mock.ClientObjectRelatedPostButton("recipe"));
        }
        finally
        {
            OctopusSDK.Mock.Reset();
            UnityEngine.Object.DestroyImmediate(host);
        }
    }

    [Test]
    public void OpenCreatePost_HandsTheLocalizedLabelToTheMock()
    {
        OctopusSDK.Mock.Reset();
        OctopusSDK.Mock.Enabled = true;
        try
        {
            var label = OctopusLocalizedText.PerLanguage(Texts("en", "Open", "es", "Abrir"), "es");
            OctopusSDK.OpenCreatePost(new OctopusPrefilledPost { Text = "t", CtaLocalizedLabel = label, CtaUrl = "https://x.test" });
            Assert.AreEqual(label, OctopusSDK.Mock.LastPrefilledPost.CtaLocalizedLabel);
        }
        finally { OctopusSDK.Mock.Reset(); }
    }
}
