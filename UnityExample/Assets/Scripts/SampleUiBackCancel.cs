using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Marks a confirmation's Cancel button, so the hardware back (<see cref="OctopusSampleBackHandler"/>)
/// cancels an open confirmation before it leaves the screen.
///
/// A component rather than a naming convention: matching a <c>-cancel</c> id suffix would have
/// fired any future action button that happened to end with it. Add it with <see cref="Mark"/>.
/// </summary>
[RequireComponent(typeof(Button))]
public sealed class SampleUiBackCancel : MonoBehaviour
{
    /// <summary>Marks <paramref name="button"/> as a confirmation's Cancel and returns it.</summary>
    public static RectTransform Mark(RectTransform button)
    {
        if (button.GetComponent<SampleUiBackCancel>() == null)
            button.gameObject.AddComponent<SampleUiBackCancel>();
        return button;
    }
}
