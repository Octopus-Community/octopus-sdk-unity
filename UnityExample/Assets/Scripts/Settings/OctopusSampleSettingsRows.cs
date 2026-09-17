using System;
using System.Collections.Generic;

/// <summary>
/// One row contributed to the Settings tab's Support group by code the Settings screen must not
/// name. Immutable on purpose: a registry entry that a caller can mutate after registering is a
/// registry whose contents no test can pin down.
/// </summary>
public sealed class OctopusSampleSettingsRow
{
    /// <summary>Creates a row. <paramref name="action"/> may be null for a row that only states a fact.</summary>
    public OctopusSampleSettingsRow(string id, string title, string subtitle, Action action)
    {
        Id = id;
        Title = title;
        Subtitle = subtitle;
        Action = action;
    }

    /// <summary>The row's <c>GameObject.name</c>, i.e. what a QA script taps.</summary>
    public string Id { get; private set; }

    /// <summary>The row's headline.</summary>
    public string Title { get; private set; }

    /// <summary>The line under the headline. May be null.</summary>
    public string Subtitle { get; private set; }

    /// <summary>What a tap runs, or null for a row with no affordance.</summary>
    public Action Action { get; private set; }
}

/// <summary>
/// Registration seam for Settings rows that live outside <c>Assets/Scripts/</c>.
///
/// It exists for exactly the reason <see cref="OctopusSampleLog"/> exists, and the constraint is
/// the same one: <c>UnityExample/Assets/Debug</c> is <c>export-ignore</c>d in `.gitattributes`, so
/// the public mirror ships this sample with that folder absent, and
/// `ci/mirror-export-guard/check-mirror-export.sh`'s type-name backstop fails the build if anything
/// outside `Debug/` so much as names a type from it. A `Developer tools` row cannot therefore reach
/// the debug console directly from here — the Settings screen would stop compiling in the mirror,
/// and naming the console's class even in this sentence is enough to turn that guard red, which is
/// why the console is described rather than named anywhere in this file.
///
/// So the traffic runs the other way: the Settings screen renders whatever is in
/// <see cref="Support"/>, and `Debug/` puts itself there from its own
/// <c>[RuntimeInitializeOnLoadMethod]</c> bootstrap. Remove `Debug/` and the group simply comes up
/// one row shorter, with nothing to fix.
///
/// The registry is static and survives scene loads, which is what a
/// <c>[RuntimeInitializeOnLoadMethod]</c> registration needs; <see cref="Clear"/> exists for tests,
/// which would otherwise inherit whatever a previous test registered.
/// </summary>
public static class OctopusSampleSettingsRows
{
    private static readonly List<OctopusSampleSettingsRow> Rows = new List<OctopusSampleSettingsRow>();

    /// <summary>Raised whenever the set of registered rows changes, so an open screen can repaint.</summary>
    public static event Action Changed;

    /// <summary>
    /// The registered rows, in registration order, rendered above the built-in `About` row.
    /// A copy: a caller holding the live list could reorder the Settings screen from the outside.
    /// </summary>
    public static IList<OctopusSampleSettingsRow> Support
    {
        get { return Rows.ToArray(); }
    }

    /// <summary>
    /// Adds a row, or replaces the one already registered under <paramref name="id"/>.
    ///
    /// Replacing rather than appending is not defensive: in the editor a domain reload re-runs
    /// every <c>[RuntimeInitializeOnLoadMethod]</c> against a registry that is not always empty,
    /// and two identical `Developer tools` rows is the shape of bug nobody reads a stack trace for.
    /// </summary>
    public static void Register(string id, string title, string subtitle, Action action)
    {
        if (string.IsNullOrEmpty(id)) throw new ArgumentException("A settings row needs an id.", "id");

        var row = new OctopusSampleSettingsRow(id, title, subtitle, action);
        for (var i = 0; i < Rows.Count; i++)
        {
            if (Rows[i].Id != id) continue;
            Rows[i] = row;
            Raise();
            return;
        }

        Rows.Add(row);
        Raise();
    }

    /// <summary>Empties the registry. For tests, which must not inherit each other's rows.</summary>
    public static void Clear()
    {
        if (Rows.Count == 0) return;
        Rows.Clear();
        Raise();
    }

    private static void Raise()
    {
        var handler = Changed;
        if (handler != null) handler();
    }
}
