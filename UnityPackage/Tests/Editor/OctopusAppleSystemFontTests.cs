using NUnit.Framework;

public class OctopusAppleSystemFontTests
{
    [TestCase("SFProDisplay-Bold.otf")]
    [TestCase("SFProDisplay-Medium.otf")]
    [TestCase("SFProText-Light.ttf")]
    [TestCase("SF-Pro-Display-Bold.otf")]
    [TestCase("SF-Pro-Rounded-Semibold.otf")]
    [TestCase("SF-Pro.ttf")]
    [TestCase("SF-Compact-Text-Regular.otf")]
    [TestCase("SFMono-Regular.otf")]
    [TestCase("NewYorkMedium-Bold.otf")]
    [TestCase("NewYorkExtraLarge-Regular.otf")]
    [TestCase("NewYork.ttf")]
    [TestCase("SFUIText-Regular.otf")]
    [TestCase("SFNS.ttf")]
    [TestCase("SFNSMono.ttf")]
    [TestCase("SF-Arabic-Rounded.ttf")]
    [TestCase("SFHebrew-Bold.otf")]
    [TestCase("sf_pro_display_bold.ttf")]
    [TestCase("SFProDisplay-Bold")]
    [TestCase("Assets/Fonts/SFProDisplay-Bold.otf")]
    public void DetectsAppleSystemFamilies(string fileName)
    {
        Assert.IsTrue(OctopusThemeSettings.IsAppleSystemFont(fileName));
    }

    [TestCase("Inter-Bold.ttf")]
    [TestCase("Onest-ExtraLight.ttf")]
    [TestCase("LiberationSans.ttf")]
    [TestCase("MySFProClone-Bold.ttf")]
    [TestCase("NewYorker.ttf")]
    [TestCase("SFProverbialGothic-Regular.ttf")]
    [TestCase("SFProTextile-Bold.ttf")]
    [TestCase("Assets/Fonts/NewYorker-Bold.otf")]
    [TestCase("")]
    [TestCase(null)]
    public void IgnoresOtherFonts(string fileName)
    {
        Assert.IsFalse(OctopusThemeSettings.IsAppleSystemFont(fileName));
    }
}
