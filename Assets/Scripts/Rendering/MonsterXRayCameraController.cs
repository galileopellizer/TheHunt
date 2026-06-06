using Unity.Netcode;
using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public class MonsterXRayCameraController : MonoBehaviour
{
    private static readonly int MonsterXRayEnabledId = Shader.PropertyToID("_MonsterXRayEnabled");

    [SerializeField] private GamePlayerBodyRole roleState;
    [SerializeField] private NetworkObject ownerNetworkObject;

    private Camera targetCamera;

    private void Awake()
    {
        targetCamera = GetComponent<Camera>();
        if (roleState == null)
            roleState = GetComponentInParent<GamePlayerBodyRole>();
        if (ownerNetworkObject == null)
            ownerNetworkObject = GetComponentInParent<NetworkObject>();
    }

    private void OnEnable()
    {
        RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
        RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
    }

    private void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
        RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;
        Shader.SetGlobalFloat(MonsterXRayEnabledId, 0f);
    }

    private void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
    {
        if (camera != targetCamera)
            return;

        Shader.SetGlobalFloat(MonsterXRayEnabledId, ShouldEnableXRay() ? 1f : 0f);
    }

    private void OnEndCameraRendering(ScriptableRenderContext context, Camera camera)
    {
        if (camera != targetCamera)
            return;

        Shader.SetGlobalFloat(MonsterXRayEnabledId, 0f);
    }

    private bool ShouldEnableXRay()
    {
        if (!isActiveAndEnabled)
            return false;

        if (ownerNetworkObject != null && !ownerNetworkObject.IsOwner)
            return false;

        if (roleState == null)
            return false;

        return roleState.Role.Value == PlayerRole.Monster;
    }
}
