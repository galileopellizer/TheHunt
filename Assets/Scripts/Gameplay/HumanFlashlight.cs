using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Attach to the player body prefab.
/// Gives humans a toggleable flashlight (F key).
/// The spotlight is synced over the network so other players see the beam.
/// Monster never gets a flashlight.
/// </summary>
[DisallowMultipleComponent]
public class HumanFlashlight : NetworkBehaviour
{
    [Header("Flashlight")]
    [SerializeField] private KeyCode toggleKey = KeyCode.F;
    [SerializeField] private Light flashlight;

    [Header("Light Settings")]
    [SerializeField] private float range       = 20f;
    [SerializeField] private float spotAngle   = 60f;
    [SerializeField] private float intensity   = 3f;
    [SerializeField] private Color lightColor  = Color.white;

    public NetworkVariable<bool> FlashlightOn = new(
        true,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner
    );

    private GamePlayerBodyRole bodyRole;

    public override void OnNetworkSpawn()
    {
        bodyRole = GetComponent<GamePlayerBodyRole>();

        // Configure light settings
        if (flashlight != null)
        {
            flashlight.type       = LightType.Spot;
            flashlight.range      = range;
            flashlight.spotAngle  = spotAngle;
            flashlight.intensity  = intensity;
            flashlight.color      = lightColor;
        }

        FlashlightOn.OnValueChanged += (_, on) => ApplyFlashlight(on);

        if (bodyRole != null)
            bodyRole.Role.OnValueChanged += (_, role) => OnRoleChanged(role);

        // Apply initial state once role is known
        ApplyFlashlight(FlashlightOn.Value);
    }

    public override void OnNetworkDespawn()
    {
        FlashlightOn.OnValueChanged -= (_, on) => ApplyFlashlight(on);
    }

    private void Update()
    {
        if (!IsOwner) return;
        if (bodyRole == null || bodyRole.Role.Value != PlayerRole.Human) return;

        if (Input.GetKeyDown(toggleKey))
            FlashlightOn.Value = !FlashlightOn.Value;
    }

    private void OnRoleChanged(PlayerRole role)
    {
        if (role != PlayerRole.Human)
        {
            if (IsOwner) FlashlightOn.Value = false;
            ApplyFlashlight(false);
        }
        else
        {
            ApplyFlashlight(FlashlightOn.Value);
        }
    }

    private void ApplyFlashlight(bool on)
    {
        if (flashlight == null) return;

        bool isHuman = bodyRole != null && bodyRole.Role.Value == PlayerRole.Human;
        flashlight.enabled = on && isHuman;
    }
}
