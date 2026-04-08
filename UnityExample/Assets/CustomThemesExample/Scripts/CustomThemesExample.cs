using UnityEngine;

public class CustomThemesExample : MonoBehaviour
{
    void Start()
    {
        OctopusSDK.Initialize(OctopusExampleConfig.Instance.Default.apiKey, ConnectionMode.OctopusAuth());
    }

    public void Light()
    {
        OctopusSDK.SetTheme(
            colorScheme: new OctopusColorScheme(
                primary: new Color32(255, 0, 0, 255),
                primaryLow: new Color32(255, 179, 179, 255),
                primaryHigh: new Color32(204, 0, 0, 255),
                onPrimary: new Color32(255, 255, 255, 255)
            ),
            logo: new OctopusLogo(
                androidDrawableName: "theme_cherry_logo",
                iOSResourceName: "Data/Raw/theme_cherry_logo.png"
            )
        );
        OctopusSDK.Open();
    }

    public void Dark()
    {
        OctopusSDK.SetTheme(
            colorScheme: new OctopusColorScheme(
                primary: new Color32(74, 21, 75, 255),
                primaryLow: new Color32(187, 167, 189, 255),
                primaryHigh: new Color32(54, 13, 56, 255),
                onPrimary: new Color32(255, 255, 255, 255)
            ),
            logo: new OctopusLogo(
                androidDrawableName: "theme_grape_logo",
                iOSResourceName: "Data/Raw/theme_grape_logo.png"
            )
        );
        OctopusSDK.Open();
    }

    public void Custom1()
    {
        OctopusSDK.SetTheme(
            colorScheme: new OctopusColorScheme(
                primary: new Color32(255, 69, 0, 255),
                primaryLow: new Color32(255, 198, 179, 255),
                primaryHigh: new Color32(255, 140, 100, 255),
                onPrimary: new Color32(255, 255, 255, 255)
            ),
            logo: new OctopusLogo(
                androidDrawableName: "theme_orange_logo",
                iOSResourceName: "Data/Raw/theme_orange_logo.png"
            )
         );
        OctopusSDK.Open();
    }

    public void Custom2()
    {
        OctopusSDK.SetTheme(
            colorScheme: new OctopusColorScheme(
                primary: new Color32(24, 119, 242, 255),
                primaryLow: new Color32(185, 213, 248, 255),
                primaryHigh: new Color32(110, 170, 255, 255),
                onPrimary: new Color32(255, 255, 255, 255)
            ),
            logo: new OctopusLogo(
                androidDrawableName: "theme_blueberry_logo",
                iOSResourceName: "Data/Raw/theme_blueberry_logo.png"
            ),
            fonts: new OctopusFonts(
                body2: new OctopusFont("onest_extralight", "Onest-ExtraLight", 18),
                navBarItem: new OctopusFont("onest_bold", "Onest-Bold", 16)
            )

        );
        OctopusSDK.Open();
    }

    public void CustomName()
    {
        OctopusSDK.SetTheme(
            colorScheme: new OctopusColorScheme(
                primary: new Color32(74, 21, 75, 255),
                primaryLow: new Color32(187, 167, 189, 255),
                primaryHigh: new Color32(54, 13, 56, 255),
                onPrimary: new Color32(255, 255, 255, 255)
            ),
            appName: "My Custom App Name",
            navBarUsesPrimaryColor: true,
            fonts: new OctopusFonts(
                title1: new OctopusFont("freckleface_regular", "FreckleFace-Regular", 36),
                title2: new OctopusFont("freckleface_regular", "FreckleFace-Regular", 28),
                body1: new OctopusFont("onest_extralight", "Onest-ExtraLight", 17),
                body2: new OctopusFont("onest_extralight", "Onest-ExtraLight", 14),
                caption1: new OctopusFont("onest_bold", "Onest-Bold", 12),
                caption2: new OctopusFont("onest_bold", "Onest-Bold", 10),
                navBarItem: new OctopusFont("onest_bold", "Onest-Bold", 17)
            )
        );
        OctopusSDK.Open();
    }
}
