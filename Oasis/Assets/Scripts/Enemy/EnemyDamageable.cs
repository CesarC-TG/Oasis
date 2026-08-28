using UnityEngine;
using UnityEngine.UI;
using Oasis.Combat;
using Oasis.UI;

namespace Oasis.Enemy
{
    /// <summary>
    /// Simple enemy — placeholder for Vaciados/Garra/Fusionado.
    /// Implements IDamageable for the combat pipeline.
    /// Displays a world-space health bar, spawns damage numbers,
    /// applies hit flash and knockback.
    /// </summary>
    public class EnemyDamageable : MonoBehaviour, IDamageable
    {
        [Header("Stats")]
        public float MaxHP = 80f;
        public float Defense = 20f;

        [Header("Health Bar (world-space)")]
        public Slider HealthBar;
        public Canvas HealthBarCanvas;

        [Header("Knockback")]
        public float KnockbackForce = 0.3f;
        public float KnockbackDuration = 0.12f;

        public float CurrentHP { get; private set; }
        public bool IsDead { get; private set; }

        private EnemyVisuals _visuals;
        private Animator _animator;
        private Vector3 _knockbackVelocity;
        private float _knockbackTimer;

        private static readonly int AnimHurt  = Animator.StringToHash("Hurt");
        private static readonly int AnimDeath = Animator.StringToHash("Death");

        private void Awake()
        {
            CurrentHP = MaxHP;
            if (HealthBar != null)
            {
                HealthBar.maxValue = MaxHP;
                HealthBar.value = MaxHP;
            }
            _visuals = GetComponent<EnemyVisuals>();
            _animator = GetComponent<Animator>();
        }

        private void Update()
        {
            // Apply knockback
            if (_knockbackTimer > 0f)
            {
                _knockbackTimer -= Time.deltaTime;
                transform.Translate(_knockbackVelocity * Time.deltaTime, Space.World);
            }
        }

        private void LateUpdate()
        {
            // Face health bar toward camera
            if (HealthBarCanvas != null && Camera.main != null)
                HealthBarCanvas.transform.forward = Camera.main.transform.forward;
        }

        public void ApplyDamage(DamageData data)
        {
            if (IsDead) return;

            float rawDamage = DamageCalculator.CalculateRaw(ref data);
            float netDamage = DamageCalculator.ApplyDefense(rawDamage, Defense);

            CurrentHP = Mathf.Max(0, CurrentHP - netDamage);

            // Update health bar
            if (HealthBar != null)
                HealthBar.value = CurrentHP;

            // Fire global event (VFX, SFX, UI can listen)
            CombatEventBus.FireDamageDealt(data, netDamage);

            // Juice: damage number
            Vector3 spawnPos = data.HitPoint != Vector3.zero ? data.HitPoint : transform.position + Vector3.up * 1.5f;
            DamageNumber.Spawn(spawnPos, netDamage, data.WasCritical, false);

            // Juice: hit flash
            if (_visuals != null)
                _visuals.FlashHit();

            // Animator: hurt trigger
            if (_animator != null)
                _animator.SetTrigger(AnimHurt);

            // Juice: knockback away from source
            if (data.Source != null)
            {
                Vector3 knockDir = (transform.position - data.Source.transform.position).normalized;
                knockDir.y = 0f;
                _knockbackVelocity = knockDir * KnockbackForce;
                _knockbackTimer = KnockbackDuration;
            }

            Debug.Log($"[Enemy] Took {netDamage:F1} damage from {data.Source?.name ?? "???"} | HP: {CurrentHP}/{MaxHP}" +
                     (data.WasCritical ? " [CRIT!]" : ""));

            if (CurrentHP <= 0f)
                Die();
        }

        public float GetDefense() => Defense;

        public Transform GetTransform() => transform;

        private void Die()
        {
            if (IsDead) return;
            IsDead = true;
            CombatEventBus.FireEntityKilled(gameObject);
            Debug.Log($"[Enemy] {gameObject.name} killed!");

            // Animator: death trigger
            if (_animator != null)
                _animator.SetTrigger(AnimDeath);

            // Disable after death animation plays
            float deathDelay = (_animator != null) ? 1.5f : 0f;
            Destroy(gameObject, deathDelay);
        }
    }
}
