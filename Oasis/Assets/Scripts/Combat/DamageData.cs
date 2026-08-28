namespace Oasis.Combat
{
    /// <summary>
    /// The "currency" of the combat pipeline. Carries all modifier context
    /// from attacker to defender through the DamageCalculator.
    /// </summary>
    public enum DamageType { Physical, Radiation, Fire, Tech, Environmental }

    public struct DamageData
    {
        public float BaseDamage;
        public float WeaponMultiplier;    // 1.0 if unarmed
        public float SkillMultiplier;     // 1.0 for basic attack
        public float RadiationMultiplier; // 1.0 in safe zones
        public float BeastMultiplier;     // 1.0 if beast inactive
        public float CritChance;          // 0.0–0.4
        public float CritMultiplier;      // 1.5–2.5

        public UnityEngine.GameObject Source;
        public DamageType Type;
        public UnityEngine.Vector3 HitPoint;
        public UnityEngine.Vector3 HitNormal;
        public bool WasCritical;            // Set by DamageCalculator on crit

        /// <summary>Create a simple physical attack (no multipliers, no crit).</summary>
        public static DamageData Physical(float damage, UnityEngine.GameObject source)
        {
            return new DamageData
            {
                BaseDamage = damage,
                WeaponMultiplier = 1.0f,
                SkillMultiplier = 1.0f,
                RadiationMultiplier = 1.0f,
                BeastMultiplier = 1.0f,
                CritChance = 0f,
                CritMultiplier = 1.0f,
                Source = source,
                Type = DamageType.Physical
            };
        }
    }
}
