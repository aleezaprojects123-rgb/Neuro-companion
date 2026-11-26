import os
import numpy as np
from PIL import Image

def generate_seamless_gravel(size=2048):
    print(f"Generating {size}x{size} seamless PBR gravel textures...")
    np.random.seed(42)

    # 1. Coordinate grid
    x = np.linspace(0, 1, size, endpoint=False)
    y = np.linspace(0, 1, size, endpoint=False)
    xv, yv = np.meshgrid(x, y)

    # Helper for seamless periodic noise using 4D Fourier synthesis
    def periodic_noise(freq_x, freq_y, octaves=4, decay=0.55):
        h = np.zeros((size, size), dtype=np.float32)
        for o in range(octaves):
            fx = freq_x * (2 ** o)
            fy = freq_y * (2 ** o)
            amp = decay ** o
            # 2D torus embedding
            kx = 2 * np.pi * fx
            ky = 2 * np.pi * fy
            phase1 = np.random.uniform(0, 2 * np.pi)
            phase2 = np.random.uniform(0, 2 * np.pi)
            h += amp * 0.5 * (np.sin(kx * xv + phase1) * np.cos(ky * yv + phase2) +
                              np.cos(kx * xv * 1.3 - phase2) * np.sin(ky * yv * 1.1 + phase1))
        # normalize to [0, 1]
        h = (h - h.min()) / (h.max() - h.min() + 1e-6)
        return h

    # Macro ground variation (earthy patchiness)
    macro_variation = periodic_noise(3, 3, octaves=3, decay=0.6)
    
    # Mid-frequency soil/gravel bed
    mid_bed = periodic_noise(16, 16, octaves=4, decay=0.5)

    # High-frequency sand & grit
    fine_grit = periodic_noise(64, 64, octaves=3, decay=0.5)

    # 2. Cellular / Voronoi individual pebbles
    # We scatter N seamless pebble centers
    n_pebbles = 12000
    # Random pebble positions in [0, 1)
    px = np.random.uniform(0, 1, n_pebbles)
    py = np.random.uniform(0, 1, n_pebbles)
    # Radii in pixels: 4 to 14 pixels (approx 4mm - 15mm scale)
    pr = np.random.uniform(3.5, 12.0, n_pebbles)
    p_height = np.random.uniform(0.35, 1.0, n_pebbles)

    # Pebble height map
    pebble_map = np.zeros((size, size), dtype=np.float32)

    # Fast rasterization of pebbles onto the grid using 3x3 periodic wrapping
    # Split into a lower-res grid for fast neighborhood search or draw circular splats
    # Drawing splats directly:
    print("Rasterizing realistic pebbles...")
    # Make a collection of pebble stamps of varying sizes
    stamp_sizes = [7, 11, 15, 21, 27]
    stamps = []
    for s in stamp_sizes:
        rad = s // 2
        sx, sy = np.meshgrid(np.arange(-rad, rad + 1), np.arange(-rad, rad + 1))
        dist_sq = (sx ** 2 + sy ** 2) / (rad ** 2)
        # Dome profile
        stamp = np.clip(1.0 - dist_sq, 0, 1) ** 0.65
        # Irregular pebble distortion
        stamp_noise = 1.0 + 0.15 * np.sin(sx * 0.8 + sy * 0.6)
        stamps.append((stamp * stamp_noise).astype(np.float32))

    # Place pebbles
    for i in range(n_pebbles):
        cx = int(px[i] * size)
        cy = int(py[i] * size)
        h_val = p_height[i]
        
        # choose stamp
        r_idx = int(np.clip((pr[i] - 3.5) / (12.0 - 3.5) * len(stamps), 0, len(stamps) - 1))
        st = stamps[r_idx]
        s_rad = stamp_sizes[r_idx] // 2
        
        x_min, x_max = cx - s_rad, cx + s_rad + 1
        y_min, y_max = cy - s_rad, cy + s_rad + 1
        
        # Periodic wrap slices
        for dy, sy_idx in enumerate(range(y_min, y_max)):
            wy = sy_idx % size
            for dx, sx_idx in enumerate(range(x_min, x_max)):
                wx = sx_idx % size
                v = st[dy, dx] * h_val
                if v > pebble_map[wy, wx]:
                    pebble_map[wy, wx] = v

    # Combine total height map: base compacted soil/sand + pebbles + micro grit
    height_map = (
        0.25 * macro_variation +
        0.20 * mid_bed +
        0.48 * pebble_map +
        0.07 * fine_grit
    )
    # Normalize height map
    height_map = (height_map - height_map.min()) / (height_map.max() - height_map.min())

    # 3. Ambient Occlusion from height crevices
    # Crevices between pebbles are darker
    ao = np.clip((height_map - 0.2) / 0.8, 0, 1)
    ao = ao ** 0.55  # Soft curve

    # 4. Synthesize Photorealistic Albedo (Warm Beige / Soft Earthy Gravel)
    print("Generating natural warm beige albedo...")
    # Base palette:
    # Warm beige: (212, 196, 168)
    # Earthy ochre: (196, 178, 148)
    # Pale sandstone: (228, 218, 196)
    # River pebble grey/tan: (178, 166, 150)
    # Dark crevice soil: (128, 114, 94)

    c_warm_beige = np.array([218.0, 204.0, 176.0], dtype=np.float32)
    c_ochre      = np.array([198.0, 180.0, 146.0], dtype=np.float32)
    c_sandstone  = np.array([232.0, 222.0, 200.0], dtype=np.float32)
    c_grey_pebble= np.array([174.0, 164.0, 150.0], dtype=np.float32)
    c_crevice    = np.array([118.0, 106.0, 88.0], dtype=np.float32)

    # Blend based on pebble map and variations
    albedo = np.zeros((size, size, 3), dtype=np.float32)
    
    # Base sand/compacted earth bed
    for c in range(3):
        albedo[:, :, c] = (
            c_warm_beige[c] * 0.55 +
            c_ochre[c] * (0.45 * macro_variation) +
            c_sandstone[c] * (0.25 * mid_bed)
        )

    # Overlay pebble colors
    pebble_mask = np.clip(pebble_map * 2.2, 0, 1)[:, :, np.newaxis]
    # Random pebble color variation (using periodic noise)
    p_color_selector = periodic_noise(24, 24, octaves=2)
    
    pebble_color = np.zeros((size, size, 3), dtype=np.float32)
    for c in range(3):
        pebble_color[:, :, c] = np.where(
            p_color_selector < 0.35,
            c_sandstone[c] * 0.95 + 10.0 * fine_grit,
            np.where(
                p_color_selector < 0.70,
                c_warm_beige[c] * 0.98 + 8.0 * fine_grit,
                c_grey_pebble[c] * 1.02 + 8.0 * fine_grit
            )
        )

    albedo = albedo * (1.0 - pebble_mask) + pebble_color * pebble_mask

    # Crevice shading & ambient occlusion contact shadows
    crevice_blend = (1.0 - ao)  # shape (size, size)
    for c in range(3):
        albedo[:, :, c] = albedo[:, :, c] * (1.0 - crevice_blend * 0.45) + c_crevice[c] * (crevice_blend * 0.45)

    # Natural fine speckling
    albedo += (fine_grit[:, :, np.newaxis] - 0.5) * 12.0
    albedo = np.clip(albedo, 0, 255).astype(np.uint8)

    # 5. Normal Map from Height Map (Sobel gradient with periodic wrap)
    print("Computing tangent-space normal map...")
    # Periodic shifts for seamless edges
    dx = (np.roll(height_map, -1, axis=1) - np.roll(height_map, 1, axis=1)) * 0.5
    dy = (np.roll(height_map, -1, axis=0) - np.roll(height_map, 1, axis=0)) * 0.5

    # Scale strength of normal bump
    bump_strength = 6.5
    nx = -dx * bump_strength
    ny = -dy * bump_strength
    nz = np.ones_like(height_map)

    # Normalize vectors
    len_n = np.sqrt(nx**2 + ny**2 + nz**2) + 1e-8
    nx /= len_n
    ny /= len_n
    nz /= len_n

    # Convert to [0, 255] tangent space: R = Nx, G = Ny, B = Nz
    normal_rgb = np.zeros((size, size, 3), dtype=np.uint8)
    normal_rgb[:, :, 0] = np.clip((nx * 0.5 + 0.5) * 255.0, 0, 255).astype(np.uint8)
    normal_rgb[:, :, 1] = np.clip((ny * 0.5 + 0.5) * 255.0, 0, 255).astype(np.uint8)
    normal_rgb[:, :, 2] = np.clip((nz * 0.5 + 0.5) * 255.0, 0, 255).astype(np.uint8)

    # 6. Smoothness / Roughness Mask
    # Real gravel path is mostly matte (~0.15 - 0.22 smoothness), with polished pebbles slightly smoother (~0.28)
    smoothness = 0.14 + 0.12 * pebble_map + 0.04 * (1.0 - fine_grit)
    smoothness_img = np.clip(smoothness * 255.0, 0, 255).astype(np.uint8)

    # Save textures
    out_dir = r"D:\NeuroCompanion\Unity\NeuroCompanion\Assets\Textures\Environment\Pathway"
    albedo_path = os.path.join(out_dir, "Pathway_Gravel_Albedo.png")
    normal_path = os.path.join(out_dir, "Pathway_Gravel_Normal.png")
    smooth_path = os.path.join(out_dir, "Pathway_Gravel_Smoothness.png")

    Image.fromarray(albedo).save(albedo_path, quality=95)
    print(f"Saved Albedo: {albedo_path}")

    Image.fromarray(normal_rgb).save(normal_path)
    print(f"Saved Normal: {normal_path}")

    Image.fromarray(smoothness_img).save(smooth_path)
    print(f"Saved Smoothness: {smooth_path}")
    print("All pathway textures generated successfully!")

if __name__ == "__main__":
    generate_seamless_gravel(2048)
