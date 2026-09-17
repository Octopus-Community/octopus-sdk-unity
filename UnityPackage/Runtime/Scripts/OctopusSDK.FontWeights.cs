using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using UnityEngine;

public partial class OctopusSDK
{
    // Applied after the existing name/size setters have restored their native defaults.
    // Keeping those entry points intact also preserves the legacy bridge ABI.
    private static void SetFontWeights(OctopusFonts fonts)
    {
        string json = FontWeightsToJson(fonts);
#if UNITY_EDITOR
        Mock.Record("SetFontWeights", json);
#elif UNITY_ANDROID
        using (AndroidJavaClass plugin = new AndroidJavaClass("com.octopuscommunity.bridge.Bridge"))
        {
            plugin.CallStatic("setFontWeights", json);
        }
#elif UNITY_IOS
        OctopusSdkSetFontWeights(json);
#endif
    }

    internal static string FontWeightsToJson(OctopusFonts fonts)
    {
        var rows = new List<Dictionary<string, string>>();
        if (fonts != null)
        {
            AddFontWeight(rows, "title1", fonts.Title1);
            AddFontWeight(rows, "title2", fonts.Title2);
            AddFontWeight(rows, "body1", fonts.Body1);
            AddFontWeight(rows, "body2", fonts.Body2);
            AddFontWeight(rows, "caption1", fonts.Caption1);
            AddFontWeight(rows, "caption2", fonts.Caption2);
            AddFontWeight(rows, "navBarItem", fonts.NavBarItem);
        }
        return OctopusJson.WriteArray(rows, new HashSet<string> { "fontWeight" });
    }

    private static void AddFontWeight(List<Dictionary<string, string>> rows, string slot, OctopusFont font)
    {
        if (font == null || !font.FontWeight.HasValue) return;
        rows.Add(new Dictionary<string, string>
        {
            { "slot", slot },
            { "fontWeight", font.FontWeight.Value.ToString(CultureInfo.InvariantCulture) }
        });
    }

#if UNITY_IOS && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void OctopusSdkSetFontWeights(string json);
#endif
}
