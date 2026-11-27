// ============================================================
// GardenSparrowAgent.cs — Unity 6.3 LTS
// NeuroCompanion — Lifelike Sparrow Agent with Calming 5s Breaks
// ============================================================

using UnityEngine;

namespace NeuroCompanion.Environment.NewBirds
{
    public class GardenSparrowAgent : MonoBehaviour
    {
        public enum SparrowMode
        {
            SittingOnly,
            FlyWithBreak
        }

        [Header("Behavior Mode")]
        public SparrowMode mode = SparrowMode.SittingOnly;

        [Tooltip("Rest duration between flights so patient eyes don't get stressed.")]
        public float breakDuration = 5.0f;

        [Header("Flight Settings")]
        public float flySpeed = 3.2f;
        public Vector3[] flightWaypoints;
        public float arcHeight = 0.8f;

        [Header("Visual Body Reference")]
        public Transform visualBody;

        // State tracking
        private enum State { Resting, TakingOff, Cruising, Landing }
        private State _state = State.Resting;
        private float _stateTimer = 0f;
        private int _currentWpIndex = 0;
        private Vector3 _startFlightPos;
        private Vector3 _targetFlightPos;
        private float _flightProgress = 0f;
        private float _totalFlightDist = 1f;

        // Head/body glance micro-animation
        private float _nextGlanceTime = 0f;
        private Quaternion _targetGlanceRot = Quaternion.identity;
        private float _peckTimer = 0f;

        private void Start()
        {
            if (visualBody == null && transform.childCount > 0)
                visualBody = transform.GetChild(0);

            _state = State.Resting;
            _stateTimer = 0f;
            _nextGlanceTime = Random.Range(1.2f, 3.5f);

            if (flightWaypoints != null && flightWaypoints.Length > 0)
            {
                transform.position = flightWaypoints[0];
            }
        }

        private void Update()
        {
            _stateTimer += Time.deltaTime;

            if (mode == SparrowMode.SittingOnly)
            {
                UpdateRestingBehaviors();
            }
            else
            {
                switch (_state)
                {
                    case State.Resting:
                        UpdateRestingBehaviors();
                        if (_stateTimer >= breakDuration)
                        {
                            StartFlightToNextWaypoint();
                        }
                        break;

                    case State.Cruising:
                        UpdateCruising();
                        break;
                }
            }
        }

        private void UpdateRestingBehaviors()
        {
            // 1. Subtle, calming breathing / body micro-bob
            float breath = Mathf.Sin(Time.time * 2.5f) * 0.003f;
            if (visualBody != null)
            {
                visualBody.localPosition = new Vector3(0f, breath, 0f);
            }

            // 2. Realistic bird saccadic head glances
            if (_stateTimer >= _nextGlanceTime)
            {
                _nextGlanceTime = _stateTimer + Random.Range(1.5f, 4.0f);
                float glanceYaw = Random.Range(-35f, 35f);
                float glancePitch = Random.Range(-12f, 10f);
                _targetGlanceRot = Quaternion.Euler(glancePitch, glanceYaw, 0f);

                // Chance to peck at ground/perch
                if (Random.value < 0.35f)
                {
                    _peckTimer = 0.45f;
                }
            }

            // Quick saccade interpolation
            if (_peckTimer > 0f)
            {
                _peckTimer -= Time.deltaTime;
                Quaternion peckRot = Quaternion.Euler(28f, _targetGlanceRot.eulerAngles.y, 0f);
                if (visualBody != null)
                    visualBody.localRotation = Quaternion.Slerp(visualBody.localRotation, peckRot, Time.deltaTime * 18f);
            }
            else
            {
                if (visualBody != null)
                    visualBody.localRotation = Quaternion.Slerp(visualBody.localRotation, _targetGlanceRot, Time.deltaTime * 10f);
            }
        }

        private void StartFlightToNextWaypoint()
        {
            if (flightWaypoints == null || flightWaypoints.Length < 2) return;

            _currentWpIndex = (_currentWpIndex + 1) % flightWaypoints.Length;
            _startFlightPos = transform.position;
            _targetFlightPos = flightWaypoints[_currentWpIndex];
            _totalFlightDist = Vector3.Distance(_startFlightPos, _targetFlightPos);

            _flightProgress = 0f;
            _state = State.Cruising;
            _stateTimer = 0f;
        }

        private void UpdateCruising()
        {
            float speedRatio = flySpeed / Mathf.Max(0.5f, _totalFlightDist);
            _flightProgress += Time.deltaTime * speedRatio;

            float t = Mathf.Clamp01(_flightProgress);
            // Smooth ease in and out
            float easeT = Mathf.SmoothStep(0f, 1f, t);

            // Arced parabolic flight curve
            Vector3 directPos = Vector3.Lerp(_startFlightPos, _targetFlightPos, easeT);
            float arc = Mathf.Sin(t * Mathf.PI) * arcHeight;
            Vector3 currentPos = directPos + Vector3.up * arc;
            transform.position = currentPos;

            // Face flight direction
            Vector3 dir = (_targetFlightPos - _startFlightPos).normalized;
            if (dir.sqrMagnitude > 0.001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(dir);
                // Subtle bank into flight
                float bank = Mathf.Sin(t * Mathf.PI * 2f) * 4f;
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot * Quaternion.Euler(0f, 0f, bank), Time.deltaTime * 8f);
            }

            // Gentle wing flap body oscillation
            float flapBob = Mathf.Sin(Time.time * 28f) * 0.012f;
            if (visualBody != null)
            {
                visualBody.localPosition = new Vector3(0f, flapBob, 0f);
            }

            if (t >= 1.0f)
            {
                transform.position = _targetFlightPos;
                _state = State.Resting;
                _stateTimer = 0f;
                if (visualBody != null)
                {
                    visualBody.localPosition = Vector3.zero;
                    visualBody.localRotation = Quaternion.identity;
                }
            }
        }
    }
}
