using UnityEngine;
using UnityEngine.SceneManagement;

public class StartSceneUI : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject mainPanel;
    [SerializeField] private GameObject settingsPanel;

    private void Start()
    {
        if (mainPanel)     mainPanel.SetActive(true);
        if (settingsPanel) settingsPanel.SetActive(false);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape) && settingsPanel != null && settingsPanel.activeSelf)
            OnBackPressed();
    }

    public void OnPlayPressed()
    {
        SceneManager.LoadScene("MainScene");
    }

    public void OnSettingsPressed()
    {
        if (mainPanel)     mainPanel.SetActive(false);
        if (settingsPanel) settingsPanel.SetActive(true);
    }

    public void OnBackPressed()
    {
        if (settingsPanel) settingsPanel.SetActive(false);
        if (mainPanel)     mainPanel.SetActive(true);
    }

    public void OnExitPressed()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
