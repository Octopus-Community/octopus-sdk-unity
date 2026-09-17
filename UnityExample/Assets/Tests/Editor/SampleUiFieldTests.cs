using TMPro;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

public class SampleUiFieldTests
{
    [TestCase(false)]
    [TestCase(true)]
    public void FieldUsesExistingStyleAndSupportsMultilineInput(bool multiline)
    {
        var host = new GameObject("FieldTest");
        try
        {
            var field = SampleUi.Field("description", host.transform, "", multiline);
            Assert.AreEqual(multiline ? TMP_InputField.LineType.MultiLineNewline : TMP_InputField.LineType.SingleLine,
                field.lineType);
            // TMP_InputField rewrites the text component's wrapping when lineType is assigned:
            // SingleLine lands on PreserveWhitespaceNoWrap, MultiLineNewline on Normal.
            Assert.AreEqual(multiline ? TextWrappingModes.Normal : TextWrappingModes.PreserveWhitespaceNoWrap,
                field.textComponent.textWrappingMode);
            Assert.AreEqual(OctopusSampleBranding.MinTouchUnits * (multiline ? 3f : 1f),
                field.GetComponent<LayoutElement>().preferredHeight);
            Assert.AreEqual(SampleUi.FieldBackground, field.GetComponent<Image>().color);
            Assert.AreEqual(SampleUi.TitleColor, field.textComponent.color);
            Assert.IsFalse(field.textComponent.richText);
            if (multiline)
            {
                field.text = "first line\nsecond line";
                Assert.AreEqual("first line\nsecond line", field.text);
            }
        }
        finally
        {
            Object.DestroyImmediate(host);
        }
    }
}
