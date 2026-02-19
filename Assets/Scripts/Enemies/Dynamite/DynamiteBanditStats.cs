using UnityEngine;

[CreateAssetMenu(menuName = "Enemies/Bandit Stats", fileName = "BanditStats")]
public class BanditStats : EnemyStats
{
    // NOTE:
    // This file is NOT a MonoBehaviour. It does not run in the scene.
    // It’s just a “settings card” (data) that other scripts read.

    [Header("Follow / Positioning (relative to Train)")]

    [Tooltip("How far to the side of the train the bandit tries to stay. Bigger number = farther out.")]
    [Min(0f)]
    public float lateralOffset = 3.5f;

    [Tooltip("How far behind the train (train local -Z) the bandit tries to stay.")]
    [Min(0f)]
    public float followDistanceBack = 2f;

    [Header("Throwing Dynamite")]

    [Tooltip("Seconds between throws (if the bandit is allowed to throw).")]
    [Min(0f)]
    public float throwCooldown = 4.0f;

    [Tooltip("How long the dynamite stays in the air (seconds). Lower = faster throw, higher = floatier arc.")]
    public Vector2 arcTimeRange = new Vector2(0.6f, 1.1f);

    [Tooltip("How inaccurate the throw is (meters). Higher = more random landing spots.")]
    [Min(0f)]
    public float throwInaccuracy = 0.5f;

    [Header("Train Interior Clamp (landing area in train local space)")]

    [Tooltip("Minimum local X (left/right) allowed for landing point inside the train area.")]
    public float minTrainX = -0.8f;

    [Tooltip("Maximum local X (left/right) allowed for landing point inside the train area.")]
    public float maxTrainX = 0.8f;

    [Tooltip("Minimum local Z (forward/back) allowed for landing point inside the train area.")]
    public float minTrainZ = -1.8f;

    [Tooltip("Maximum local Z (forward/back) allowed for landing point inside the train area.")]
    public float maxTrainZ = 1.8f;

    [Header("Spawn/Despawn")]

    [Tooltip("If the bandit is farther than this from the train, it can be considered 'gone' or despawned.")]
    [Min(0f)]
    public float despawnDistance = 25f;

    [Tooltip("Seconds to wait before respawning another bandit.")]
    [Min(0f)]
    public float respawnDelay = 5f;

    /// <summary>
    /// Returns a random flight time for a throw, using arcTimeRange.
    /// This is useful so scripts don’t repeat Random.Range everywhere.
    /// </summary>
    public float GetRandomArcTimeSeconds()
    {
        // We make sure min <= max so Random.Range doesn't act weird.
        float min = Mathf.Min(arcTimeRange.x, arcTimeRange.y);
        float max = Mathf.Max(arcTimeRange.x, arcTimeRange.y);
        return Random.Range(min, max);
    }

    /// <summary>
    /// Clamps a point that is in TRAIN LOCAL SPACE into a safe rectangle area.
    /// This helps keep landing points from going off the deck.
    /// </summary>
    public Vector3 ClampTrainLocalPoint(Vector3 localPoint)
    {
        localPoint.x = Mathf.Clamp(localPoint.x, minTrainX, maxTrainX);
        localPoint.z = Mathf.Clamp(localPoint.z, minTrainZ, maxTrainZ);
        return localPoint;
    }

    /// <summary>
    /// A quick Rect view of the allowed landing area (still train local space).
    /// This is mostly for debugging and clean code.
    /// </summary>
    public Rect TrainClampRect
    {
        get
        {
            float xMin = Mathf.Min(minTrainX, maxTrainX);
            float xMax = Mathf.Max(minTrainX, maxTrainX);
            float zMin = Mathf.Min(minTrainZ, maxTrainZ);
            float zMax = Mathf.Max(minTrainZ, maxTrainZ);

            return Rect.MinMaxRect(xMin, zMin, xMax, zMax);
        }
    }

    // ----------------------------
    // Validation (runs in editor)
    // ----------------------------

    private void OnValidate()
    {
        // OnValidate runs in the Unity editor when you change values.
        // This helps catch mistakes early.

        // Make sure ranges are sensible.
        arcTimeRange.x = Mathf.Max(0.05f, arcTimeRange.x);
        arcTimeRange.y = Mathf.Max(arcTimeRange.x, arcTimeRange.y);

        // Ensure clamp ranges are ordered properly (min <= max).
        if (minTrainX > maxTrainX) (minTrainX, maxTrainX) = (maxTrainX, minTrainX);
        if (minTrainZ > maxTrainZ) (minTrainZ, maxTrainZ) = (maxTrainZ, minTrainZ);

        // These should never be negative.
        lateralOffset = Mathf.Max(0f, lateralOffset);
        followDistanceBack = Mathf.Max(0f, followDistanceBack);
        throwCooldown = Mathf.Max(0f, throwCooldown);
        throwInaccuracy = Mathf.Max(0f, throwInaccuracy);
        despawnDistance = Mathf.Max(0f, despawnDistance);
        respawnDelay = Mathf.Max(0f, respawnDelay);
    }
}
