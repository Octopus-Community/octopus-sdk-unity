using UnityEngine;
using UnityEngine.UI;

/// <summary>Refreshes device preferences without rebuilding screens or losing input focus.</summary>
public class SampleUiCanvas : MonoBehaviour
{
    private Vector2Int _screen;

    private void OnApplicationFocus(bool focused)
    {
        if (focused) OctopusSampleTextScale.Refresh();
    }

    private void Update()
    {
        var screen = new Vector2Int(Screen.width, Screen.height);
        if (_screen == screen) return;
        _screen = screen;
        var scaler = GetComponent<CanvasScaler>();
        scaler.referenceResolution = new Vector2(SampleUi.ReferenceWidthFor(SampleUi.ScreenDpWidth()),
            SampleUi.CanvasReference.y);
    }
}
