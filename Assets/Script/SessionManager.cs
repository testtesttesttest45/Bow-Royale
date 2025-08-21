using UnityEngine;
using Unity.Netcode;

public static class SessionManager
{
    public static void CleanUpSession()
    {
        // 1. Shutdown the network, if still running
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            NetworkManager.Singleton.Shutdown();
        }
        // 2. Destroy all singletons (they could have been re-created if you joined/hosted before)
        if (NetworkManager.Singleton != null)
            Object.Destroy(NetworkManager.Singleton.gameObject);

        if (MultiplayerManager.Instance != null)
            Object.Destroy(MultiplayerManager.Instance.gameObject);

        if (LobbyManager.Instance != null)
            Object.Destroy(LobbyManager.Instance.gameObject);

        // Add any other singletons here

        // 3. Reset all static/global flags and UI references
        GameOverUI.GameHasEnded = false;
        GameOverUI.RematchInProgress = false;
        Bot.GameHasStarted = false;
        GamePauseUI.Instance?.HidePause();
        GamePauseUI.Instance = null;
    }
}
