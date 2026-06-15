using Unity.Netcode;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Attach to the player body prefab.
/// Enables a post-process Volume only for the local monster player.
/// Assign your Global Volume to the volumeObject field.
/// </summary>
[DisallowMultipleComponent]
public class MonsterVisionVolume : NetworkBehaviour
{
    [SerializeField] private string volumeName = "Global Volume";
    private Volume monsterVolume;

    private GamePlayerBodyRole bodyRole;

    public override void OnNetworkSpawn()
    {
        if (!IsOwner)
        {
            enabled = false;
            return;
        }

        // Find volume in scene by name
        var go = GameObject.Find(volumeName);
        if (go != null) monsterVolume = go.GetComponent<Volume>();

        bodyRole = GetComponent<GamePlayerBodyRole>();

        if (bodyRole != null)
            bodyRole.Role.OnValueChanged += OnRoleChanged;

        // Apply immediately in case role is already set
        if (bodyRole != null)
            SetVolume(bodyRole.Role.Value == PlayerRole.Monster);
    }

    public override void OnNetworkDespawn()
    {
        if (bodyRole != null)
            bodyRole.Role.OnValueChanged -= OnRoleChanged;

        SetVolume(false);
    }

    private void OnRoleChanged(PlayerRole old, PlayerRole current)
    {
        SetVolume(current == PlayerRole.Monster);
    }

    private void SetVolume(bool on)
    {
        if (monsterVolume != null)
            monsterVolume.enabled = on;
    }
}
