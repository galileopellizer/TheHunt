using Unity.Collections;
using TMPro;
using UnityEngine;
using Unity.Netcode;

public class PlayerNameTag : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TMP_Text nameLabel;
    [SerializeField] private Transform followTarget;
    [SerializeField] private Vector3 worldOffset = new Vector3(0f, 2f, 0f);

    [Header("Behavior")]
    // [SerializeField] private bool faceCamera = true;
    // [SerializeField] private Camera targetCamera;
    [SerializeField] private bool hideForOwner = true;
    [SerializeField] private bool limitByDistance = true;
    [SerializeField] private float maxVisibleDistance = 20f;
    [SerializeField] private float maxVisibleDistanceForMonster = 2f;
    [SerializeField] private Camera targetCameraOverride;
    [SerializeField] private CanvasGroup visibilityGroup;
    [SerializeField] private GameObject visibilityRoot;

    [Header("Data")]
    [SerializeField] private GamePlayerBodyNameState nameState;
    [SerializeField] private NetworkObject ownerNetworkObject;

    private Transform followTransform;
    private Camera cachedCamera;
    private bool lastVisible = true;

    private void Awake()
    {
        // if (targetCamera == null)
        // {
        //     targetCamera = Camera.main;
        // }

        if (nameState == null)
        {
            nameState = GetComponentInParent<GamePlayerBodyNameState>();
        }

        if (ownerNetworkObject == null)
        {
            ownerNetworkObject = GetComponentInParent<NetworkObject>();
        }

        followTransform = transform;
        if (transform.parent != null && transform.parent.GetComponent<Billboard>() != null)
        {
            followTransform = transform.parent;
        }

        if (visibilityGroup == null)
        {
            visibilityGroup = GetComponentInChildren<CanvasGroup>(true);
        }

        if (visibilityRoot == null && nameLabel != null && nameLabel.gameObject != gameObject)
        {
            visibilityRoot = nameLabel.gameObject;
        }
    }

    private void OnEnable()
    {
        if (nameState != null)
        {
            nameState.PlayerName.OnValueChanged += HandleNameChanged;
            if (nameState.PlayerName.Value.Length > 0)
            {
                nameLabel.text = nameState.PlayerName.Value.ToString();
            }
        }
    }

    private void OnDisable()
    {
        if (nameState != null)
        {
            nameState.PlayerName.OnValueChanged -= HandleNameChanged;
        }
    }

    private void LateUpdate()
    {
        bool shouldShow = true;
        if (hideForOwner && ownerNetworkObject != null && ownerNetworkObject.IsOwner)
        {
            shouldShow = false;
        }

        if (shouldShow && limitByDistance)
        {
            var cam = GetTargetCamera();
            if (cam != null)
            {
                Vector3 targetPos = followTarget != null ? followTarget.position : followTransform.position;
                float sqrDist = (cam.transform.position - targetPos).sqrMagnitude;

                // Use a tighter distance limit if the local player is the monster
                float effectiveMax = maxVisibleDistance;
                if (IsLocalPlayerMonster())
                    effectiveMax = maxVisibleDistanceForMonster;

                if (sqrDist > effectiveMax * effectiveMax)
                    shouldShow = false;
            }
        }

        ApplyVisibility(shouldShow);

        if (!shouldShow)
        {
            // Skip positional updates when hidden to reduce work.
            return;
        }

        if (followTarget != null)
        {
            followTransform.position = followTarget.position + worldOffset;
        }

        // Rotation handled elsewhere (e.g., Billboard). Keeping this block
        // commented so name tag can be oriented by another component.
        // if (faceCamera && targetCamera != null)
        // {
        //     Vector3 dir = targetCamera.transform.position - transform.position;
        //     if (dir.sqrMagnitude > 0.0001f)
        //     {
        //         transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
        //     }
        // }
    }

    private void HandleNameChanged(FixedString32Bytes previous, FixedString32Bytes current)
    {
        if (nameLabel == null)
            return;

        if (current.Length > 0)
        {
            nameLabel.text = current.ToString();
        }
    }

    private Camera GetTargetCamera()
    {
        if (targetCameraOverride != null && targetCameraOverride.enabled && targetCameraOverride.gameObject.activeInHierarchy)
        {
            return targetCameraOverride;
        }

        if (cachedCamera != null && cachedCamera.enabled && cachedCamera.gameObject.activeInHierarchy)
        {
            var owner = cachedCamera.GetComponentInParent<NetworkObject>();
            if (owner != null && owner.IsOwner)
                return cachedCamera;
        }

        cachedCamera = FindLocalPlayerCamera();
        if (cachedCamera == null)
            cachedCamera = Camera.main;
        return cachedCamera;
    }

    private static Camera FindLocalPlayerCamera()
    {
        if (NetworkManager.Singleton == null || NetworkManager.Singleton.LocalClient == null)
            return null;

        var playerObject = NetworkManager.Singleton.LocalClient.PlayerObject;
        if (playerObject == null)
            return null;

        var cams = playerObject.GetComponentsInChildren<Camera>(true);
        for (int i = 0; i < cams.Length; i++)
        {
            if (cams[i] != null && cams[i].enabled && cams[i].gameObject.activeInHierarchy)
                return cams[i];
        }

        return null;
    }

    private float nextMonsterRoleCheck;
    private bool cachedIsMonster;

    private bool IsLocalPlayerMonster()
    {
        if (Time.time < nextMonsterRoleCheck) return cachedIsMonster;
        nextMonsterRoleCheck = Time.time + 0.5f;

        if (NetworkManager.Singleton == null) { cachedIsMonster = false; return false; }
        ulong localId = NetworkManager.Singleton.LocalClientId;
        foreach (var role in FindObjectsOfType<GamePlayerBodyRole>())
        {
            var no = role.GetComponent<NetworkObject>();
            if (no != null && no.OwnerClientId == localId)
            {
                cachedIsMonster = role.Role.Value == PlayerRole.Monster;
                return cachedIsMonster;
            }
        }
        cachedIsMonster = false;
        return false;
    }

    private void ApplyVisibility(bool shouldShow)
    {
        if (lastVisible == shouldShow)
            return;

        lastVisible = shouldShow;

        if (visibilityGroup != null)
        {
            visibilityGroup.alpha = shouldShow ? 1f : 0f;
            visibilityGroup.blocksRaycasts = shouldShow;
            visibilityGroup.interactable = shouldShow;
            return;
        }

        if (visibilityRoot != null && visibilityRoot != gameObject)
        {
            visibilityRoot.SetActive(shouldShow);
            return;
        }

        if (nameLabel != null)
        {
            nameLabel.enabled = shouldShow;
        }
    }
}
