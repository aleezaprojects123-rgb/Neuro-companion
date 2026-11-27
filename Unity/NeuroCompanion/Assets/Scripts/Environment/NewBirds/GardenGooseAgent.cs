// ============================================================
// GardenGooseAgent.cs — Unity 6.3 LTS
// NeuroCompanion — Lifelike Sitting Goose with Gentle Animations
// ============================================================

using UnityEngine;

namespace NeuroCompanion.Environment.NewBirds
{
    public class GardenGooseAgent : MonoBehaviour
    {
        [Header("Animation Setup")]
        public Animator animator;

        [Header("Gentle Idle Behaviors")]
        public float lookInterval = 4.0f;
        public float glanceAngleMax = 25f;

        private float _timer = 0f;
        private Quaternion _baseRotation;
        private Quaternion _targetGlanceRot = Quaternion.identity;

        private void Start()
        {
            if (animator == null)
                animator = GetComponentInChildren<Animator>();

            _baseRotation = transform.rotation;
            _targetGlanceRot = _baseRotation;
            _timer = Random.Range(1f, lookInterval);
        }

        private void Update()
        {
            _timer += Time.deltaTime;

            // Occasional gentle neck look around while sitting
            if (_timer >= lookInterval)
            {
                _timer = 0f;
                float yaw = Random.Range(-glanceAngleMax, glanceAngleMax);
                float pitch = Random.Range(-5f, 8f);
                _targetGlanceRot = _baseRotation * Quaternion.Euler(pitch, yaw, 0f);
            }

            transform.rotation = Quaternion.Slerp(transform.rotation, _targetGlanceRot, Time.deltaTime * 2.5f);

            // Subtle breathing motion for realism
            float breath = Mathf.Sin(Time.time * 1.6f) * 0.004f;
            transform.position = new Vector3(transform.position.x, transform.position.y + breath * Time.deltaTime, transform.position.z);
        }
    }
}
