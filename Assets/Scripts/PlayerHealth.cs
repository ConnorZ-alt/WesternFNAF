using UnityEngine;

[DisallowMultipleComponent]
public class PlayerHealth : MonoBehaviour, IDamageable
{
    [Header("Health")]
    [SerializeField] private float maxHealthPoints = 100f;
    [SerializeField] private bool logDamage = true;

    [Header("Optional Knockback")]
    [SerializeField] private bool applyKnockback = false;
    [SerializeField] private float knockbackForce = 6f;

    public float CurrentHealthPoints { get; private set; }
    public bool IsDead { get; private set; }

    private Rigidbody rigidbodyComponent;

    void Awake()
    {
        CurrentHealthPoints = maxHealthPoints;
        rigidbodyComponent = GetComponent<Rigidbody>();
        IsDead = false;
    }

    private void OnEnable()
    {
        if (CurrentHealthPoints <= 0f || IsDead)
        {
            CurrentHealthPoints = maxHealthPoints;
            IsDead = false;
        }
    }

    public void TakeDamage(float damageAmount)
    {
        if (IsDead) return;

        CurrentHealthPoints = Mathf.Max(0f, CurrentHealthPoints - damageAmount);
        if (logDamage) Debug.Log($"[PlayerHealth] -{damageAmount} (HP {CurrentHealthPoints}/{maxHealthPoints})");

        if (applyKnockback && rigidbodyComponent != null)
        {
            // Small outward impulse from source if available
            // (Callers can also AddExplosionForce directly.)
            rigidbodyComponent.AddForce(Vector3.back * knockbackForce, ForceMode.Impulse);
        }

        if (CurrentHealthPoints <= 0f)
            Die();
    }

    public void Heal(float healAmount)
    {
        if (IsDead) return;
        CurrentHealthPoints = Mathf.Min(maxHealthPoints, CurrentHealthPoints + healAmount);
    }

    public void Kill() => Die();
    
    private void Die()
    {
        if (IsDead) return;
        IsDead = true;
        
        // Send to Game Over scene
        var sceneManagement = FindObjectOfType<SceneManagement>();
        if (sceneManagement != null)
        {
            sceneManagement.OnGameOver();
        }
        else
        {
            Debug.LogWarning("[PlayerHealth] SceneManagement not found. Implement game-over flow.");
        }
    }
}
