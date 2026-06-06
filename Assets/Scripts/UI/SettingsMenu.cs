using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Attach to a Settings panel GameObject.
/// Toggle with Escape. Sliders control master volume and mouse sensitivity.
/// Leave button disconnects and returns to StartScene.
/// </summary>
public class SettingsMenu : MonoBehaviour
{
    [Header("Panel")]
    [SerializeField] private GameObject panel;

    [Header("Sliders")]
    [SerializeField] private Slider volumeSlider;
    [SerializeField] private Slider sensitivitySlider;

    [Header("Sensitivity Range")]
    [SerializeField] private float minSensitivity = 0.5f;
    [SerializeField] private float maxSensitivity = 10f;

    // Persist settings across scenes
    private const string VolumeKey      = "MasterVolume";
    private const string SensitivityKey = "MouseSensitivity";

    private PlayerController localController;
    private bool isOpen;

    private void Start()
    {
        // Load saved values
        float savedVolume      = PlayerPrefs.GetFloat(VolumeKey,      1f);
        float savedSensitivity = PlayerPrefs.GetFloat(SensitivityKey, 2f);

        if (volumeSlider)
        {
            volumeSlider.minValue = 0f;
            volumeSlider.maxValue = 1f;
            volumeSlider.value    = savedVolume;
            volumeSlider.onValueChanged.AddListener(OnVolumeChanged);
        }

        if (sensitivitySlider)
        {
            sensitivitySlider.minValue = minSensitivity;
            sensitivitySlider.maxValue = maxSensitivity;
            sensitivitySlider.value    = savedSensitivity;
            sensitivitySlider.onValueChanged.AddListener(OnSensitivityChanged);
        }

        AudioListener.volume = savedVolume;

        if (panel) panel.SetActive(false);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
            Toggle();

        // Cache local controller lazily
        if (localController == null)
            FindLocalController();
    }

    public bool IsOpen => isOpen;

    private bool IsGameOver()
    {
        var rm = FindObjectOfType<GameRoundManager>();
        return rm != null && rm.Phase.Value == GamePhase.GameOver;
    }

    private void Toggle()
    {
        isOpen = !isOpen;
        if (panel) panel.SetActive(isOpen);

        if (!IsGameOver())
        {
            Cursor.lockState = isOpen ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible   = isOpen;
        }
    }

    private void OnVolumeChanged(float value)
    {
        AudioListener.volume = value;
        PlayerPrefs.SetFloat(VolumeKey, value);
    }

    private void OnSensitivityChanged(float value)
    {
        PlayerPrefs.SetFloat(SensitivityKey, value);
        if (localController != null)
            localController.SetMouseSensitivity(value);
    }

    public void OnLeavePressed()
    {
        var leaver = FindObjectOfType<LeaveSessionAndDeleteHost>();
        if (leaver != null)
        {
            leaver.Leave();
            return;
        }

        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.Shutdown();

        SceneManager.LoadScene("StartScene", LoadSceneMode.Single);
    }

    private void FindLocalController()
    {
        if (NetworkManager.Singleton == null) return;
        foreach (var no in FindObjectsOfType<NetworkObject>())
        {
            if (!no.IsOwner) continue;
            var pc = no.GetComponent<PlayerController>();
            if (pc != null) { localController = pc; break; }
        }
    }
}
