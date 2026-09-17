using System.Collections.Generic;

/// <summary>
/// Sample-owned screens that QA can open with `--es qaScenario &lt;id&gt;`, alongside the shared
/// catalogue's scenarios.
///
/// Why a second list rather than a 28th row in <see cref="OctopusScenarioCatalog"/>: that catalogue
/// is a verbatim mirror of pm-tools' `scenarios-catalog.yaml`, the cross-platform contract every
/// SDK sample answers to, and its own tests pin it at 27 entries with a preset and a result id
/// each. Reef Run is this sample's own screen, not an SDK scenario the other platforms owe — so it
/// gets its own list here, and the QA command line stays exactly what it would have been.
/// </summary>
public static class OctopusSampleQaDestinations
{
    /// <summary>Reef Run, the sample's in-app game.</summary>
    public const string Arcade = "arcade";

    /// <summary>The destination ids this sample can open. Disjoint from the catalogue's ids.</summary>
    public static readonly IReadOnlyList<string> Ids = new List<string> { Arcade };

    /// <summary>Whether <paramref name="id"/> is a destination of this sample.</summary>
    public static bool Has(string id)
    {
        foreach (var known in Ids)
        {
            if (known == id) return true;
        }
        return false;
    }

    /// <summary>The tab a destination is reached from, so QA lands on the same screen a player does.</summary>
    public static string TabOf(string id)
    {
        return id == Arcade ? "home" : null;
    }
}
