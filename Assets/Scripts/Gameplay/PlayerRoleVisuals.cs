using Unity.Netcode;
using UnityEngine;

public class PlayerRoleVisuals : NetworkBehaviour
{
    [Header("Prefab assets (Project window)")]
    [SerializeField] private GameObject humanModelPrefab;
    [SerializeField] private GameObject monsterModelPrefab;

    [Header("Spawned instances (runtime)")]
    [SerializeField] private Transform modelParent; // optional (defaults to this transform)

    private GameObject humanInstance;
    private GameObject monsterInstance;

    public MonsterAnimationController MonsterAnimController { get; private set; }

    private GamePlayerBodyRole roleState;

    private void Awake()
    {
        if (modelParent == null) modelParent = transform;
    }

    public override void OnNetworkSpawn()
    {
        roleState = GetComponent<GamePlayerBodyRole>();
        roleState.Role.OnValueChanged += OnRoleChanged;

        if (humanInstance == null && humanModelPrefab != null)
        {
            humanInstance = Instantiate(humanModelPrefab, modelParent);

            // Hide eye from local owner
            if (IsOwner)
            {
                int hiddenLayer = LayerMask.NameToLayer("LocalHidden");
                if (hiddenLayer >= 0)
                {
                    var eye = humanInstance.transform.FindDeepChild("Eye");
                    if (eye != null) eye.gameObject.layer = hiddenLayer;
                }
            }
        }

        if (monsterInstance == null && monsterModelPrefab != null)
        {
            monsterInstance = Instantiate(monsterModelPrefab, modelParent);
            MonsterAnimController = monsterInstance.GetComponentInChildren<MonsterAnimationController>(true);
            if (MonsterAnimController != null)
            {
                MonsterAnimController.Initialize(
                    GetComponent<PlayerController>(),
                    GetComponent<PlayerHealth>(),
                    GetComponent<GamePlayerBodyRole>(),
                    GetComponent<NetworkObject>()
                );
            }
        }

        OnRoleChanged(PlayerRole.Human, roleState.Role.Value);
    }


    public override void OnNetworkDespawn()
    {
        if (roleState != null)
            roleState.Role.OnValueChanged -= OnRoleChanged;
    }

    private void OnRoleChanged(PlayerRole oldRole, PlayerRole newRole)
    {
        if (humanInstance) humanInstance.SetActive(newRole == PlayerRole.Human);
        if (monsterInstance) monsterInstance.SetActive(newRole == PlayerRole.Monster);
        
    }
}
