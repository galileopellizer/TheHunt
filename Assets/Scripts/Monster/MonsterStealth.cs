using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Monster stealth system.
///
/// PASSIVE: Stand still for passiveDelay seconds → fade invisible + enhanced senses.
///          Move → fade visible again + screech.
///
/// ACTIVE:  Press Space → consume 1 charge → fade invisible for activeDuration seconds
///          or until the monster lands a hit → reveal + screech (jumpscare).
///          Maximum activeCharges charges per match.
/// </summary>
public class MonsterStealth : NetworkBehaviour
{
    [Header("Passive stealth")]
    [SerializeField] private float passiveDelay       = 3f;   // seconds still before going invisible
    [SerializeField] private float passiveFadeTime    = 1.5f;
    [SerializeField] private float moveThreshold      = 0.05f;

    [Header("Active stealth")]
    [SerializeField] private KeyCode activeKey        = KeyCode.Space;
    [SerializeField] private float activeDuration     = 10f;
    [SerializeField] private int   maxActiveCharges   = 2;
    [SerializeField] private float activeFadeTime     = 0.5f;

    [Header("Enhanced senses (while passive invisible)")]
    [SerializeField] private float senseMultiplier    = 1.5f; // multiplied on top of SoundOrb distances

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip[] revealClips; // played when becoming visible again

    // ── Network state ──────────────────────────────────────────────────────────
    public NetworkVariable<bool> IsPassiveInvisible = new(false,
        NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public NetworkVariable<bool> IsActiveInvisible = new(false,
        NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public NetworkVariable<int> ActiveChargesLeft = new(2,
        NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // ── Static accessor so SoundOrbVisual can read enhanced sense mult ─────────
    public static MonsterStealth LocalMonsterStealth { get; private set; }
    public float CurrentSenseMultiplier => IsPassiveInvisible.Value ? senseMultiplier : 1f;

    // ── UI helpers ─────────────────────────────────────────────────────────────
    public float PassiveDelayTotal  => passiveDelay;
    public float StillProgress      => (IsPassiveInvisible.Value || IsActiveInvisible.Value) ? 1f : Mathf.Clamp01(stillTimer / passiveDelay);
    public int   MaxActiveCharges   => maxActiveCharges;

    public void ResetStillTimer() => stillTimer = 0f;

    // ── Runtime ────────────────────────────────────────────────────────────────
    private float stillTimer;
    private float activeStealthElapsed;
    private Vector3 lastPosition;
    private Coroutine activeCooldownCoroutine;

    public float ActiveStealthProgress => IsActiveInvisible.Value
        ? 1f - Mathf.Clamp01(activeStealthElapsed / activeDuration)
        : 0f;

    // ── NGO ────────────────────────────────────────────────────────────────────
    public override void OnNetworkSpawn()
    {
        if (IsServer)
            ActiveChargesLeft.Value = maxActiveCharges;

        IsPassiveInvisible.OnValueChanged += OnPassiveChanged;
        IsActiveInvisible.OnValueChanged  += OnActiveChanged;

        if (IsOwner)
        {
            lastPosition = transform.position;
            var role = GetComponent<GamePlayerBodyRole>();
            if (role != null)
                role.Role.OnValueChanged += OnRoleChanged;
            // Set immediately if role is already assigned
            if (role != null && role.Role.Value == PlayerRole.Monster)
                LocalMonsterStealth = this;
        }

        // Snap to initial state — ensure renderers are visible
        SetRenderersEnabled(GetMonsterRenderers(), true);
    }

    public override void OnNetworkDespawn()
    {
        IsPassiveInvisible.OnValueChanged -= OnPassiveChanged;
        IsActiveInvisible.OnValueChanged  -= OnActiveChanged;

        if (LocalMonsterStealth == this) LocalMonsterStealth = null;

        if (IsOwner)
        {
            var role = GetComponent<GamePlayerBodyRole>();
            if (role != null) role.Role.OnValueChanged -= OnRoleChanged;
        }
    }

    private void OnRoleChanged(PlayerRole prev, PlayerRole current)
    {
        if (!IsOwner) return;
        LocalMonsterStealth = current == PlayerRole.Monster ? this : null;
    }

    // ── Owner update ───────────────────────────────────────────────────────────
    private void Update()
    {
        if (!IsOwner) return;

        var role = GetComponent<GamePlayerBodyRole>();
        if (role == null || role.Role.Value != PlayerRole.Monster) return;

        var health = GetComponent<PlayerHealth>();
        if (health != null && health.IsDead.Value) return;

        if (IsActiveInvisible.Value)
            activeStealthElapsed += Time.deltaTime;
        else
            activeStealthElapsed = 0f;

        TrackPassiveStealth();
        HandleActiveInput();
    }

    private void TrackPassiveStealth()
    {
        if (IsActiveInvisible.Value) return; // active takes priority

        float moved = Vector3.Distance(transform.position, lastPosition);
        lastPosition = transform.position;

        if (moved < moveThreshold)
        {
            stillTimer += Time.deltaTime;
            Debug.Log($"[Stealth] Still {stillTimer:F1}s moved={moved:F4}");
            if (stillTimer >= passiveDelay && !IsPassiveInvisible.Value)
            {
                Debug.Log("[Stealth] Triggering passive invisible");
                SetPassiveInvisibleServerRpc(true);
            }
        }
        else
        {
            stillTimer = 0f;
            if (IsPassiveInvisible.Value)
                SetPassiveInvisibleServerRpc(false);
        }
    }

    private void HandleActiveInput()
    {
        if (!Input.GetKeyDown(activeKey)) return;
        if (IsActiveInvisible.Value) return;
        if (ActiveChargesLeft.Value <= 0) return;

        ActivateActiveStealthServerRpc();
    }

    // ── ServerRpcs ─────────────────────────────────────────────────────────────
    [ServerRpc]
    private void SetPassiveInvisibleServerRpc(bool invisible)
    {
        if (IsActiveInvisible.Value) return;
        IsPassiveInvisible.Value = invisible;
    }

    [ServerRpc]
    private void ActivateActiveStealthServerRpc()
    {
        if (ActiveChargesLeft.Value <= 0) return;
        if (IsActiveInvisible.Value) return;

        ActiveChargesLeft.Value--;
        IsPassiveInvisible.Value = false;
        IsActiveInvisible.Value  = true;

        if (activeCooldownCoroutine != null) StopCoroutine(activeCooldownCoroutine);
        activeCooldownCoroutine = StartCoroutine(ActiveStealthTimeout());
    }

    // Called from MonsterAttack on any attack attempt (hit or miss)
    public void OnAttack()
    {
        if (!IsServer) return;

        if (IsActiveInvisible.Value)
        {
            if (activeCooldownCoroutine != null) StopCoroutine(activeCooldownCoroutine);
            StartCoroutine(RevealAfterDelay(0f));
        }
        else if (IsPassiveInvisible.Value)
        {
            IsPassiveInvisible.Value = false;
        }
    }

    private IEnumerator ActiveStealthTimeout()
    {
        yield return new WaitForSeconds(activeDuration);
        RevealActive();
    }

    private IEnumerator RevealAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        RevealActive();
    }

    private void RevealActive()
    {
        IsActiveInvisible.Value = false;
        ScreechClientRpc();
    }

    // ── Visibility callbacks (all clients) ─────────────────────────────────────
    private void OnPassiveChanged(bool prev, bool current)
    {
        StopAllAlphaCoroutines();
        fadeCoroutine = StartCoroutine(FadeAlpha(current ? 0f : 1f, passiveFadeTime));
    }

    private void OnActiveChanged(bool prev, bool current)
    {
        StopAllAlphaCoroutines();
        float target = current ? 0f : 1f;
        StartCoroutine(FadeAlpha(target, activeFadeTime));
    }

    [ClientRpc]
    private void ScreechClientRpc()
    {
        if (audioSource != null && revealClips != null && revealClips.Length > 0)
        {
            var clip = revealClips[Random.Range(0, revealClips.Length)];
            if (clip != null) audioSource.PlayOneShot(clip);
        }

        // Trigger reveal animation on monster
        var visuals = GetComponent<PlayerRoleVisuals>();
        visuals?.MonsterAnimController?.TriggerReveal();
    }

    // ── Visibility ─────────────────────────────────────────────────────────────
    private Coroutine fadeCoroutine;

    private void StopAllAlphaCoroutines()
    {
        if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
    }

    private List<Renderer> GetMonsterRenderers()
    {
        var result = new List<Renderer>();
        int hiddenLayer = LayerMask.NameToLayer("LocalHidden");
        foreach (var r in GetComponentsInChildren<Renderer>(true))
        {
            if (r.gameObject.layer == hiddenLayer) continue;
            if (r.gameObject.name == "SoundOrb") continue; // managed by SoundOrbVisual
            result.Add(r);
        }
        return result;
    }

    private IEnumerator FadeAlpha(float targetAlpha, float duration)
    {
        // Flicker out effect — quick flashes then hide, or reverse
        var renderers = GetMonsterRenderers();
        bool goingInvisible = targetAlpha < 0.5f;

        if (goingInvisible)
        {
            // Flicker then disappear
            for (int i = 0; i < 4; i++)
            {
                SetRenderersEnabled(renderers, false);
                yield return new WaitForSeconds(0.07f);
                SetRenderersEnabled(renderers, true);
                yield return new WaitForSeconds(0.07f);
            }
            SetRenderersEnabled(renderers, false);
        }
        else
        {
            // Reappear with a flash
            SetRenderersEnabled(renderers, true);
        }
    }

    private void SetRenderersEnabled(List<Renderer> renderers, bool enabled)
    {
        foreach (var r in renderers)
            if (r != null) r.enabled = enabled;
    }
}
