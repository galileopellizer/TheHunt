using UnityEngine;

/// <summary>
/// Attach to the player root. Updates all CaveAudioOcclusion components
/// on this player each frame so they know whether THIS player's sounds
/// are coming from inside or outside the cave.
///
/// Uses the same CaveZone trigger: a separate trigger volume (or the same one)
/// should set a per-player cave state. Since we can't use the static LocalPlayerInCave
/// for remote players, we use Y position as a fallback for non-local players.
/// </summary>
public class CavePlayerAudioTracker : MonoBehaviour
{
    [Tooltip("Y position below which this player is considered inside the cave.")]
    [SerializeField] private float caveYThreshold = 0f;

    private CaveAudioOcclusion[] occlusionComponents;
    private bool inCave;
    private Unity.Netcode.NetworkObject netObj;

    private void Awake()
    {
        netObj = GetComponent<Unity.Netcode.NetworkObject>();
        occlusionComponents = GetComponentsInChildren<CaveAudioOcclusion>(true);
    }

    private void Update()
    {
        // Local player: use the accurate trigger-based value
        // Remote players: use Y threshold (good enough for occlusion purposes)
        if (netObj != null && netObj.IsOwner)
            inCave = CaveZone.LocalPlayerInCave;
        else
            inCave = transform.position.y < caveYThreshold;

        foreach (var occ in occlusionComponents)
            occ.sourceInCave = inCave;
    }
}
