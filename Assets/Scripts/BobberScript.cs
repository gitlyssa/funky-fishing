using UnityEngine;

public class BobberScript : MonoBehaviour
{
    public GameObject bobber;

    public float waterHeight = 6f;
    public float fallSpeed = 8f;
    public float bobAmplitude = 0.9f;
    public float bobFrequency = 10f;

    private bool isFloating = false;

    void Update()
    {
        if (!isFloating)
        {
            // Fall until we reach the water surface
            if (bobber.transform.position.y > waterHeight)
            {
                bobber.transform.position += Vector3.down * fallSpeed * Time.deltaTime;
            }
            else
            {
                // Lock to surface and start floating
                isFloating = true;
            }
        }
        else
        {
            // Bob up and down around the water surface
            float bobOffset = Mathf.Sin(Time.time * bobFrequency) * bobAmplitude;

            bobber.transform.position = new Vector3(
                bobber.transform.position.x,
                waterHeight + bobOffset,
                bobber.transform.position.z
            );
        }
    }

    void Start()
    {
        bobber = this.gameObject;
    }
}