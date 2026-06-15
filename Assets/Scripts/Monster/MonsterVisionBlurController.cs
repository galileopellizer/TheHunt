using UnityEngine;

[DisallowMultipleComponent]
public class MonsterVisionBlurController : MonoBehaviour
{
    [Header("Depth Blur")]
    [Tooltip("Distance where blur starts (meters).")]
    [SerializeField] private float blurStart = 2.0f;
    [Tooltip("Distance where blur is fully applied (meters).")]
    [SerializeField] private float blurEnd = 6.0f;
    [Tooltip("Maximum blur radius in pixels at full blur.")]
    [SerializeField] private float maxBlurRadius = 2.0f;

    [Header("Tint")]
    [SerializeField] private Color tintColor = new Color(0.35f, 0.0f, 0.0f, 1f);
    [Range(0f, 1f)][SerializeField] private float tintStrength = 0.5f;
    [SerializeField] private bool tintUsesBlur = true;
    [Range(0f, 1f)][SerializeField] private float tintMin = 0.15f;

    [Header("Vignette")]
    [SerializeField] private Color vignetteColor = Color.black;
    [Range(0f, 1f)][SerializeField] private float vignetteIntensity = 0.45f;
    [Range(0f, 1f)][SerializeField] private float vignetteSmoothness = 0.2f;
    [Range(0.1f, 1f)][SerializeField] private float vignetteRadius = 0.8f;

    public float BlurStart => blurStart;
    public float BlurEnd => blurEnd;
    public float MaxBlurRadius => maxBlurRadius;
    public Color TintColor => tintColor;
    public float TintStrength => tintStrength;
    public bool TintUsesBlur => tintUsesBlur;
    public float TintMin => tintMin;
    public Color VignetteColor => vignetteColor;
    public float VignetteIntensity => vignetteIntensity;
    public float VignetteSmoothness => vignetteSmoothness;
    public float VignetteRadius => vignetteRadius;

    public bool IsActive { get; private set; }

    [Header("Role Toggle")]
    [SerializeField] private GamePlayerBodyRole bodyRole;
    [SerializeField] private bool forceActive;
    private bool subscribed;

    private void Awake()
    {
        TryResolveBodyRole();
    }

    private void OnEnable()
    {
        // Defer subscription until we can resolve the local owner role.
        subscribed = false;
    }

    private void OnDisable()
    {
        if (bodyRole != null && subscribed)
            bodyRole.Role.OnValueChanged -= OnRoleChanged;
        subscribed = false;
    }

    private void OnRoleChanged(PlayerRole oldRole, PlayerRole newRole)
    {
        ApplyRole(newRole);
    }

    private void ApplyRole(PlayerRole role)
    {
        IsActive = forceActive || role == PlayerRole.Monster;
    }

    private void Update()
    {
        if (subscribed)
            return;

        if (bodyRole == null)
            TryResolveBodyRole();

        if (bodyRole == null)
        {
            IsActive = forceActive;
            return;
        }

        if (forceActive)
        {
            IsActive = true;
            return;
        }

        if (!bodyRole.IsOwner)
        {
            IsActive = false;
            return;
        }

        bodyRole.Role.OnValueChanged += OnRoleChanged;
        subscribed = true;
        ApplyRole(bodyRole.Role.Value);
    }

    private void TryResolveBodyRole()
    {
        if (bodyRole != null)
            return;

        bodyRole = GetComponentInParent<GamePlayerBodyRole>();
        if (bodyRole != null)
            return;

        // Fallback: find local owner body if hierarchy isn't a parent chain.
        foreach (var role in FindObjectsOfType<GamePlayerBodyRole>())
        {
            if (role.IsOwner)
            {
                bodyRole = role;
                break;
            }
        }
    }

    private void OnValidate()
    {
        if (blurEnd < blurStart)
            blurEnd = blurStart;
        if (maxBlurRadius < 0f)
            maxBlurRadius = 0f;
    }
}
