using UnityEngine;
using UnityEngine.SceneManagement;

public class PondSelectManagerScript : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public void FunkPondSelected()
    {
        Debug.Log("Funk Pond Selected");
        SceneManager.LoadScene("Pond_Level_1");
    }
}