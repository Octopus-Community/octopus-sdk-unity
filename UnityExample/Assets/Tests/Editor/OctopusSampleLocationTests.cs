using NUnit.Framework;

public class OctopusSampleLocationTests
{
    [TestCase(OctopusSampleTab.Home, "Tab · Home")]
    [TestCase(OctopusSampleTab.Scenarios, "Tab · Scenarios")]
    [TestCase(OctopusSampleTab.Community, "Tab · Community")]
    public void SettingsPreservesTheLastContentTab(OctopusSampleTab tab, string expected)
    {
        var location = new OctopusSampleLocation();
        location.OpenTab(tab);
        Assert.AreEqual(expected, location.WhereText(tab));
        location.OpenTab(OctopusSampleTab.Settings);
        Assert.AreEqual(expected, location.WhereText(OctopusSampleTab.Settings));
    }

    [Test]
    public void SettingsWithoutContentUsesTheExistingFallback()
    {
        var location = new OctopusSampleLocation();
        location.OpenTab(OctopusSampleTab.Settings);
        Assert.AreEqual("Tab · Settings", location.WhereText(OctopusSampleTab.Settings));
    }

    [Test]
    public void ClosingTheOverlayKeepsItsIdThroughSettings()
    {
        var location = new OctopusSampleLocation();
        location.OpenTab(OctopusSampleTab.Scenarios);
        location.OpenScenario("connection");
        Assert.AreEqual("Scenario · connection",
            location.WhereText(OctopusSampleTab.Scenarios, "connection"));

        // Once the overlay closes there is no live scenario argument; the stored id survives.
        Assert.AreEqual("Scenarios · connection", location.WhereText(OctopusSampleTab.Scenarios));
        location.OpenTab(OctopusSampleTab.Settings);
        Assert.AreEqual("Scenarios · connection", location.WhereText(OctopusSampleTab.Settings));
    }

    [Test]
    public void NewScenarioReplacesThePreviousId()
    {
        var location = new OctopusSampleLocation();
        location.OpenScenario("connection");
        location.OpenTab(OctopusSampleTab.Settings);
        location.OpenScenario("locale");
        location.OpenTab(OctopusSampleTab.Settings);
        Assert.AreEqual("Scenarios · locale", location.WhereText(OctopusSampleTab.Settings));
    }

    [TestCase(OctopusSampleTab.Home, "Tab · Home")]
    [TestCase(OctopusSampleTab.Scenarios, "Tab · Scenarios")]
    [TestCase(OctopusSampleTab.Community, "Tab · Community")]
    public void OpeningContentReplacesTheClosedScenario(OctopusSampleTab tab, string expected)
    {
        var location = new OctopusSampleLocation();
        location.OpenScenario("connection");
        location.OpenTab(tab);
        location.OpenTab(OctopusSampleTab.Settings);
        Assert.AreEqual(expected, location.WhereText(OctopusSampleTab.Settings));
    }
}
