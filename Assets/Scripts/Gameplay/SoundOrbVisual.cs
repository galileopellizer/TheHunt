using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Attach to every human player body.
/// - Walking/running: shows orb at varying intensity based on distance.
/// - Idle during enrage: shows same orb with a heartbeat pulse.
/// </summary>
[DisallowMultipleComponent]
public class SoundOrbVisual : MonoBehaviour
{
    [Header("Shader")]
    [SerializeField] private string orbShaderName = "Hidden/SoundOrb";

    [Header("Walk Orb")]
    [SerializeField] private float walkSpeedThreshold = 0.4f;
    [SerializeField] private float walkMaxDistance    = 15f;
    [SerializeField] private float walkBaseIntensity  = 0.45f;
    [SerializeField] private float walkOrbScale       = 2.5f;

    [Header("Run Orb")]
    [SerializeField] private float runMaxDistance   = 35f;
    [SerializeField] private float runBaseIntensity = 1f;
    [SerializeField] private float runOrbScale      = 4f;

    [Header("Heartbeat (idle during enrage)")]
    [SerializeField] private float heartbeatMaxDistance = 12f;
    [SerializeField] private float heartbeatBPM         = 70f;
    [SerializeField] private float heartbeatPanicBPM    = 160f;   // BPM when monster is right next to you
    [SerializeField] private float heartbeatPanicDist   = 8f;     // distance at which panic BPM kicks in
    [SerializeField] private float heartbeatIntensity   = 0.6f;

    [Header("Smoothing")]
    [SerializeField] private float fadeSpeed = 8f;

    [Header("Orb Offset")]
    [SerializeField] private Vector3 orbOffset = new Vector3(0f, 0f, 0f);

    // Runtime
    private GamePlayerBodyRole ownerRole;
    private GamePlayerBodyRevealState revealState;
    private GameRoundManager roundManager;
    private NetworkObject ownerNetObj;
    private GameObject orbGO;
    private MeshRenderer orbRenderer;
    private Material orbMaterial;
    private static readonly int IntensityId = Shader.PropertyToID("_Intensity");

    private Vector3 lastPos;
    private float smoothedSpeed;
    private float currentDisplayIntensity;
    private float beatPhase;

    private float nextRoleRefreshTime;
    private bool cachedIsLocalMonster;
    private Transform cachedMonsterTransform;

    // ── Unity ─────────────────────────────────────────────────────────────────
    private void Awake()
    {
        ownerRole   = GetComponent<GamePlayerBodyRole>();
        revealState = GetComponent<GamePlayerBodyRevealState>()
                   ?? GetComponentInParent<GamePlayerBodyRevealState>()
                   ?? GetComponentInChildren<GamePlayerBodyRevealState>();
        ownerNetObj = GetComponent<NetworkObject>();
        lastPos     = transform.position;

        if (revealState == null)
            Debug.LogWarning("[SoundOrbVisual] GamePlayerBodyRevealState not found.", this);
    }

    private void Start()
    {
        roundManager = Object.FindObjectOfType<GameRoundManager>();
        CreateOrb();
        SetOrbVisible(false);
    }

    private void OnDestroy()
    {
        if (orbGO != null)     Destroy(orbGO);
        if (orbMaterial != null) Destroy(orbMaterial);
    }

    private void Update()
    {
        if (!IsLocalMonster())                                          { SetOrbVisible(false); return; }
        if (ownerRole != null && ownerRole.Role.Value != PlayerRole.Human) { SetOrbVisible(false); return; }

        bool isRunning = revealState != null && revealState.RevealToMonster.Value;

        // Check if human is crouching — suppresses walk/run orb
        var pc = GetComponentInParent<PlayerController>();
        bool isCrouching = pc != null && pc.IsCrouching.Value;

        Vector3 pos    = transform.position;
        float rawSpeed = (pos - lastPos).magnitude / Mathf.Max(Time.deltaTime, 0.0001f);
        lastPos        = pos;
        smoothedSpeed  = Mathf.Lerp(smoothedSpeed, rawSpeed, Time.deltaTime * 5f);
        bool isWalking = !isRunning && !isCrouching && smoothedSpeed >= walkSpeedThreshold;
        bool isIdle    = !isRunning && !isWalking;

        float dist          = GetDistanceToLocalMonster();
        float targetIntensity = 0f;
        float targetScale     = walkOrbScale;

        bool enraged = roundManager != null && roundManager.Phase.Value == GamePhase.Enrage;

        // Scale sensing with monster progression — more effigies burned = sharper senses
        float senseMult = roundManager != null ? roundManager.MonsterSpeedMultiplier.Value : 1f;

        float scaledRunDist   = runMaxDistance   * senseMult;
        float scaledWalkDist  = walkMaxDistance  * senseMult;
        float scaledBeatDist  = heartbeatMaxDistance * senseMult;

        if (isRunning && !isCrouching && dist < scaledRunDist)
        {
            float t         = 1f - Mathf.Clamp01(dist / scaledRunDist);
            targetIntensity = runBaseIntensity * t * senseMult;
            targetScale     = runOrbScale;
        }
        else if (isWalking && dist < scaledWalkDist)
        {
            float t         = 1f - Mathf.Clamp01(dist / scaledWalkDist);
            targetIntensity = walkBaseIntensity * t * senseMult;
            targetScale     = walkOrbScale;
        }
        else if (isIdle && enraged && dist < scaledBeatDist)
        {
            // Heartbeat pulse — lub-dub rhythm, faster when monster is close
            float proximityT = 1f - Mathf.Clamp01(dist / heartbeatPanicDist);
            float bpm        = Mathf.Lerp(heartbeatBPM, heartbeatPanicBPM, proximityT);

            beatPhase += Time.deltaTime * (bpm / 60f);
            beatPhase %= 1f;

            float pulse = 0f;
            if (beatPhase < 0.08f)
                pulse = Mathf.Sin(beatPhase / 0.08f * Mathf.PI);
            else if (beatPhase > 0.15f && beatPhase < 0.27f)
                pulse = Mathf.Sin((beatPhase - 0.15f) / 0.12f * Mathf.PI) * 0.7f;

            float distT     = 1f - Mathf.Clamp01(dist / heartbeatMaxDistance);
            targetIntensity = heartbeatIntensity * distT * pulse;
            targetScale     = Mathf.Lerp(walkOrbScale, runOrbScale, proximityT) * (0.85f + pulse * 0.15f);
        }

        // Smooth fade for walk/run, direct for heartbeat (it has its own pulse)
        if (isIdle && enraged)
            currentDisplayIntensity = targetIntensity;
        else
            currentDisplayIntensity = Mathf.Lerp(currentDisplayIntensity, targetIntensity, Time.deltaTime * fadeSpeed);

        bool show = currentDisplayIntensity > 0.01f;
        SetOrbVisible(show);

        if (show && orbMaterial != null)
        {
            orbMaterial.SetFloat(IntensityId, currentDisplayIntensity);
            orbGO.transform.localScale = Vector3.one * targetScale;
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────
    private bool IsLocalMonster()
    {
        if (Time.time < nextRoleRefreshTime) return cachedIsLocalMonster;

        nextRoleRefreshTime    = Time.time + 0.25f;
        cachedIsLocalMonster   = false;
        cachedMonsterTransform = null;

        var nm = NetworkManager.Singleton;
        if (nm == null) return false;

        ulong localId = nm.LocalClientId;
        foreach (var role in FindObjectsOfType<GamePlayerBodyRole>())
        {
            var no = role.GetComponent<NetworkObject>();
            if (no != null && no.OwnerClientId == localId)
            {
                cachedIsLocalMonster   = role.Role.Value == PlayerRole.Monster;
                cachedMonsterTransform = cachedIsLocalMonster ? role.transform : null;
                break;
            }
        }

        return cachedIsLocalMonster;
    }

    private float GetDistanceToLocalMonster()
    {
        if (cachedMonsterTransform != null)
            return Vector3.Distance(transform.position, cachedMonsterTransform.position);
        return float.MaxValue;
    }

    private void SetOrbVisible(bool visible)
    {
        if (orbRenderer != null && orbRenderer.enabled != visible)
            orbRenderer.enabled = visible;
    }

    private void CreateOrb()
    {
        if (orbGO != null) return;

        var shader = Shader.Find(orbShaderName);
        if (shader == null)
        {
            Debug.LogWarning($"[SoundOrbVisual] Shader not found: {orbShaderName}", this);
            return;
        }

        orbMaterial = new Material(shader) { name = "SoundOrb_Mat (Runtime)" };

        orbGO = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        orbGO.name = "SoundOrb";
        Destroy(orbGO.GetComponent<Collider>());

        orbGO.transform.SetParent(transform, false);
        orbGO.transform.localPosition = orbOffset;
        orbGO.transform.localScale    = Vector3.one * walkOrbScale;

        orbRenderer = orbGO.GetComponent<MeshRenderer>();
        orbRenderer.sharedMaterial    = orbMaterial;
        orbRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        orbRenderer.receiveShadows    = false;
        orbRenderer.enabled           = false;
    }
}
