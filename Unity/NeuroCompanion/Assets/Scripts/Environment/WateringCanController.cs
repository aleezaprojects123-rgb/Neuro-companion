using UnityEngine;

namespace NeuroCompanion.Environment
{
    /// <summary>
    /// Isolated controller for the gardening watering can pouring effect.
    /// Manages explicit activation/deactivation of the water particle stream.
    ///
    /// GUARANTEE: Water is strictly OFF by default and will NEVER pour automatically
    /// on scene start, proximity triggers, or idle holding states.
    /// Water pours ONLY when explicitly triggered by future gardening interactions.
    /// </summary>
    [DisallowMultipleComponent]
    public class WateringCanController : MonoBehaviour
    {
        [Header("Water Effect References")]
        [Tooltip("The ParticleSystem attached to the sprinkler rose point.")]
        [SerializeField] private ParticleSystem _pourEffect;

        [Tooltip("Optional soft water trickle audio source.")]
        [SerializeField] private AudioSource _waterAudioSource;

        [Header("State")]
        [SerializeField] private bool _isWatering = false;

        /// <summary>
        /// Whether water is currently pouring from the watering can.
        /// </summary>
        public bool IsWatering => _isWatering;

        private void Awake()
        {
            // Enforce default state: water is completely OFF on start
            EnforceWaterState(false);
        }

        private void Start()
        {
            // Extra safety guard: ensure no emission on start
            if (!_isWatering)
            {
                EnforceWaterState(false);
            }
        }

        /// <summary>
        /// Explicitly begins pouring water from the sprinkler rose.
        /// To be called when Carl tilts the can over a plant during the watering activity.
        /// </summary>
        [ContextMenu("Start Watering")]
        public void StartWatering()
        {
            SetWatering(true);
        }

        /// <summary>
        /// Explicitly stops pouring water immediately.
        /// To be called when watering ends or Carl returns the can to upright position.
        /// </summary>
        [ContextMenu("Stop Watering")]
        public void StopWatering()
        {
            SetWatering(false);
        }

        /// <summary>
        /// Sets the explicit watering state.
        /// </summary>
        /// <param name="active">True to pour water; false to immediately stop emission.</param>
        public void SetWatering(bool active)
        {
            if (_isWatering == active) return;

            _isWatering = active;
            EnforceWaterState(active);

            Debug.Log($"[WateringCanController] Water state changed: {(_isWatering ? "POURING" : "STOPPED")}");
        }

        private void EnforceWaterState(bool active)
        {
            if (_pourEffect != null)
            {
                if (active)
                {
                    if (!_pourEffect.isPlaying)
                        _pourEffect.Play(true);
                }
                else
                {
                    if (_pourEffect.isPlaying)
                        _pourEffect.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                    else
                    {
                        var emission = _pourEffect.emission;
                        emission.enabled = false;
                    }
                }
            }

            if (_waterAudioSource != null)
            {
                if (active)
                {
                    if (!_waterAudioSource.isPlaying)
                        _waterAudioSource.Play();
                }
                else
                {
                    if (_waterAudioSource.isPlaying)
                        _waterAudioSource.Stop();
                }
            }
        }

        private void OnDisable()
        {
            // Safety: stop emission immediately whenever object is disabled
            EnforceWaterState(false);
            _isWatering = false;
        }

        /// <summary>
        /// Configures the particle system reference programmatically (used by builder).
        /// </summary>
        public void InitializeReferences(ParticleSystem pourEffect, AudioSource audioSource = null)
        {
            _pourEffect = pourEffect;
            _waterAudioSource = audioSource;
            EnforceWaterState(false);
        }
    }
}
