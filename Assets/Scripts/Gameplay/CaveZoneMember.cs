using UnityEngine;

/// <summary>
/// Attach alongside CaveAudioOcclusion on AudioSources that live inside the cave
/// (e.g. ambient cave sounds). For player-owned AudioSources (footsteps, roar),
/// this is set dynamically via CaveZone trigger — attach this and leave isInCave
/// unchecked for surface sounds, checked for permanently-cave sounds.
///
/// For player footstep/roar AudioSources: don't use this — instead let
/// CavePlayerAudioTracker update CaveAudioOcclusion.sourceInCave at runtime.
/// </summary>
public class CaveZoneMember : MonoBehaviour
{
    [Tooltip("Is this AudioSource permanently inside the cave?")]
    [SerializeField] private bool isInCave = false;

    private void Awake()
    {
        var occlusion = GetComponent<CaveAudioOcclusion>();
        if (occlusion != null)
            occlusion.sourceInCave = isInCave;
    }
}
