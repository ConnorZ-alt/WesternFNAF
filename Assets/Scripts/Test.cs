using UnityEngine;
using UnityEngine.SceneManagement;

public class Test : MonoBehaviour
{
    // private string sceneOne = "TitleScreen";
    // private string sceneTwo = "OptionsScreen";
    // private string sceneThree = "GameOverScreen";
    // private string sceneFour = "ResultsScreen";

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKey(KeyCode.R))
        {
            SceneManager.LoadScene(1);
            // print("Reloaded");
        }
        
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            LoadScene(0);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            LoadScene(2);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            LoadScene(3);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha4))
        {
            LoadScene(4);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha5))
        {
            LoadScene(5);
        }
    }
    
    public void LoadScene(int sceneName)
    {
        SceneManager.LoadScene(sceneName);
    }
}
