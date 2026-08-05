using UnityEngine;
using Oasis.Combat;

namespace Oasis.Audio
{
    /// <summary>
    /// Placeholder combat audio system.
    /// Subscribes to CombatEventBus and plays spatial/UI one-shots.
    /// Replace AudioClip references with final SFX during production.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class CombatAudio : MonoBehaviour
    {
        [Header("Audio Sources")]
        [Tooltip("2D audio source for UI sounds.")]
        public AudioSource UiAudioSource;

        [Tooltip("3D spatial audio source for world-space combat sounds.")]
        public AudioSource SpatialAudioSource;

        [Header("SFX Clips")]
        public AudioClip SfxMeleeSwing;
        public AudioClip SfxMeleeImpact;
        public AudioClip SfxEnemyHurt;
        public AudioClip SfxEnemyDeath;
        public AudioClip SfxPlayerHurt;
        public AudioClip SfxUIDamage;

        [Header("Variation")]
        [Range(0f, 0.5f)]
        [Tooltip("Random pitch offset applied per hit (± this value).")]
        public float PitchVariation = 0.1f;

        private readonly Collider[] _hitCheck = new Collider[4];

        void OnEnable()
        {
            CombatEventBus.OnDamageDealt += HandleDamageDealt;
            CombatEventBus.OnEntityKilled += HandleEntityKilled;
        }

        void OnDisable()
        {
            CombatEventBus.OnDamageDealt -= HandleDamageDealt;
            CombatEventBus.OnEntityKilled -= HandleEntityKilled;
        }

        void Reset()
        {
            if (SpatialAudioSource == null)
                SpatialAudioSource = GetComponent<AudioSource>();
            if (SpatialAudioSource != null)
                SpatialAudioSource.spatialBlend = 1f;
        }

        void HandleDamageDealt(DamageData data, float netDamage)
        {
            bool isPlayerAttack = data.Source != null &&
                                  data.Source.TryGetComponent<PlayerController>(out _);

            if (isPlayerAttack)
            {
                PlaySpatial(SfxMeleeSwing, data.Source.transform.position);
                PlaySpatial(SfxMeleeImpact, data.HitPoint);
            }

            bool isEnemyHit = CheckForEnemyAt(data.HitPoint);
            if (isEnemyHit)
                PlaySpatial(SfxEnemyHurt, data.HitPoint);
            else if (!isPlayerAttack)
                PlaySpatial(SfxPlayerHurt, data.HitPoint);

            // UI click on player attack only
            if (isPlayerAttack)
                PlayUI(SfxUIDamage);
        }

        void HandleEntityKilled(GameObject entity)
        {
            if (entity == null) return;
            PlaySpatial(SfxEnemyDeath, entity.transform.position);
        }

        bool CheckForEnemyAt(Vector3 position)
        {
            int count = Physics.OverlapSphereNonAlloc(position, 0.8f, _hitCheck);
            for (int i = 0; i < count; i++)
            {
                if (_hitCheck[i] != null &&
                    _hitCheck[i].TryGetComponent<Oasis.Enemy.EnemyDamageable>(out _))
                    return true;
            }
            return false;
        }

        void PlaySpatial(AudioClip clip, Vector3 position)
        {
            if (clip == null || SpatialAudioSource == null) return;
            SpatialAudioSource.transform.position = position;
            SpatialAudioSource.pitch = 1f + Random.Range(-PitchVariation, PitchVariation);
            SpatialAudioSource.PlayOneShot(clip);
        }

        void PlayUI(AudioClip clip)
        {
            if (clip == null || UiAudioSource == null) return;
            UiAudioSource.pitch = 1f + Random.Range(-PitchVariation, PitchVariation);
            UiAudioSource.PlayOneShot(clip);
        }
    }
}
