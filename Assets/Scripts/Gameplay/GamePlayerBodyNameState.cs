using Unity.Collections;
using Unity.Netcode;

public class GamePlayerBodyNameState : NetworkBehaviour
{
    public NetworkVariable<FixedString32Bytes> PlayerName =
        new NetworkVariable<FixedString32Bytes>(
            "",
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
}
