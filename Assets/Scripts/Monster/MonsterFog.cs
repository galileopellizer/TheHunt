using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Attach once to the player body prefab.
/// Applies different fog settings depending on whether the local player is monster or human.
/// </summary>
[DisallowMultipleComponent]
public class MonsterFog : NetworkBehaviour
{
    [System.Serializable]
    public class FogSettings
    {
        public bool enabled = true;
        public Color fogColor = new Color(0.05f, 0.05f, 0.05f);
        public float fogStart = 0f;
        public float fogEnd   = 15f;
    }

    [Header("Monster Fog")]
    [SerializeField] private FogSettings monsterFog = new FogSettings
    {
        enabled  = true,
        fogColor = new Color(0.05f, 0.05f, 0.05f),
        fogStart = 0f,
        fogEnd   = 15f
    };

    [Header("Human Fog")]
    [SerializeField] private FogSettings humanFog = new FogSettings
    {
        enabled  = true,
        fogColor = new Color(0.05f, 0.05f, 0.05f),
        fogStart = 0f,
        fogEnd   = 60f
    };

    private GamePlayerBodyRole bodyRole;
    private bool fogApplied;

    public override void OnNetworkSpawn()
    {
        if (!IsOwner) return;
        bodyRole = GetComponent<GamePlayerBodyRole>();
        if (bodyRole != null)
        {
            bodyRole.Role.OnValueChanged += OnRoleChanged;
            ApplyForRole(bodyRole.Role.Value);
        }
    }

    public override void OnNetworkDespawn()
    {
        if (bodyRole != null)
            bodyRole.Role.OnValueChanged -= OnRoleChanged;
        RestoreFog();
    }

    private void OnRoleChanged(PlayerRole old, PlayerRole current) => ApplyForRole(current);

    private void ApplyForRole(PlayerRole role)
    {
        FogSettings settings = role == PlayerRole.Monster ? monsterFog : humanFog;

        if (!settings.enabled)
        {
            RestoreFog();
            return;
        }

        fogApplied = true;
        RenderSettings.fog             = true;
        RenderSettings.fogMode         = FogMode.Linear;
        RenderSettings.fogColor        = settings.fogColor;
        RenderSettings.fogStartDistance = settings.fogStart;
        RenderSettings.fogEndDistance   = settings.fogEnd;
    }

    private void RestoreFog()
    {
        if (!fogApplied) return;
        fogApplied = false;
        RenderSettings.fog = false;
    }
}
