using UnityEngine;


public class BobberButtonInput : MonoBehaviour
{
    public BobberArcCaster caster;
    public PondManager pondManager;

    public KeyCode castKey = KeyCode.C;
    public KeyCode yankKey = KeyCode.Y;
    public KeyCode tensionKey = KeyCode.H;

    void Update()
    {
        if (!caster) return;

        if (Input.GetKeyDown(castKey)) caster.Cast();
        if (Input.GetKeyDown(yankKey)) HandleYank();
        if (Input.GetKeyDown(tensionKey)) caster.ToggleTension();
    }
    private void HandleYank()
    {
        if (!pondManager || !caster) return;

        GameObject fish = pondManager.GetClosestFish(pondManager.playerBobber);

        if (fish != null)
        {
            Debug.Log("Fish hooked! Entering tension state.");

            caster.ToggleTension(); // enter tension state
            
            // START BEATMAP
        }
        else
        {
            Debug.Log("No fish nearby. Normal yank.");
            caster.Yank();
        }
    }

}
