using Unity.Netcode;
using UnityEngine;
using Object = UnityEngine.Object;

[DisallowMultipleComponent]
public class RoleBasedUIVisibility : MonoBehaviour
{
    [Header("UI Roots")]
    [SerializeField] private GameObject humanRoot;
    [SerializeField] private GameObject monsterRoot;

    [Header("Behavior")]
    [SerializeField] private bool hideAllUntilResolved = true;
    [SerializeField] private float findInterval = 0.5f;

    private GamePlayerBodyRole localBodyRole;
    private float nextFindTime;
    private bool subscribed;

    private void Awake()
    {
        if (hideAllUntilResolved)
            SetActiveRoots(false, false);
    }

    private void OnEnable()
    {
        subscribed = false;
        TryResolveRole();
        if (hideAllUntilResolved && localBodyRole == null)
            SetActiveRoots(false, false);
    }

    private void OnDisable()
    {
        if (localBodyRole != null && subscribed)
            localBodyRole.Role.OnValueChanged -= HandleRoleChanged;
        subscribed = false;
    }

    private void Update()
    {
        if (localBodyRole == null)
            TryResolveRole();
    }

    private void TryResolveRole()
    {
        if (Time.time < nextFindTime)
            return;

        localBodyRole = null;
        var network = NetworkManager.Singleton;
        if (network != null)
        {
            foreach (var no in Object.FindObjectsOfType<NetworkObject>())
            {
                if (!no.IsSpawned || !no.IsOwner)
                    continue;

                localBodyRole = no.GetComponent<GamePlayerBodyRole>();
                if (localBodyRole != null)
                    break;
            }
        }

        if (localBodyRole != null && !subscribed)
        {
            localBodyRole.Role.OnValueChanged += HandleRoleChanged;
            subscribed = true;
            ApplyRole(localBodyRole.Role.Value);
        }
        else if (hideAllUntilResolved)
        {
            SetActiveRoots(false, false);
        }

        nextFindTime = Time.time + Mathf.Max(0.1f, findInterval);
    }

    private void HandleRoleChanged(PlayerRole oldRole, PlayerRole newRole)
    {
        ApplyRole(newRole);
    }

    private void ApplyRole(PlayerRole role)
    {
        SetActiveRoots(role == PlayerRole.Human, role == PlayerRole.Monster);
    }

    private void SetActiveRoots(bool humanActive, bool monsterActive)
    {
        if (humanRoot != null)
            humanRoot.SetActive(humanActive);
        if (monsterRoot != null)
            monsterRoot.SetActive(monsterActive);
    }
}
