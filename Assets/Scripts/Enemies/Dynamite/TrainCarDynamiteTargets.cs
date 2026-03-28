using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class TrainCarDynamiteTargets : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TrainPathFollower carFollower;
    [SerializeField] private Transform banditAnchor;
    [SerializeField] private Collider landingCollider;
    [SerializeField] private List<Transform> targetPoints = new();

    public TrainPathFollower CarFollower => carFollower;
    public Transform BanditAnchor => banditAnchor != null ? banditAnchor : transform;
    public Collider LandingCollider => landingCollider;
    public bool HasTargets => carFollower != null && targetPoints != null && targetPoints.Count > 0;

    public Transform GetRandomTargetPoint()
    {
        if (targetPoints == null || targetPoints.Count == 0)
            return null;

        int index = Random.Range(0, targetPoints.Count);
        return targetPoints[index];
    }

    private void OnValidate()
    {
        if (carFollower == null)
            carFollower = GetComponent<TrainPathFollower>();

        // Leave targetPoints alone so the Inspector can edit them.
    }
}