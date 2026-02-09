using UnityEngine;
public class LifeBeacon : MonoBehaviour
{
    void OnEnable()  { Debug.Log("[Player] Enabled"); }
    void OnDisable() { Debug.Log("[Player] Disabled"); }
    void OnDestroy() { Debug.Log("[Player] Destroyed"); }
}