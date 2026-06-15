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
    public GameObject[] characterPrefabs;
    
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
            UpdatePlayerList();

    }

    private void OnClientChanged(ulong clientId)
    {
        if (!IsServer) return;
        if (NetworkManager.Singleton == null) return;
        if (!NetworkManager.Singleton.IsListening) return;

        StartCoroutine(DelayedUpdatePlayerList());
    }

    private System.Collections.IEnumerator DelayedUpdatePlayerList()
    {
        yield return new WaitForSeconds(0.3f);
        UpdatePlayerList();
    }

        
    private void UpdatePlayerList()
    {
        if (!IsServer) return;
        UpdatePlayerListClientRpc();
    }

    [ClientRpc]
    private void UpdatePlayerListClientRpc()
    {
        foreach (var e in entries) Destroy(e);
        entries.Clear();

        var allNameStates = FindObjectsOfType<PlayerNameState>();
        int index = 1;
        foreach (var ns in allNameStates)
        {
            var entry = Instantiate(playerEntryPrefab, playerListParent);
            var slot  = entry.GetComponent<LobbyPlayerSlotUI>();

            string displayName = ns.PlayerName.Value.Length > 0
                ? ns.PlayerName.Value.ToString()
                : $"Player {index}";

            // Deterministic: ClientId % count — same result on every machine, no sync needed
            int charIdx = ns.GetCharacterIndex();
            var prefab = (characterPrefabs != null && charIdx < characterPrefabs.Length)
                ? characterPrefabs[charIdx]
                : (characterPrefabs != null && characterPrefabs.Length > 0 ? characterPrefabs[0] : null);

            slot.Setup(displayName, prefab, null);
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

        NetworkManager.Singleton.SceneManager.LoadScene(MapSettings.SelectedScene, LoadSceneMode.Single);
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
