using UnityEngine;

public sealed class OctopusSampleHostCallbackView : OctopusSampleSettingsDetailView
{
    private string _title, _message;
    public static void Open(string title, string message)
    {
        var previous = FindAnyObjectByType<OctopusSampleHostCallbackView>();
        if (previous != null) previous.Close();
        var view = new GameObject("OctopusSampleHostCallbackView").AddComponent<OctopusSampleHostCallbackView>();
        view._title = title; view._message = message; view.InitializeView();
    }
    protected override void Build()
    {
        var content = Content("host-callback-screen", _title, "host-callback-back", true);
        SampleUi.FlexibleLabel(content, _message, SampleUi.TextBody, SampleUi.TitleColor);
        SampleUi.Button("host-callback-close", content, "Close", Close);
    }
}
