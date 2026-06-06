using Unity.Netcode;
using UnityEngine;

[DisallowMultipleComponent]
public class DeathFreeCam : NetworkBehaviour
{
    [Header("Free Cam")]
    [SerializeField] private float flySpeed         = 8f;
    [SerializeField] private float fastMultiplier   = 2.5f;
    [SerializeField] private float mouseSensitivity = 2f;

    [Header("Despawn")]
    [SerializeField] private float despawnDelay = 5f;

    [Header("Refs")]
    [SerializeField] private Camera playerCamera;

    private PlayerHealth playerHealth;
    private bool freeCamActive;
    private float pitch;

    public override void OnNetworkSpawn()
    {
        playerHealth = GetComponent<PlayerHealth>();
        if (playerCamera == null)
            playerCamera = GetComponentInChildren<Camera>(true);

        Debug.Log($"[DeathFreeCam] OnNetworkSpawn — IsOwner={IsOwner} camera={playerCamera != null}");

        if (playerHealth != null)
            playerHealth.IsDead.OnValueChanged += OnDeadChanged;
    }

    public override void OnNetworkDespawn()
    {
        if (playerHealth != null)
            playerHealth.IsDead.OnValueChanged -= OnDeadChanged;
    }

    private void OnDeadChanged(bool previous, bool current)
    {
        if (!current) return;

        // Detach camera immediately on owner so it survives despawn
        if (IsOwner)
            ActivateFreeCam();

        // Server triggers despawn after delay
        if (IsServer)
            StartCoroutine(DespawnAfterDelay());
    }

    private void ActivateFreeCam()
    {
        Debug.Log($"[DeathFreeCam] ActivateFreeCam — camera={playerCamera != null}");
        if (playerCamera == null)
        {
            Debug.LogWarning("[DeathFreeCam] No camera found!");
            return;
        }

        playerCamera.transform.SetParent(null);
        DontDestroyOnLoad(playerCamera.gameObject);

        // Add free cam controller to the camera so it keeps running after body despawns
        var freeCam = playerCamera.gameObject.AddComponent<FreeCamController>();
        freeCam.mouseSensitivity = mouseSensitivity;
        freeCam.flySpeed         = flySpeed;
        freeCam.fastMultiplier   = fastMultiplier;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible   = false;
    }

    private void Update()
    {
        if (!freeCamActive || playerCamera == null) return;

        var settings = FindObjectOfType<SettingsMenu>();
        if (settings != null && settings.IsOpen) return;

        float mx = Input.GetAxis("Mouse X") * mouseSensitivity;
        float my = Input.GetAxis("Mouse Y") * mouseSensitivity;

        pitch -= my;
        pitch  = Mathf.Clamp(pitch, -89f, 89f);

        playerCamera.transform.Rotate(Vector3.up * mx, Space.World);
        playerCamera.transform.rotation = Quaternion.Euler(pitch, playerCamera.transform.eulerAngles.y, 0f);

        float speed  = flySpeed * (Input.GetKey(KeyCode.LeftShift) ? fastMultiplier : 1f);
        Vector3 move = Vector3.zero;
        if (Input.GetKey(KeyCode.W)) move += playerCamera.transform.forward;
        if (Input.GetKey(KeyCode.S)) move -= playerCamera.transform.forward;
        if (Input.GetKey(KeyCode.A)) move -= playerCamera.transform.right;
        if (Input.GetKey(KeyCode.D)) move += playerCamera.transform.right;
        if (Input.GetKey(KeyCode.E)) move += Vector3.up;
        if (Input.GetKey(KeyCode.Q)) move -= Vector3.up;

        playerCamera.transform.position += move.normalized * speed * Time.deltaTime;
    }

    private System.Collections.IEnumerator DespawnAfterDelay()
    {
        yield return new WaitForSeconds(despawnDelay);
        var no = GetComponent<NetworkObject>();
        if (no != null && no.IsSpawned)
            no.Despawn(true);
    }
}
