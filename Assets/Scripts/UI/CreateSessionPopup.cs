using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Popup shown when the host clicks Create Session.
/// Collects map choice, then triggers the real session creation.
/// </summary>
public class CreateSessionPopup : MonoBehaviour
{
    [Header("Popup panel")]
    [SerializeField] private GameObject popupPanel;

    [Header("Fields")]
    [SerializeField] private TMP_Dropdown mapDropdown;
    [SerializeField] private TMP_Dropdown effigyDropdown;

    [Header("Map scene names — must match Build Settings exactly")]
    [SerializeField] private string[] mapSceneNames = { "GameScene" };

    [Header("Effigy count options")]
    [SerializeField] private int effigyMin     = 1;
    [SerializeField] private int effigyMax     = 6;
    [SerializeField] private int effigyDefault = 3;


    private void Awake()
    {
        if (popupPanel) popupPanel.SetActive(false);

        if (mapDropdown != null && mapSceneNames.Length > 0)
        {
            mapDropdown.ClearOptions();
            var options = new System.Collections.Generic.List<string>();
            foreach (var s in mapSceneNames)
                options.Add(FormatMapName(s));
            mapDropdown.AddOptions(options);
        }

        if (effigyDropdown != null)
        {
            effigyDropdown.ClearOptions();
            var options = new System.Collections.Generic.List<string>();
            for (int i = effigyMin; i <= effigyMax; i++)
                options.Add(i.ToString());
            effigyDropdown.AddOptions(options);
            effigyDropdown.value = effigyDefault - effigyMin; // select default
        }
    }

    // Called by the main menu "Create Session" button
    public void OpenPopup()
    {
        if (popupPanel) popupPanel.SetActive(true);
    }

    public void ClosePopup()
    {
        if (popupPanel) popupPanel.SetActive(false);
        ResetPopup();
    }

    private void ResetPopup()
    {
        if (mapDropdown != null)   mapDropdown.value   = 0;
        if (effigyDropdown != null) effigyDropdown.value = effigyDefault - effigyMin;
    }

    // Called by the popup's Confirm / Create button
    public void OnConfirmCreate()
    {
        // Store selected map
        int idx = mapDropdown != null ? mapDropdown.value : 0;
        MapSettings.SelectedScene = (mapSceneNames != null && idx < mapSceneNames.Length)
            ? mapSceneNames[idx]
            : "GameScene";

        if (effigyDropdown != null)
            MapSettings.EffigyCount = effigyMin + effigyDropdown.value;

        // Popup stays open — widget Create Session button handles the actual connection
    }

    private static string FormatMapName(string sceneName)
    {
        // "ForestMap" → "Forest Map", "GameScene" → "Game Scene" etc.
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < sceneName.Length; i++)
        {
            if (i > 0 && char.IsUpper(sceneName[i]))
                sb.Append(' ');
            sb.Append(sceneName[i]);
        }
        return sb.ToString();
    }
}
