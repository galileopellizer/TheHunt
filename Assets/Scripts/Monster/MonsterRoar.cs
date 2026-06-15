using System.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Attach to the player body prefab (same object as GamePlayerBodyRole).
/// Plays a roar sound locally whenever an effigy is burned, but only if
/// this body is the local monster player.
/// </summary>
[DisallowMultipleComponent]
public class MonsterRoar : MonoBehaviour
{
    [Header("Audio")]
    [SerializeField] private AudioClip roarClip;
    [SerializeField] private float roarVolume = 1f;
    [Tooltip("Seconds to wait after the effigy burns before playing the roar. Set this to bell clip length minus ~0.5s.")]
    [SerializeField] private float roarDelay = 2f;

    private GamePlayerBodyRole bodyRole;
    private AudioSource audioSource;
    private GameRoundManager roundManager;
    private int lastBurnedCount = -1;
    private bool subscribed;

    private void Awake()
    {
        bodyRole = GetComponent<GamePlayerBodyRole>();

        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.spatialBlend = 1f;   // 3D — everyone hears it from the monster's position
        audioSource.minDistance  = 5f;
        audioSource.maxDistance  = 50f;
        audioSource.rolloffMode  = AudioRolloffMode.Logarithmic;
        audioSource.playOnAwake  = false;
    }

    private void Update()
    {
        // Only the monster body should play the roar
        if (bodyRole == null || bodyRole.Role.Value != PlayerRole.Monster) return;

        TrySubscribe();

        if (roundManager == null) return;

        int burned = roundManager.BurnedEffigies.Value;
        if (lastBurnedCount < 0)
        {
            lastBurnedCount = burned;
            return;
        }

        if (burned > lastBurnedCount)
        {
            lastBurnedCount = burned;
            // Only roar on the last effigy
            if (burned >= roundManager.TotalEffigies.Value && roundManager.TotalEffigies.Value > 0)
                PlayRoar();
        }
    }

    private void TrySubscribe()
    {
        if (subscribed || roundManager != null) return;
        roundManager = Object.FindObjectOfType<GameRoundManager>();
        if (roundManager != null)
        {
            lastBurnedCount = roundManager.BurnedEffigies.Value;
            subscribed = true;
        }
    }

    private void PlayRoar()
    {
        if (roarClip == null || audioSource == null) return;
        StartCoroutine(PlayRoarDelayed());
    }

    private IEnumerator PlayRoarDelayed()
    {
        if (roarDelay > 0f)
            yield return new WaitForSeconds(roarDelay);
        audioSource.PlayOneShot(roarClip, roarVolume);
    }

}
