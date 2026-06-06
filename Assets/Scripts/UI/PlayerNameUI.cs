using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayerNameUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TMP_Text nameLabel;
    [SerializeField] private TMP_InputField nameInput;
    [SerializeField] private Button saveButton;

    [Header("Behavior")]
    [SerializeField] private string playerPrefsKey = "player_name";
    [SerializeField] private int randomMin = 1000;
    [SerializeField] private int randomMax = 9999;
    [SerializeField] private bool requireChangeFromDefault = true;

    private string initialName;

    private void OnEnable()
    {
        if (nameInput != null)
        {
            nameInput.onValueChanged.AddListener(OnNameChanged);
        }

        if (saveButton != null)
        {
            saveButton.onClick.AddListener(SaveName);
        }
    }

    private void OnDisable()
    {
        if (nameInput != null)
        {
            nameInput.onValueChanged.RemoveListener(OnNameChanged);
        }

        if (saveButton != null)
        {
            saveButton.onClick.RemoveListener(SaveName);
        }
    }

    private void Start()
    {
        initialName = LoadOrGenerateName();
        EnsureSaved(initialName);
        SetNameUI(initialName);
        UpdateSaveButton();
    }

    private string LoadOrGenerateName()
    {
        if (PlayerPrefs.HasKey(playerPrefsKey))
        {
            return PlayerPrefs.GetString(playerPrefsKey);
        }

        int randomNumber = Random.Range(randomMin, randomMax + 1);
        return $"Player {randomNumber}";
    }

    private void EnsureSaved(string value)
    {
        if (PlayerPrefs.HasKey(playerPrefsKey))
            return;

        PlayerPrefs.SetString(playerPrefsKey, value);
        PlayerPrefs.Save();
    }

    private void SetNameUI(string value)
    {
        if (nameInput != null)
        {
            nameInput.text = value;
        }

        if (nameLabel != null)
        {
            nameLabel.text = value;
        }
    }

    private void OnNameChanged(string value)
    {
        if (nameLabel != null)
        {
            nameLabel.text = value;
        }

        UpdateSaveButton();
    }

    private void UpdateSaveButton()
    {
        if (saveButton == null || nameInput == null)
        {
            return;
        }

        string trimmed = nameInput.text.Trim();
        bool hasName = trimmed.Length > 0;
        bool changedFromInitial = !requireChangeFromDefault || trimmed != initialName;
        saveButton.interactable = hasName && changedFromInitial;
    }

    private void SaveName()
    {
        if (nameInput == null)
        {
            return;
        }

        string trimmed = nameInput.text.Trim();
        if (trimmed.Length == 0)
        {
            return;
        }

        PlayerPrefs.SetString(playerPrefsKey, trimmed);
        PlayerPrefs.Save();

        initialName = trimmed;
        if (nameLabel != null)
        {
            nameLabel.text = trimmed;
        }

        UpdateSaveButton();
    }
}
