using Unity.Netcode;
using UnityEngine;
using System.Collections;

public class ClientDisconnectNotifier : MonoBehaviour
{
    private bool hasShutdown = false;

    void OnApplicationQuit()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsHost && !hasShutdown)
        {
            hasShutdown = true;
            StartCoroutine(SafeShutdown());
        }
    }

    IEnumerator SafeShutdown()
    {
        NetworkManager.Singleton.Shutdown();
        yield return new WaitForSecondsRealtime(0.2f); // wait before app quits
    }
}
