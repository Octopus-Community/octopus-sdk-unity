using System.Globalization;
using System.Threading;
using NUnit.Framework;

public class OctopusCompactCountTests
{
    // Ported from Flutter's octopus_count_format_test.dart (default locale).
    [TestCase(0L, "0")]
    [TestCase(7L, "7")]
    [TestCase(999L, "999")]
    [TestCase(-1L, "-1")]
    [TestCase(-12345L, "-12345")]
    [TestCase(1000L, "1K")]
    [TestCase(1234L, "1.2K")]
    [TestCase(1999L, "1.9K")]
    [TestCase(9999L, "9.9K")]
    [TestCase(1950L, "1.9K")]
    [TestCase(9950000L, "9.9M")]
    [TestCase(10000L, "10K")]
    [TestCase(12345L, "12K")]
    [TestCase(99999L, "99K")]
    [TestCase(999999L, "999K")]
    [TestCase(2000L, "2K")]
    [TestCase(1000000L, "1M")]
    [TestCase(1234567L, "1.2M")]
    [TestCase(9999999L, "9.9M")]
    [TestCase(10000000L, "10M")]
    [TestCase(999999999L, "999M")]
    [TestCase(1000000000L, "1B")]
    [TestCase(1234567890L, "1.2B")]
    [TestCase(9999999999L, "9.9B")]
    [TestCase(12000000000L, "12B")]
    [TestCase(1099L, "1K")]
    [TestCase(1100L, "1.1K")]
    [TestCase(10000000000L, "10B")]
    [TestCase(9223372036854775807L, "9223372036B")]
    [TestCase(-9223372036854775808L, "-9223372036854775808")]
    public void FormatsLikeFlutterAndReactNative(long count, string expected)
    {
        Assert.AreEqual(expected, OctopusSDK.FormatOctopusCompactCount(count));
    }

    [TestCase("fr-FR")]
    [TestCase("de-DE")]
    [TestCase("es-ES")]
    [TestCase("en-US")]
    public void UsesInvariantFormattingRegardlessOfCurrentCulture(string culture)
    {
        var previous = Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo(culture);
            Assert.AreEqual("1.2K", OctopusSDK.FormatOctopusCompactCount(1234));
            Assert.AreEqual("1.2M", OctopusSDK.FormatOctopusCompactCount(1234567));
            Assert.AreEqual("1.5B", OctopusSDK.FormatOctopusCompactCount(1500000000));
            Assert.AreEqual("2K", OctopusSDK.FormatOctopusCompactCount(2000));
            Assert.AreEqual("10K", OctopusSDK.FormatOctopusCompactCount(10000));
            Assert.AreEqual("42", OctopusSDK.FormatOctopusCompactCount(42));
            Assert.AreEqual("-12345", OctopusSDK.FormatOctopusCompactCount(-12345));
        }
        finally { Thread.CurrentThread.CurrentCulture = previous; }
    }

    [Test]
    public void WorksWithoutInitializationOrMockDispatch()
    {
        OctopusSDK.Mock.Reset();
        Assert.AreEqual("1.9K", OctopusSDK.FormatOctopusCompactCount(1999));
        Assert.AreEqual(0, OctopusSDK.Mock.Calls.Count);
    }
}
