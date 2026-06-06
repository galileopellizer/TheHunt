using Unity.Netcode;
using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;

public class GameRoundUI : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private GameRoundManager roundManager;
    [SerializeField] private TMP_Text effigyProgressText;
    [SerializeField] private GameObject endPanel;
    [SerializeField] private TMP_Text winnerText;
    [SerializeField] private GameObject returnToLobbyButton;
    [SerializeField] private GameObject leaveSessionButton;

    [Header("Enrage UI")]
    [SerializeField] private GameObject enragePanel;
    [SerializeField] private TMP_Text enrageTimerText;

    private void Awake()
    {
        if (endPanel) endPanel.SetActive(false);
        if (enragePanel) enragePanel.SetActive(false);
    }

    private void Update()
    {
        if (roundManager == null) return;

        var winner = roundManager.Winner.Value;
        if (winner != GameWinner.None && endPanel && !endPanel.activeSelf)
        {
            if (enragePanel) enragePanel.SetActive(false);
            if (effigyProgressText) effigyProgressText.gameObject.SetActive(false);
            endPanel.SetActive(true);
            if (winnerText)
                winnerText.text = winner == GameWinner.Humans ? "HUMANS WIN" : "MONSTERS WIN";

            DisableLocalPlayerControls();
            ConfigureEndButtons();
        }

        if (roundManager.Phase.Value == GamePhase.GameOver) return;

        UpdateEffigyProgressText();
        UpdateEnrageUI();
    }

    private void UpdateEnrageUI()
    {
        bool enraged = roundManager.Phase.Value == GamePhase.Enrage;

        // Hide effigy progress during enrage
        if (effigyProgressText) effigyProgressText.gameObject.SetActive(!enraged);

        if (enraged)
        {
            if (enragePanel && !enragePanel.activeSelf)
                enragePanel.SetActive(true);

            if (enrageTimerText)
            {
                int seconds = Mathf.CeilToInt(Mathf.Max(0f, roundManager.EnrageTimeRemaining.Value));
                enrageTimerText.text = $"SURVIVE: {seconds}";
            }
        }
        else
        {
            if (enragePanel && enragePanel.activeSelf && roundManager.Phase.Value != GamePhase.GameOver)
                enragePanel.SetActive(false);
        }
    }

    private void UpdateEffigyProgressText()
    {
        if (effigyProgressText == null) return;

        int total = Mathf.Max(0, roundManager.TotalEffigies.Value);
        int burned = Mathf.Clamp(roundManager.BurnedEffigies.Value, 0, total);
        effigyProgressText.text = $"EFFIGIES {burned}/{total}";
    }

    public void ReturnToLobby()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
        {
            NetworkManager.Singleton.SceneManager.LoadScene("LobbyScene", LoadSceneMode.Single);
        }
    }

    public void LeaveSession()
    {
        var leaver = FindObjectOfType<LeaveSessionAndDeleteHost>();
        if (leaver != null)
        {
            leaver.Leave();
            return;
        }

        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.Shutdown();

        SceneManager.LoadScene("MainScene", LoadSceneMode.Single);
    }
    
    private void DisableLocalPlayerControls()
    {
        var localBody = FindLocalOwnedBody();
        if (localBody == null) return;

        var controller = localBody.GetComponentInChildren<PlayerController>();
        if (controller) controller.enabled = false;

        var characterController = localBody.GetComponentInChildren<CharacterController>();
        if (characterController) characterController.enabled = false;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private GameObject FindLocalOwnedBody()
    {
        if (NetworkManager.Singleton == null) return null;

        foreach (var no in Object.FindObjectsOfType<NetworkObject>())
        {
            if (!no.IsSpawned) continue;
            if (!no.IsOwner) continue;
            if (no.GetComponent<GamePlayerBodyRole>() == null) continue;
            return no.gameObject;
        }
        return null;
    }

    private void ConfigureEndButtons()
    {
        bool isHost = NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost;
        if (returnToLobbyButton) returnToLobbyButton.SetActive(isHost);
        if (leaveSessionButton) leaveSessionButton.SetActive(!isHost);
    }
}
