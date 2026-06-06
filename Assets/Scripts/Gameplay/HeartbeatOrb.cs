using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Plays a 2D looping heartbeat sound for the local human during enrage.
/// Pitch speeds up as the monster gets closer.
/// </summary>
[DisallowMultipleComponent]
public class HeartbeatOrb : MonoBehaviour
{
    [Header("Human Heartbeat Audio")]
    [SerializeField] private AudioClip heartbeatClip;
    [SerializeField] private float volume          = 0.6f;
    [SerializeField] private float panicStartDist  = 10f;   // distance at which pitch starts rising
    [SerializeField] private float basePitch       = 1f;
    [SerializeField] private float panicPitch      = 1.8f;

    private GamePlayerBodyRole ownerRole;
    private NetworkObject ownerNetObj;
    private GameRoundManager roundManager;
    private AudioSource audioSource;

    private float nextMonsterRefreshTime;
    private Transform cachedMonsterTransform;

    private void Awake()
    {
        ownerRole   = GetComponent<GamePlayerBodyRole>()   ?? GetComponentInParent<GamePlayerBodyRole>();
        ownerNetObj = GetComponent<NetworkObject>()        ?? GetComponentInParent<NetworkObject>();

        audioSource               = gameObject.AddComponent<AudioSource>();
        audioSource.spatialBlend  = 0f;
        audioSource.loop          = true;
        audioSource.playOnAwake   = false;
        audioSource.volume        = 0f;
        audioSource.clip          = heartbeatClip;
    }

    private void Start()
    {
        roundManager = Object.FindObjectOfType<GameRoundManager>();
    }

    private void Update()
    {
        if (heartbeatClip == null) return;
        if (ownerNetObj == null || !ownerNetObj.IsOwner) return;
        if (ownerRole == null || ownerRole.Role.Value != PlayerRole.Human) return;

        bool enraged = roundManager != null && roundManager.Phase.Value == GamePhase.Enrage;

        if (enraged)
        {
            if (!audioSource.isPlaying) audioSource.Play();
            audioSource.volume = Mathf.MoveTowards(audioSource.volume, volume, Time.deltaTime * 0.5f);

            float dist       = GetDistanceToMonster();
            float proximityT = 1f - Mathf.Clamp01(dist / panicStartDist);
            audioSource.pitch = Mathf.Lerp(basePitch, panicPitch, proximityT);
        }
        else
        {
            audioSource.volume = Mathf.MoveTowards(audioSource.volume, 0f, Time.deltaTime * 0.5f);
            if (audioSource.volume <= 0f && audioSource.isPlaying)
                audioSource.Stop();
        }
    }

    private float GetDistanceToMonster()
    {
        if (Time.time > nextMonsterRefreshTime)
        {
            nextMonsterRefreshTime = Time.time + 0.25f;
            cachedMonsterTransform = null;

            var nm = NetworkManager.Singleton;
            if (nm != null)
            {
                foreach (var role in FindObjectsOfType<GamePlayerBodyRole>())
                {
                    if (role.Role.Value == PlayerRole.Monster)
                    {
                        cachedMonsterTransform = role.transform;
                        break;
                    }
                }
            }
        }

        if (cachedMonsterTransform != null)
            return Vector3.Distance(transform.position, cachedMonsterTransform.position);
        return float.MaxValue;
    }
}
