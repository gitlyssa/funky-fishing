using UnityEngine;
using UnityEngine.InputSystem;
[DefaultExecutionOrder(-100)]
public class MouseHardwareReader : MonoBehaviour
{   
    
    private RhythmInputProcessor processor;

    public float maxRadius = 300f;
    void Start()
    {
        processor = GetComponent<RhythmInputProcessor>();
        
    }

    // Update is called once per frame
    void Update()
    {
        Vector2 mousePos = Mouse.current.position.ReadValue();
        Vector2 centerScreen = new Vector2(Screen.width / 2f, Screen.height / 2f);
        Vector2 offset = mousePos - centerScreen;

        float x = Mathf.Clamp(offset.x / maxRadius, -1f, 1f);
        float y = Mathf.Clamp(offset.y / maxRadius, -1f, 1f);

        processor.RawInput = new Vector2(x, y);
        processor.RawReelButton = Mouse.current.leftButton.isPressed;
    }
}
