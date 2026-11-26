// ============================================================
// BossTalkingController.cs — Unity 6.3 LTS Runtime Script
// Subtle life-like speech gestures & audio sync for The Boss
// ============================================================

using UnityEngine;

public class BossTalkingController : MonoBehaviour
{
    public AudioSource audioSource;
    public Transform headBone;

    [Header("Speech Head Motion")]
    [Range(0.5f, 5f)]
    public float speechNodAmount = 2.0f;
    [Range(1f, 15f)]
    public float nodSpeed = 8.0f;

    [Header("Idle Life (Breathing / Subtle Sway)")]
    public bool enableIdleLife = true;
    public float idleBreathingSpeed = 1.2f;
    public float idleBreathingAmount = 0.5f;

    private Quaternion _headRestRotation;
    private float[] _samples = new float[256];
    private float _smoothedAmp = 0f;

    private void Start()
    {
        // Auto-find head bone
        if (headBone == null)
        {
            headBone = FindBone(transform, "Head");
        }

        if (headBone != null)
        {
            _headRestRotation = headBone.localRotation;
        }

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }

        if (audioSource != null && audioSource.clip != null && !audioSource.isPlaying)
        {
            audioSource.loop = true;
            audioSource.Play();
        }
    }

    private void LateUpdate()
    {
        if (headBone == null) return;

        float nodOffset = 0f;

        // 1. Audio-driven nodding while speaking
        if (audioSource != null && audioSource.isPlaying)
        {
            float amp = GetAudioRMS();
            _smoothedAmp = Mathf.Lerp(_smoothedAmp, amp, Time.deltaTime * nodSpeed);
            nodOffset = _smoothedAmp * speechNodAmount * 15f;
        }
        else
        {
            _smoothedAmp = Mathf.Lerp(_smoothedAmp, 0f, Time.deltaTime * 5f);
        }

        // 2. Idle breathing sway
        float idleSway = 0f;
        if (enableIdleLife)
        {
            idleSway = Mathf.Sin(Time.time * idleBreathingSpeed * Mathf.PI * 2f) * idleBreathingAmount;
        }

        // Apply clean rotation to head without ever deforming the mesh!
        Quaternion targetRot = _headRestRotation * Quaternion.Euler(nodOffset + idleSway, 0f, 0f);
        headBone.localRotation = Quaternion.Slerp(headBone.localRotation, targetRot, Time.deltaTime * 10f);
    }

    private float GetAudioRMS()
    {
        if (audioSource == null || !audioSource.isPlaying) return 0f;
        audioSource.GetOutputData(_samples, 0);
        float sum = 0f;
        for (int i = 0; i < _samples.Length; i++)
        {
            sum += _samples[i] * _samples[i];
        }
        return Mathf.Sqrt(sum / _samples.Length);
    }

    private static Transform FindBone(Transform root, string partName)
    {
        if (root.name.ToLower().Contains(partName.ToLower())) return root;
        foreach (Transform child in root)
        {
            Transform found = FindBone(child, partName);
            if (found != null) return found;
        }
        return null;
    }
}
