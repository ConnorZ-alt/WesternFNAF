using UnityEngine;

public class BootPhysics : MonoBehaviour
{
    void Awake()
    {
        // Ensure transforms moved by scripts are reflected in the physics world before queries.
        Physics.autoSyncTransforms = true;
        // (Optional) Make sure FixedUpdate cadence is sane in WebGL builds
        Time.fixedDeltaTime = 0.02f; // 50 Hz
    }
}