using UnityEngine;
using System.Collections;

namespace NeuroCompanion.Environment.Birds
{
    [RequireComponent(typeof(BirdAnimator))]
    public class BirdAgent : MonoBehaviour
    {
        [Header("Species Configuration")]
        public BirdSpecies species;
        public string birdId = "Bird_01";
        public float flightSpeed = 2.4f;
        public bool isGroundBird = false;

        [Header("Audio")]
        public AudioSource audioSource;
        public AudioClip chirpClip;

        [Header("Perch / Navigation")]
        public Transform homePerch;
        public Transform alternatePerch;

        private BirdAnimator animator;
        private BirdState currentState = BirdState.Perched;
        private Coroutine activeBehaviorRoutine;

        // Walking State
        private Vector3 groundOrigin;
        private Vector3 currentGroundTarget;
        private bool isWalking = false;

        public BirdState State => currentState;
        public bool IsFlying => currentState == BirdState.TakingOff || currentState == BirdState.Flying || currentState == BirdState.Landing;

        void Awake()
        {
            animator = GetComponent<BirdAnimator>();
            if (audioSource == null) audioSource = GetComponent<AudioSource>();
            if (audioSource != null)
            {
                audioSource.spatialBlend = 1.0f; // 100% 3D spatial audio
                audioSource.rolloffMode = AudioRolloffMode.Logarithmic;
                audioSource.minDistance = 1.5f;
                audioSource.maxDistance = 25.0f;
                audioSource.playOnAwake = false;
            }
        }

        void Start()
        {
            groundOrigin = transform.position;
            if (isGroundBird)
            {
                currentState = BirdState.IdleResting;
                activeBehaviorRoutine = StartCoroutine(GroundRoutine());
            }
            else
            {
                currentState = BirdState.Perched;
                activeBehaviorRoutine = StartCoroutine(PerchRoutine());
            }
            animator.SetState(currentState);
        }

        // =====================================================================
        // AUDIO PLAYBACK (Called by BirdAudioController)
        // =====================================================================
        public void PlayChirp(float volume = 0.8f, float pitchVariation = 0.05f)
        {
            if (audioSource == null || chirpClip == null) return;
            if (audioSource.isPlaying) return;

            audioSource.pitch = 1.0f + Random.Range(-pitchVariation, pitchVariation);
            audioSource.volume = volume;
            audioSource.clip = chirpClip;
            audioSource.Play();
        }

        public void StopChirp()
        {
            if (audioSource != null && audioSource.isPlaying)
            {
                audioSource.Stop();
            }
        }

        // =====================================================================
        // AUTONOMOUS ROUTINES
        // =====================================================================
        private IEnumerator PerchRoutine()
        {
            while (true)
            {
                // Rest peacefully for 35 to 80 seconds
                float restTime = Random.Range(35.0f, 80.0f);
                yield return new WaitForSeconds(restTime);

                // If alternate perch is assigned, occasional short flight
                if (alternatePerch != null && !IsFlying)
                {
                    Transform dest = (Vector3.Distance(transform.position, homePerch.position) < 0.2f) ? alternatePerch : homePerch;
                    yield return StartCoroutine(FlyToPerch(dest));
                }
            }
        }

        private IEnumerator GroundRoutine()
        {
            while (true)
            {
                // Rest / search on ground for 15 to 30 seconds
                float pauseTime = Random.Range(15.0f, 30.0f);
                yield return new WaitForSeconds(pauseTime);

                // Pick a gentle target within 0.4m to 0.9m radius on lawn
                Vector2 r = Random.insideUnitCircle * 0.8f;
                Vector3 target = groundOrigin + new Vector3(r.x, 0f, r.y);
                yield return StartCoroutine(WalkToPosition(target));
            }
        }

        private IEnumerator WalkToPosition(Vector3 target)
        {
            currentState = BirdState.Walking;
            animator.SetState(currentState);

            Vector3 start = transform.position;
            float dist = Vector3.Distance(start, target);
            if (dist < 0.05f)
            {
                currentState = BirdState.IdleResting;
                animator.SetState(currentState);
                yield break;
            }

            float duration = dist / 0.35f; // Gentle, slow walking speed (0.35 m/s)
            float elapsed = 0f;

            // Turn towards target
            Vector3 dir = (target - start).normalized;
            if (dir.sqrMagnitude > 0.001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(dir, Vector3.up);
                float turnElapsed = 0f;
                Quaternion startRot = transform.rotation;
                while (turnElapsed < 0.3f)
                {
                    turnElapsed += Time.deltaTime;
                    transform.rotation = Quaternion.Slerp(startRot, targetRot, turnElapsed / 0.3f);
                    yield return null;
                }
            }

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                transform.position = Vector3.Lerp(start, target, t);
                yield return null;
            }

            transform.position = target;
            currentState = BirdState.IdleResting;
            animator.SetState(currentState);
        }

        // =====================================================================
        // FLIGHT PATH & LANDING
        // =====================================================================
        public IEnumerator FlyToPerch(Transform targetPerch)
        {
            if (targetPerch == null) yield break;

            Vector3 pStart = transform.position;
            Vector3 pEnd = targetPerch.position;
            Quaternion rotEnd = targetPerch.rotation;

            float distance = Vector3.Distance(pStart, pEnd);
            if (distance < 0.1f) yield break;

            // 1. Take-Off phase (0.45s)
            currentState = BirdState.TakingOff;
            animator.SetState(currentState);

            Vector3 flightDir = (pEnd - pStart).normalized;
            Vector3 takeOffTarget = pStart + Vector3.up * 0.35f + flightDir * 0.25f;

            float tTakeOff = 0f;
            while (tTakeOff < 0.45f)
            {
                tTakeOff += Time.deltaTime;
                float normT = tTakeOff / 0.45f;
                transform.position = Vector3.Lerp(pStart, takeOffTarget, normT * normT);
                if (flightDir.sqrMagnitude > 0.001f)
                {
                    Quaternion look = Quaternion.LookRotation(flightDir + Vector3.up * 0.2f, Vector3.up);
                    transform.rotation = Quaternion.Slerp(transform.rotation, look, Time.deltaTime * 6f);
                }
                yield return null;
            }

            // 2. Flying Phase (Curved Arc flight)
            currentState = BirdState.Flying;
            animator.SetState(currentState);

            float totalFlightTime = Mathf.Max(1.4f, distance / flightSpeed);
            float flightElapsed = 0f;

            // Apex elevation for natural gentle parabola
            float apexElevation = Mathf.Clamp(distance * 0.22f, 0.4f, 1.2f);
            Vector3 pMid = (pStart + pEnd) * 0.5f + Vector3.up * apexElevation;

            Vector3 prevPos = transform.position;

            while (flightElapsed < totalFlightTime)
            {
                flightElapsed += Time.deltaTime;
                float u = Mathf.Clamp01(flightElapsed / totalFlightTime);

                // Quadratic Bezier curve
                Vector3 currentPos = (1f - u) * (1f - u) * takeOffTarget +
                                     2f * (1f - u) * u * pMid +
                                     u * u * (pEnd + Vector3.up * 0.15f);

                transform.position = currentPos;

                Vector3 travelDir = (currentPos - prevPos).normalized;
                if (travelDir.sqrMagnitude > 0.0001f)
                {
                    // Gentle banking during curve
                    Quaternion forwardLook = Quaternion.LookRotation(travelDir, Vector3.up);
                    transform.rotation = Quaternion.Slerp(transform.rotation, forwardLook, Time.deltaTime * 8f);
                }
                prevPos = currentPos;

                // Enter landing state when approaching destination (last 25% of flight)
                if (u > 0.75f && currentState != BirdState.Landing)
                {
                    currentState = BirdState.Landing;
                    animator.SetState(currentState);
                }

                yield return null;
            }

            // 3. Settle on Perch
            float settleElapsed = 0f;
            Vector3 preSettlePos = transform.position;
            Quaternion preSettleRot = transform.rotation;
            while (settleElapsed < 0.35f)
            {
                settleElapsed += Time.deltaTime;
                float s = settleElapsed / 0.35f;
                transform.position = Vector3.Lerp(preSettlePos, pEnd, s);
                transform.rotation = Quaternion.Slerp(preSettleRot, rotEnd, s);
                yield return null;
            }

            transform.position = pEnd;
            transform.rotation = rotEnd;

            currentState = BirdState.Perched;
            animator.SetState(currentState);
        }
    }
}
