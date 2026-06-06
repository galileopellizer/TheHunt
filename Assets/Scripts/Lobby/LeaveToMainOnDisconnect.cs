using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LeaveToMainOnDisconnect : MonoBehaviour
{
    [SerializeField] private string mainSceneName = "MainScene";
    private bool hasLoaded;

    private void OnEnable()
    {
        if (NetworkManager.Singleton == null) return;
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
        NetworkManager.Singleton.OnClientStopped += OnClientStopped;
        NetworkManager.Singleton.OnServerStopped += OnServerStopped;
    }

    private void OnDisable()
    {
        if (NetworkManager.Singleton == null) return;
        NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        NetworkManager.Singleton.OnClientStopped -= OnClientStopped;
        NetworkManager.Singleton.OnServerStopped -= OnServerStopped;
    }

    private void OnClientDisconnected(ulong clientId)
    {
        if (NetworkManager.Singleton == null) return;
        if (NetworkManager.Singleton.IsServer) return;
        if (clientId != NetworkManager.Singleton.LocalClientId) return;

        LoadMainScene();
    }

    private void OnClientStopped(bool wasHost)
    {
        LoadMainScene();
    }

    private void OnServerStopped(bool wasHost)
    {
        if (!wasHost) return;
        LoadMainScene();
    }

    private void LoadMainScene()
    {
        if (hasLoaded) return;
        hasLoaded = true;
        SceneManager.LoadScene(mainSceneName, LoadSceneMode.Single);
    }
}
