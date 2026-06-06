using Unity.Netcode;
using UnityEngine;

public class GamePlayerBodyRevealState : NetworkBehaviour
{
    [Header("Reveal Settings")]
    [SerializeField] private float speedThreshold = 7.5f;
    [SerializeField] private float revealHoldSeconds = 0.35f;
    [SerializeField] private bool debugForceReveal = false;

    public NetworkVariable<bool> RevealToMonster = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public NetworkVariable<double> RevealUntilServerTime = new(
        0d,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            RevealToMonster.Value = false;
            RevealUntilServerTime.Value = 0d;
        }
    }

    private void Update()
    {
        if (!IsServer)
            return;

        if (debugForceReveal)
        {
            SetRevealForSeconds(revealHoldSeconds);
            return;
        }

        if (RevealToMonster.Value && GetServerTime() >= RevealUntilServerTime.Value)
        {
            RevealToMonster.Value = false;
            RevealUntilServerTime.Value = 0d;
        }
    }

    [ServerRpc]
    public void ReportMoveSpeedServerRpc(float speed, ServerRpcParams rpcParams = default)
    {
        if (!IsServer)
            return;

        var role = GetComponent<GamePlayerBodyRole>();
        if (role == null || role.Role.Value != PlayerRole.Human)
        {
            if (RevealToMonster.Value)
                RevealToMonster.Value = false;
            return;
        }

        if (speed >= speedThreshold)
        {
            SetRevealForSeconds(revealHoldSeconds);
        }
    }

    private void SetRevealForSeconds(float seconds)
    {
        if (!IsServer)
            return;

        double now = GetServerTime();
        double until = now + Mathf.Max(0f, seconds);
        if (until > RevealUntilServerTime.Value)
            RevealUntilServerTime.Value = until;

        if (!RevealToMonster.Value)
            RevealToMonster.Value = true;

    }

    private double GetServerTime()
    {
        var network = NetworkManager.Singleton;
        if (network != null)
            return network.ServerTime.Time;
        return Time.timeAsDouble;
    }
}
