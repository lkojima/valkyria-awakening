using UnityEngine;

/// <summary>
/// IA especial para bosses com fases. Herda de EnemyController.
/// </summary>
public class BossAI : EnemyController
{
    [Header("Phases")]
    [Range(0f, 1f)] public float phase2HealthPercent = 0.5f;
    [Range(0f, 1f)] public float phase3HealthPercent = 0.2f;
    public float specialAttackCooldown = 10f;

    int _currentPhase = 1;
    float _specialTimer;

    protected override void Update()
    {
        base.Update();
        _specialTimer -= Time.deltaTime;

        CheckPhaseTransition();

        if (_specialTimer <= 0f && _currentPhase >= 2)
        {
            DoSpecialAttack();
            _specialTimer = specialAttackCooldown;
        }
    }

    void CheckPhaseTransition()
    {
        float healthPct = (float)currentHealth / maxHealth;

        if (healthPct <= phase3HealthPercent && _currentPhase < 3)
        {
            EnterPhase(3);
        }
        else if (healthPct <= phase2HealthPercent && _currentPhase < 2)
        {
            EnterPhase(2);
        }
    }

    void EnterPhase(int phase)
    {
        _currentPhase = phase;
        animator?.SetInteger("Phase", phase);
        Debug.Log($"[Boss] {name} entrou na FASE {phase}!");

        // Buffs por fase
        switch (phase)
        {
            case 2:
                moveSpeed *= 1.3f;
                attackSpeed *= 1.2f; // se existir
                break;
            case 3:
                moveSpeed *= 1.5f;
                damage = Mathf.RoundToInt(damage * 1.5f);
                break;
        }
    }

    void DoSpecialAttack()
    {
        animator?.SetTrigger("Special");
        Debug.Log($"[Boss] {name} usou ATAQUE ESPECIAL da Fase {_currentPhase}!");

        // TODO: Implementar ataque especial por fase
        // Fase 2: Invocar minions, área de efeito
        // Fase 3: Raio, meteoro, transformação
    }
}