using UnityEngine;

public class TrainWheelManager : MonoBehaviour
{
    public Transform trainRoot;
    public Transform[] wheels;
    public float radius = 0.35f;

    private Vector3 lastPosition;

    void Start()
    {
        lastPosition = trainRoot.position;
    }

    void Update()
    {
        Vector3 delta = trainRoot.position - lastPosition;
        float distance = delta.magnitude;

        float angle = (distance / (2f * Mathf.PI * radius)) * 360f;

        foreach (var wheel in wheels)
        {
            wheel.Rotate(Vector3.right, angle, Space.Self);
        }

        lastPosition = trainRoot.position;
    }
}