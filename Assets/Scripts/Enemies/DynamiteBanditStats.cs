using UnityEngine;

[CreateAssetMenu(menuName = "Enemies/Bandit Stats", fileName = "BanditStats")]
public class BanditStats : EnemyStats
{
    [Header("Follow / Positioning (relative to Train)")]
    [Tooltip("Side offset (+ right, - left) from the train while pacing")]
    public float lateralOffset = 3.5f;

    [Tooltip("How far behind the train (−Z local) the bandit rides")]
    public float followDistanceBack = 2f;

    [Header("Throwing Dynamite")]
    [Tooltip("Seconds between throws")]
    [Min(0f)] public float throwCooldown = 4.0f;

    [Tooltip("Ballistic flight time range (seconds) for thrown dynamite")]
    public Vector2 arcTimeRange = new Vector2(0.6f, 1.1f);

    [Tooltip("Random landing error (meters) added to X/Z")]
    [Min(0f)] public float throwInaccuracy = 0.5f;

    [Header("Train Interior Clamp (landing area in train local space)")]
    public float minTrainX = -0.8f;
    public float maxTrainX =  0.8f;
    public float minTrainZ = -1.8f;
    public float maxTrainZ =  1.8f;

    [Header("Spawn/Despawn")]
    [Tooltip("If bandit is farther than this from the train, consider despawn/leave")]
    [Min(0f)] public float despawnDistance = 25f;

    [Tooltip("Seconds to wait before respawning another bandit")]
    [Min(0f)] public float respawnDelay = 5f;
}