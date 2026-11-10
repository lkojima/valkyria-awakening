using UnityEngine;

/// <summary>
/// Arma equipável pelo player. Configurado via JSON do PrefabGenerator.
/// </summary>
public class Weapon : MonoBehaviour
{
    public enum WeaponType { Melee = 0, Ranged = 1, Magic = 2 }

    [Header("Info")]
    public string weaponName = "Weapon";
    public WeaponType weaponType = WeaponType.Melee;

    [Header("Stats")]
    public int damage = 10;
    public float attackSpeed = 1f;      // ataques por segundo
    public float range = 1.5f;
    public float knockback = 2f;
    [Range(0f, 1f)] public float criticalChance = 0.05f;
    public float criticalMultiplier = 1.5f;

    [Header("Ranged/Magic")]
    public GameObject projectilePrefab;
    public float projectileSpeed = 15f;
    public int manaCost = 0;

    [Header("Special")]
    public bool applyPoison = false;
    public int poisonDamage = 3;
    public float poisonDuration = 5f;
    public bool cleave = false;
    public float cleaveAngle = 120f;

    float _nextAttackTime;

    public bool CanAttack => Time.time >= _nextAttackTime;

    public void Attack(Vector2 direction, Transform owner)
    {
        _nextAttackTime = Time.time + 1f / attackSpeed;

        bool isCrit = Random.value < criticalChance;
        int finalDamage = isCrit ? Mathf.RoundToInt(damage * criticalMultiplier) : damage;

        switch (weaponType)
        {
            case WeaponType.Melee:
                DoMeleeAttack(direction, finalDamage, isCrit, owner);
                break;
            case WeaponType.Ranged:
                FireProjectile(direction, finalDamage, isCrit, owner);
                break;
            case WeaponType.Magic:
                CastSpell(direction, finalDamage, isCrit, owner);
                break;
        }
    }

    void DoMeleeAttack(Vector2 dir, int dmg, bool crit, Transform owner)
    {
        // OverlapBox na frente do player
        Vector2 origin = (Vector2)owner.position + dir * (range * 0.5f);
        var hits = Physics2D.OverlapBoxAll(origin, new Vector2(range, 0.5f), 0f, LayerMask.GetMask("Enemy"));

        foreach (var hit in hits)
        {
            if (hit.TryGetComponent<EnemyController>(out var enemy))
                enemy.TakeDamage(dmg);

            // Knockback
            if (hit.TryGetComponent<Rigidbody2D>(out var rb))
                rb.AddForce(dir * knockback, ForceMode2D.Impulse);
        }

        // Cleave (área em cone)
        if (cleave)
        {
            var cleaveHits = Physics2D.OverlapCircleAll(owner.position, range, LayerMask.GetMask("Enemy"));
            foreach (var hit in cleaveHits)
            {
                Vector2 toEnemy = (hit.transform.position - owner.position).normalized;
                float angle = Vector2.Angle(dir, toEnemy);
                if (angle <= cleaveAngle * 0.5f && hit.TryGetComponent<EnemyController>(out var e))
                    e.TakeDamage(Mathf.RoundToInt(dmg * 0.7f));
            }
        }

        if (crit) Debug.Log($"[Weapon] CRITICAL HIT! {finalDamage} damage");
    }

    void FireProjectile(Vector2 dir, int dmg, bool crit, Transform owner)
    {
        if (projectilePrefab == null) return;

        var proj = Instantiate(projectilePrefab, owner.position, Quaternion.identity);
        if (proj.TryGetComponent<Projectile>(out var p))
        {
            p.Initialize(dir, dmg, projectileSpeed, crit, owner);
        }
    }

    void CastSpell(Vector2 dir, int dmg, bool crit, Transform owner)
    {
        // Similar a Ranged mas pode ter efeitos de área, mana cost, etc.
        FireProjectile(dir, dmg, crit, owner);
    }
}