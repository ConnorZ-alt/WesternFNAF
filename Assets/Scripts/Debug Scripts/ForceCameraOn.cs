using UnityEngine;
public class ForceCameraOn : MonoBehaviour
{
    void Awake()
    {
        var cam = GetComponent<Camera>();
        cam.enabled = true;
        cam.cullingMask = ~0;   // Everything
        cam.targetDisplay = 0;  // Display 1
        Debug.Log("[ForceCameraOn] Camera forced on");
    }
}