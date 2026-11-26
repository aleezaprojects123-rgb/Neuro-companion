// ============================================================
// CarlLipSyncController.cs — Unity 6.3 LTS Runtime Script
// NeuroCompanion — Isolated Lip-Sync Controller for Carl Fredricksen
//
// Animates ONLY the lower lip and mouth aperture in sync with speech.
// Leaves Carl's nose, cheeks, eyes, and upper face 100% UNTOUCHED.
// ============================================================

using UnityEngine;

[RequireComponent(typeof(SkinnedMeshRenderer))]
public class CarlLipSyncController : MonoBehaviour
{
    [Header("Audio Source")]
    [Tooltip("Audio source playing Carl's speech clip.")]
    public AudioSource audioSource;

    [Header("Lip Movement Settings")]
    [Tooltip("Sensitivity to audio volume.")]
    [Range(1f, 10f)]
    public float amplitudeSensitivity = 4.2f;

    [Tooltip("Maximum mouth opening percentage (0-100). 60-75% gives clean, natural speech.")]
    [Range(30f, 100f)]
    public float maxMouthOpenWeight = 72f;

    [Tooltip("Speed of opening lips on speech syllables.")]
    [Range(10f, 40f)]
    public float attackSpeed = 26f;

    [Tooltip("Speed of closing lips on pauses.")]
    [Range(8f, 30f)]
    public float releaseSpeed = 16f;

    [Header("Conversational Head Gestures")]
    [Tooltip("Enable subtle head nod on speech emphasis.")]
    public bool enableHeadMotion = true;

    public Transform headOrBustTransform;

    [Range(0f, 3f)]
    public float maxHeadNodAngle = 1.4f;

    [Range(0f, 2f)]
    public float maxHeadTiltAngle = 0.8f;

    [Header("Live State (Read Only)")]
    [Range(0f, 1f)]
    public float currentMouthOpen = 0f;

    // --- Private ---
    private SkinnedMeshRenderer _smr;
    private int _idxMouthOpen = -1;
    private int _idxLipWide = -1;

    private float[] _samples = new float[256];
    private Quaternion _restRotation;

    private void Awake()
    {
        _smr = GetComponent<SkinnedMeshRenderer>();
        if (_smr != null && _smr.sharedMesh != null)
        {
            _idxMouthOpen = _smr.sharedMesh.GetBlendShapeIndex("MouthOpen");
            _idxLipWide = _smr.sharedMesh.GetBlendShapeIndex("LipWide");
        }

        if (headOrBustTransform == null) headOrBustTransform = transform;
        _restRotation = headOrBustTransform.localRotation;
    }

    private void Start()
    {
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null) audioSource = GetComponentInChildren<AudioSource>();
        }

        if (audioSource != null && audioSource.clip != null && !audioSource.isPlaying)
        {
            audioSource.loop = true;
            audioSource.Play();
        }
    }

    private void Update()
    {
        float targetOpen = 0f;

        if (audioSource != null && audioSource.isPlaying)
        {
            float rms = GetAudioRMS();
            float speechPower = Mathf.Pow(Mathf.Clamp01(rms * amplitudeSensitivity), 0.75f);
            targetOpen = speechPower;
        }

        // Smooth attack and release
        float speed = (targetOpen > currentMouthOpen) ? attackSpeed : releaseSpeed;
        currentMouthOpen = Mathf.Lerp(currentMouthOpen, targetOpen, 1f - Mathf.Exp(-Time.deltaTime * speed));

        // Animate ONLY lower lip aperture
        if (_smr != null)
        {
            if (_idxMouthOpen >= 0)
            {
                _smr.SetBlendShapeWeight(_idxMouthOpen, currentMouthOpen * maxMouthOpenWeight);
            }

            if (_idxLipWide >= 0)
            {
                // Gentle lip corner stretch on speech peaks
                float wideWeight = Mathf.Clamp01(currentMouthOpen * 1.2f) * 25f;
                _smr.SetBlendShapeWeight(_idxLipWide, wideWeight);
            }
        }

        // Gentle conversational gestures (pitch/tilt/breath)
        if (enableHeadMotion && headOrBustTransform != null)
        {
            float time = Time.time;
            float nod = -currentMouthOpen * maxHeadNodAngle;
            float tilt = Mathf.Sin(time * 1.6f) * currentMouthOpen * maxHeadTiltAngle;
            float breath = Mathf.Sin(time * 1.2f) * 0.2f;

            headOrBustTransform.localRotation = _restRotation * Quaternion.Euler(nod + breath, 0f, tilt);
        }
    }

    private float GetAudioRMS()
    {
        if (audioSource == null || !audioSource.isPlaying) return 0f;
        audioSource.GetOutputData(_samples, 0);
        float sum = 0f;
        for (int i = 0; i < _samples.Length; i++) sum += _samples[i] * _samples[i];
        return Mathf.Sqrt(sum / _samples.Length);
    }
}
