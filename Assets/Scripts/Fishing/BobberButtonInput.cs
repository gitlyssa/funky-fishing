using UnityEngine;

public class BobberButtonInput : MonoBehaviour
{
    public BobberArcCaster caster;

    public KeyCode castKey = KeyCode.C;
    public KeyCode yankKey = KeyCode.Y;
    public KeyCode tensionKey = KeyCode.H;

    void Update()
    {
        if (!caster) return;

        if (Input.GetKeyDown(castKey)) caster.Cast();
        if (Input.GetKeyDown(yankKey)) caster.Yank();
        if (Input.GetKeyDown(tensionKey)) caster.ToggleTension();
    }
}
