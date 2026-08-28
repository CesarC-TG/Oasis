using UnityEngine;

namespace Oasis.Combat
{
    /// <summary>
    /// Pure-function damage calculator. Stateless, testable in isolation.
    /// Tuning knobs are static properties — configurable at runtime.
    /// </summary>
    public static class DamageCalculator
    {
        /// <summary>Floor damage applied even when defense exceeds raw damage.</summary>
        public static float MinDamageClamp { get; set; } = 1.0f;

        /// <summary>Ceiling on raw damage before defense reduction.</summary>
        public static float MaxDamageClamp { get; set; } = 1000f;

        /// <summary>
        /// Calculate raw damage from all attacker-side multipliers.
        /// Multiplicative stacking, capped at MaxDamageClamp.
        /// </summary>
        public static float CalculateRaw(ref DamageData data)
        {
            float raw = data.BaseDamage
                      * data.WeaponMultiplier
                      * data.SkillMultiplier
                      * data.RadiationMultiplier
                      * data.BeastMultiplier;

            raw = Mathf.Min(raw, MaxDamageClamp);

            data.WasCritical = false;
            if (Random.value < data.CritChance)
            {
                raw *= data.CritMultiplier;
                data.WasCritical = true;
            }

            return raw;
        }

        /// <summary>
        /// Reduce raw damage by flat defense. Never returns below MinDamageClamp.
        /// </summary>
        public static float ApplyDefense(float rawDamage, float defense)
        {
            return Mathf.Max(MinDamageClamp, rawDamage - defense);
        }
    }
}
