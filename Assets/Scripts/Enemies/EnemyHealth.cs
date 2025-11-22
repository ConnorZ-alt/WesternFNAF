using UnityEngine;

public class EnemyHealth : MonoBehaviour, IDamageable
{
    [SerializeField] private float maxHP = 1f;
    [SerializeField] private bool destroyOnDeath = false; // let DynamiteBandit handle teardown

    public float CurrentHP { get; private set; }
    public bool IsDead { get; private set; }

    private void Awake()
    {
        CurrentHP = maxHP;
        IsDead = false;
    }

    public void TakeDamage(float amount)
    {
        if (IsDead) return;
        CurrentHP = Mathf.Max(0f, CurrentHP - amount);
        if (CurrentHP <= 0f) Die();
    }

    private void Die()
    {
        if (IsDead) return;
        IsDead = true;

        // IMPORTANT: hand off to the bandit's lifecycle
        var bandit = GetComponentInParent<DynamiteBandit>();
        if (bandit != null)
        {
            Debug.Log("[EnemyHealth] Delegating death to DynamiteBandit.Kill()");
            bandit.Kill();   // DynamiteBandit will invoke onFinished and destroy/disable itself.
            return;
        }
        
        // Fallback (if this isn't a bandit)
        Debug.LogWarning("[EnemyHealth] No DynamiteBandit found; destroying GameObject directly.");
        if (destroyOnDeath)
        {
            Destroy(gameObject);
        }
    }
}