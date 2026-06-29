using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class EventsExample : MonoBehaviour
{
    [SerializeField]
    Text LogsTextUi;

    System.Random random = new System.Random();

    void Start()
    {
        OctopusSDK.Initialize(OctopusExampleConfig.Instance.Default.apiKey, ConnectionMode.OctopusAuth());
        OctopusSDK.OnOctopusEvent += OnOctopusEvent;
    }

    void OnDestroy()
    {
        OctopusSDK.OnOctopusEvent -= OnOctopusEvent;
    }

    // OnOctopusEvent fires on a BACKGROUND thread while Octopus is on screen — do thread-safe /
    // backend work here, and marshal Unity-API work (updating this Text) to the main thread.
    void OnOctopusEvent(OctopusEvent e)
    {
        OctopusMainThread.Post(() =>
        {
            LogsTextUi.text = $"Octopus event: {e.Kind}\r\n" + LogsTextUi.text;
        });
    }

    public void TrackAccessToCommunityTrue()
    {
        OctopusSDK.TrackAccessToCommunity(true);
    }

    public void TrackAccessToCommunityFalse()
    {
        OctopusSDK.TrackAccessToCommunity(false);
    }

    public void TrackCustomEvent()
    {
        string name = "purchase";
        var props = new Dictionary<string, string>
        {
            { "price", ((random.NextDouble() * 50.0) + 50.0).ToString().Substring(0, 5) },
            { "currency", "USD" },
            { "purchased_at", DateTime.UtcNow.ToString() }
        };
        OctopusSDK.Track(name, props);

        string log = "Event sent:\r\n\r\nname : " + name + "\r\n";
        foreach (var item in props)
        {
            log += item.Key + " : " + item.Value + "\r\n";
        }
        LogsTextUi.text = log;
    }

    public void OpenOctopus()
    {
        OctopusSDK.Open();
    }
}
