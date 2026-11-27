// ============================================================
// WavUtility.cs — Unity 6.3 LTS
// NeuroCompanion — Converts audio sample buffers to/from standard PCM WAV
// Supports 100% dynamic, in-memory on-the-fly speech synthesis decoding
// ============================================================

using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace NeuroCompanion.Carl
{
    public static class WavUtility
    {
        public static float[] DownsampleTo16k(float[] sourceSamples, int sourceRate)
        {
            if (sourceRate == 16000 || sourceSamples == null || sourceSamples.Length == 0)
                return sourceSamples;

            float ratio = (float)sourceRate / 16000f;
            int targetLength = Mathf.FloorToInt(sourceSamples.Length / ratio);
            float[] target = new float[targetLength];

            for (int i = 0; i < targetLength; i++)
            {
                int sourceIdx = Mathf.FloorToInt(i * ratio);
                target[i] = sourceSamples[Mathf.Min(sourceIdx, sourceSamples.Length - 1)];
            }
            return target;
        }

        public static byte[] FromAudioSamples(float[] samples, int channels, int sampleRate)
        {
            if (samples == null || samples.Length == 0)
                return new byte[0];

            using (var memoryStream = new MemoryStream())
            {
                using (var writer = new BinaryWriter(memoryStream))
                {
                    int sampleCount = samples.Length;
                    short bitsPerSample = 16;
                    int byteRate = sampleRate * channels * (bitsPerSample / 8);
                    short blockAlign = (short)(channels * (bitsPerSample / 8));
                    int subChunk2Size = sampleCount * (bitsPerSample / 8);
                    int chunkSize = 36 + subChunk2Size;

                    // RIFF chunk descriptor
                    writer.Write(new char[4] { 'R', 'I', 'F', 'F' });
                    writer.Write(chunkSize);
                    writer.Write(new char[4] { 'W', 'A', 'V', 'E' });

                    // "fmt " sub-chunk
                    writer.Write(new char[4] { 'f', 'm', 't', ' ' });
                    writer.Write(16); // SubChunk1Size (16 for PCM)
                    writer.Write((short)1); // AudioFormat (1 for PCM)
                    writer.Write((short)channels);
                    writer.Write(sampleRate);
                    writer.Write(byteRate);
                    writer.Write(blockAlign);
                    writer.Write(bitsPerSample);

                    // "data" sub-chunk
                    writer.Write(new char[4] { 'd', 'a', 't', 'a' });
                    writer.Write(subChunk2Size);

                    // Write 16-bit PCM samples
                    for (int i = 0; i < sampleCount; i++)
                    {
                        float clamped = Mathf.Clamp(samples[i], -1.0f, 1.0f);
                        short sample = (short)(clamped * 32767f);
                        writer.Write(sample);
                    }

                    writer.Flush();
                    return memoryStream.ToArray();
                }
            }
        }

        public static AudioClip ToAudioClip(byte[] wavBytes, string clipName = "CarlDynamicVoice")
        {
            if (wavBytes == null || wavBytes.Length < 44)
                return null;

            try
            {
                int channels = BitConverter.ToInt16(wavBytes, 22);
                int sampleRate = BitConverter.ToInt32(wavBytes, 24);
                int bitsPerSample = BitConverter.ToInt16(wavBytes, 34);

                if (channels <= 0 || sampleRate <= 0 || bitsPerSample != 16)
                    return null;

                // Locate "data" chunk
                int pos = 12;
                while (pos + 8 <= wavBytes.Length)
                {
                    string chunkId = Encoding.ASCII.GetString(wavBytes, pos, 4);
                    int chunkSize = BitConverter.ToInt32(wavBytes, pos + 4);

                    if (chunkId == "data")
                    {
                        pos += 8;
                        int sampleCount = chunkSize / 2;
                        float[] samples = new float[sampleCount];

                        for (int i = 0; i < sampleCount && (pos + i * 2 + 1) < wavBytes.Length; i++)
                        {
                            short val = BitConverter.ToInt16(wavBytes, pos + i * 2);
                            samples[i] = val / 32768.0f;
                        }

                        AudioClip clip = AudioClip.Create(clipName, sampleCount / channels, channels, sampleRate, false);
                        clip.SetData(samples, 0);
                        return clip;
                    }

                    pos += 8 + chunkSize;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[WavUtility] ToAudioClip error: " + ex.Message);
            }

            return null;
        }
    }
}
