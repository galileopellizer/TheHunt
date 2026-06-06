using Unity.Netcode;
using UnityEngine;
using Object = UnityEngine.Object;

[DisallowMultipleComponent]
public class EffigyState : NetworkBehaviour
{
    [Header("Burn")]
    [SerializeField] private float burnDuration = 3f;
    [SerializeField] private float interactDistance = 2.5f;

    public NetworkVariable<bool> IsBurning = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public NetworkVariable<bool> IsBurned = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public NetworkVariable<double> BurnEndServerTime = new(
        0d,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        IsBurning.Value = false;
        IsBurned.Value = false;
        BurnEndServerTime.Value = 0d;
    }

    private void Update()
    {
        if (!IsServer) return;
        if (!IsBurning.Value || IsBurned.Value) return;

        if (GetServerTime() >= BurnEndServerTime.Value)
        {
            IsBurning.Value = false;
            IsBurned.Value = true;
            BurnEndServerTime.Value = 0d;
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void TryStartBurnServerRpc(ServerRpcParams rpcParams = default)
    {
        if (!IsServer) return;
        if (IsBurned.Value || IsBurning.Value) return;

        ulong senderClientId = rpcParams.Receive.SenderClientId;
        if (!IsValidHumanBurnRequest(senderClientId))
            return;

        IsBurning.Value = true;
        BurnEndServerTime.Value = GetServerTime() + Mathf.Max(0.1f, burnDuration);
    }

    private bool IsValidHumanBurnRequest(ulong clientId)
    {
        var network = NetworkManager.Singleton;
        if (network == null) return false;
        if (!network.ConnectedClients.TryGetValue(clientId, out var client)) return false;

        var identity = client.PlayerObject;
        if (identity == null) return false;

        var roleState = identity.GetComponent<PlayerRoleState>();
        if (roleState == null || roleState.Role.Value != PlayerRole.Human) return false;

        var body = FindOwnedBody(clientId);
        if (body == null) return false;

        var bodyRole = body.GetComponent<GamePlayerBodyRole>();
        if (bodyRole == null || bodyRole.Role.Value != PlayerRole.Human) return false;

        var health = body.GetComponent<PlayerHealth>();
        if (health != null && health.IsDead.Value) return false;

        float sqrDist = (body.transform.position - transform.position).sqrMagnitude;
        return sqrDist <= interactDistance * interactDistance;
    }

    private static NetworkObject FindOwnedBody(ulong ownerClientId)
    {
        foreach (var no in Object.FindObjectsOfType<NetworkObject>())
        {
            if (!no.IsSpawned) continue;
            if (no.OwnerClientId != ownerClientId) continue;
            if (no.GetComponent<GamePlayerBodyRole>() == null) continue;
            return no;
        }

        return null;
    }

    private double GetServerTime()
    {
        var network = NetworkManager.Singleton;
        if (network != null)
            return network.ServerTime.Time;
        return Time.timeAsDouble;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactDistance);
    }
#endif
}
