using System;
using UnityEngine;

/// <summary>
/// The one number Reef Run keeps between launches: the best score on this device.
///
/// Storage is injected so an EditMode test can run the whole screen without touching the editor's
/// own <see cref="PlayerPrefs"/> — the sample stores nothing else locally, and nothing here ever
/// reaches the SDK or the session.
/// </summary>
public sealed class OctopusReefRunProgress
{
    /// <summary>Kept from the previous mini-game so a tester's best score survives the change.</summary>
    public const string BestKey = "octopus.sample.arcade.best";

    private readonly Func<string, int, int> _read;
    private readonly Action<string, int> _write;
    private readonly Action _save;

    public OctopusReefRunProgress() : this(PlayerPrefs.GetInt, PlayerPrefs.SetInt, PlayerPrefs.Save) { }

    public OctopusReefRunProgress(Func<string, int, int> read, Action<string, int> write, Action save)
    {
        _read = read;
        _write = write;
        _save = save;
    }

    public int Best { get { return Math.Max(0, _read(BestKey, 0)); } }

    /// <summary>Stores <paramref name="score"/> when it improves on <see cref="Best"/>. Returns whether it did.</summary>
    public bool Record(int score)
    {
        if (score <= Best) return false;
        _write(BestKey, score);
        _save();
        return true;
    }
}
