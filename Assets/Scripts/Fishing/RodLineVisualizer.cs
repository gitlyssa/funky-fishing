using UnityEngine;

public class RodLineVisualizer : MonoBehaviour
{
    public Transform rodTip;
    public Transform bobber;
    public LineRenderer line;

    void Reset()
    {
        line = GetComponent<LineRenderer>();
    }

    void LateUpdate()
    {
        if (!rodTip || !bobber || !line) return;

        line.positionCount = 2;
        line.SetPosition(0, rodTip.position);
        line.SetPosition(1, bobber.position);
    }
}
