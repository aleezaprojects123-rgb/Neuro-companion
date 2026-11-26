using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace NeuroCompanion.Environment.Birds
{
    public class BirdBehaviourController : MonoBehaviour
    {
        [Header("Flock Settings")]
        public List<BirdAgent> allBirds = new List<BirdAgent>();
        public int maxSimultaneousFliers = 2;
        public float minFlightInterval = 25.0f;
        public float maxFlightInterval = 60.0f;

        [Header("References")]
        public BirdAudioController audioController;

        private float flightTimer = 0f;
        private float nextFlightInterval = 30.0f;

        void Start()
        {
            if (allBirds == null || allBirds.Count == 0)
            {
                allBirds = new List<BirdAgent>(GetComponentsInChildren<BirdAgent>(true));
            }

            if (audioController == null)
            {
                audioController = GetComponent<BirdAudioController>();
                if (audioController == null && transform.parent != null)
                {
                    audioController = transform.parent.GetComponentInChildren<BirdAudioController>();
                }
            }

            if (audioController != null)
            {
                foreach (var bird in allBirds)
                {
                    audioController.RegisterBird(bird);
                }
            }

            nextFlightInterval = Random.Range(minFlightInterval, maxFlightInterval);
        }

        void Update()
        {
            if (allBirds == null || allBirds.Count == 0) return;
            if (Time.timeScale <= 0f) return;

            flightTimer += Time.deltaTime;
            if (flightTimer >= nextFlightInterval)
            {
                flightTimer = 0f;
                nextFlightInterval = Random.Range(minFlightInterval, maxFlightInterval);
                TryTriggerFlight();
            }
        }

        private void TryTriggerFlight()
        {
            // Count current fliers
            int activeFliers = 0;
            List<BirdAgent> readyCandidates = new List<BirdAgent>();

            foreach (var b in allBirds)
            {
                if (b == null) continue;
                if (b.IsFlying) activeFliers++;
                else if (!b.isGroundBird && b.alternatePerch != null)
                {
                    readyCandidates.Add(b);
                }
            }

            if (activeFliers < maxSimultaneousFliers && readyCandidates.Count > 0)
            {
                var candidate = readyCandidates[Random.Range(0, readyCandidates.Count)];
                Transform dest = (Vector3.Distance(candidate.transform.position, candidate.homePerch.position) < 0.2f) ?
                                  candidate.alternatePerch : candidate.homePerch;
                StartCoroutine(candidate.FlyToPerch(dest));
            }
        }
    }
}
