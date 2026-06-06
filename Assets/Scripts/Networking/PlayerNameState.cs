using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public class PlayerNameState : NetworkBehaviour
{
    private const string PlayerPrefsKey = "player_name";

    public NetworkVariable<FixedString32Bytes> PlayerName =
        new NetworkVariable<FixedString32Bytes>(
            "",
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

    public override void OnNetworkSpawn()
    {
        if (!IsOwner)
            return;

        string name = LoadLocalName();
        SetNameServerRpc(name);
    }

    private static string LoadLocalName()
    {
        if (PlayerPrefs.HasKey(PlayerPrefsKey))
        {
            return PlayerPrefs.GetString(PlayerPrefsKey);
        }

        int randomNumber = Random.Range(1000, 10000);
        return $"Player {randomNumber}";
    }

    [ServerRpc]
    private void SetNameServerRpc(string name, ServerRpcParams rpcParams = default)
    {
        string trimmed = (name ?? string.Empty).Trim();
        if (trimmed.Length == 0)
        {
            trimmed = $"Player {rpcParams.Receive.SenderClientId}";
        }

        if (trimmed.Length > 32)
        {
            trimmed = trimmed.Substring(0, 32);
        }

        PlayerName.Value = trimmed;
    }
}
