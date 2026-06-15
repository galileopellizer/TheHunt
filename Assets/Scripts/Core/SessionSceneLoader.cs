using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SessionSceneLoader : MonoBehaviour
{
    private bool subscribed = false;

    void Update()
    {
        if (!subscribed && NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            subscribed = true;
            Debug.Log($"[SessionSceneLoader] Subscribed. IsServer={NetworkManager.Singleton.IsServer} IsClient={NetworkManager.Singleton.IsClient} IsListening={NetworkManager.Singleton.IsListening}");
        }
    }

    private void OnClientConnected(ulong clientId)
    {
        Debug.Log($"[SessionSceneLoader] Client connected: {clientId}, IsServer: {NetworkManager.Singleton.IsServer}");
        if (NetworkManager.Singleton.IsServer)
        {
            NetworkManager.Singleton.SceneManager.LoadScene("LobbyScene", LoadSceneMode.Single);
        }
    }

    private void OnDisable()
    {
        if (NetworkManager.Singleton != null && subscribed)
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
    }
}
