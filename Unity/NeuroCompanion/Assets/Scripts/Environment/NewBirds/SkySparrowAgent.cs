// ============================================================
// SkySparrowAgent.cs — Unity 6.3 LTS
// NeuroCompanion — Sparrows with Realistic Wing Flapping & 5-Minute Rest Intervals
//
// Addresses dementia patient comfort:
// - Sparrows sit peacefully for ~5 minutes between flights so eyes do NOT get tired or strained.
// - During flight, wings flap actively with authentic avian kinematics (8-10 Hz burst & glide).
// - When perched, wings fold seamlessly and the bird performs gentle resting micro-animations.
// ============================================================

using UnityEngine;

namespace NeuroCompanion.Environment.NewBirds
{
    public class SkySparrowAgent : MonoBehaviour
    {
        public enum SkyState
        {
            RestingOnPerch,   // Default: peaceful 5-minute sitting break
            TakingOffToSky,   // Ascending with rapid wing flaps (~2-3 sec)
            SoaringInSky,     // Smooth arced flight across the sky with flapping & glides (~15-20 sec)
            LandingOnPerch    // Descending gently back to perch with braking flaps (~3-4 sec)
        }

        [Header("Flight State & Timing")]
        public SkyState currentState = SkyState.RestingOnPerch;
        [Tooltip("Rest duration on perch between flights in seconds. Default: 300s (5 minutes).")]
        public float restBreakDuration = 300.0f; // 5 minutes between flights
        [Tooltip("Staggered initial delay before the very first flight, in seconds.")]
        public float initialPerchDelay = 60.0f;
        public float flySpeed = 3.6f;
        public float bankIntensity = 22.0f;

        [Header("Wing Flapping Kinematics")]
        public Transform leftWing;
        public Transform rightWing;
        public float flapFrequency = 9.0f;   // 9 Hz sparrow wing flap rate
        public float maxFlapAngle = 40.0f;   // 40 degree stroke amplitude
        public float wingSweepAngle = 10.0f; // forward/backward sweep

        [Header("Perch & Flight Waypoints")]
        public Vector3 perchPosition;
        public Quaternion perchRotation;
        public Vector3[] skyFlightWaypoints;

        [Header("Visual References")]
        public Transform modelTransform;

        // Internal timing and state tracking
        private float _stateTimer = 0f;
        private float _wingCycle = 0f;
        private float _glideTimer = 0f;
        private bool _isGliding = false;
        private int _currentSkyWpIndex = 0;
        private Vector3 _flightTarget;
        private int _lapsCompleted = 0;
        private float _nextHeadGlanceTime = 3.0f;
        private float _headGlanceTimer = 0f;
        private Quaternion _currentHeadRot = Quaternion.identity;
        private Quaternion _targetHeadRot = Quaternion.identity;

        private void Start()
        {
            if (modelTransform == null && transform.childCount > 0)
                modelTransform = transform.GetChild(0);

            // Locate left and right wings if already children
            if (leftWing == null)
            {
                var lw = transform.Find("Wing_Left");
                if (lw != null) leftWing = lw;
            }
            if (rightWing == null)
            {
                var rw = transform.Find("Wing_Right");
                if (rw != null) rightWing = rw;
            }

            // Start perched peacefully by default
            currentState = SkyState.RestingOnPerch;
            _stateTimer = Mathf.Max(0f, restBreakDuration - initialPerchDelay); // Stagger initial flight
            transform.position = perchPosition;
            transform.rotation = perchRotation;

            // Fold wings while resting
            SetWingsActive(false);

            if (skyFlightWaypoints != null && skyFlightWaypoints.Length > 0)
            {
                _flightTarget = skyFlightWaypoints[0];
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            _stateTimer += dt;

            switch (currentState)
            {
                case SkyState.RestingOnPerch:
                    UpdateResting(dt);
                    break;

                case SkyState.TakingOffToSky:
                    UpdateTakeoff(dt);
                    break;

                case SkyState.SoaringInSky:
                    UpdateSoaring(dt);
                    break;

                case SkyState.LandingOnPerch:
                    UpdateLanding(dt);
                    break;
            }
        }

        // ============================================================
        // 1. RESTING ON PERCH (approx. 5 minutes duration)
        // ============================================================
        private void UpdateResting(float dt)
        {
            transform.position = perchPosition;
            transform.rotation = perchRotation;

            // Ensure wings are folded and hidden against body
            SetWingsActive(false);

            // Natural micro-animations: subtle breathing bob & occasional curious head glances
            float breath = Mathf.Sin(Time.time * 2.2f) * 0.002f;

            _headGlanceTimer += dt;
            if (_headGlanceTimer >= _nextHeadGlanceTime)
            {
                _headGlanceTimer = 0f;
                _nextHeadGlanceTime = Random.Range(3.5f, 9.0f);
                float randomYaw = Random.Range(-28f, 28f);
                float randomPitch = Random.Range(-8f, 12f);
                _targetHeadRot = Quaternion.Euler(randomPitch, randomYaw, 0f);
            }

            _currentHeadRot = Quaternion.Slerp(_currentHeadRot, _targetHeadRot, dt * 5.0f);

            if (modelTransform != null)
            {
                modelTransform.localPosition = new Vector3(0f, breath, 0f);
                modelTransform.localRotation = _currentHeadRot;
            }

            // After approx. 5 minutes, take off for a relaxing sky flight
            if (_stateTimer >= restBreakDuration)
            {
                TriggerFlightNow();
            }
        }

        // ============================================================
        // 2. TAKING OFF TO SKY (~2-3 sec)
        // ============================================================
        private void UpdateTakeoff(float dt)
        {
            SetWingsActive(true);

            Vector3 targetSkyPos = (skyFlightWaypoints != null && skyFlightWaypoints.Length > 0)
                ? skyFlightWaypoints[0]
                : perchPosition + Vector3.up * 5.0f + transform.forward * 4.0f;

            Vector3 toSky = (targetSkyPos - transform.position).normalized;

            transform.position += toSky * (flySpeed * 0.9f) * dt;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(toSky), dt * 4.5f);

            // Rapid, energetic takeoff wing flaps (10 Hz)
            AnimateWings(dt, freqMultiplier: 1.15f, strokeMultiplier: 1.1f, allowGlide: false);

            if (Vector3.Distance(transform.position, targetSkyPos) < 1.8f || _stateTimer > 3.5f)
            {
                currentState = SkyState.SoaringInSky;
                _stateTimer = 0f;
                _currentSkyWpIndex = 0;
            }
        }

        // ============================================================
        // 3. SOARING IN SKY (Full flight circuit with active flaps & glides)
        // ============================================================
        private void UpdateSoaring(float dt)
        {
            SetWingsActive(true);

            if (skyFlightWaypoints == null || skyFlightWaypoints.Length == 0) return;

            Vector3 toTarget = _flightTarget - transform.position;
            float dist = toTarget.magnitude;

            if (dist < 1.2f)
            {
                _currentSkyWpIndex = (_currentSkyWpIndex + 1) % skyFlightWaypoints.Length;
                _flightTarget = skyFlightWaypoints[_currentSkyWpIndex];

                // Once one complete circuit is completed, begin gentle descent to perch
                if (_currentSkyWpIndex == 0)
                {
                    _lapsCompleted++;
                    if (_lapsCompleted >= 1)
                    {
                        _lapsCompleted = 0;
                        currentState = SkyState.LandingOnPerch;
                        _stateTimer = 0f;
                        return;
                    }
                }
            }

            // Move forward
            Vector3 moveDir = (_flightTarget - transform.position).normalized;
            transform.position += moveDir * flySpeed * dt;

            // Bank naturally into turns
            if (moveDir.sqrMagnitude > 0.001f)
            {
                Quaternion targetLook = Quaternion.LookRotation(moveDir);
                float turnAngle = Vector3.SignedAngle(transform.forward, moveDir, Vector3.up);
                float bank = Mathf.Clamp(-turnAngle * bankIntensity, -38f, 38f);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetLook * Quaternion.Euler(0f, 0f, bank), dt * 3.8f);
            }

            // Animate flapping wings with authentic burst-and-glide sparrow pattern
            AnimateWings(dt, freqMultiplier: 1.0f, strokeMultiplier: 1.0f, allowGlide: true);
        }

        // ============================================================
        // 4. LANDING ON PERCH (~3-4 sec braking flight)
        // ============================================================
        private void UpdateLanding(float dt)
        {
            SetWingsActive(true);

            Vector3 toPerch = perchPosition - transform.position;
            float dist = toPerch.magnitude;

            transform.position = Vector3.MoveTowards(transform.position, perchPosition, flySpeed * 0.85f * dt);

            if (toPerch.sqrMagnitude > 0.01f)
            {
                Quaternion look = Quaternion.LookRotation(toPerch.normalized);
                transform.rotation = Quaternion.Slerp(transform.rotation, look, dt * 4.0f);
            }

            // Braking flaps (slower, wider strokes)
            AnimateWings(dt, freqMultiplier: 0.7f, strokeMultiplier: 1.2f, allowGlide: false);

            if (dist < 0.25f || _stateTimer > 4.5f)
            {
                transform.position = perchPosition;
                transform.rotation = perchRotation;
                currentState = SkyState.RestingOnPerch;
                _stateTimer = 0f;
                SetWingsActive(false);
                if (modelTransform != null)
                {
                    modelTransform.localPosition = Vector3.zero;
                    modelTransform.localRotation = Quaternion.identity;
                }
            }
        }

        // ============================================================
        // WING FLAPPING & GLIDE CONTROLLER
        // ============================================================
        private void AnimateWings(float dt, float freqMultiplier, float strokeMultiplier, bool allowGlide)
        {
            if (allowGlide)
            {
                _glideTimer += dt;
                // Burst-and-glide: 2.2 seconds flapping, then 0.8 seconds graceful glide
                if (!_isGliding && _glideTimer > 2.2f)
                {
                    _isGliding = true;
                    _glideTimer = 0f;
                }
                else if (_isGliding && _glideTimer > 0.8f)
                {
                    _isGliding = false;
                    _glideTimer = 0f;
                }
            }
            else
            {
                _isGliding = false;
            }

            if (_isGliding)
            {
                // Wings held flat/slight dihedral angle in soothing glide
                float glideDihedral = 12.0f;
                if (leftWing != null) leftWing.localRotation = Quaternion.Slerp(leftWing.localRotation, Quaternion.Euler(0f, 15f, glideDihedral), dt * 10f);
                if (rightWing != null) rightWing.localRotation = Quaternion.Slerp(rightWing.localRotation, Quaternion.Euler(0f, -15f, -glideDihedral), dt * 10f);
                if (modelTransform != null) modelTransform.localPosition = Vector3.zero;
                return;
            }

            _wingCycle += dt * flapFrequency * freqMultiplier;

            // Primary flap stroke: up and down stroke
            float strokeSin = Mathf.Sin(_wingCycle * Mathf.PI * 2f);
            float strokeCos = Mathf.Cos(_wingCycle * Mathf.PI * 2f);

            float flapAngle = strokeSin * (maxFlapAngle * strokeMultiplier);
            float sweepAngle = strokeCos * (wingSweepAngle * strokeMultiplier);
            float twistAngle = strokeSin * 8.0f; // Aerodynamic pitch flex

            if (leftWing != null)
            {
                leftWing.localRotation = Quaternion.Euler(sweepAngle, 18f + twistAngle, flapAngle);
            }
            if (rightWing != null)
            {
                rightWing.localRotation = Quaternion.Euler(sweepAngle, -18f - twistAngle, -flapAngle);
            }

            // Subtle whole-body lift bobbing synchronized with downstroke
            if (modelTransform != null)
            {
                float bodyLift = -strokeSin * 0.015f;
                modelTransform.localPosition = new Vector3(0f, bodyLift, 0f);
            }
        }

        private void SetWingsActive(bool active)
        {
            if (leftWing != null && leftWing.gameObject.activeSelf != active)
                leftWing.gameObject.SetActive(active);

            if (rightWing != null && rightWing.gameObject.activeSelf != active)
                rightWing.gameObject.SetActive(active);
        }

        // ============================================================
        // PUBLIC / CONTEXT MENU TRIGGERS
        // ============================================================
        [ContextMenu("Fly Now (Debug Trigger)")]
        public void TriggerFlightNow()
        {
            currentState = SkyState.TakingOffToSky;
            _stateTimer = 0f;
            _currentSkyWpIndex = 0;
            if (skyFlightWaypoints != null && skyFlightWaypoints.Length > 0)
                _flightTarget = skyFlightWaypoints[0];

            SetWingsActive(true);
            Debug.Log($"[{gameObject.name}] Taking off for a soothing sky flight with flapping wings!");
        }

        [ContextMenu("Perch and Rest Now")]
        public void TriggerPerchNow()
        {
            currentState = SkyState.RestingOnPerch;
            _stateTimer = 0f;
            transform.position = perchPosition;
            transform.rotation = perchRotation;
            SetWingsActive(false);
            if (modelTransform != null)
            {
                modelTransform.localPosition = Vector3.zero;
                modelTransform.localRotation = Quaternion.identity;
            }
            Debug.Log($"[{gameObject.name}] Sitting peacefully on perch for ~5 minutes.");
        }
    }
}
