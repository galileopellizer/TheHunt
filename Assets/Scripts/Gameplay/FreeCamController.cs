using UnityEngine;

/// <summary>
/// Added to the camera GameObject at runtime when the player dies.
/// Handles free-roam movement independently of the player body.
/// </summary>
public class FreeCamController : MonoBehaviour
{
    public float flySpeed         = 8f;
    public float fastMultiplier   = 2.5f;
    public float mouseSensitivity = 2f;

    private float pitch;

    private void Start()
    {
        pitch = transform.eulerAngles.x;
        if (pitch > 180f) pitch -= 360f;
    }

    private void Update()
    {
        // Stop movement when game is over so the end screen is usable
        var roundManager = FindObjectOfType<GameRoundManager>();
        if (roundManager != null && roundManager.Phase.Value == GamePhase.GameOver)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            return;
        }

        var settings = FindObjectOfType<SettingsMenu>();
        if (settings != null && settings.IsOpen) return;

        // Look
        float mx = Input.GetAxis("Mouse X") * mouseSensitivity;
        float my = Input.GetAxis("Mouse Y") * mouseSensitivity;

        pitch -= my;
        pitch  = Mathf.Clamp(pitch, -89f, 89f);

        transform.Rotate(Vector3.up * mx, Space.World);
        transform.rotation = Quaternion.Euler(pitch, transform.eulerAngles.y, 0f);

        // Move
        float speed  = flySpeed * (Input.GetKey(KeyCode.LeftShift) ? fastMultiplier : 1f);
        Vector3 move = Vector3.zero;
        if (Input.GetKey(KeyCode.W)) move += transform.forward;
        if (Input.GetKey(KeyCode.S)) move -= transform.forward;
        if (Input.GetKey(KeyCode.A)) move -= transform.right;
        if (Input.GetKey(KeyCode.D)) move += transform.right;
        if (Input.GetKey(KeyCode.E)) move += Vector3.up;
        if (Input.GetKey(KeyCode.Q)) move -= Vector3.up;

        transform.position += move.normalized * speed * Time.deltaTime;
    }
}
