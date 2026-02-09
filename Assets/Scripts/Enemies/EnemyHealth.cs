using System;
using UnityEngine;

[DisallowMultipleComponent]
public class EnemyHealth : MonoBehaviour, IDamageable
{
    // This script is the enemy’s “health bar” in code.
    // It tracks how much HP the enemy has, and it decides when the enemy is dead.

    [Header("Health")]
    [SerializeField] private float maxHP = 1f;

    [Tooltip("If true and this enemy is NOT a DynamiteBandit, we destroy this GameObject when it dies.")]
    [SerializeField] private bool destroyOnDeath = false; // DynamiteBandit usually handles its own teardown.

    // Public read-only properties so other scripts can check health safely.
    public float CurrentHP { get; private set; }
    public bool IsDead { get; private set; }
    
    // Other scripts can “listen” for death without this script needing to know them.
    public event Action<EnemyHealth> OnDied;

    private void Awake()
    {
        // Awake runs when this object is created.
        // We set starting values here.
        ResetHealth();
    }

    /// <summary>
    /// Resets HP back to max and marks the enemy alive.
    /// This is useful if you ever want to reuse enemies instead of destroying them.
    /// </summary>
    public void ResetHealth()
    {
        CurrentHP = Mathf.Max(0f, maxHP);
        IsDead = false;
    }

    /// <summary>
    /// This is called when something (like Dynamite) damages the enemy.
    /// </summary>
    public void TakeDamage(float amount)
    {
        // If the enemy is already dead, ignore more damage.
        if (IsDead) return;

        // If the damage amount is weird (0 or negative), ignore it.
        if (amount <= 0f) return;

        // Subtract HP but never go below 0.
        CurrentHP = Mathf.Max(0f, CurrentHP - amount);

        // If HP hit 0, the enemy is dead.
        if (CurrentHP <= 0f)
            Die();
    }

    private void Die()
    {
        // State pattern: once we are Dead, we never go back in this script.
        if (IsDead) return;
        IsDead = true;

        // Tell listeners (like score systems, UI, etc.) that we died.
        OnDied?.Invoke(this);

        // IMPORTANT:
        // If this enemy is a DynamiteBandit, let the bandit script handle the full “death sequence”.
        // That bandit knows how to notify the spawner and clean up itself safely.
        DynamiteBandit bandit = GetComponentInParent<DynamiteBandit>();
        if (bandit != null)
        {
            Debug.Log("[EnemyHealth] Delegating death to DynamiteBandit.Kill().");
            bandit.Kill();
            return;
        }

        // Fallback:
        // If this is NOT a bandit, we can optionally destroy it here.
        if (destroyOnDeath)
        {
            Debug.Log("[EnemyHealth] Destroying GameObject because destroyOnDeath is true.");
            Destroy(gameObject);
        }
        else
        {
            // If we are not destroying it, we at least stop it from taking more hits.
            // (In a real game you might disable AI, play an animation, etc.)
            Debug.Log("[EnemyHealth] Enemy is dead, but not destroying (destroyOnDeath is false).");
        }
    }
}
