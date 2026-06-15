using UnityEngine;

/// <summary>
/// Place this on a trigger volume covering the cave interior.
/// Tracks whether the local player is currently inside the cave.
/// </summary>
[RequireComponent(typeof(Collider))]
public class CaveZone : MonoBehaviour
{
    public static bool LocalPlayerInCave { get; private set; }

    private void Awake()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (IsLocalPlayer(other))
            LocalPlayerInCave = true;
    }

    private void OnTriggerExit(Collider other)
    {
        if (IsLocalPlayer(other))
            LocalPlayerInCave = false;
    }

    private static bool IsLocalPlayer(Collider other)
    {
        var netObj = other.GetComponentInParent<Unity.Netcode.NetworkObject>();
        return netObj != null && netObj.IsOwner;
    }
}
