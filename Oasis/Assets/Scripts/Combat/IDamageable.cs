using UnityEngine;

namespace Oasis.Combat
{
    /// <summary>
    /// Anything that can receive damage in the combat pipeline.
    /// Implemented by PlayerStats, enemy health, destructible objects.
    /// </summary>
    public interface IDamageable
    {
        void ApplyDamage(DamageData data);
        float GetDefense();
        Transform GetTransform();
    }

    /// <summary>
    /// Environmental hazards that deal periodic non-combat damage.
    /// (Silencio, radiation zones, fire, etc.)
    /// </summary>
    public interface IHazard
    {
        DamageData GetHazardDamage();
        float GetDamageInterval();
        bool IsTargetInHazard(Collider target);
    }
}
