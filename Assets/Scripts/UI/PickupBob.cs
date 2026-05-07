using UnityEngine;

[DisallowMultipleComponent]
public class PickupBob : MonoBehaviour
{
    [Header("Float")]
    [SerializeField] private float floatAmplitude = 0.15f;
    [SerializeField] private float floatFrequency = 1f;

    [Header("Spin")]
    [SerializeField] private float spinDegreesPerSecond = 90f;

    private Vector3 startLocalPosition;

    private void Start()
    {
        // Cache starting position so we bob relative to wherever
        // the pickup was placed in the scene.
        startLocalPosition = transform.localPosition;
    }

    private void Update()
    {
        // Bob up and down using a sine wave.
        float newY = startLocalPosition.y 
                     + Mathf.Sin(Time.time * floatFrequency * Mathf.PI * 2f) 
                     * floatAmplitude;

        transform.localPosition = new Vector3(
            startLocalPosition.x,
            newY,
            startLocalPosition.z
        );

        // Spin around Y axis.
        transform.Rotate(0f, spinDegreesPerSecond * Time.deltaTime, 0f, Space.World);
    }
}