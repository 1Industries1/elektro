using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;

public class GameMenuController : MonoBehaviour
{
    public void RestartLevel()
    {
        StartCoroutine(RestartRoutine());
    }

    private IEnumerator RestartRoutine()
    {
        var nm = NetworkManager.Singleton;

        if (nm != null && nm.IsListening)
        {
            nm.Shutdown();     // beendet Host/Client + Transport sauber
            // nm.DisconnectReason = "" ; // <-- entfernen!
            yield return null; // 1–2 Frames warten
            yield return null;
        }

        string active = SceneManager.GetActiveScene().name;
        SceneManager.LoadScene(active, LoadSceneMode.Single);
    }

    public void QuitGame()
    {
        var nm = NetworkManager.Singleton;
        if (nm != null && nm.IsListening) nm.Shutdown();

    #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
    #elif UNITY_WEBGL
        Debug.Log("Quit not supported on WebGL.");
    #else
        Application.Quit();
    #endif
    }
}
