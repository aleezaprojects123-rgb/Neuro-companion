import os
import numpy as np
from PIL import Image

def generate_cobblestone_pbr(size=1024):
    print("Generating 1024x1024 cobblestone paver PBR textures...")
    np.random.seed(101)

    # Grid: U across block width, V along block length
    # A single texture will tile e.g. 1 block across U (or 2) and 4 blocks along V
    blocks_u = 1
    blocks_v = 4

    u = np.linspace(0, blocks_u, size, endpoint=False)
    v = np.linspace(0, blocks_v, size, endpoint=False)
    uv_u, uv_v = np.meshgrid(u, v)

    # Local block coordinates in [0, 1)
    local_u = uv_u % 1.0
    local_v = uv_v % 1.0

    # Mortar joint margins: 8% on each side
    margin_u = 0.08
    margin_v = 0.08

    # Distance to block edge for bevel
    dist_edge_u = np.minimum(local_u, 1.0 - local_u) / margin_u
    dist_edge_v = np.minimum(local_v, 1.0 - local_v) / margin_v
    edge_factor = np.clip(np.minimum(dist_edge_u, dist_edge_v), 0, 1)

    # Crowned top profile for each stone
    block_dome = (np.sin(local_u * np.pi) * np.sin(local_v * np.pi)) ** 0.45
    height_map = block_dome * edge_factor

    # Add stone roughness noise
    # Periodic noise helper
    def periodic_noise(freq_x, freq_y, octaves=3):
        h = np.zeros((size, size), dtype=np.float32)
        for o in range(octaves):
            fx = freq_x * (2 ** o)
            fy = freq_y * (2 ** o)
            amp = 0.5 ** o
            kx = 2 * np.pi * fx
            ky = 2 * np.pi * fy
            p1 = np.random.uniform(0, 2 * np.pi)
            p2 = np.random.uniform(0, 2 * np.pi)
            h += amp * np.sin(kx * (uv_u / blocks_u) + p1) * np.cos(ky * (uv_v / blocks_v) + p2)
        return (h - h.min()) / (h.max() - h.min() + 1e-6)

    stone_grain = periodic_noise(32, 32, octaves=4)
    stone_chisel = periodic_noise(8, 16, octaves=3)

    # Combined stone height:
    stone_height = height_map * 0.85 + (stone_grain * 0.10 + stone_chisel * 0.05) * edge_factor

    # Normal map from height
    dx = (np.roll(stone_height, -1, axis=1) - np.roll(stone_height, 1, axis=1)) * 0.5
    dy = (np.roll(stone_height, -1, axis=0) - np.roll(stone_height, 1, axis=0)) * 0.5

    bump_strength = 7.0
    nx = -dx * bump_strength
    ny = -dy * bump_strength
    nz = np.ones_like(stone_height)

    len_n = np.sqrt(nx**2 + ny**2 + nz**2) + 1e-8
    nx /= len_n
    ny /= len_n
    nz /= len_n

    normal_rgb = np.zeros((size, size, 3), dtype=np.uint8)
    normal_rgb[:, :, 0] = np.clip((nx * 0.5 + 0.5) * 255.0, 0, 255).astype(np.uint8)
    normal_rgb[:, :, 1] = np.clip((ny * 0.5 + 0.5) * 255.0, 0, 255).astype(np.uint8)
    normal_rgb[:, :, 2] = np.clip((nz * 0.5 + 0.5) * 255.0, 0, 255).astype(np.uint8)

    # Albedo: Warm grey / buff granite with dark mortar joints
    c_granite_light = np.array([205.0, 198.0, 188.0], dtype=np.float32)
    c_granite_mid   = np.array([182.0, 175.0, 165.0], dtype=np.float32)
    c_granite_dark  = np.array([142.0, 136.0, 128.0], dtype=np.float32)
    c_mortar        = np.array([72.0, 68.0, 62.0], dtype=np.float32)

    albedo = np.zeros((size, size, 3), dtype=np.float32)
    for c in range(3):
        # Stone color with mineral grain
        stone_col = (
            c_granite_light[c] * 0.6 +
            c_granite_mid[c] * (0.35 * stone_chisel) +
            c_granite_dark[c] * (0.25 * stone_grain)
        )
        # Blend mortar into crevices
        albedo[:, :, c] = stone_col * edge_factor + c_mortar[c] * (1.0 - edge_factor)

    albedo = np.clip(albedo, 0, 255).astype(np.uint8)

    # Output paths
    out_dir = r"D:\NeuroCompanion\Unity\NeuroCompanion\Assets\Textures\Environment\Pathway"
    albedo_path = os.path.join(out_dir, "Cobblestone_Paver_Albedo.png")
    normal_path = os.path.join(out_dir, "Cobblestone_Paver_Normal.png")

    Image.fromarray(albedo).save(albedo_path, quality=95)
    print(f"Saved Cobblestone Albedo: {albedo_path}")

    Image.fromarray(normal_rgb).save(normal_path)
    print(f"Saved Cobblestone Normal: {normal_path}")

if __name__ == "__main__":
    generate_cobblestone_pbr(1024)
