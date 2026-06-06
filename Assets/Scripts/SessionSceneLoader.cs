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
        }
    }

    private void OnClientConnected(ulong clientId)
    {
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
