using UnityEngine;

public partial class OctopusSDK
{
#if UNITY_ANDROID && !UNITY_EDITOR
    // Lane A transport: a C# implementation of the Kotlin OctopusBridgeListener interface.
    // The bridge invokes these directly via JNI on its calling thread — independent of the
    // Unity player loop — so they run even while the Octopus UI is shown and the loop is paused.
    // Held in a static field so the proxy target is not garbage-collected.
    private static OctopusBridgeListenerProxy _bridgeListener;

    private class OctopusBridgeListenerProxy : AndroidJavaProxy
    {
        public OctopusBridgeListenerProxy() : base("com.octopuscommunity.bridge.OctopusBridgeListener") { }

        // Runs synchronously on the Android UI thread. Must be a fast routing decision.
        // Isolate customer-handler exceptions from the native caller: on throw, return the safe
        // default (HandledByOctopus) so a faulty handler cannot crash the Octopus UI thread.
        public int resolveUrlStrategy(string url)
        {
            try
            {
                return ResolveUrlStrategyCode(url);
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
                return (int)UrlOpeningStrategy.HandledByOctopus;
            }
        }

        // Octopus Event (flat JSON envelope) — enqueue to the shared dispatcher (parsed + raised
        // on its background consumer), so delivery matches iOS and the customer handler doesn't
        // run on this JNI/Kotlin-channel thread.
        public void onEvent(string json) => OctopusEventDispatcher.Enqueue(json);

        // Loop-independent token request (Lane A). Kicks off the async host token provider on this
        // JNI/background thread (TriggerOnTokenRequested is async void; its continuation runs off the
        // Unity main thread, so it completes while the player loop is paused). The minted token is
        // returned to native via SetUserToken -> Bridge.setUserToken.
        public void requestToken()
        {
            try
            {
                OctopusSDK.TriggerOnTokenRequested();
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
            }
        }

        // Loop-independent bridge-share signature request (Lane A). Runs the host SignBridgeShare
        // callback on this JNI/background thread; the JWT returns to native via SetBridgeShareSignature
        // -> Bridge.setBridgeShareSignature.
        public void requestBridgeShareSignature(string fingerprint)
        {
            try
            {
                OctopusSDK.TriggerBridgeShareSign(fingerprint);
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
            }
        }
    }

    private static void RegisterBridgeListener()
    {
        if (_bridgeListener != null) return; // one proxy kept alive; don't re-register on re-Initialize
        using (AndroidJavaClass plugin = new AndroidJavaClass("com.octopuscommunity.bridge.Bridge"))
        {
            _bridgeListener = new OctopusBridgeListenerProxy();
            plugin.CallStatic("setBridgeListener", _bridgeListener);
        }
    }
#endif
}
