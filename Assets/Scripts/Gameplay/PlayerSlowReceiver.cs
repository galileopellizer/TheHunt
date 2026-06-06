using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Attach to the player body prefab.
/// Receives slow RPC from the server and applies it to the local PlayerController.
/// </summary>
[DisallowMultipleComponent]
public class PlayerSlowReceiver : NetworkBehaviour
{
    public void ApplySlowRpc(float duration, float multiplier)
    {
        ApplySlowClientRpc(duration, multiplier);
    }

    [ClientRpc]
    private void ApplySlowClientRpc(float duration, float multiplier)
    {
        if (!IsOwner) return;
        var pc = GetComponent<PlayerController>();
        if (pc != null) pc.ApplySlow(duration, multiplier);
    }
}
