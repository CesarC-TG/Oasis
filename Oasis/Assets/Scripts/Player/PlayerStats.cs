using UnityEngine;
using UnityEngine.Events;

public class PlayerStats : MonoBehaviour
{
    [Header("Vida")]
    public float maxHP = 100f;
    public float hpRegenRate = 2f;
    public float hpRegenDelay = 10f;   // 10s sin daño antes de regenerar

    [Header("Stamina")]
    public float maxStamina = 100f;
    public float staminaDrainRate = 15f;
    public float staminaRegenRate = 20f;
    public float staminaRegenDelay = 1.5f;
    public float runGracePeriod = 1.5f;         // Sprint Burst: 1.5s gratis antes de drenar
    [Range(0f, 1f)] public float minStaminaToRun = 0.2f;   // requiere 20% para salir de exhaustion

    public float CurrentHP { get; private set; }
    public float CurrentStamina { get; private set; }
    public bool IsExhausted { get; private set; }

    public UnityEvent onDeath;

    private float _hpRegenTimer;
    private float _staminaRegenTimer;
    private float _runTimer;
    private bool _isDead;

    void Awake()
    {
        CurrentHP = maxHP;
        CurrentStamina = maxStamina;
    }

    void Update()
    {
        RegenerateHP();
        RegenerateStamina();
        UpdateExhaustion();
    }

    // --- Stamina publica ---

    public bool CanRun()
    {
        if (IsExhausted) return CurrentStamina >= maxStamina * minStaminaToRun;
        return CurrentStamina > 0;
    }

    // Sprint Burst: 1.5s gratis, luego drena stamina. Devuelve false si se agota.
    public bool UseRunStamina()
    {
        _runTimer += Time.deltaTime;

        if (_runTimer < runGracePeriod) return true;

        if (CurrentStamina <= 0)
        {
            _runTimer = 0;
            return false;
        }

        DrainStamina(staminaDrainRate * Time.deltaTime);
        return true;
    }

    public void ResetRunTimer() => _runTimer = 0;

    // para usar desde habilidades de combate mas adelante
    public bool UseStamina(float amount)
    {
        if (CurrentStamina < amount) return false;
        DrainStamina(amount);
        return true;
    }

    // --- Daño y curacion ---

    public void TakeDamage(float amount)
    {
        if (_isDead) return;
        CurrentHP = Mathf.Max(0, CurrentHP - amount);
        _hpRegenTimer = 0;

        if (CurrentHP <= 0) Die();
    }

    public void Heal(float amount)
    {
        CurrentHP = Mathf.Min(maxHP, CurrentHP + amount);
    }

    // --- Privados ---

    void DrainStamina(float amount)
    {
        CurrentStamina = Mathf.Max(0, CurrentStamina - amount);
        _staminaRegenTimer = 0;
    }

    void RegenerateHP()
    {
        if (_isDead || CurrentHP >= maxHP) return;
        _hpRegenTimer += Time.deltaTime;
        if (_hpRegenTimer >= hpRegenDelay)
            CurrentHP = Mathf.Min(maxHP, CurrentHP + hpRegenRate * Time.deltaTime);
    }

    void RegenerateStamina()
    {
        if (CurrentStamina >= maxStamina) return;
        _staminaRegenTimer += Time.deltaTime;
        if (_staminaRegenTimer >= staminaRegenDelay)
            CurrentStamina = Mathf.Min(maxStamina, CurrentStamina + staminaRegenRate * Time.deltaTime);
    }

    void UpdateExhaustion()
    {
        if (CurrentStamina <= 0)
            IsExhausted = true;
        else if (CurrentStamina >= maxStamina * minStaminaToRun)
            IsExhausted = false;
    }

    void Die()
    {
        _isDead = true;
        onDeath?.Invoke();
    }
}
