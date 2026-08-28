using UnityEngine;

namespace Oasis.Combat
{
    /// <summary>
    /// Centralized event bus for all combat events.
    /// Decouples VFX, SFX, UI, and quest systems from combat logic.
    /// 
    /// Subscribers register in OnEnable, unregister in OnDisable.
    /// Domain Reload safety: static delegates cleared on assembly reload.
    /// </summary>
    public static class CombatEventBus
    {
        /// <summary>Fired when damage is applied. float = net damage actually dealt.</summary>
        public static event System.Action<DamageData, float> OnDamageDealt;

        /// <summary>Fired when an entity reaches 0 HP.</summary>
        public static event System.Action<GameObject> OnEntityKilled;

        /// <summary>Fired when an entity is healed. float = amount healed.</summary>
        public static event System.Action<GameObject, float> OnHealed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ClearOnDomainReload()
        {
            OnDamageDealt = null;
            OnEntityKilled = null;
            OnHealed = null;
        }

        public static void FireDamageDealt(DamageData data, float netDamage)
        {
            OnDamageDealt?.Invoke(data, netDamage);
        }

        public static void FireEntityKilled(GameObject entity)
        {
            OnEntityKilled?.Invoke(entity);
        }

        public static void FireHealed(GameObject entity, float amount)
        {
            OnHealed?.Invoke(entity, amount);
        }
    }
}
