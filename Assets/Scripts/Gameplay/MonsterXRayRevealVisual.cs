using Unity.Netcode;
using UnityEngine;

[DisallowMultipleComponent]
public class MonsterXRayRevealVisual : MonoBehaviour
{
    [Header("X-Ray Look")]
    [SerializeField] private Color xrayColor = new(0.15f, 1f, 0.65f, 0.7f);
    [SerializeField] private Shader xrayShader;
    [SerializeField] private string xrayShaderName = "Hidden/MonsterXRay";

    [Header("Refs")]
    [SerializeField] private GamePlayerBodyRevealState revealState;

    private MeshRenderer xrayRenderer;
    private Material xrayMaterial;
    private bool lastRevealValue;
    private float nextPollTime;
    private bool lastLocalMonster;
    private float nextLocalRoleRefreshTime;
    private NetworkObject ownerNetworkObject;
    private bool subscribed;

    private void Awake()
    {
        ownerNetworkObject = GetComponentInParent<NetworkObject>();
        TryResolveRevealState();
        EnsureXRayRenderer();
    }

    private void OnEnable()
    {
        subscribed = false;
        TryResolveRevealState();
    }

    private void OnDisable()
    {
        if (revealState != null && subscribed)
            revealState.RevealToMonster.OnValueChanged -= HandleRevealChanged;
        subscribed = false;
    }

    private void HandleRevealChanged(bool previous, bool current)
    {
        ApplyReveal(current);
    }

    private void ApplyReveal(bool shouldShow)
    {
        lastRevealValue = shouldShow;
        bool showForLocal = shouldShow && IsLocalMonster();
        if (xrayRenderer != null)
            xrayRenderer.enabled = showForLocal;
    }

    private void Update()
    {
        if (revealState == null)
        {
            TryResolveRevealState();
            if (revealState == null)
            {
                nextPollTime = Time.time + 0.25f;
                return;
            }
        }

        if (Time.time < nextPollTime)
            return;

        if (xrayRenderer == null)
            EnsureXRayRenderer();

        bool current = GetRevealActive();
        bool localMonster = IsLocalMonster();
        if (current != lastRevealValue || localMonster != lastLocalMonster)
            ApplyReveal(current);

        nextPollTime = Time.time + 0.05f;
    }

    private bool IsLocalMonster()
    {
        if (Time.time < nextLocalRoleRefreshTime)
            return lastLocalMonster;

        lastLocalMonster = false;
        var network = NetworkManager.Singleton;
        if (network != null)
        {
            ulong localId = network.LocalClientId;
            foreach (var role in FindObjectsOfType<GamePlayerBodyRole>())
            {
                var netObj = role.GetComponent<NetworkObject>();
                if (netObj != null && netObj.OwnerClientId == localId)
                {
                    lastLocalMonster = role.Role.Value == PlayerRole.Monster;
                    goto Done;
                }
            }

            var localObject = network.LocalClient != null ? network.LocalClient.PlayerObject : null;
            if (localObject != null)
            {
                var roleState = localObject.GetComponent<PlayerRoleState>();
                if (roleState != null)
                    lastLocalMonster = roleState.Role.Value == PlayerRole.Monster;
            }
        }

    Done:
        nextLocalRoleRefreshTime = Time.time + 0.25f;
        return lastLocalMonster;
    }

    private bool GetRevealActive()
    {
        if (revealState == null)
            return false;

        bool flag = revealState.RevealToMonster.Value;
        var network = NetworkManager.Singleton;
        if (network == null)
            return flag;

        double now = network.ServerTime.Time;
        double until = revealState.RevealUntilServerTime.Value;
        return flag || (until > 0d && now < until);
    }

    private void TryResolveRevealState()
    {
        if (revealState == null)
            revealState = GetComponentInParent<GamePlayerBodyRevealState>();

        if (revealState == null && ownerNetworkObject != null)
        {
            ulong ownerId = ownerNetworkObject.OwnerClientId;
            foreach (var state in FindObjectsOfType<GamePlayerBodyRevealState>())
            {
                var netObj = state.GetComponent<NetworkObject>();
                if (netObj != null && netObj.OwnerClientId == ownerId)
                {
                    revealState = state;
                    break;
                }
            }
        }

        if (revealState != null && !subscribed)
        {
            revealState.RevealToMonster.OnValueChanged += HandleRevealChanged;
            subscribed = true;
            ApplyReveal(revealState.RevealToMonster.Value);
        }
    }

    private void EnsureXRayRenderer()
    {
        if (xrayRenderer != null)
            return;

        var sourceFilter = GetComponent<MeshFilter>();
        var sourceRenderer = GetComponent<MeshRenderer>();
        if (sourceFilter == null || sourceRenderer == null)
            return;

        if (xrayShader == null)
            xrayShader = Shader.Find(xrayShaderName);

        if (xrayShader == null)
        {
            Debug.LogWarning($"[MonsterXRayRevealVisual] Shader not found: {xrayShaderName}", this);
            return;
        }

        xrayMaterial = new Material(xrayShader)
        {
            name = "MonsterXRay_Mat (Runtime)"
        };
        xrayMaterial.SetColor("_XRayColor", xrayColor);

        var xrayObj = new GameObject("XRay")
        {
            layer = gameObject.layer
        };
        xrayObj.transform.SetParent(transform, false);

        var xrayFilter = xrayObj.AddComponent<MeshFilter>();
        xrayFilter.sharedMesh = sourceFilter.sharedMesh;

        xrayRenderer = xrayObj.AddComponent<MeshRenderer>();
        xrayRenderer.sharedMaterial = xrayMaterial;
        xrayRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        xrayRenderer.receiveShadows = false;
        xrayRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        xrayRenderer.enabled = false;
    }
}
