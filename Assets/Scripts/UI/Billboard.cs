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
        if (NetworkManager.Singleton == null) return null;

        // Search all NetworkObjects for the locally owned game body with a camera
        foreach (var no in FindObjectsOfType<NetworkObject>())
        {
            if (!no.IsOwner) continue;
            if (no.GetComponent<GamePlayerBodyRole>() == null) continue;
            var cams = no.GetComponentsInChildren<Camera>(true);
            foreach (var c in cams)
                if (c != null && c.enabled && c.gameObject.activeInHierarchy)
                    return c;
        }

        return null;
    }
}
