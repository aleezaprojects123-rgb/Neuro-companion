using UnityEngine;

namespace NeuroCompanion.Environment.Birds
{
    public class BirdAnimator : MonoBehaviour
    {
        [Header("Skeleton References")]
        public Transform body;
        public Transform head;
        public Transform leftEye;
        public Transform rightEye;
        public Transform leftWing;
        public Transform rightWing;
        public Transform tail;
        public Transform leftLeg;
        public Transform rightLeg;

        [Header("Species Config")]
        public BirdSpecies species;
        public float flapFrequency = 7.0f;
        public float maxFlapAngle = 42.0f;

        // Internal State
        private BirdState currentState = BirdState.IdleResting;
        private Vector3 initialHeadLocalRot;
        private Vector3 targetHeadLocalRot;
        private float headTurnTimer;
        private float headTurnDuration = 1.0f;
        private float nextHeadTurnTime = 3.0f;

        private float blinkTimer;
        private float nextBlinkTime = 4.0f;
        private bool isBlinking = false;
        private float blinkDuration = 0.12f;

        private float ruffleTimer;
        private float nextRuffleTime = 18.0f;
        private bool isRuffling = false;
        private float ruffleDuration = 0.45f;

        private float walkCycle;
        private float flightCycle;
        private float breathingPhase;

        private Vector3 initialBodyLocalPos;
        private Quaternion initialLeftWingRot;
        private Quaternion initialRightWingRot;
        private Quaternion initialLeftLegRot;
        private Quaternion initialRightLegRot;
        private Quaternion initialTailRot;

        void Awake()
        {
            if (body != null) initialBodyLocalPos = body.localPosition;
            if (head != null) initialHeadLocalRot = head.localEulerAngles;
            if (leftWing != null) initialLeftWingRot = leftWing.localRotation;
            if (rightWing != null) initialRightWingRot = rightWing.localRotation;
            if (leftLeg != null) initialLeftLegRot = leftLeg.localRotation;
            if (rightLeg != null) initialRightLegRot = rightLeg.localRotation;
            if (tail != null) initialTailRot = tail.localRotation;

            breathingPhase = Random.Range(0f, Mathf.PI * 2f);
            nextHeadTurnTime = Random.Range(3.0f, 8.0f);
            nextBlinkTime = Random.Range(3.0f, 7.0f);
            nextRuffleTime = Random.Range(15.0f, 28.0f);
        }

        public void SetState(BirdState newState)
        {
            currentState = newState;
            if (newState == BirdState.Flying)
            {
                if (leftWing != null) leftWing.gameObject.SetActive(true);
                if (rightWing != null) rightWing.gameObject.SetActive(true);
                // Reset legs to tucked
                if (leftLeg != null) leftLeg.localRotation = Quaternion.Euler(45f, 0f, 0f);
                if (rightLeg != null) rightLeg.localRotation = Quaternion.Euler(45f, 0f, 0f);
            }
            else if (newState == BirdState.TakingOff)
            {
                if (leftWing != null) leftWing.gameObject.SetActive(true);
                if (rightWing != null) rightWing.gameObject.SetActive(true);
            }
            else if (newState == BirdState.Landing)
            {
                if (leftWing != null) leftWing.gameObject.SetActive(true);
                if (rightWing != null) rightWing.gameObject.SetActive(true);
                // Extend legs forward
                if (leftLeg != null) leftLeg.localRotation = Quaternion.Euler(-15f, 0f, 0f);
                if (rightLeg != null) rightLeg.localRotation = Quaternion.Euler(-15f, 0f, 0f);
            }
            else if (newState == BirdState.Perched || newState == BirdState.IdleResting || newState == BirdState.Walking)
            {
                if (leftWing != null) leftWing.gameObject.SetActive(false);
                if (rightWing != null) rightWing.gameObject.SetActive(false);
                if (leftLeg != null) leftLeg.localRotation = initialLeftLegRot;
                if (rightLeg != null) rightLeg.localRotation = initialRightLegRot;
            }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            // 1. Blinking (eyes scale down briefly)
            UpdateBlinking(dt);

            switch (currentState)
            {
                case BirdState.IdleResting:
                case BirdState.Perched:
                    UpdateIdle(dt);
                    break;

                case BirdState.Walking:
                    UpdateWalking(dt);
                    break;

                case BirdState.TakingOff:
                    UpdateTakeOff(dt);
                    break;

                case BirdState.Flying:
                    UpdateFlying(dt);
                    break;

                case BirdState.Landing:
                    UpdateLanding(dt);
                    break;
            }
        }

        private void UpdateBlinking(float dt)
        {
            blinkTimer += dt;
            if (!isBlinking)
            {
                if (blinkTimer >= nextBlinkTime)
                {
                    isBlinking = true;
                    blinkTimer = 0f;
                    SetEyesScale(new Vector3(1f, 0.1f, 1f));
                }
            }
            else
            {
                if (blinkTimer >= blinkDuration)
                {
                    isBlinking = false;
                    blinkTimer = 0f;
                    nextBlinkTime = Random.Range(4.0f, 9.0f);
                    SetEyesScale(Vector3.one);
                }
            }
        }

        private void SetEyesScale(Vector3 scale)
        {
            if (leftEye != null) leftEye.localScale = scale;
            if (rightEye != null) rightEye.localScale = scale;
        }

        private void UpdateIdle(float dt)
        {
            // Subtle breathing motion
            breathingPhase += dt * 2.2f;
            float breath = Mathf.Sin(breathingPhase) * 0.0018f;
            if (body != null)
            {
                body.localPosition = initialBodyLocalPos + new Vector3(0f, breath, 0f);
            }

            // Head turning
            headTurnTimer += dt;
            if (headTurnTimer >= nextHeadTurnTime)
            {
                headTurnTimer = 0f;
                nextHeadTurnTime = Random.Range(5.0f, 12.0f);
                float yaw = Random.Range(-35f, 35f);
                float pitch = Random.Range(-12f, 18f);
                targetHeadLocalRot = initialHeadLocalRot + new Vector3(pitch, yaw, 0f);
            }

            if (head != null)
            {
                head.localRotation = Quaternion.Slerp(head.localRotation, Quaternion.Euler(targetHeadLocalRot), dt * 3.5f);
            }

            // Feather ruffle
            ruffleTimer += dt;
            if (!isRuffling && ruffleTimer >= nextRuffleTime)
            {
                isRuffling = true;
                ruffleTimer = 0f;
            }
            else if (isRuffling)
            {
                float ruffle = Mathf.Sin(ruffleTimer * 30f) * 4.0f;
                if (body != null) body.localRotation = Quaternion.Euler(0f, 0f, ruffle);
                if (tail != null) tail.localRotation = initialTailRot * Quaternion.Euler(0f, ruffle * 1.5f, 0f);

                if (ruffleTimer >= ruffleDuration)
                {
                    isRuffling = false;
                    ruffleTimer = 0f;
                    nextRuffleTime = Random.Range(20.0f, 35.0f);
                    if (body != null) body.localRotation = Quaternion.identity;
                    if (tail != null) tail.localRotation = initialTailRot;
                }
            }
        }

        private void UpdateWalking(float dt)
        {
            walkCycle += dt * 6.0f;
            float legSwing = Mathf.Sin(walkCycle) * 22f;
            float bob = Mathf.Abs(Mathf.Sin(walkCycle)) * 0.003f;

            if (leftLeg != null) leftLeg.localRotation = initialLeftLegRot * Quaternion.Euler(legSwing, 0f, 0f);
            if (rightLeg != null) rightLeg.localRotation = initialRightLegRot * Quaternion.Euler(-legSwing, 0f, 0f);
            if (body != null) body.localPosition = initialBodyLocalPos + new Vector3(0f, bob, 0f);

            // Subtle head bobbing characteristic of walking birds
            if (head != null)
            {
                float headBob = Mathf.Sin(walkCycle * 2f) * 6f;
                head.localRotation = Quaternion.Euler(initialHeadLocalRot + new Vector3(headBob, 0f, 0f));
            }
        }

        private void UpdateTakeOff(float dt)
        {
            // Wings partially unfold and begin fast initial flaps
            flightCycle += dt * flapFrequency * 1.2f;
            float wingAngle = Mathf.Sin(flightCycle * Mathf.PI * 2f) * maxFlapAngle;
            if (leftWing != null) leftWing.localRotation = Quaternion.Euler(0f, 15f, wingAngle);
            if (rightWing != null) rightWing.localRotation = Quaternion.Euler(0f, -15f, -wingAngle);
            if (leftLeg != null) leftLeg.localRotation = Quaternion.Euler(30f, 0f, 0f);
            if (rightLeg != null) rightLeg.localRotation = Quaternion.Euler(30f, 0f, 0f);
        }

        private void UpdateFlying(float dt)
        {
            flightCycle += dt * flapFrequency;
            float flap = Mathf.Sin(flightCycle * Mathf.PI * 2f);
            float wingAngle = flap * maxFlapAngle;
            float wingSweep = Mathf.Cos(flightCycle * Mathf.PI * 2f) * 8f;

            if (leftWing != null) leftWing.localRotation = Quaternion.Euler(wingSweep, 20f, wingAngle);
            if (rightWing != null) rightWing.localRotation = Quaternion.Euler(wingSweep, -20f, -wingAngle);

            // Tail provides gentle pitch stabilization
            if (tail != null)
            {
                float tailPitch = -flap * 5f;
                tail.localRotation = initialTailRot * Quaternion.Euler(tailPitch, 0f, 0f);
            }
        }

        private void UpdateLanding(float dt)
        {
            // Wings flared back, slow braking flaps
            flightCycle += dt * flapFrequency * 0.6f;
            float wingAngle = Mathf.Sin(flightCycle * Mathf.PI * 2f) * (maxFlapAngle * 0.7f);
            if (leftWing != null) leftWing.localRotation = Quaternion.Euler(-10f, 35f, wingAngle);
            if (rightWing != null) rightWing.localRotation = Quaternion.Euler(-10f, -35f, -wingAngle);

            if (leftLeg != null) leftLeg.localRotation = Quaternion.Euler(-20f, 0f, 0f);
            if (rightLeg != null) rightLeg.localRotation = Quaternion.Euler(-20f, 0f, 0f);
        }
    }
}
