using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuButton : MonoBehaviour
{

    GameObject settings_menu_panel;
    // Start is called before the first frame update
    void Start()
    {
        settings_menu_panel.SetActive(false);
    }

    private void Awake()
    {
        settings_menu_panel = GameObject.Find("SettingsMenu");
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void NewWorld()
    {
        SceneManager.LoadScene("Scenes/WorldAutomaton");
    }

    public void LoadWorld()
    {

    }

    public void OpenSettings()
    {
        settings_menu_panel.SetActive(true);
    }

    public void QuitGame()
    {
#if UNITY_STANDALONE
        Application.Quit();
#endif

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}
