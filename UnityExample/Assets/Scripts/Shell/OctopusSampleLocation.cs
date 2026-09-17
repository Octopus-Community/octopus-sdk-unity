/// <summary>The last content location, retained while navigating through Settings and Support.</summary>
public sealed class OctopusSampleLocation
{
    private OctopusSampleTab? _tab;
    private string _scenarioId;

    public void OpenTab(OctopusSampleTab tab)
    {
        // Settings and its sub-screens are reporting routes, not replacement content.
        if (tab == OctopusSampleTab.Settings) return;
        _tab = tab;
        _scenarioId = null;
    }

    public void OpenScenario(string scenarioId)
    {
        if (string.IsNullOrEmpty(scenarioId)) return;
        _tab = OctopusSampleTab.Scenarios;
        _scenarioId = scenarioId;
    }

    public string WhereText(OctopusSampleTab selectedTab, string openScenarioId = null)
    {
        // The visible overlay keeps its existing label. Its destruction never clears history.
        if (!string.IsNullOrEmpty(openScenarioId)) return "Scenario · " + openScenarioId;
        if (!string.IsNullOrEmpty(_scenarioId))
            return "Scenarios · " + _scenarioId;

        return "Tab · " + OctopusSampleShell.Title(_tab ?? selectedTab);
    }
}
