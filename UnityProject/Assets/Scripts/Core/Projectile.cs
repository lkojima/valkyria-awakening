using UnityEngine;

/// <summary>
/// Projétil genérico para armas ranged/magic.
/// </summary>
public class Projectile : MonoBehaviour
{
    Vector2 _direction;
    int _damage;
    float _speed;
    bool _isCritical;
    Transform _owner;
    float _lifeTime = 5f;

    public void Initialize(Vector2 direction, int damage, float speed, bool isCritical, Transform owner)
    {
        _direction = direction.normalized;
        _damage = damage;
        _speed = speed;
        _isCritical = isCritical;
        _owner = owner;

        // Rotaciona para olhar na direção
        float angle = Mathf.Atan2(_direction.y, _direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);

        Destroy(gameObject, _lifeTime);
    }

    void Update()
    {
        transform.position += (Vector3)_direction * _speed * Time.deltaTime;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.transform == _owner) return; // ignora dono

        if (other.TryGetComponent<EnemyController>(out var enemy))
        {
            enemy.TakeDamage(_damage);
            if (_isCritical) Debug.Log($"[Projectile] CRITICAL! {_damage} damage");
        }

        // Para em qualquer colisão sólida
        if (other.GetComponent<Collider2D>() != null && !other.isTrigger)
            Destroy(gameObject);
    }
}