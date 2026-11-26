using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace NeuroCompanion.Environment.Birds
{
    public class BirdAudioController : MonoBehaviour
    {
        [Header("Master Controls")]
        [Range(0f, 1f)] public float masterVolume = 0.75f;
        public bool isAudioEnabled = true;
        public bool isScheduleEnabled = true;

        [Header("Schedule Settings (Production default: 300s / 5 minutes)")]
        public float scheduleInterval = 300.0f;
        public float sessionDuration = 22.0f;

        [Header("Test / Verification Config")]
        public bool isTestMode = false;
        public float testInterval = 10.0f;

        [Header("Status (Read Only)")]
        [SerializeField] private float timer = 0f;
        [SerializeField] private bool isSessionActive = false;
        [SerializeField] private int completedSessions = 0;

        private List<BirdAgent> registeredBirds = new List<BirdAgent>();
        private Coroutine activeSessionRoutine;

        public float ElapsedTimer => timer;
        public bool IsSessionActive => isSessionActive;
        public int CompletedSessions => completedSessions;

        void Awake()
        {
            timer = 0f;
            isSessionActive = false;
        }

        public void RegisterBird(BirdAgent bird)
        {
            if (bird != null && !registeredBirds.Contains(bird))
            {
                registeredBirds.Add(bird);
            }
        }

        public void UnregisterBird(BirdAgent bird)
        {
            if (bird != null && registeredBirds.Contains(bird))
            {
                registeredBirds.Remove(bird);
            }
        }

        void Update()
        {
            if (!isScheduleEnabled || !isAudioEnabled) return;
            if (Time.timeScale <= 0f) return;

            float effectiveInterval = isTestMode ? testInterval : scheduleInterval;

            if (!isSessionActive)
            {
                timer += Time.deltaTime;
                if (timer >= effectiveInterval)
                {
                    StartChirpingSession();
                }
            }
        }

        public void StartChirpingSession()
        {
            if (isSessionActive) return;
            if (activeSessionRoutine != null) StopCoroutine(activeSessionRoutine);
            activeSessionRoutine = StartCoroutine(ChirpingSessionRoutine());
        }

        public void StopChirpingSession()
        {
            if (activeSessionRoutine != null)
            {
                StopCoroutine(activeSessionRoutine);
                activeSessionRoutine = null;
            }

            foreach (var b in registeredBirds)
            {
                if (b != null) b.StopChirp();
            }

            isSessionActive = false;
            timer = 0f;
        }

        private IEnumerator ChirpingSessionRoutine()
        {
            isSessionActive = true;
            timer = 0f;
            Debug.Log($"[BirdAudioController] Starting 5-minute scheduled chirping session (Duration: {sessionDuration:F1}s, Registered Birds: {registeredBirds.Count})");

            float sessionElapsed = 0f;

            while (sessionElapsed < sessionDuration)
            {
                if (!isAudioEnabled) break;

                // Pick 1 to 3 random birds to call in a staggered manner
                if (registeredBirds.Count > 0)
                {
                    int birdsToCall = Random.Range(1, Mathf.Min(4, registeredBirds.Count + 1));
                    for (int i = 0; i < birdsToCall; i++)
                    {
                        var bird = registeredBirds[Random.Range(0, registeredBirds.Count)];
                        if (bird != null)
                        {
                            bird.PlayChirp(masterVolume * Random.Range(0.85f, 1.0f));
                        }
                    }
                }

                // Natural pause between calls (1.2 to 3.5 seconds)
                float pause = Random.Range(1.4f, 3.2f);
                sessionElapsed += pause;
                yield return new WaitForSeconds(pause);
            }

            isSessionActive = false;
            completedSessions++;
            timer = 0f;
            Debug.Log($"[BirdAudioController] Chirping session completed. Total sessions: {completedSessions}. Returning to peaceful quiet garden ambience until next interval.");
        }

        void OnDisable()
        {
            StopChirpingSession();
        }

        void OnApplicationQuit()
        {
            StopChirpingSession();
        }
    }
}
