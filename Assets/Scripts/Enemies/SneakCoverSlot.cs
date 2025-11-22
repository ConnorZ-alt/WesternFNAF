using UnityEngine;

[DisallowMultipleComponent]
public class SneakCoverSlot : MonoBehaviour
{
    [Tooltip("If set, the bandit will face this transform while hiding/peeking. Otherwise it uses this slot's forward.")]
    public Transform faceHint;

    [Tooltip("Optional slight local offset the bandit should apply when occupying this slot (meters).")]
    public Vector3 localOffset;

    // Reserved at runtime by SneakBandit; you generally don't touch this in the inspector.
    [System.NonSerialized] public bool isReserved;

    public Vector3 WorldPosition => transform.TransformPoint(localOffset);
    public Vector3 WorldForward  => (faceHint ? (faceHint.position - transform.position).normalized : transform.forward);
}