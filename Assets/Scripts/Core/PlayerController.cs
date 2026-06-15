using System.Collections;
using Unity.Netcode;
using UnityEngine;
using Object = UnityEngine.Object;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : NetworkBehaviour
{
    [Header("Refs")]
    [SerializeField] private Transform cameraPivot; // optional (head)
    [SerializeField] private Camera playerCamera;

    [Header("Move")]
    [SerializeField] private float humanSpeed = 5.5f;
    [SerializeField] private float monsterSpeed = 6f;
    [SerializeField] private float monsterSprintMultiplier = 1.33f;
    [SerializeField] private float gravity = -9.81f;

    [Header("Look")]
    [SerializeField] private float mouseSensitivity = 2f;
    [SerializeField] private float pitchClamp = 85f;

    [Header("Run (Human)")]
    [SerializeField] private float humanRunMultiplier = 1.5f;
    [SerializeField] private float maxStamina = 5f;
    [SerializeField] private float staminaDrainPerSecond = 1.2f;
    [SerializeField] private float staminaRegenPerSecond = 0.9f;
    [SerializeField] private float staminaRegenDelay = 0.5f;
    [SerializeField] private float minStaminaToRun = 0.1f;

    [Header("Crouch")]
    [SerializeField] private float crouchSpeedMultiplier = 0.35f;

    public NetworkVariable<bool> IsCrouching = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner
    );

    private GamePlayerBodyRole bodyRole;
    private CharacterController cc;
    private float verticalVel;
    private float pitch;
    private float moveSpeed = 5f;
    private float stunEndTime;
    private GamePlayerBodyRevealState revealState;
    private float nextRevealReportTime;
    private float lastHorizontalSpeed;
    private float currentStamina;
    private float staminaRegenStartTime;
    private bool isRunning;
    private bool wasRunning;
    private GameRoundManager roundManager;
    private float slowMultiplier = 1f;
    private float slowEndTime;

    // Ladder
    private bool isOnLadder;
    [SerializeField] private float climbSpeed = 3f;
    [Header("Ladder Audio")]
    [SerializeField] private AudioClip ladderClimbClip;
    [SerializeField] private float ladderVolume = 0.7f;
    [SerializeField] private float ladderPitch = 1f;
    private AudioSource ladderAudio;
    public void SetOnLadder(bool value)
    {
        isOnLadder = value;
        if (!value) StopLadderAudio();
    }

    
    
    private void Awake()
    {
        cc = GetComponent<CharacterController>();
        if (cameraPivot == null) cameraPivot = transform;
    }

    private bool initialized;
    
    public override void OnNetworkSpawn()
    {
        if (!IsOwner)
        {
            cc.enabled = false;
            if (playerCamera) playerCamera.enabled = false;
            if (playerCamera) playerCamera.GetComponent<AudioListener>().enabled = false;
            return;
        }

        
        
        bodyRole = GetComponent<GamePlayerBodyRole>();
        revealState = GetComponent<GamePlayerBodyRevealState>();
        currentStamina = maxStamina;
        roundManager = Object.FindObjectOfType<GameRoundManager>();
        
        if (bodyRole != null)
        {
            ApplyRoleSpeed(bodyRole.Role.Value);
            bodyRole.Role.OnValueChanged += (oldRole, newRole) => ApplyRoleSpeed(newRole);
        }
        
        mouseSensitivity = PlayerPrefs.GetFloat("MouseSensitivity", mouseSensitivity);

Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        cc.enabled = false;
        StartCoroutine(EnableControllerNextFrame());
    }
    
    private void ApplyRoleSpeed(PlayerRole role)
    {
        moveSpeed = role == PlayerRole.Monster ? monsterSpeed : humanSpeed;
    }

    private IEnumerator EnableControllerNextFrame()
    {
        yield return null;
        verticalVel = -2f;
        cc.enabled = true;
        initialized = true;   // <- move it here
    }



    private void Update()
    {
        if (!IsOwner || !initialized) return;
        if (cc == null || !cc.enabled) return; 

        if (IsStunned())
        {
            StopLadderAudio();
            isOnLadder = false;
            ApplyGravityOnly();
            return;
        }

        Look();
        if (isOnLadder)
            ClimbLadder();
        else
            Move();
        ReportRevealSpeed();
    }

    private void Look()
    {
        var settings = FindObjectOfType<SettingsMenu>();
        if (settings != null && settings.IsOpen) return;

        float mx = Input.GetAxis("Mouse X") * mouseSensitivity;
        float my = Input.GetAxis("Mouse Y") * mouseSensitivity;

        transform.Rotate(Vector3.up * mx);

        pitch -= my;
        pitch = Mathf.Clamp(pitch, -pitchClamp, pitchClamp);

        if (cameraPivot != null)
            cameraPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    private void Move()
    {
        if (cc == null || !cc.enabled) return;
        float x = Input.GetAxisRaw("Horizontal");
        float z = Input.GetAxisRaw("Vertical");

        Vector3 move = (transform.right * x + transform.forward * z).normalized;

        float speed = moveSpeed;

        isRunning = false;

        float currentSlow = Time.time < slowEndTime ? slowMultiplier : 1f;

        if (bodyRole != null && bodyRole.Role.Value == PlayerRole.Monster)
        {
            float globalMult = roundManager != null ? roundManager.MonsterSpeedMultiplier.Value : 1f;
            speed = monsterSpeed * globalMult;
            if (Input.GetKey(KeyCode.LeftShift))
                speed *= monsterSprintMultiplier;
            speed *= currentSlow;
        }
        else
        {
            speed = humanSpeed;
            bool wantsCrouch = Input.GetKey(KeyCode.LeftControl) && move.sqrMagnitude > 0.0001f;
            bool wantsRun = Input.GetKey(KeyCode.LeftShift) && move.sqrMagnitude > 0.0001f && !wantsCrouch;
            bool canRun = currentStamina > minStaminaToRun;
            isRunning = wantsRun && canRun;
            bool isCrouching = wantsCrouch && !isRunning;
            IsCrouching.Value = isCrouching;
            if (isRunning)
                speed *= humanRunMultiplier;
            else if (isCrouching)
                speed *= crouchSpeedMultiplier;
            speed *= currentSlow;
        }

        UpdateStamina(isRunning);

        if (cc.isGrounded && verticalVel < 0f)
            verticalVel = -2f;

        verticalVel += gravity * Time.deltaTime;

        Vector3 velocity = move * speed;
        velocity.y = verticalVel;

        lastHorizontalSpeed = move.magnitude * speed;
        cc.Move(velocity * Time.deltaTime);
    }

    private void StartLadderAudio()
    {
        if (ladderClimbClip == null) return;
        if (ladderAudio == null)
        {
            ladderAudio = gameObject.AddComponent<AudioSource>();
            ladderAudio.spatialBlend  = 1f;
            ladderAudio.rolloffMode   = AudioRolloffMode.Linear;
            ladderAudio.minDistance   = 1f;
            ladderAudio.maxDistance   = 20f;
            ladderAudio.loop          = true;
            ladderAudio.playOnAwake   = false;
            ladderAudio.volume        = ladderVolume;
            ladderAudio.clip          = ladderClimbClip;
        }
        if (!ladderAudio.isPlaying)
        {
            ladderAudio.pitch = ladderPitch * Random.Range(0.9f, 1.1f);
            ladderAudio.time  = Random.Range(0f, ladderClimbClip.length);
            ladderAudio.Play();
        }
    }

    private void StopLadderAudio()
    {
        if (ladderAudio != null && ladderAudio.isPlaying)
            ladderAudio.Stop();
    }

    private void ClimbLadder()
    {
        if (cc == null || !cc.enabled) return;

        float z = Input.GetAxisRaw("Vertical");   // W = up, S = down
        float x = Input.GetAxisRaw("Horizontal");

        verticalVel = 0f;   // suppress gravity while on ladder

        Vector3 velocity = new Vector3(transform.right.x * x, z * climbSpeed, transform.right.z * x);
        cc.Move(velocity * Time.deltaTime);

        // Audio: only play while actually moving up or down
        if (Mathf.Abs(z) > 0.1f)
            StartLadderAudio();
        else
            StopLadderAudio();
    }

    private void ReportRevealSpeed()
    {
        if (revealState == null)
            return;
        if (bodyRole == null || bodyRole.Role.Value != PlayerRole.Human)
            return;

        if (Time.time < nextRevealReportTime)
            return;

        revealState.ReportMoveSpeedServerRpc(lastHorizontalSpeed);
        nextRevealReportTime = Time.time + 0.1f;
    }

    private void ApplyGravityOnly()
    {
        if (cc == null || !cc.enabled) return;

        if (cc.isGrounded && verticalVel < 0f)
            verticalVel = -2f;

        verticalVel += gravity * Time.deltaTime;
        Vector3 velocity = new Vector3(0f, verticalVel, 0f);
        cc.Move(velocity * Time.deltaTime);
    }

    private void UpdateStamina(bool running)
    {
        if (bodyRole == null || bodyRole.Role.Value != PlayerRole.Human)
        {
            isRunning = false;
            wasRunning = false;
            currentStamina = maxStamina;
            if (IsCrouching.Value) IsCrouching.Value = false;
            return;
        }

        if (running)
        {
            currentStamina -= staminaDrainPerSecond * Time.deltaTime;
            if (currentStamina <= 0f)
            {
                currentStamina = 0f;
                isRunning = false;
                staminaRegenStartTime = Time.time + staminaRegenDelay;
            }
        }
        else
        {
            if (wasRunning)
                staminaRegenStartTime = Time.time + staminaRegenDelay;

            if (Time.time >= staminaRegenStartTime)
                currentStamina += staminaRegenPerSecond * Time.deltaTime;
        }

        currentStamina = Mathf.Clamp(currentStamina, 0f, maxStamina);
        wasRunning = running;
    }

    private bool IsStunned()
    {
        return Time.time < stunEndTime;
    }

    public void Stun(float duration)
    {
        if (!IsOwner) return;
        if (duration <= 0f) return;

        float endTime = Time.time + duration;
        if (endTime > stunEndTime)
            stunEndTime = endTime;
    }

    public float CurrentStamina => currentStamina;
    public float MaxStamina => maxStamina;
    public bool IsRunning => isRunning;
    public bool IsMonsterSprinting => bodyRole != null && bodyRole.Role.Value == PlayerRole.Monster && Input.GetKey(KeyCode.LeftShift);
    public bool IsMonsterWalking => bodyRole != null && bodyRole.Role.Value == PlayerRole.Monster
        && (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.D))
        && !Input.GetKey(KeyCode.LeftShift);
    public bool IsMonsterRunning => bodyRole != null && bodyRole.Role.Value == PlayerRole.Monster
        && (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.D))
        && Input.GetKey(KeyCode.LeftShift);
    public bool IsHumanRole => bodyRole != null && bodyRole.Role.Value == PlayerRole.Human;
    public void SetMouseSensitivity(float value) => mouseSensitivity = value;
    public void ApplySlow(float duration, float multiplier)
    {
        slowMultiplier = multiplier;
        slowEndTime    = Time.time + duration;
        Debug.Log($"[PlayerController] Slow applied — multiplier={multiplier} duration={duration}");
    }
}
