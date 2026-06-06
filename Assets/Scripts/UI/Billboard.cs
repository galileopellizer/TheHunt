using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

public class Billboard : MonoBehaviour
{
    [SerializeField] private Camera targetCameraOverride;

    private Camera cachedCamera;
    private NetworkTransform netTransform;

    private void Awake()
    {
        netTransform = GetComponent<NetworkTransform>();
    }

    private void LateUpdate()
    {
        if (NetworkManager.Singleton != null && !NetworkManager.Singleton.IsClient)
            return;

        if (netTransform != null && netTransform.enabled)
            return;

        var cam = GetTargetCamera();
        if (!cam)
            return;

        Vector3 dir = transform.position - cam.transform.position;
        if (dir.sqrMagnitude < 0.0001f)
            return;

        float yaw = Quaternion.LookRotation(dir).eulerAngles.y;
        transform.rotation = Quaternion.Euler(0f, yaw, 0f);

    }

    private Camera GetTargetCamera()
    {
        if (targetCameraOverride != null && targetCameraOverride.enabled && targetCameraOverride.gameObject.activeInHierarchy)
            return targetCameraOverride;

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
}
