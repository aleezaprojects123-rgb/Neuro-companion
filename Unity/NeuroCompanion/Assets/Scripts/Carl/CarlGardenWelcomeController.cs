// ============================================================
// CarlGardenWelcomeController.cs — Unity 6.3 LTS
// NeuroCompanion — Carl Fredricksen Garden Pathway Welcome
//
// Features:
// 1. Starts from far away down the curved garden pathway.
// 2. Walks smoothly along pathway waypoints with joyful waddling.
// 3. Arrives in front of player, turns, and performs a respectful bow.
// 4. Delivers welcome greeting with dynamic lip sync and mouth movement.
// 5. Settles into a peaceful, breathing idle waiting for the player.
// ============================================================

using UnityEngine;

namespace NeuroCompanion.Carl
{
    public class CarlGardenWelcomeController : MonoBehaviour
    {
        public enum WalkState
        {
            WaitingForPatientMode,
            WaitingForBreeze,
            WalkingIn,
            ArrivingAndTurning,
            Bowing,
            Welcoming,
            IdleWaiting
        }

        [Header("Pathway Waypoints")]
        public Vector3[] pathWaypoints = new Vector3[]
        {
            new Vector3(14.5f, 0.05f, -7.0f),    // Far away on pathway
            new Vector3(11.8f, 0.05f, -12.5f),   // Mid pathway
            new Vector3(7.5f, 0.05f, -17.5f),    // Approaching curve
            new Vector3(5.2f, 0.05f, -23.5f),    // Near curve
            new Vector3(3.6f, 0.05f, -26.2f)     // Final spot facing player
        };

        [Header("Movement Dynamics")]
        public float walkSpeed = 3.2f; // Brisk, joyful approach
        public float stepFrequency = 4.0f;
        public float stepHopHeight = 0.035f;
        public float waddleRollAngle = 3.2f;
        public float initialStartDelay = 0.5f;

        [Header("Audio & Speech")]
        public AudioSource audioSource;
        public AudioClip welcomeClip;

        [Header("Head & Face Rig")]
        public Transform headTransform;
        public SkinnedMeshRenderer faceSkinnedMesh;

        [Header("Lip Sync Settings")]
        [Range(1f, 8f)]
        public float lipAudioSensitivity = 4.5f;
        [Range(40f, 90f)]
        public float maxMouthOpen = 75f;

        [Header("Current State")]
        public WalkState currentState = WalkState.WaitingForPatientMode;

        // Private variables
        private int _currentWaypointIndex = 0;
        private float _stateTimer = 0f;
        private float _walkTimer = 0f;
        private Quaternion _targetFacingRotation;
        private Vector3 _basePosition;
        private float[] _audioSamples = new float[256];
        private float _currentMouthWeight = 0f;

        private void Start()
        {
            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
                if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
            }

            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0.35f;
            audioSource.volume = 1.0f;

            if (welcomeClip != null)
                audioSource.clip = welcomeClip;

            // Find face skinned mesh if not assigned
            if (faceSkinnedMesh == null)
                faceSkinnedMesh = GetComponentInChildren<SkinnedMeshRenderer>();

            // Place Carl at far start position and remain resting
            ResetToResting();
        }

        public void StartEntranceSequence()
        {
            if (pathWaypoints != null && pathWaypoints.Length > 0)
            {
                transform.position = pathWaypoints[0];
                _basePosition = pathWaypoints[0];
                if (pathWaypoints.Length > 1)
                {
                    Vector3 lookFirst = (pathWaypoints[1] - pathWaypoints[0]).normalized;
                    lookFirst.y = 0f;
                    if (lookFirst.sqrMagnitude > 0.001f)
                        transform.rotation = Quaternion.LookRotation(lookFirst);
                }
            }

            _currentWaypointIndex = 0;
            _stateTimer = 0f;
            _walkTimer = 0f;
            currentState = WalkState.WalkingIn;
            Debug.Log("[CarlGardenWelcomeController] Patient Mode active — Carl running in from pathway!");
        }

        public void ResetToResting()
        {
            currentState = WalkState.WaitingForPatientMode;
            _currentWaypointIndex = 0;
            _stateTimer = 0f;
            _walkTimer = 0f;

            if (pathWaypoints != null && pathWaypoints.Length > 0)
            {
                transform.position = pathWaypoints[0];
                _basePosition = pathWaypoints[0];
                if (pathWaypoints.Length > 1)
                {
                    Vector3 lookFirst = (pathWaypoints[1] - pathWaypoints[0]).normalized;
                    lookFirst.y = 0f;
                    if (lookFirst.sqrMagnitude > 0.001f)
                        transform.rotation = Quaternion.LookRotation(lookFirst);
                }
            }

            if (audioSource != null && audioSource.isPlaying)
            {
                audioSource.Stop();
            }

            if (faceSkinnedMesh != null)
            {
                faceSkinnedMesh.SetBlendShapeWeight(0, 0f);  // Close mouth
                faceSkinnedMesh.SetBlendShapeWeight(1, 25f); // Gentle resting smile
            }

            if (headTransform != null)
            {
                headTransform.localRotation = Quaternion.identity;
            }
        }

        private void Update()
        {
            if (currentState == WalkState.WaitingForPatientMode)
                return;

            _stateTimer += Time.deltaTime;

            switch (currentState)
            {
                case WalkState.WaitingForBreeze:
                    UpdateWaitingForBreeze();
                    break;

                case WalkState.WalkingIn:
                    UpdateWalking();
                    break;

                case WalkState.ArrivingAndTurning:
                    UpdateTurning();
                    break;

                case WalkState.Bowing:
                    UpdateBowing();
                    break;

                case WalkState.Welcoming:
                    UpdateWelcoming();
                    break;

                case WalkState.IdleWaiting:
                    UpdateIdle();
                    break;
            }
        }

        private void UpdateWaitingForBreeze()
        {
            // Start walking once breeze begins (or after delay)
            if (_stateTimer >= initialStartDelay)
            {
                currentState = WalkState.WalkingIn;
                _stateTimer = 0f;
                _walkTimer = 0f;
            }
        }

        private void UpdateWalking()
        {
            if (pathWaypoints == null || pathWaypoints.Length == 0) return;

            Vector3 targetWp = pathWaypoints[_currentWaypointIndex];
            Vector3 toTarget = targetWp - _basePosition;
            toTarget.y = 0f;

            float dist = toTarget.magnitude;

            if (dist < 0.25f)
            {
                _currentWaypointIndex++;
                if (_currentWaypointIndex >= pathWaypoints.Length)
                {
                    // Reached final spot in front of player
                    _basePosition = pathWaypoints[pathWaypoints.Length - 1];
                    transform.position = _basePosition;
                    currentState = WalkState.ArrivingAndTurning;
                    _stateTimer = 0f;

                    Vector3 camPos = Camera.main != null ? Camera.main.transform.position : new Vector3(2.6f, 1.32f, -28.5f);
                    Vector3 lookDir = camPos - _basePosition;
                    lookDir.y = 0f;
                    if (lookDir.sqrMagnitude > 0.01f)
                    {
                        _targetFacingRotation = Quaternion.LookRotation(lookDir);
                    }
                    return;
                }
                targetWp = pathWaypoints[_currentWaypointIndex];
                toTarget = targetWp - _basePosition;
                toTarget.y = 0f;
            }

            Vector3 moveDir = toTarget.normalized;
            _basePosition += moveDir * walkSpeed * Time.deltaTime;
            _walkTimer += Time.deltaTime;

            if (moveDir.sqrMagnitude > 0.001f)
            {
                Quaternion moveRot = Quaternion.LookRotation(moveDir);
                transform.rotation = Quaternion.Slerp(transform.rotation, moveRot, Time.deltaTime * 6f);
            }

            // Step hop and gentle waddle
            float stepCycle = _walkTimer * stepFrequency;
            float hop = Mathf.Abs(Mathf.Sin(stepCycle * Mathf.PI)) * stepHopHeight;
            float waddle = Mathf.Sin(stepCycle * Mathf.PI) * waddleRollAngle;

            transform.position = _basePosition + new Vector3(0f, hop, 0f);
            transform.rotation *= Quaternion.Euler(0f, 0f, waddle);
        }

        private void UpdateTurning()
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, _targetFacingRotation, Time.deltaTime * 4.5f);
            transform.position = Vector3.Lerp(transform.position, _basePosition, Time.deltaTime * 6f);

            if (_stateTimer > 0.9f || Quaternion.Angle(transform.rotation, _targetFacingRotation) < 4f)
            {
                transform.rotation = _targetFacingRotation;
                transform.position = _basePosition;
                currentState = WalkState.Bowing;
                _stateTimer = 0f;
            }
        }

        private void UpdateBowing()
        {
            // Respectful, polite bow: ~1.8 seconds total duration
            float bowDuration = 1.8f;
            float t = Mathf.Clamp01(_stateTimer / bowDuration);

            // Smooth bow down (0 to 0.4), hold (0.4 to 0.6), return up (0.6 to 1.0)
            float bowAngle = 0f;
            if (t < 0.45f)
            {
                float easeDown = Mathf.SmoothStep(0f, 1f, t / 0.45f);
                bowAngle = Mathf.Lerp(0f, 18f, easeDown);
            }
            else if (t < 0.65f)
            {
                bowAngle = 18f; // Hold bow respectfully
            }
            else
            {
                float easeUp = Mathf.SmoothStep(0f, 1f, (t - 0.65f) / 0.35f);
                bowAngle = Mathf.Lerp(18f, 0f, easeUp);
            }

            // Apply forward pitch lean for the bow
            transform.rotation = _targetFacingRotation * Quaternion.Euler(bowAngle, 0f, 0f);

            // Head tilts down slightly more for polite nod
            if (headTransform != null)
            {
                headTransform.localRotation = Quaternion.Euler(bowAngle * 0.4f, 0f, 0f);
            }

            if (t >= 1.0f)
            {
                transform.rotation = _targetFacingRotation;
                if (headTransform != null) headTransform.localRotation = Quaternion.identity;
                StartWelcomeSpeech();
            }
        }

        private void StartWelcomeSpeech()
        {
            currentState = WalkState.Welcoming;
            _stateTimer = 0f;

            if (audioSource != null && welcomeClip != null)
            {
                audioSource.clip = welcomeClip;
                audioSource.Play();
            }

            // Show speech bubble for welcome greeting
            var dialogueCtrl = GetComponent<CarlDialogueController>();
            if (dialogueCtrl == null) dialogueCtrl = FindFirstObjectByType<CarlDialogueController>();
            if (dialogueCtrl != null)
            {
                if (dialogueCtrl.speechBubblePanel != null) dialogueCtrl.speechBubblePanel.SetActive(true);
                if (dialogueCtrl.speakerNameText != null) dialogueCtrl.speakerNameText.text = "👴 Carl Fredricksen";
                if (dialogueCtrl.speechBubbleText != null)
                {
                    dialogueCtrl.speechBubbleText.text = "Hello there, my friend! Welcome to our peaceful garden. Take a deep breath of the fresh air with me.";
                }
            }
        }

        private void UpdateWelcoming()
        {
            // 1. Dynamic Audio Lip Sync
            float targetMouth = 0f;
            if (audioSource != null && audioSource.isPlaying)
            {
                audioSource.GetOutputData(_audioSamples, 0);
                float sum = 0f;
                for (int i = 0; i < _audioSamples.Length; i++)
                    sum += _audioSamples[i] * _audioSamples[i];

                float rms = Mathf.Sqrt(sum / _audioSamples.Length);
                float audioLevel = Mathf.Clamp01(rms * lipAudioSensitivity);

                // Add procedural syllable rhythm so speech feels natural
                float syllablePacing = Mathf.Sin(_stateTimer * 14f) * 0.5f + 0.5f;
                targetMouth = Mathf.Clamp01(audioLevel * 0.7f + (audioLevel > 0.05f ? syllablePacing * 0.35f : 0f)) * maxMouthOpen;
            }

            // Smooth lip attack & decay
            float lerpSpeed = targetMouth > _currentMouthWeight ? 28f : 16f;
            _currentMouthWeight = Mathf.Lerp(_currentMouthWeight, targetMouth, Time.deltaTime * lerpSpeed);

            // Apply blendshapes to face
            if (faceSkinnedMesh != null)
            {
                // BlendShape 0: MouthOpen
                faceSkinnedMesh.SetBlendShapeWeight(0, _currentMouthWeight);
                // BlendShape 1: MouthSmile (warm friendly smile during speaking)
                float smile = 35f + Mathf.Sin(_stateTimer * 2f) * 15f;
                faceSkinnedMesh.SetBlendShapeWeight(1, smile);
            }

            // 2. Conversational head gestures while talking
            float nod = Mathf.Sin(_stateTimer * 2.8f) * 3.0f + Mathf.Sin(_stateTimer * 5.0f) * 1.5f;
            float tilt = Mathf.Sin(_stateTimer * 1.6f) * 2.0f;
            float breath = Mathf.Sin(_stateTimer * 1.8f) * 0.015f;

            transform.position = _basePosition + new Vector3(0f, breath, 0f);

            if (headTransform != null)
            {
                headTransform.localRotation = Quaternion.Euler(nod, tilt, 0f);
            }

            // When audio finishes, transition to gentle idle and unlock conversational voice
            if (audioSource != null && !audioSource.isPlaying && _stateTimer > 2.0f)
            {
                currentState = WalkState.IdleWaiting;
                if (faceSkinnedMesh != null)
                {
                    faceSkinnedMesh.SetBlendShapeWeight(0, 0f);  // Close mouth
                    faceSkinnedMesh.SetBlendShapeWeight(1, 35f); // Keep gentle smile
                }

                // Dismiss welcome bubble
                var dialogueCtrl = GetComponent<CarlDialogueController>();
                if (dialogueCtrl == null) dialogueCtrl = FindFirstObjectByType<CarlDialogueController>();
                if (dialogueCtrl != null && dialogueCtrl.speechBubblePanel != null)
                {
                    dialogueCtrl.speechBubblePanel.SetActive(false);
                }

                // UNLOCK hands-free voice dialogue!
                var voiceListener = GetComponent<CarlVoiceListener>();
                if (voiceListener == null) voiceListener = FindFirstObjectByType<CarlVoiceListener>();
                if (voiceListener != null)
                {
                    voiceListener.EnableVoiceListening();
                }
            }
        }

        private void UpdateIdle()
        {
            float t = Time.time;
            float breath = Mathf.Sin(t * 1.4f) * 0.01f;
            float lookYaw = Mathf.Sin(t * 0.7f) * 2.5f;
            float nod = Mathf.Sin(t * 0.9f) * 1.5f;

            transform.position = _basePosition + new Vector3(0f, breath, 0f);

            if (headTransform != null)
            {
                headTransform.localRotation = Quaternion.Euler(nod, lookYaw, 0f);
            }

            if (faceSkinnedMesh != null)
            {
                faceSkinnedMesh.SetBlendShapeWeight(0, 0f);  // Mouth relaxed closed
                faceSkinnedMesh.SetBlendShapeWeight(1, 35f); // Friendly smile
            }
        }

        [ContextMenu("Restart Welcome Sequence")]
        public void RestartSequence()
        {
            Start();
        }
    }
}
