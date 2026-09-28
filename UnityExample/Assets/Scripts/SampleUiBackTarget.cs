using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Marks an app bar built by <see cref="SampleUi.AppBar"/> as a screen, so the hardware back
/// (<see cref="OctopusSampleBackHandler"/>) can find the topmost one without every view polling
/// input itself.
///
/// <see cref="Back"/> is the header's own on-screen Back button, or null for a screen that has
/// none (the shell's tabs, first-launch configuration). The handler presses that very button, so
/// the hardware back and the on-screen Back cannot drift into two code paths.
/// </summary>
public sealed class SampleUiBackTarget : MonoBehaviour
{
    /// <summary>The header's on-screen Back, or null when the screen has none.</summary>
    public Button Back;
}
