using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Plays a heartbeat sound for the local human player when the monster
/// is invisible (passive or active stealth) and nearby.
/// Attach to the GamePlayerBody prefab.
/// </summary>
[DisallowMultipleComponent]
public class HumanHeartbeat : MonoBehaviour
{
    [Header("Audio")]
    [SerializeField] private AudioClip heartbeatClip;
    private AudioSource audioSource;

    [Header("Distances")]
    [SerializeField] private float triggerDistance = 12f;
    [SerializeField] private float panicDistance   = 4f;

    [Header("BPM")]
    [SerializeField] private float calmBpm  = 60f;
    [SerializeField] private float panicBpm = 160f;

    [Header("Audio")]
    [SerializeField, Range(0f, 1f)] private float volume = 0.3f;

    private GamePlayerBodyRole roleComponent;
    private NetworkObject networkObject;
    private MonsterStealth cachedStealth;
    private float nextStealthRefresh;
    private float nextBeatTime;
    private bool wasInvisible;

    private void Awake()
    {
        roleComponent = GetComponent<GamePlayerBodyRole>();
        networkObject = GetComponent<NetworkObject>();

        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake  = false;
        audioSource.spatialBlend = 0f;
        audioSource.volume       = volume;
    }

    private void Update()
    {
        if (networkObject == null || !networkObject.IsOwner) return;
        if (roleComponent == null || roleComponent.Role.Value != PlayerRole.Human) return;

        if (Time.time > nextStealthRefresh)
        {
            nextStealthRefresh = Time.time + 1f;
            cachedStealth = null;
            foreach (var s in FindObjectsOfType<MonsterStealth>())
            {
                var role = s.GetComponent<GamePlayerBodyRole>();
                if (role != null && role.Role.Value == PlayerRole.Monster)
                {
                    cachedStealth = s;
                    break;
                }
            }
        }

        if (cachedStealth == null) return;

        bool monsterInvisible = cachedStealth.IsPassiveInvisible.Value || cachedStealth.IsActiveInvisible.Value;
        float dist = Vector3.Distance(transform.position, cachedStealth.transform.position);
        bool inRange = dist <= triggerDistance;

        // Stop immediately when monster becomes visible or out of range
        if ((!monsterInvisible || !inRange) && wasInvisible)
        {
            audioSource.Stop();
            wasInvisible = false;
            return;
        }

        if (!monsterInvisible || !inRange) return;

        wasInvisible = true;

        float t          = 1f - Mathf.Clamp01(dist / panicDistance);
        float currentBpm = Mathf.Lerp(calmBpm, panicBpm, t);

        if (Time.time >= nextBeatTime)
        {
            nextBeatTime = Time.time + (60f / currentBpm);
            if (heartbeatClip != null)
            {
                audioSource.volume = volume;
                audioSource.PlayOneShot(heartbeatClip);
            }
        }
    }
}
