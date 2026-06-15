using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Drives the HumanAnimator parameters.
/// Attach to the human model prefab root (which has the Animator).
/// </summary>
public class HumanAnimationController : MonoBehaviour
{
    private static readonly int SpeedId      = Animator.StringToHash("Speed");
    private static readonly int SpeedZId     = Animator.StringToHash("SpeedZ");
    private static readonly int CrouchId     = Animator.StringToHash("IsCrouching");
    private static readonly int DeadId       = Animator.StringToHash("IsDead");

    private Animator animator;
    private PlayerController playerController;
    private PlayerHealth playerHealth;
    private NetworkObject networkObject;
    private GamePlayerBodyRole bodyRole;

    // Remote player speed tracking
    private Vector3 lastPos;
    private float smoothedSpeed;
    private float smoothedSpeedZ;

    public void Initialize(PlayerController pc, PlayerHealth ph, GamePlayerBodyRole role, NetworkObject no)
    {
        playerController = pc;
        playerHealth     = ph;
        bodyRole         = role;
        networkObject    = no;

        animator  = GetComponent<Animator>();
        lastPos   = transform.root.position;
    }

    private void Update()
    {
        if (animator == null) return;

        // Death
        bool isDead = playerHealth != null && playerHealth.IsDead.Value;
        animator.SetBool(DeadId, isDead);
        if (isDead)
        {
            animator.applyRootMotion = true;
            return;
        }
        animator.applyRootMotion = false;

        if (networkObject != null && networkObject.IsOwner)
            UpdateOwner();
        else
            UpdateRemote();
    }

    private void UpdateOwner()
    {
        if (playerController == null) return;

        // Use CharacterController velocity for accurate speed
        var cc = playerController.GetComponent<CharacterController>();
        Vector3 vel = cc != null ? cc.velocity : Vector3.zero;
        vel.y = 0f;

        float speed  = vel.magnitude;
        // Project onto local forward/back for SpeedZ
        float speedZ = Vector3.Dot(vel, transform.root.forward);

        animator.SetFloat(SpeedId,  speed,  0.1f, Time.deltaTime);
        animator.SetFloat(SpeedZId, speedZ, 0.1f, Time.deltaTime);
        animator.SetBool(CrouchId, playerController.IsCrouching.Value);
    }

    private void UpdateRemote()
    {
        Vector3 pos    = transform.root.position;
        Vector3 delta  = pos - lastPos;
        lastPos        = pos;

        float rawSpeed = delta.magnitude / Mathf.Max(Time.deltaTime, 0.0001f);
        smoothedSpeed  = Mathf.Lerp(smoothedSpeed, rawSpeed, Time.deltaTime * 8f);

        // Project onto root forward for backward detection
        float rawZ    = Vector3.Dot(delta.normalized, transform.root.forward) * rawSpeed;
        smoothedSpeedZ = Mathf.Lerp(smoothedSpeedZ, rawZ, Time.deltaTime * 8f);

        bool isCrouching = playerController != null && playerController.IsCrouching.Value;

        animator.SetFloat(SpeedId,  smoothedSpeed,  0.1f, Time.deltaTime);
        animator.SetFloat(SpeedZId, smoothedSpeedZ, 0.1f, Time.deltaTime);
        animator.SetBool(CrouchId, isCrouching);
    }
}
