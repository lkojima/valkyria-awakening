using UnityEngine;

/// <summary>
/// Comportamento base de inimigo. Preencha os campos no Inspector (ou via JSON do PrefabGenerator).
/// </summary>
public class EnemyController : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 3f;
    public float detectionRange = 8f;
    public float attackRange = 1.5f;

    [Header("Combat")]
    public int maxHealth = 30;
    public int currentHealth;
    public int damage = 10;
    public float attackCooldown = 1.5f;
    public int xpValue = 25;

    [Header("References")]
    public Transform player;
    public Animator animator;
    public AudioSource audioSource;

    float _attackTimer;
    Rigidbody2D _rb;
    SpriteRenderer _sr;

    protected virtual void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _sr = GetComponent<SpriteRenderer>();
        currentHealth = maxHealth;
        if (player == null) player = GameObject.FindWithTag("Player")?.transform;
    }

    protected virtual void Update()
    {
        if (player == null) return;

        _attackTimer -= Time.deltaTime;
        float dist = Vector2.Distance(transform.position, player.position);

        if (dist <= detectionRange)
        {
            HandleMovement(dist);
            HandleAttack(dist);
        }
    }

    protected virtual void HandleMovement(float distToPlayer)
    {
        if (distToPlayer > attackRange)
        {
            Vector2 dir = (player.position - transform.position).normalized;
            _rb.linearVelocity = dir * moveSpeed;

            // Flip sprite
            if (_sr != null) _sr.flipX = dir.x < 0;

            animator?.SetBool("Moving", true);
        }
        else
        {
            _rb.linearVelocity = Vector2.zero;
            animator?.SetBool("Moving", false);
        }
    }

    protected virtual void HandleAttack(float distToPlayer)
    {
        if (distToPlayer <= attackRange && _attackTimer <= 0f)
        {
            _attackTimer = attackCooldown;
            animator?.SetTrigger("Attack");
            // TODO: Implementar dano real ao player
            Debug.Log($"[Enemy] {name} atacou por {damage} de dano");
        }
    }

    public virtual void TakeDamage(int amount)
    {
        currentHealth -= amount;
        animator?.SetTrigger("Hit");

        if (currentHealth <= 0)
            Die();
    }

    protected virtual void Die()
    {
        animator?.SetTrigger("Die");
        // TODO: Drop XP, loot, etc.
        Destroy(gameObject, 1f); // espera animação
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}