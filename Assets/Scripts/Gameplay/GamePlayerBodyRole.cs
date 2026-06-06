using Unity.Netcode;

public class GamePlayerBodyRole : NetworkBehaviour
{
    public NetworkVariable<PlayerRole> Role = new(
        PlayerRole.Human,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );
}