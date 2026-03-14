using UnityEngine;

public class PondSelectManagerScript : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public void FunkPondSelected()
    {
        Debug.Log("Funk Pond Selected");
        SceneTransitionManager.LoadSceneWithLoading("Pond_Level_1");
    }

    public void TutorialSelected()
    {
        Debug.Log("Tutorial Selected");
        SceneTransitionManager.LoadSceneWithLoading("Tutorial_Level");
    }
} 
