using System.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Drives the monster model's Animator based on movement, health and game phase.
/// Attach to the same GameObject as PlayerController and GamePlayerBodyRole.
/// The Animator should be on the model child object — assign it in the inspector.
/// </summary>
[DisallowMultipleComponent]
public class MonsterAnimationController : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Animator animator;

    [Header("Rage")]
    [Tooltip("Delay before rage animation plays — match this to MonsterRoar roarDelay.")]
    [SerializeField] private float rageAnimDelay = 2f;
    [Tooltip("How long the color shift to red takes (should match rage clip length).")]
    [SerializeField] private float rageColorDuration = 3f;
    [SerializeField] private Color rageColor = new Color(1f, 0.05f, 0.05f, 1f);

    [Header("Animator Parameters")]
    [SerializeField] private string isWalkingParam     = "IsWalking";      // bool
    [SerializeField] private string isRunningParam     = "IsRunning";      // bool
    [SerializeField] private string isWalkingBackParam = "IsWalkingBack";  // bool
    [SerializeField] private string strafeParam   = "StrafeX";   // float  — -1 left, 0 none, 1 right
    [SerializeField] private string isDeadParam   = "IsDead";    // bool
    [SerializeField] private string rageParam     = "Rage";      // trigger
    [SerializeField] private string getHitParam   = "GetHit";    // trigger
    [SerializeField] private string jumpParam      = "Jump";      // trigger
    [SerializeField] private string[] attackParams = { "Attack" }; // add one entry per attack animation trigger
    [SerializeField] private string revealParam   = "Reveal";   // trigger

    [Header("Smoothing")]
    [SerializeField] private float animDamping = 0.1f;

    // Refs resolved at runtime
    private PlayerController playerController;
    private PlayerHealth playerHealth;
    private GamePlayerBodyRole bodyRole;
    private GameRoundManager roundManager;
    private NetworkObject networkObject;

    // State tracking
    private bool rageFired;
    private bool deathFired;
    private float smoothedStrafe;
    private Vector3 lastPosition;

    // ── Unity ─────────────────────────────────────────────────────────────────
    private void Awake()
    {
        if (animator == null)
            animator = GetComponent<Animator>() ?? GetComponentInChildren<Animator>();

        if (animator != null)
            animator.applyRootMotion = false;
    }

    /// <summary>
    /// Called by PlayerRoleVisuals after the monster model is instantiated,
    /// passing in references from the parent GamePlayerBody.
    /// </summary>
    public void Initialize(PlayerController pc, PlayerHealth ph, GamePlayerBodyRole role, NetworkObject no)
    {
        playerController = pc;
        playerHealth     = ph;
        bodyRole         = role;
        networkObject    = no;
        roundManager     = Object.FindObjectOfType<GameRoundManager>();
        lastPosition     = transform.position;

        if (no != null && no.IsOwner)
            HideModelForLocalOwner();
    }

    private void HideModelForLocalOwner()
    {
        foreach (var r in GetComponentsInChildren<Renderer>(true))
            r.enabled = false;
    }

    private void Update()
    {
        if (animator == null) return;

        HandleDeath();
        if (deathFired) return;

        HandleRage();
        HandleMovement();
    }

    // ── Death ─────────────────────────────────────────────────────────────────
    private void HandleDeath()
    {
        if (deathFired) return;
        if (playerHealth == null || !playerHealth.IsDead.Value) return;

        deathFired = true;
        animator.SetBool(isDeadParam, true);
    }

    // ── Rage (enrage phase) ───────────────────────────────────────────────────
    private void HandleRage()
    {
        if (rageFired) return;
        if (roundManager == null) return;
        if (roundManager.Phase.Value != GamePhase.Enrage) return;

        rageFired = true;
        if (animator == null) { Debug.LogWarning("[MonsterAnim] animator is null — Rage failed"); return; }
        StartCoroutine(TriggerRageDelayed());
    }

    // ── Movement ──────────────────────────────────────────────────────────────
    private void HandleMovement()
    {
        float targetSpeed  = 0f;
        float targetStrafe = 0f;

        bool isWalking     = false;
        bool isWalkingBack = false;
        bool isRunning     = false;

        if (playerController != null && networkObject != null && networkObject.IsOwner)
        {
            bool w     = Input.GetKey(KeyCode.W);
            bool s     = Input.GetKey(KeyCode.S);
            bool a     = Input.GetKey(KeyCode.A);
            bool d     = Input.GetKey(KeyCode.D);
            bool shift = Input.GetKey(KeyCode.LeftShift);

            // Order matters — establish mutually exclusive states
            isRunning     = shift && (w || a || d);
            isWalkingBack = s && !w && !shift;
            isWalking     = !isRunning && !isWalkingBack && (w || a || d);

            if (a && !d)      targetStrafe = -1f;
            else if (d && !a) targetStrafe =  1f;
            else              targetStrafe =  0f;
        }
        else
        {
            Vector3 localVel = transform.InverseTransformDirection(
                (transform.position - lastPosition) / Mathf.Max(Time.deltaTime, 0.0001f));
            lastPosition = transform.position;

            float forwardSpeed = localVel.z;
            float speed        = localVel.magnitude;
            isRunning     = speed >= 7f && forwardSpeed > 0f;
            isWalkingBack = forwardSpeed < -0.5f;
            isWalking     = !isRunning && !isWalkingBack && speed >= 0.5f;
            targetStrafe  = Mathf.Clamp(localVel.x / 7f, -1f, 1f);
        }

        smoothedStrafe = Mathf.Lerp(smoothedStrafe, targetStrafe, Time.deltaTime / Mathf.Max(animDamping, 0.001f));
        if (Mathf.Abs(smoothedStrafe) < 0.01f) smoothedStrafe = 0f;

        animator.SetBool(isWalkingParam,     isWalking);
        animator.SetBool(isRunningParam,     isRunning);
        animator.SetBool(isWalkingBackParam, isWalkingBack);
        animator.SetFloat(strafeParam,       smoothedStrafe);

        // Scale animation playback speed to match actual movement speed
        float baseSpeed = isRunning ? 8f : 5.5f; // match monsterSpeed / humanSpeed in PlayerController
        var cc = GetComponent<CharacterController>() ?? GetComponentInParent<CharacterController>();
        if (cc != null && baseSpeed > 0f)
        {
            float actualSpeed = new Vector3(cc.velocity.x, 0f, cc.velocity.z).magnitude;
            animator.speed = (actualSpeed > 0.1f) ? actualSpeed / baseSpeed : 1f;
        }

    }

    // ── Public triggers (call from other scripts if needed) ───────────────────
    public void TriggerGetHit()
    {
        if (animator != null)
            animator.SetTrigger(getHitParam);
    }

    public void TriggerJump()
    {
        if (animator != null)
            animator.SetTrigger(jumpParam);
    }

    private IEnumerator TriggerRageDelayed()
    {
        if (rageAnimDelay > 0f)
            yield return new WaitForSeconds(rageAnimDelay);

        animator.SetTrigger(rageParam);
        StartCoroutine(ShiftColorToRed());
    }

    private IEnumerator ShiftColorToRed()
    {
        // Find all skinned mesh renderers on the monster model
        var renderers = GetComponentsInChildren<SkinnedMeshRenderer>();
        if (renderers.Length == 0) yield break;

        // Create material instances so we don't modify the shared asset
        var materials = new Material[renderers.Length];
        var originalColors = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            materials[i] = new Material(renderers[i].material);
            renderers[i].material = materials[i];
            originalColors[i] = materials[i].GetColor("_BaseColor");
        }

        // Lerp to red over rageColorDuration
        float elapsed = 0f;
        while (elapsed < rageColorDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / rageColorDuration);

            for (int i = 0; i < materials.Length; i++)
                materials[i].SetColor("_BaseColor", Color.Lerp(originalColors[i], rageColor, t));

            yield return null;
        }
    }

    public int PickAttackIndex()
    {
        if (attackParams == null || attackParams.Length == 0) return 0;
        return Random.Range(0, attackParams.Length);
    }

    public void TriggerAttack(int index = 0)
    {
        if (animator == null) { Debug.LogWarning("[MonsterAnim] animator is null — TriggerAttack failed"); return; }
        if (attackParams == null || attackParams.Length == 0) return;
        index = Mathf.Clamp(index, 0, attackParams.Length - 1);
        animator.SetTrigger(attackParams[index]);
    }

    public void TriggerReveal()
    {
        if (animator == null) return;
        animator.SetTrigger(revealParam);
    }
}
