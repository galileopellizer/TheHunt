using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Attach to the effigy prefab (same GameObject as EffigyState).
/// - Shows a bright pulsing sound orb while the effigy is burning (monster-only).
/// - Plays a monster roar locally on the monster client when the effigy finishes burning.
/// </summary>
[DisallowMultipleComponent]
public class EffigyOrbAndSound : MonoBehaviour
{
    [Header("Orb")]
    [SerializeField] private string orbShaderName = "Hidden/SoundOrb";
    [SerializeField] private Color orbColor = new Color(1f, 0.4f, 0.05f, 1f); // fiery orange
    [SerializeField] private float orbScale = 5f;
    [SerializeField] private float orbMaxDistance = 60f;   // effigy burning is very loud
    [SerializeField] private float orbBaseIntensity = 1f;
    [SerializeField] private Vector3 orbOffset = new Vector3(0f, 1f, 0f);

    [Header("Pulse")]
    [SerializeField] private float pulseSpeed = 2.5f;
    [SerializeField] private float pulseAmount = 0.18f;    // ±18% scale oscillation

    [Header("Linger")]
    [Tooltip("How long the orb stays visible after the effigy finishes burning.")]
    [SerializeField] private float lingerDuration = 5f;

    [Header("Effigy Bell")]
    [SerializeField] private AudioClip bellClip;
    [SerializeField] private float bellVolume = 1f;

    // Runtime
    private EffigyState effigyState;
    private GameObject orbGO;
    private MeshRenderer orbRenderer;
    private Material orbMaterial;
    private AudioSource audioSource;
    private static readonly int IntensityId = Shader.PropertyToID("_Intensity");

    private bool wasSubscribed;
    private bool wasBurning;
    private float lingerUntil = -1f;

    // Local monster cache
    private float nextRoleRefreshTime;
    private bool cachedIsLocalMonster;
    private Transform cachedMonsterTransform;

    // ── Unity ─────────────────────────────────────────────────────────────────
    private void Awake()
    {
        effigyState = GetComponent<EffigyState>();

        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.spatialBlend = 1f;   // 3D — bell rings from the effigy position
        audioSource.minDistance  = 5f;
        audioSource.maxDistance  = 60f;
        audioSource.rolloffMode  = AudioRolloffMode.Logarithmic;
        audioSource.playOnAwake  = false;

        CreateOrb();
        SetOrbVisible(false);
    }

    private void OnDestroy()
    {
        Unsubscribe();
        if (orbGO != null) Destroy(orbGO);
        if (orbMaterial != null) Destroy(orbMaterial);
    }

    private void OnEnable()  => TrySubscribe();
    private void OnDisable() => Unsubscribe();

    private void Update()
    {
        TrySubscribe();

        if (effigyState == null) return;

        bool burning = effigyState.IsBurning.Value || Time.time < lingerUntil;

        if (!burning || !IsLocalMonster())
        {
            SetOrbVisible(false);
            return;
        }

        // Distance-based intensity
        float dist = GetDistanceToLocalMonster();
        if (dist > orbMaxDistance)
        {
            SetOrbVisible(false);
            return;
        }

        float t         = 1f - Mathf.Clamp01(dist / orbMaxDistance);
        float intensity = orbBaseIntensity * t;

        // Pulse
        float pulse = 1f + Mathf.Sin(Time.time * pulseSpeed) * pulseAmount;
        float scale = orbScale * pulse;

        SetOrbVisible(true);
        orbGO.transform.localScale = Vector3.one * scale;
        orbMaterial.SetFloat(IntensityId, intensity);
    }

    // ── Subscriptions ─────────────────────────────────────────────────────────
    private void TrySubscribe()
    {
        if (wasSubscribed || effigyState == null || !effigyState.IsSpawned) return;

        effigyState.IsBurned.OnValueChanged += OnBurnedChanged;
        wasSubscribed = true;
    }

    private void Unsubscribe()
    {
        if (!wasSubscribed || effigyState == null) return;
        effigyState.IsBurned.OnValueChanged -= OnBurnedChanged;
        wasSubscribed = false;
    }

    private void OnBurnedChanged(bool previous, bool current)
    {
        if (!current) return;

        // Keep the orb alive for the linger duration
        lingerUntil = Time.time + lingerDuration;

        // Count burned effigies directly — don't rely on GameRoundManager's delayed sync
        int burned = 0, total = 0;
        foreach (var e in Object.FindObjectsOfType<EffigyState>())
        {
            if (e == null || !e.isActiveAndEnabled) continue;
            total++;
            if (e.IsBurned.Value) burned++;
        }

        bool isLastEffigy = total > 0 && burned >= total;
        if (isLastEffigy && bellClip != null && audioSource != null)
            audioSource.PlayOneShot(bellClip, bellVolume);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────
    private bool IsLocalMonster()
    {
        if (Time.time < nextRoleRefreshTime)
            return cachedIsLocalMonster;

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
            Debug.LogWarning($"[EffigyOrbAndSound] Shader not found: {orbShaderName}", this);
            return;
        }

        orbMaterial = new Material(shader) { name = "EffigyOrb_Mat (Runtime)" };
        orbMaterial.SetColor("_Color", orbColor);

        orbGO = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        orbGO.name = "EffigyOrb";
        Destroy(orbGO.GetComponent<Collider>());

        orbGO.transform.SetParent(transform, false);
        orbGO.transform.localPosition = orbOffset;
        orbGO.transform.localScale    = Vector3.one * orbScale;

        orbRenderer = orbGO.GetComponent<MeshRenderer>();
        orbRenderer.sharedMaterial    = orbMaterial;
        orbRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        orbRenderer.receiveShadows    = false;
        orbRenderer.enabled           = false;
    }
}
