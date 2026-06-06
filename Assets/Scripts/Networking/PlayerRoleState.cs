using Unity.Netcode;

public enum PlayerRole : byte { Human, Monster }

public class PlayerRoleState : NetworkBehaviour
{
    public NetworkVariable<PlayerRole> Role = new(
        PlayerRole.Human,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );
}