using UnityEngine;

//
// EnemyStats is a ScriptableObject, which means:
// - It is NOT an enemy in the scene.
// - It is a “data file” asset you make in the Project window.
// - Enemies can read these values to know how they should behave.
//
// This is useful because you can change enemy settings without rewriting code.
//

public abstract class EnemyStats : ScriptableObject
{
    [Header("Common Info")]

    [Tooltip("A name that shows up in the Inspector so humans know what this is.")]
    public string displayName = "Enemy";

    [Header("Health")]

    [Tooltip("How much health this enemy starts with.")]
    [Min(1)]
    public int maxHealth = 1;

    [Header("Movement")]

    [Tooltip("How fast the enemy moves in meters per second.")]
    [Min(0f)]
    public float moveSpeed = 5f;

    [Tooltip("How quickly the enemy speeds up to its moveSpeed.")]
    [Min(0f)]
    public float acceleration = 10f;

    // This runs in the editor when values change (and sometimes at load time).
    // It helps keep your stats from becoming “impossible” or broken.
    private void OnValidate()
    {
        // These are just safety clamps.
        // Even though we have [Min()], it’s good to be extra safe.
        if (maxHealth < 1) maxHealth = 1;
        if (moveSpeed < 0f) moveSpeed = 0f;
        if (acceleration < 0f) acceleration = 0f;

        // If displayName is empty, give it a default so it’s not blank in the Inspector.
        if (string.IsNullOrWhiteSpace(displayName))
            displayName = name; // name = the asset file name
    }
}