// ============================================================
// CarlCleanTalkingController.cs — Unity 6.3 LTS Runtime Script
// NeuroCompanion — Carl Fredricksen Conversational Gestures
// ============================================================

using UnityEngine;

public class CarlCleanTalkingController : MonoBehaviour
{
    public Transform headObject;
    public AudioSource audioSource;
    public float sensitivity = 3.5f;
    public float maxNodAngle = 2.5f;
    public float maxTiltAngle = 1.2f;

    private Quaternion _restRotation;
    private float[] _samples = new float[256];
    private float _smoothedAmp = 0f;

    private void Start()
    {
        if (headObject == null) headObject = transform;
        _restRotation = headObject.localRotation;

        if (audioSource == null) audioSource = GetComponent<AudioSource>();
        if (audioSource != null && audioSource.clip != null && !audioSource.isPlaying)
        {
            audioSource.loop = true;
            audioSource.Play();
        }
    }

    private void Update()
    {
        float target = 0f;
        if (audioSource != null && audioSource.isPlaying)
        {
            audioSource.GetOutputData(_samples, 0);
            float sum = 0f;
            for (int i = 0; i < _samples.Length; i++) sum += _samples[i] * _samples[i];
            float rms = Mathf.Sqrt(sum / _samples.Length);
            target = Mathf.Clamp01(rms * sensitivity);
        }

        _smoothedAmp = Mathf.Lerp(_smoothedAmp, target, Time.deltaTime * 15f);

        if (headObject != null)
        {
            float nod = -_smoothedAmp * maxNodAngle;
            float tilt = Mathf.Sin(Time.time * 2f) * _smoothedAmp * maxTiltAngle;
            float idle = Mathf.Sin(Time.time * 1.2f) * 0.2f;
            headObject.localRotation = _restRotation * Quaternion.Euler(nod + idle, 0f, tilt);
        }
    }
}
