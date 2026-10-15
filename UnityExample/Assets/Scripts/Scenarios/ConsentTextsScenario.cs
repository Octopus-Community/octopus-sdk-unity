using System.Collections.Generic;

public sealed class ConsentTextsScenario : CommunityScenarioPilot
{
    private const string SHORT_NOTICE = "By posting, you agree to the [community charter](https://example.com/charter).";
    private const string LONG_NOTICE = SHORT_NOTICE + " And to our [event rules](https://example.com/events).\n\n" +
        "**Be kind to other members.** Members who post results, photos or routes keep the rights to them " +
        "and let the community show them in the app.\n\n" +
        "*Your data:* we keep your posts for as long as your account exists. Read the " +
        "[privacy notice](https://example.com/privacy) to learn how to export or delete them.\n\n" +
        "Questions? Write to us through the [contact page](https://example.com/contact).";
    private readonly OctopusConsentTexts[] _texts =
    {
        OctopusConsentTexts.Implicit(SHORT_NOTICE),
        OctopusConsentTexts.MultiCheckbox(
            "I accept the **community terms** ([read them](https://example.com/terms))",
            "I accept the [privacy notice](https://example.com/privacy)",
            "I will follow the [*community rules*](https://example.com/rules)"),
        OctopusConsentTexts.SingleCheckbox(
            "I accept the [community terms](https://example.com/terms) and the [rules](https://example.com/rules)",
            "Your data is handled as described in our [privacy notice](https://example.com/privacy)."),
        OctopusConsentTexts.Implicit(LONG_NOTICE),
        OctopusConsentTexts.MultiCheckbox(
            "I have read and accept the **community membership terms**, including the " +
                "[event rules](https://example.com/events) and [refund policy](https://example.com/refunds).\nMembership renews every season.",
            "I accept the [privacy notice](https://example.com/privacy). *Results are public* and stay on the leaderboard after I leave.",
            "I will follow the [community rules](https://example.com/rules): respect other members, share responsibly, and report abuse.\n\nRepeated breaches end a membership."),
        OctopusConsentTexts.SingleCheckbox(
            "I accept the **community membership terms** ([read them](https://example.com/terms)) and the [rules](https://example.com/rules).",
            "We use your posts, photos and results to run the community.\n\n" +
                "They stay visible to members until you delete them. Read the [privacy notice](https://example.com/privacy) " +
                "to export or erase your data, or write through the [contact page](https://example.com/contact).\n\n" +
                "# This heading stays plain text\n- and so does this list item"),
        OctopusConsentTexts.SingleCheckbox("", "   "),
        OctopusConsentTexts.Implicit(SHORT_NOTICE),
    };
    private int _generation;

    public ConsentTextsScenario() : base("consentTexts",
        new OctopusScenarioField("preset", "Consent wording preset", true))
    {
        Add(1, PresetLabel(1, "Implicit — short notice with a link"), "1");
        Add(2, PresetLabel(2, "Multi checkbox — short labels with links"), "2");
        Add(3, PresetLabel(3, "Single checkbox — short label and text below"), "3");
        Add(4, PresetLabel(4, "Implicit — long notice (scrolls past 5 lines)"), "4");
        Add(5, PresetLabel(5, "Multi checkbox — long labels, several links"), "5");
        Add(6, PresetLabel(6, "Single checkbox — long, multi-paragraph"), "6");
        Add(7, PresetLabel(7, "Single checkbox — empty fields (SDK wordings)"), "7");
        Add(8, PresetLabel(8, "Multi checkbox with implicit texts (SDK wordings)"), "8");
        Add(9, "Clear overrides (backend texts and mode)", "backend");
    }

    public override bool CanCustomize { get { return false; } }
    public override IReadOnlyList<string> ApiSymbols
    { get { return new[] { "DebugOverrideTermsAcceptanceMode", "DebugOverrideConsentTexts", "DebugGetCommunityConfig" }; } }
    public override string ParameterNotice
    { get { return "Each preset sets the consent mode before its texts; preset 8 deliberately mismatches them. Clear restores both backend mode and texts. Open Community and make a first contribution with a member who has not accepted the terms to inspect native wording, markdown, links and layout."; } }

    protected override void Run(OctopusScenarioFields fields)
    {
        var value = fields.Get("preset");
        int index;
        if (value != "backend" && (!int.TryParse(value, out index) || index < 1 || index > 8))
        { Report("No call made: select a consent preset."); return; }
        OctopusTermsAcceptanceMode? mode = null;
        OctopusConsentTexts texts = null;
        if (value != "backend")
        {
            index = int.Parse(value) - 1;
            texts = _texts[index];
            mode = index == 7 ? OctopusTermsAcceptanceMode.ExplicitMultiCheckbox :
                texts.Mode == OctopusConsentTextsMode.Implicit ? OctopusTermsAcceptanceMode.Implicit :
                texts.Mode == OctopusConsentTextsMode.MultiCheckbox ? OctopusTermsAcceptanceMode.ExplicitMultiCheckbox :
                OctopusTermsAcceptanceMode.ExplicitSingleCheckbox;
        }
        var sdk = OctopusScenarioSdk.Current;
        var generation = ++_generation;
        Announce("DebugOverrideTermsAcceptanceMode", mode.HasValue ? mode.ToString() : "backend");
        sdk.DebugOverrideTermsAcceptanceMode(mode);
        Announce("DebugOverrideConsentTexts", OctopusSampleCommunityConfigLines.Consent(texts));
        sdk.DebugOverrideConsentTexts(texts);
        var applied = "Applied: " + (texts == null ? "backend texts and mode" : "preset " + value) + ". ";
        ReportRunning(applied + "Reading effective config…");
        Announce("DebugGetCommunityConfig");
        sdk.DebugGetCommunityConfig(config =>
        {
            if (generation != _generation) return;
            Report(applied + (config == null ? "Effective config unavailable." :
                "Effective terms: " + config.TermsAcceptanceMode + "; consent texts: " +
                OctopusSampleCommunityConfigLines.Consent(config.ConsentTexts) + ".") +
                " Open Community and make a first contribution to inspect the native consent UI.");
        }, error =>
        {
            if (generation == _generation) Report(applied + "Config read failed: " + error);
        });
    }

    public override void Dispose() { base.Dispose(); ++_generation; }
}
