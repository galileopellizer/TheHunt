using UnityEngine;
using UnityEngine.SceneManagement;

public class BackButton : MonoBehaviour
{
    [SerializeField] private string targetScene = "StartScene";

    public void OnBackPressed()
    {
        SceneManager.LoadScene(targetScene);
    }
}
