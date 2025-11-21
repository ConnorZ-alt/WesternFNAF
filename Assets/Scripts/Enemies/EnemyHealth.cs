using UnityEngine;

public class EnemyHealth : MonoBehaviour, IDamageable
{
    [SerializeField] private float maxHP = 1f;
    [SerializeField] private bool destroyOnDeath = true;

    public float CurrentHP { get; private set; }
    public bool IsDead { get; private set; }

    void Awake() => CurrentHP = maxHP;

    public void TakeDamage(float amount)
    {
        if (IsDead) return;
        CurrentHP = Mathf.Max(0f, CurrentHP - amount);
        if (CurrentHP <= 0f) Die();
    }

    void Die()
    {
        if (IsDead) return;
        IsDead = true;

        var bandit = GetComponent<DynamiteBandit>();
        if (bandit != null && bandit.onFinished != null)
        {
            bandit.onFinished.Invoke();
        }
        
        // optional: notify spawner here, then destroy
        if (destroyOnDeath)
        {
            Destroy(gameObject);
        }
    }
}