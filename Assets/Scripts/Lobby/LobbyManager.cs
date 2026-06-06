using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using TMPro;
using UnityEngine.Rendering; // if you use TextMeshPro
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LobbyManager : NetworkBehaviour
{
    public Transform playerListParent;
    public GameObject playerEntryPrefab;
    public GameObject startButton;
    public GameObject defaultCharacterPrefab;
    
    private readonly List<GameObject> entries = new();

    public override void OnNetworkSpawn()
    {
        if (NetworkManager.Singleton == null) return;

        NetworkManager.Singleton.OnClientConnectedCallback += OnClientChanged;
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientChanged;

        // Only host can start
        bool show = IsServer;
        if (startButton) startButton.SetActive(show);

        if (show && startButton)
            startButton.GetComponentInChildren<Button>().onClick.AddListener(StartGame);
        
        // IMPORTANT: force initial sync
        if (IsServer)
        {
            UpdatePlayerListClientRpc();
        }

    }

    private void OnClientChanged(ulong clientId)
    {
        if (!IsServer) return;
        if (NetworkManager.Singleton == null) return;
        if (!NetworkManager.Singleton.IsListening) return;

        {
            UpdatePlayerListClientRpc();
        }
    }

        
    [ClientRpc]
    private void UpdatePlayerListClientRpc()
    {
        RebuildPlayerList();
    }

    void RebuildPlayerList()
    {
        if (!NetworkManager.Singleton || !NetworkManager.Singleton.IsListening)
            return;
        
        foreach (var e in entries) Destroy(e);
        entries.Clear();

        int index = 1;
        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            var entry = Instantiate(playerEntryPrefab, playerListParent);

            var slot = entry.GetComponent<LobbyPlayerSlotUI>();
            string displayName = $"Player {index}";
            PlayerNameState nameState = null;
            if (client.PlayerObject != null)
            {
                nameState = client.PlayerObject.GetComponent<PlayerNameState>();
                if (nameState != null && nameState.PlayerName.Value.Length > 0)
                {
                    displayName = nameState.PlayerName.Value.ToString();
                }
            }

            slot.Setup(displayName, defaultCharacterPrefab, nameState);

            entries.Add(entry);
            index++;
        }
    }


    public void StartGame()
    {
        if (!NetworkManager.Singleton.IsServer) return;

        var clients = NetworkManager.Singleton.ConnectedClientsList;
        int monsterIndex = Random.Range(0, clients.Count);

        for (int i = 0; i < clients.Count; i++)
        {
            var identityObj = clients[i].PlayerObject; // <-- THIS IS PlayerIdentity now
            if (identityObj == null) { Debug.LogError($"Identity null for {clients[i].ClientId}"); continue; }

            var roleState = identityObj.GetComponent<PlayerRoleState>();
            if (roleState == null) { Debug.LogError($"No PlayerRoleState on identity for {clients[i].ClientId}"); continue; }
            roleState.Role.Value = (i == monsterIndex) ? PlayerRole.Monster : PlayerRole.Human;
        }

        NetworkManager.Singleton.SceneManager.LoadScene("GameScene", LoadSceneMode.Single);
    }


    
    private void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientChanged;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientChanged;
        }

        if (startButton)
            startButton.GetComponentInChildren<Button>().onClick.RemoveListener(StartGame);
    }
}
