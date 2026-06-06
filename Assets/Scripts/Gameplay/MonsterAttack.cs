using Unity.Netcode;
using UnityEngine;

[DisallowMultipleComponent]
public class MonsterAttack : NetworkBehaviour
{
    [Header("Attack")]
    [SerializeField] private KeyCode attackKey = KeyCode.Mouse0;
    [SerializeField] private float attackRange = 2f;
    [SerializeField] private float attackCooldown = 0.7f;
    [SerializeField] private float attackDamage = 34f;
    [SerializeField] private float attackSlowDuration = 2f;
    [SerializeField] private float attackSlowMultiplier = 0.2f; // 80% speed reduction
    [SerializeField] private LayerMask playerLayer = Physics.DefaultRaycastLayers;

    [Header("Audio")]
    [SerializeField] private AudioSource attackSfxSource;
    [SerializeField] private AudioClip[] attackHitClips;
    [SerializeField] private AudioClip[] attackMissClips;
    [SerializeField, Range(0f, 1f)] private float hitVolume = 0.45f;
    [SerializeField, Range(0f, 1f)] private float missVolume = 0.25f;

    [SerializeField] private float attackSlowDurationEnrage = 3f;

    private float nextAttackTime;
    private GamePlayerBodyRole selfRole;
    private PlayerHealth selfHealth;
    private MonsterAnimationController animController;
    private GameRoundManager roundManager;

    private void Awake()
    {
        selfRole = GetComponent<GamePlayerBodyRole>();
        selfHealth = GetComponent<PlayerHealth>();
        animController = GetComponent<MonsterAnimationController>()
                      ?? GetComponentInChildren<MonsterAnimationController>(true)
                      ?? GetComponentInParent<MonsterAnimationController>();

        if (attackSfxSource == null)
            attackSfxSource = gameObject.AddComponent<AudioSource>();

        attackSfxSource.playOnAwake = false;
        attackSfxSource.spatialBlend = 1f;
        attackSfxSource.minDistance = 1f;
        attackSfxSource.maxDistance = 14f;
        attackSfxSource.rolloffMode = AudioRolloffMode.Logarithmic;
        attackSfxSource.dopplerLevel = 0f;
    }

    private void Update()
    {
        if (!IsOwner) return;
        if (selfRole == null || selfRole.Role.Value != PlayerRole.Monster) return;
        if (animController == null)
        {
            var visuals = GetComponent<PlayerRoleVisuals>();
            if (visuals != null) animController = visuals.MonsterAnimController;
        }
        if (Time.time < nextAttackTime) return;
        if (!Input.GetKeyDown(attackKey)) return;
        if (selfHealth != null && selfHealth.IsDead.Value) return;

        nextAttackTime = Time.time + attackCooldown;
        animController?.TriggerAttack();
        TryAttackServerRpc();
    }

    [ServerRpc]
    private void TryAttackServerRpc(ServerRpcParams rpcParams = default)
    {
        if (!IsServer) return;
        if (selfRole == null || selfRole.Role.Value != PlayerRole.Monster) return;
        if (selfHealth != null && selfHealth.IsDead.Value) return;

        TriggerAttackAnimClientRpc();

        PlayerHealth target = FindBestVictim();
        if (target == null)
        {
            PlayAttackSfxClientRpc(false);
            return;
        }

        target.ApplyDamage(attackDamage);
        PlayAttackSfxClientRpc(true);

        // Slow the monster itself after hitting (shorter during enrage)
        if (roundManager == null) roundManager = FindObjectOfType<GameRoundManager>();
        bool isEnrage = roundManager != null && roundManager.Phase.Value == GamePhase.Enrage;
        float slowDur = isEnrage ? attackSlowDurationEnrage : attackSlowDuration;
        SlowSelfClientRpc(slowDur);
    }

    private PlayerHealth FindBestVictim()
    {
        var hits = Physics.OverlapSphere(transform.position, attackRange, playerLayer, QueryTriggerInteraction.Ignore);

        PlayerHealth best = null;
        float bestSqrDist = float.PositiveInfinity;

        foreach (var hit in hits)
        {
            var victimBody = hit.GetComponentInParent<NetworkObject>();
            if (victimBody == null || !victimBody.IsSpawned) continue;
            if (victimBody.OwnerClientId == OwnerClientId) continue;

            var victimRole = victimBody.GetComponent<GamePlayerBodyRole>();
            if (victimRole == null || victimRole.Role.Value != PlayerRole.Human) continue;

            var health = victimBody.GetComponent<PlayerHealth>();
            if (health == null || health.IsDead.Value) continue;

            float sqrDist = (victimBody.transform.position - transform.position).sqrMagnitude;
            if (sqrDist < bestSqrDist)
            {
                bestSqrDist = sqrDist;
                best = health;
            }
        }

        return best;
    }


    [ClientRpc]
    private void SlowSelfClientRpc(float slowDuration)
    {
        if (!IsOwner) return;
        var pc = GetComponent<PlayerController>();
        if (pc != null) pc.ApplySlow(slowDuration, attackSlowMultiplier);
        GetComponentInChildren<Camera>()?.GetComponent<CameraShake>()?.Shake();
    }

    [ClientRpc]
    private void TriggerAttackAnimClientRpc()
    {
        if (IsOwner) return; // owner already triggered it locally
        if (animController == null)
        {
            var visuals = GetComponent<PlayerRoleVisuals>();
            if (visuals != null) animController = visuals.MonsterAnimController;
        }
        animController?.TriggerAttack();
    }

    [ClientRpc]
    private void PlayAttackSfxClientRpc(bool didHit)
    {
        if (attackSfxSource == null)
            return;

        var clips = didHit ? attackHitClips : attackMissClips;
        if (clips == null || clips.Length == 0) return;
        var clip = clips[Random.Range(0, clips.Length)];

        float volume = didHit ? hitVolume : missVolume;
        attackSfxSource.PlayOneShot(clip, Mathf.Clamp01(volume));
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
#endif
}
