// ============================================================
// WavUtility.cs — Unity 6.3 LTS
// NeuroCompanion — Converts audio sample buffers into standard PCM WAV bytes
// ============================================================

using System;
using System.IO;
using UnityEngine;

namespace NeuroCompanion.Carl
{
    public static class WavUtility
    {
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
    }
}
