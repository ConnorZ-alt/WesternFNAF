using UnityEngine;
using Cinemachine;

[DisallowMultipleComponent]
public class TrackCreator : MonoBehaviour
{
    [Header("Output Path")]
    [SerializeField] private CinemachinePath track;
    [SerializeField] private bool loopedTrack = false;

    [Header("Input Points")]
    [Tooltip("Put empty transforms here as your path points (in order). If null, uses this transform's children.")]
    [SerializeField] private Transform waypointsRoot;

    [Tooltip("How strong the auto tangents are. Bigger = looser curves.")]
    [Range(0.1f, 1.0f)]
    [SerializeField] private float tangentStrength = 0.5f;

    [ContextMenu("Generate Track From Points")]
    public void GenerateTrack()
    {
        if (!track)
        {
            Debug.LogError("[TrackCreator] No CinemachinePath assigned.");
            return;
        }

        Transform root = waypointsRoot ? waypointsRoot : transform;
        int count = root.childCount;

        if (count < 2)
        {
            Debug.LogError("[TrackCreator] Need at least 2 waypoint transforms.");
            return;
        }

        var wps = new CinemachinePath.Waypoint[count];

        // First pass: positions
        for (int i = 0; i < count; i++)
        {
            Transform p = root.GetChild(i);
            Vector3 localPos = track.transform.InverseTransformPoint(p.position);

            wps[i] = new CinemachinePath.Waypoint
            {
                position = localPos,
                tangent  = Vector3.zero,
                roll     = 0f
            };
        }

        // Second pass: auto tangents based on neighbors
        for (int i = 0; i < count; i++)
        {
            int prev = (i - 1);
            int next = (i + 1);

            if (loopedTrack)
            {
                prev = (prev + count) % count;
                next = next % count;
            }
            else
            {
                prev = Mathf.Clamp(prev, 0, count - 1);
                next = Mathf.Clamp(next, 0, count - 1);
            }

            Vector3 dir = (wps[next].position - wps[prev].position);
            wps[i].tangent = dir * tangentStrength;
        }

        track.m_Waypoints = wps;
        track.m_Looped = loopedTrack;

        Debug.Log($"[TrackCreator] Generated {count} waypoints for {track.name} (loop={loopedTrack}).");
    }
}