using Unity.Netcode;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(GamePlayerBodyRole))]
public class PlayerHealth : NetworkBehaviour
{
    [Header("Health")]
    [SerializeField] private float maxHealth = 100f;

    [Header("Audio")]
    [SerializeField] private AudioSource deathSfxSource;
    [SerializeField] private AudioClip humanDeathClip;
    [SerializeField, Range(0f, 1f)] private float deathVolume = 0.55f;

    public NetworkVariable<float> CurrentHealth = new(
        100f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public NetworkVariable<bool> IsDead = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private GamePlayerBodyRole bodyRole;
    private bool deathApplied;

    private void Awake()
    {
        bodyRole = GetComponent<GamePlayerBodyRole>();

        if (deathSfxSource == null)
            deathSfxSource = gameObject.AddComponent<AudioSource>();

        deathSfxSource.playOnAwake = false;
        deathSfxSource.spatialBlend = 1f;
        deathSfxSource.minDistance = 1f;
        deathSfxSource.maxDistance = 16f;
        deathSfxSource.rolloffMode = AudioRolloffMode.Logarithmic;
        deathSfxSource.dopplerLevel = 0f;
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            CurrentHealth.Value = Mathf.Max(1f, maxHealth);
            IsDead.Value = false;
            deathApplied = false;
        }

        IsDead.OnValueChanged += OnDeadChanged;
        ApplyDeathState(IsDead.Value);
    }

    public override void OnNetworkDespawn()
    {
        IsDead.OnValueChanged -= OnDeadChanged;
    }

    public void ApplyDamage(float amount)
    {
        if (!IsServer) return;
        if (amount <= 0f) return;
        if (IsDead.Value) return;

        if (bodyRole == null)
            bodyRole = GetComponent<GamePlayerBodyRole>();

        // Phase 1: only humans are valid damage targets.
        if (bodyRole != null && bodyRole.Role.Value != PlayerRole.Human)
            return;

        float next = Mathf.Max(0f, CurrentHealth.Value - amount);
        CurrentHealth.Value = next;

        HitFlashClientRpc();

        if (next <= 0f)
            IsDead.Value = true;
    }

    [ClientRpc]
    private void HitFlashClientRpc()
    {
        if (!IsOwner) return;
        HitFlash.Instance?.Flash();
    }

    private void OnDeadChanged(bool oldValue, bool newValue)
    {
        ApplyDeathState(newValue);
    }

    private void ApplyDeathState(bool dead)
    {
        if (!dead || deathApplied)
            return;

        deathApplied = true;
        PlayDeathSfx();

        var controller = GetComponent<PlayerController>();
        if (controller != null)
            controller.enabled = false;

        var cc = GetComponent<CharacterController>();
        if (cc != null)
            cc.enabled = false;

        var attack = GetComponent<MonsterAttack>();
        if (attack != null)
            attack.enabled = false;

        var interactor = GetComponent<HumanEffigyInteractor>();
        if (interactor != null)
            interactor.enabled = false;

        var footsteps = GetComponent<FootstepController>();
        if (footsteps != null)
            footsteps.enabled = false;

    }

    public float MaxHealth => Mathf.Max(1f, maxHealth);

    private void PlayDeathSfx()
    {
        if (bodyRole != null && bodyRole.Role.Value != PlayerRole.Human)
            return;

        if (humanDeathClip == null || deathSfxSource == null)
            return;

        deathSfxSource.PlayOneShot(humanDeathClip, Mathf.Clamp01(deathVolume));
    }
}
