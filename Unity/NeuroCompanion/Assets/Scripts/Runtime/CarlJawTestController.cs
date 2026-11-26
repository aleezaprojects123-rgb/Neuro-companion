// ============================================================
// CarlJawTestController.cs  — Unity 6.3 LTS Runtime Script
// NeuroCompanion — Carl Fredricksen Jaw Animation Prototype
// ============================================================

using UnityEngine;

[RequireComponent(typeof(SkinnedMeshRenderer))]
public class CarlJawTestController : MonoBehaviour
{
    public enum JawMode
    {
        AudioAmplitudeDriven,  // Drive from AudioSource amplitude (Default)
        OscillatingTest,       // Sine wave test loop — no audio needed
        ManualControl          // Set jawOpenAmount externally (0 = closed, 1 = fully open)
    }

    [Header("Bone Reference")]
    [Tooltip("Assign the Jaw_Bone transform created by CarlJawBoneCreator.")]
    public Transform jawBone;

    [Header("Mode")]
    public JawMode mode = JawMode.AudioAmplitudeDriven;

    [Header("Jaw Motion")]
    [Tooltip("Maximum jaw rotation in degrees around the rotation axis. Keep between 3 and 5 degrees to prevent face tearing.")]
    [Range(0f, 45f)]
    public float maxJawAngle = 4.5f;

    [Tooltip("Rotation axis. Negative X opens the jaw downward in standard orientation.")]
    public Vector3 rotationAxis = new Vector3(-1f, 0f, 0f);

    [Header("Audio Amplitude Settings")]
    public AudioSource audioSource;
    [Range(0.5f, 10f)]
    public float amplitudeSensitivity = 3.5f;
    [Range(1f, 30f)]
    public float responseSpeed = 12f;
    [Range(1f, 30f)]
    public float returnToNeutralSpeed = 8f;

    [Header("Oscillating Test Settings")]
    [Tooltip("Full open-close cycles per second in test mode.")]
    [Range(0.2f, 4f)]
    public float oscillationSpeed = 1.2f;

    [Header("Read-only — Current State")]
    [Range(0f, 1f)]
    public float jawOpenAmount = 0f;

    // --- Private ---
    private Quaternion _jawRestRotation;
    private float[] _audioSamples = new float[256];

    private void Start()
    {
        // Auto-find jaw bone if not explicitly assigned
        if (jawBone == null)
        {
            jawBone = FindBoneByName(transform, "Jaw_Bone");
            if (jawBone == null)
            {
                Debug.LogWarning("[CarlJaw] Jaw_Bone not found in hierarchy.");
            }
        }

        if (jawBone != null)
        {
            _jawRestRotation = jawBone.localRotation;
        }

        // Auto-find AudioSource on this object or children
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = GetComponentInChildren<AudioSource>();
            }
        }

        // Ensure audio loops during test if available
        if (audioSource != null && audioSource.clip != null && !audioSource.isPlaying)
        {
            audioSource.loop = true;
            audioSource.Play();
        }
    }

    private void Update()
    {
        if (jawBone == null) return;

        switch (mode)
        {
            case JawMode.AudioAmplitudeDriven:
                float targetAmp = 0f;
                if (audioSource != null && audioSource.isPlaying)
                {
                    float rawRms = GetAudioRMS();
                    targetAmp = Mathf.Clamp01(rawRms * amplitudeSensitivity);
                    // Fast attack
                    jawOpenAmount = Mathf.Lerp(jawOpenAmount, targetAmp, 1f - Mathf.Exp(-Time.deltaTime * responseSpeed));
                }
                else
                {
                    // Smooth return to neutral when audio stops or is quiet
                    jawOpenAmount = Mathf.Lerp(jawOpenAmount, 0f, 1f - Mathf.Exp(-Time.deltaTime * returnToNeutralSpeed));
                }
                break;

            case JawMode.OscillatingTest:
                jawOpenAmount = (Mathf.Sin(Time.time * oscillationSpeed * Mathf.PI * 2f) + 1f) * 0.5f;
                break;

            case JawMode.ManualControl:
                // jawOpenAmount controlled externally
                break;
        }

        // Apply rotation from resting pose
        float angle = jawOpenAmount * maxJawAngle;
        jawBone.localRotation = _jawRestRotation * Quaternion.AngleAxis(angle, rotationAxis);
    }

    private float GetAudioRMS()
    {
        if (audioSource == null || !audioSource.isPlaying) return 0f;
        audioSource.GetOutputData(_audioSamples, 0);
        float sum = 0f;
        for (int i = 0; i < _audioSamples.Length; i++)
        {
            sum += _audioSamples[i] * _audioSamples[i];
        }
        return Mathf.Sqrt(sum / _audioSamples.Length);
    }

    private static Transform FindBoneByName(Transform root, string name)
    {
        if (root.name == name) return root;
        foreach (Transform child in root)
        {
            Transform found = FindBoneByName(child, name);
            if (found != null) return found;
        }
        return null;
    }

    private void OnDrawGizmos()
    {
        if (jawBone == null) return;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(jawBone.position, 0.05f);
        Gizmos.color = Color.cyan;
        Gizmos.DrawRay(jawBone.position, jawBone.forward * 0.2f);
    }
}
