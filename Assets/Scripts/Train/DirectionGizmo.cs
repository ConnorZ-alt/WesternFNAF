using UnityEditor;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class DirectionGizmo : MonoBehaviour
{
    [SerializeField] float gizmoLength = 1f;
    private void OnDrawGizmos() 
    {
        var p1 = transform.position + Vector3.up;
        var p2 = transform.position + transform.forward * gizmoLength + Vector3.up;
        #if UNITY_EDITOR

        var thickness = 20;
        Handles.DrawBezier(p1, p2, p1, p2, Color.blue, null, thickness);
#endif

    }
}
