import wave
import struct
import math
import random
import os

sample_rate = 44100

def create_wav(filename, samples):
    os.makedirs(os.path.dirname(filename), exist_ok=True)
    with wave.open(filename, 'wb') as wav_file:
        wav_file.setnchannels(1)  # Mono for 3D spatial audio
        wav_file.setsampwidth(2)  # 16-bit
        wav_file.setframerate(sample_rate)
        # Apply gentle fade out to avoid clicks
        fade_len = min(len(samples), int(sample_rate * 0.05))
        for i in range(fade_len):
            gain = i / float(fade_len)
            samples[i] *= gain
            samples[len(samples) - 1 - i] *= gain
        
        # Normalize and pack
        max_val = max(abs(s) for s in samples) if samples else 1.0
        if max_val == 0: max_val = 1.0
        scale = 26000.0 / max_val  # comfortable gentle volume, not clipping
        
        raw_bytes = bytearray()
        for s in samples:
            val = int(max(-32767, min(32767, s * scale)))
            raw_bytes.extend(struct.pack('<h', val))
        wav_file.writeframes(raw_bytes)
    print(f"Created audio: {filename} ({len(samples)/sample_rate:.2f}s)")

def chirp(duration, f_start, f_end, harmonics=[(1.0, 1.0), (2.0, 0.25)], vib_rate=0, vib_depth=0):
    n = int(sample_rate * duration)
    res = [0.0] * n
    phase = 0.0
    for i in range(n):
        t = i / float(n)
        # Exponential or smooth pitch curve
        freq = f_start + (f_end - f_start) * (1.0 - math.cos(t * math.pi)) * 0.5
        if vib_rate > 0:
            freq += math.sin(2.0 * math.pi * vib_rate * (i / sample_rate)) * vib_depth
        phase += 2.0 * math.pi * freq / sample_rate
        
        # Envelope: quick attack, smooth decay
        if t < 0.15:
            env = math.sin((t / 0.15) * (math.pi / 2.0))
        else:
            env = math.cos(((t - 0.15) / 0.85) * (math.pi / 2.0))
        
        sample = 0.0
        for h_mult, h_amp in harmonics:
            sample += math.sin(phase * h_mult) * h_amp
        res[i] = sample * env
    return res

def silence(duration):
    return [0.0] * int(sample_rate * duration)

def generate_sparrow():
    # Lively, cheerful short chirps
    out = silence(0.1)
    out.extend(chirp(0.08, 4200, 5800, [(1.0, 1.0), (2.0, 0.2)]))
    out.extend(silence(0.07))
    out.extend(chirp(0.12, 3800, 6200, [(1.0, 1.0), (2.0, 0.3)]))
    out.extend(silence(0.4))
    out.extend(chirp(0.09, 4500, 5600, [(1.0, 1.0), (2.0, 0.2)]))
    out.extend(silence(0.05))
    out.extend(chirp(0.14, 3900, 5900, [(1.0, 1.0), (2.0, 0.25)]))
    out.extend(silence(0.5))
    out.extend(chirp(0.10, 4300, 5400, [(1.0, 1.0), (2.0, 0.2)]))
    out.extend(silence(0.2))
    return out

def generate_bulbul():
    # Melodious, fluty musical notes
    out = silence(0.15)
    out.extend(chirp(0.18, 2600, 3400, [(1.0, 1.0), (2.0, 0.15)], vib_rate=8, vib_depth=80))
    out.extend(silence(0.08))
    out.extend(chirp(0.22, 3600, 2800, [(1.0, 1.0), (2.0, 0.18)], vib_rate=10, vib_depth=100))
    out.extend(silence(0.06))
    out.extend(chirp(0.28, 3100, 4200, [(1.0, 1.0), (2.0, 0.12)], vib_rate=9, vib_depth=90))
    out.extend(silence(0.6))
    out.extend(chirp(0.20, 2800, 3500, [(1.0, 1.0), (2.0, 0.15)]))
    out.extend(silence(0.08))
    out.extend(chirp(0.30, 3800, 2900, [(1.0, 1.0), (2.0, 0.12)]))
    out.extend(silence(0.3))
    return out

def generate_dove():
    # Soothing, deep rhythmic coo: 'coo - COO - cuk'
    out = silence(0.2)
    # 1. soft introductory coo
    out.extend(chirp(0.32, 480, 510, [(1.0, 1.0), (2.0, 0.08), (3.0, 0.02)], vib_rate=4, vib_depth=15))
    out.extend(silence(0.12))
    # 2. emphatic, slightly higher COO
    out.extend(chirp(0.48, 560, 590, [(1.0, 1.0), (2.0, 0.09), (3.0, 0.02)], vib_rate=4.5, vib_depth=20))
    out.extend(silence(0.08))
    # 3. short dropping cuk
    out.extend(chirp(0.18, 540, 420, [(1.0, 1.0), (2.0, 0.06)]))
    out.extend(silence(0.7))
    # Repeat once softly
    out.extend(chirp(0.30, 470, 500, [(1.0, 1.0), (2.0, 0.08)], vib_rate=4, vib_depth=12))
    out.extend(silence(0.12))
    out.extend(chirp(0.45, 550, 580, [(1.0, 1.0), (2.0, 0.08)], vib_rate=4.5, vib_depth=18))
    out.extend(silence(0.08))
    out.extend(chirp(0.16, 530, 410, [(1.0, 1.0), (2.0, 0.05)]))
    out.extend(silence(0.3))
    return out

def generate_myna():
    # Bright, varied conversational chirp & trill
    out = silence(0.15)
    # High ascending click-trill
    out.extend(chirp(0.11, 2200, 3100, [(1.0, 1.0), (2.0, 0.2), (3.0, 0.1)]))
    out.extend(silence(0.05))
    out.extend(chirp(0.15, 3400, 2400, [(1.0, 1.0), (2.0, 0.25)]))
    out.extend(silence(0.08))
    # Liquid double note
    out.extend(chirp(0.24, 2600, 3600, [(1.0, 1.0), (2.0, 0.2)], vib_rate=14, vib_depth=120))
    out.extend(silence(0.4))
    out.extend(chirp(0.14, 2100, 3200, [(1.0, 1.0), (2.0, 0.2)]))
    out.extend(silence(0.06))
    out.extend(chirp(0.26, 3300, 2600, [(1.0, 1.0), (2.0, 0.2)], vib_rate=12, vib_depth=100))
    out.extend(silence(0.3))
    return out

def generate_babbler():
    # Jungle Babbler sociable chatter (soft communal murmuring/chirping)
    out = silence(0.15)
    out.extend(chirp(0.10, 2800, 3900, [(1.0, 1.0), (2.0, 0.3)]))
    out.extend(silence(0.04))
    out.extend(chirp(0.09, 3600, 2900, [(1.0, 1.0), (2.0, 0.25)]))
    out.extend(silence(0.04))
    out.extend(chirp(0.12, 2900, 4100, [(1.0, 1.0), (2.0, 0.3)]))
    out.extend(silence(0.25))
    out.extend(chirp(0.11, 3100, 4200, [(1.0, 1.0), (2.0, 0.3)]))
    out.extend(silence(0.05))
    out.extend(chirp(0.14, 4000, 3100, [(1.0, 1.0), (2.0, 0.25)]))
    out.extend(silence(0.35))
    out.extend(chirp(0.10, 3000, 3800, [(1.0, 1.0), (2.0, 0.25)]))
    out.extend(silence(0.2))
    return out

out_dir = r"D:\NeuroCompanion\Unity\NeuroCompanion\Assets\Audio\Birds"
create_wav(os.path.join(out_dir, "sparrow_chirp.wav"), generate_sparrow())
create_wav(os.path.join(out_dir, "bulbul_call.wav"), generate_bulbul())
create_wav(os.path.join(out_dir, "dove_coo.wav"), generate_dove())
create_wav(os.path.join(out_dir, "myna_call.wav"), generate_myna())
create_wav(os.path.join(out_dir, "babbler_call.wav"), generate_babbler())
print("All 5 bird species audio clips generated successfully.")
