using UnityEngine;

public abstract class EnemyStats : ScriptableObject
{
    [Header("Common")]
    [Tooltip("Display name for this enemy or config")]
    public string displayName = "Enemy";

    [Tooltip("Max health (if/when you add HP)")]
    [Min(1)] public int maxHealth = 1;

    [Tooltip("How fast the enemy generally moves (m/s)")]
    [Min(0f)] public float moveSpeed = 5f;

    [Tooltip("How quickly it accelerates to speed")]
    [Min(0f)] public float acceleration = 10f;
}