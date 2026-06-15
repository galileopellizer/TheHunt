using Unity.Netcode;
using UnityEngine;

public class PlayerRoleVisuals : NetworkBehaviour
{
    [Header("Human Prefabs (randomly selected)")]
    [SerializeField] private GameObject[] humanModelPrefabs;

    [Header("Monster Prefab")]
    [SerializeField] private GameObject monsterModelPrefab;

    [Header("Camera / Head Bone")]
    [SerializeField] private Transform headTransform; // still needed for CameraFollowBone reference

    [Header("Monster Head Bone")]
    [Tooltip("Exact name of the head bone in the monster skeleton (e.g. 'Head', 'mixamorig:Head')")]
    [SerializeField] private string monsterHeadBoneName = "Head";

    [Header("Spawned instances (runtime)")]
    [SerializeField] private Transform modelParent;

    // Synced random model index so all clients see the same human model
    private NetworkVariable<int> humanModelIndex = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private GameObject humanInstance;
    private GameObject monsterInstance;

    public MonsterAnimationController MonsterAnimController { get; private set; }
    public HumanAnimationController HumanAnimController { get; private set; }

    private GamePlayerBodyRole roleState;

    private void Awake()
    {
        if (modelParent == null) modelParent = transform;
    }

    public override void OnNetworkSpawn()
    {
        roleState = GetComponent<GamePlayerBodyRole>();
        roleState.Role.OnValueChanged += OnRoleChanged;
        humanModelIndex.OnValueChanged += (_, __) => SpawnHumanModel();

        // Use the index assigned in lobby (via PlayerNameState on the player object)
        if (IsServer)
        {
            int count = humanModelPrefabs != null ? humanModelPrefabs.Length : 1;
            humanModelIndex.Value = (int)(OwnerClientId % (ulong)count);
        }

        SpawnHumanModel();
        SpawnMonsterModel();

        OnRoleChanged(PlayerRole.Human, roleState.Role.Value);
    }

    public override void OnNetworkDespawn()
    {
        if (roleState != null)
            roleState.Role.OnValueChanged -= OnRoleChanged;
    }

    private void SpawnHumanModel()
    {
        if (humanInstance != null)
        {
            Destroy(humanInstance);
            humanInstance = null;
        }

        if (humanModelPrefabs == null || humanModelPrefabs.Length == 0) return;

        int idx = Mathf.Clamp(humanModelIndex.Value, 0, humanModelPrefabs.Length - 1);
        var prefab = humanModelPrefabs[idx];
        if (prefab == null) return;

        humanInstance = Instantiate(prefab, modelParent);

        // Hide head/hat/torso from local owner's camera
        if (IsOwner)
        {
            int hiddenLayer = LayerMask.NameToLayer("LocalHidden");
            if (hiddenLayer >= 0)
            {
                HideUpperBodyForOwner(humanInstance, hiddenLayer);
            }
        }

        // Initialize animation controller
        HumanAnimController = humanInstance.GetComponentInChildren<HumanAnimationController>(true);
        if (HumanAnimController != null)
        {
            HumanAnimController.Initialize(
                GetComponent<PlayerController>(),
                GetComponent<PlayerHealth>(),
                GetComponent<GamePlayerBodyRole>(),
                GetComponent<NetworkObject>()
            );
        }

        // Apply current role visibility
        if (roleState != null)
            humanInstance.SetActive(roleState.Role.Value == PlayerRole.Human);
    }

    private void SpawnMonsterModel()
    {
        if (monsterInstance != null || monsterModelPrefab == null) return;

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

        if (IsOwner)
        {
            int hiddenLayer = LayerMask.NameToLayer("LocalHidden");
            if (hiddenLayer >= 0)
                HideMonsterUpperBodyForOwner(monsterInstance, hiddenLayer);
        }
    }

    private static Transform FindBoneByName(Transform root, string boneName)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == boneName) return t;
        return null;
    }

    private static readonly string[] HumanUpperBodyKeywords = { "head", "hat", "hair", "helmet", "face", "neck", "cap", "hood" };
    private static readonly string[] MonsterHideKeywords    = { "eye", "teeth", "tooth" };

    private void HideUpperBodyForOwner(GameObject model, int hiddenLayer)
    {
        // Human: hide named head/hat objects + near clip
        foreach (Transform t in model.GetComponentsInChildren<Transform>(true))
        {
            string lower = t.name.ToLowerInvariant();
            foreach (var kw in HumanUpperBodyKeywords)
            {
                if (lower.Contains(kw)) { t.gameObject.layer = hiddenLayer; break; }
            }
        }

        var cam = GetComponentInChildren<Camera>();
        if (cam != null) cam.nearClipPlane = 0.35f;
    }

    private void HideMonsterUpperBodyForOwner(GameObject model, int hiddenLayer)
    {
        // Only hide eyes/teeth — they clip from inside the head.
        // Arms and legs remain visible so the owner can see themselves.
        foreach (Transform t in model.GetComponentsInChildren<Transform>(true))
        {
            string lower = t.name.ToLowerInvariant();
            if (lower.Contains("eye") || lower.Contains("teeth") || lower.Contains("tooth"))
                t.gameObject.layer = hiddenLayer;
        }
    }

    private void OnRoleChanged(PlayerRole oldRole, PlayerRole newRole)
    {
        if (humanInstance)   humanInstance.SetActive(newRole == PlayerRole.Human);
        if (monsterInstance) monsterInstance.SetActive(newRole == PlayerRole.Monster);

        if (!IsOwner) return;

        var followBone = headTransform?.GetComponent<CameraFollowBone>();
        if (followBone == null) return;

        if (newRole == PlayerRole.Monster && monsterInstance != null)
        {
            var headBone = FindBoneByName(monsterInstance.transform, monsterHeadBoneName);
            followBone.SetBone(headBone);
        }
        else
        {
            followBone.SetBone(null); // human — use fixed head position
        }
    }
}
