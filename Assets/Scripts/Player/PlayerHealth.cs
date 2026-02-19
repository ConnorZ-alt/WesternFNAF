using UnityEngine;

/// <summary>
/// PlayerHealth
/// This script stores the player's HP and handles taking damage and dying.
/// If HP reaches 0, it tells SceneManagement to trigger Game Over.
/// </summary>
[DisallowMultipleComponent]
public class PlayerHealth : MonoBehaviour, IDamageable
{
    [Header("Health")]
    [Tooltip("The most HP the player can have.")]
    [SerializeField] private float maxHealthPoints = 100f;

    [Tooltip("If true, we print damage logs in the Console for debugging.")]
    [SerializeField] private bool logDamage = true;

    [Header("Optional Knockback")]
    [Tooltip("If true, the player gets pushed a little when taking damage.")]
    [SerializeField] private bool applyKnockback = false;

    [Tooltip("How strong the push is if knockback is enabled.")]
    [SerializeField] private float knockbackForce = 6f;

    // Public read-only info so other scripts can check health safely.
    public float CurrentHealthPoints { get; private set; }
    public bool IsDead { get; private set; }

    // Optional Rigidbody (only needed if knockback is enabled).
    private Rigidbody cachedRigidbody;

    private void Awake()
    {
        // Start the player at full health.
        CurrentHealthPoints = maxHealthPoints;
        IsDead = false;

        // Only used for knockback. It's okay if it's null.
        cachedRigidbody = GetComponent<Rigidbody>();
    }

    private void OnEnable()
    {
        // If this object gets re-enabled (like restarting the level),
        // reset HP if we were dead or at 0.
        if (IsDead || CurrentHealthPoints <= 0f)
        {
            CurrentHealthPoints = maxHealthPoints;
            IsDead = false;
        }
    }

    /// <summary>
    /// This is the main way enemies/explosions hurt the player.
    /// Damage lowers HP, and if HP hits 0, we die.
    /// </summary>
    public void TakeDamage(float damageAmount)
    {
        if (IsDead) return;
        if (damageAmount <= 0f) return; // ignore weird "negative damage"

        // Subtract HP but never go below 0.
        CurrentHealthPoints = Mathf.Max(0f, CurrentHealthPoints - damageAmount);

        if (logDamage)
            Debug.Log($"[PlayerHealth] Took {damageAmount} damage. HP = {CurrentHealthPoints}/{maxHealthPoints}");

        // Optional little shove. This is super simple (just pushes backward).
        if (applyKnockback && cachedRigidbody != null)
        {
            cachedRigidbody.AddForce(Vector3.back * knockbackForce, ForceMode.Impulse);
        }

        // If we ran out of HP, die.
        if (CurrentHealthPoints <= 0f)
            Die();
    }

    /// <summary>
    /// Heals the player (but not above max HP).
    /// </summary>
    public void Heal(float healAmount)
    {
        if (IsDead) return;
        if (healAmount <= 0f) return;

        CurrentHealthPoints = Mathf.Min(maxHealthPoints, CurrentHealthPoints + healAmount);
    }

    /// <summary>
    /// Instantly kills the player (like a big explosion).
    /// </summary>
    public void Kill()
    {
        Die();
    }

    /// <summary>
    /// Marks the player dead and triggers the Game Over flow using SceneManagement.
    /// </summary>
    private void Die()
    {
        if (IsDead) return;
        IsDead = true;

        // Find the SceneManagement object and tell it to go to Game Over.
        // (This keeps "scene switching" logic in one place.)
        SceneManagement sceneManagement = FindObjectOfType<SceneManagement>();
        if (sceneManagement != null)
        {
            sceneManagement.OnGameOver();
        }
        else
        {
            Debug.LogWarning("[PlayerHealth] SceneManagement not found, so Game Over can't load.");
        }
    }
}
