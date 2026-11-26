// ============================================================
// CarlJoyfulEntranceController.cs — Unity 6.3 LTS Runtime Script
// NeuroCompanion — Joyful Carl Fredricksen Entrance & Greeting
//
// Animates Carl running in joyfully from the background with
// animated cartoon bounding hops, joyful waddle, settling in
// front of the camera, and delivering a warm, speaking greeting.
// ============================================================

using UnityEngine;

public class CarlJoyfulEntranceController : MonoBehaviour
{
    public enum EntranceState
    {
        RunningIn,
        ArriveAndSettle,
        Greeting,
        Idle
    }

    [Header("Entrance Positions")]
    [Tooltip("Starting distance down the hall in background.")]
    public Vector3 startPosition = new Vector3(0f, 0f, 16.0f);

    [Tooltip("Final standing position in front of camera.")]
    public Vector3 finalPosition = new Vector3(0f, 0f, 0f);

    [Header("Run Dynamics")]
    [Tooltip("Total run-in duration in seconds.")]
    [Range(1.5f, 6.0f)]
    public float runDuration = 3.2f;

    [Tooltip("Vertical hop height per running step.")]
    [Range(0.1f, 0.8f)]
    public float stepHopHeight = 0.35f;

    [Tooltip("Running stride frequency (steps per second).")]
    [Range(1.5f, 6.0f)]
    public float stepFrequency = 3.6f;

    [Tooltip("Side-to-side torso waddle roll in degrees.")]
    [Range(1f, 15f)]
    public float waddleRollAngle = 7.0f;

    [Tooltip("Forward pitch lean while running.")]
    [Range(0f, 15f)]
    public float forwardPitchAngle = 5.0f;

    [Header("Greeting Audio")]
    public AudioSource audioSource;
    public AudioClip greetingClip;

    [Header("Lip Sync Reference")]
    public CarlLipSyncController lipSyncController;

    [Header("State (Read Only)")]
    public EntranceState currentState = EntranceState.RunningIn;

    // --- Private ---
    private float _timer = 0f;
    private Quaternion _facingRotation;

    private void Awake()
    {
        // Carl faces -Z towards the camera
        _facingRotation = Quaternion.Euler(0f, 180f, 0f);

        if (lipSyncController == null)
            lipSyncController = GetComponent<CarlLipSyncController>();

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();
    }

    private void Start()
    {
        RestartEntrance();
    }

    [ContextMenu("Restart Joyful Entrance")]
    public void RestartEntrance()
    {
        _timer = 0f;
        currentState = EntranceState.RunningIn;
        transform.position = startPosition;
        transform.rotation = _facingRotation;

        // Temporarily mute speech until Carl arrives
        if (audioSource != null && audioSource.isPlaying)
        {
            audioSource.Stop();
        }

        if (lipSyncController != null)
        {
            lipSyncController.enabled = false;
        }
    }

    private void Update()
    {
        _timer += Time.deltaTime;

        switch (currentState)
        {
            case EntranceState.RunningIn:
                UpdateRunningIn();
                break;

            case EntranceState.ArriveAndSettle:
                UpdateArriveAndSettle();
                break;

            case EntranceState.Greeting:
                UpdateGreeting();
                break;

            case EntranceState.Idle:
                UpdateIdle();
                break;
        }
    }

    private void UpdateRunningIn()
    {
        float t = Mathf.Clamp01(_timer / runDuration);
        
        // Smooth deceleration towards camera
        float easeT = Mathf.SmoothStep(0f, 1f, t);
        Vector3 groundPos = Vector3.Lerp(startPosition, finalPosition, easeT);

        // Joyful vertical hop on every running step
        float stepCycle = _timer * stepFrequency;
        float hop = Mathf.Abs(Mathf.Sin(stepCycle * Mathf.PI)) * stepHopHeight * (1f - easeT * 0.4f);
        groundPos.y += hop;
        transform.position = groundPos;

        // Joyful cartoon waddle (side-to-side roll & pitch)
        float waddleRoll = Mathf.Sin(stepCycle * Mathf.PI) * waddleRollAngle * (1f - easeT * 0.3f);
        float leanPitch = forwardPitchAngle * (1f - easeT);
        transform.rotation = _facingRotation * Quaternion.Euler(leanPitch, 0f, waddleRoll);

        if (t >= 1.0f)
        {
            currentState = EntranceState.ArriveAndSettle;
            _timer = 0f;
        }
    }

    private void UpdateArriveAndSettle()
    {
        // Cute springy settle bounce on arrival
        float settleTime = 0.55f;
        float t = _timer / settleTime;

        if (t <= 1.0f)
        {
            float bounce = Mathf.Sin(t * Mathf.PI * 2.5f) * Mathf.Exp(-t * 3.5f) * 0.18f;
            transform.position = finalPosition + new Vector3(0f, bounce, 0f);
            transform.rotation = _facingRotation * Quaternion.Euler(-bounce * 12f, 0f, 0f);
        }
        else
        {
            transform.position = finalPosition;
            transform.rotation = _facingRotation;
            StartGreeting();
        }
    }

    private void StartGreeting()
    {
        currentState = EntranceState.Greeting;
        _timer = 0f;

        // Play warm greeting audio
        if (audioSource != null)
        {
            if (greetingClip != null)
            {
                audioSource.clip = greetingClip;
            }
            audioSource.loop = false;
            audioSource.Play();
        }

        // Enable LipSync to animate his mouth with the greeting speech
        if (lipSyncController != null)
        {
            lipSyncController.enabled = true;
        }
    }

    private void UpdateGreeting()
    {
        if (audioSource != null && !audioSource.isPlaying && _timer > 2.0f)
        {
            currentState = EntranceState.Idle;
        }
    }

    private void UpdateIdle()
    {
        // Gentle breathing and looking warmly at user
        float time = Time.time;
        float breath = Mathf.Sin(time * 1.4f) * 0.15f;
        float idleYaw = Mathf.Sin(time * 0.8f) * 0.5f;
        transform.position = finalPosition + new Vector3(0f, breath * 0.05f, 0f);
        transform.rotation = _facingRotation * Quaternion.Euler(breath, idleYaw, 0f);
    }
}
