import os
import numpy as np
from PIL import Image

def gen_grass_blade_albedo(size=2048):
    """
    Generates a photorealistic crisp individual grass blade albedo texture.
    Each blade is a tall thin vertical strip with natural green variation,
    slight tip browning, and realistic blade-to-blade color variation.
    """
    print(f"Generating {size}x{size} real grass blade albedo...")
    np.random.seed(55)

    img = np.zeros((size, size, 3), dtype=np.float32)

    # --- Background: dark moist soil / sub-canopy shadow
    soil_dark = np.array([38.0, 48.0, 28.0])
    for c in range(3):
        img[:, :, c] = soil_dark[c]

    # --- Blade color palette (natural spring lawn)
    palettes = [
        np.array([72.0, 118.0, 46.0]),   # Rich mid-green
        np.array([84.0, 130.0, 52.0]),   # Bright fresh green
        np.array([62.0, 108.0, 42.0]),   # Deeper forest green
        np.array([96.0, 140.0, 58.0]),   # Yellow-green
        np.array([55.0,  95.0, 36.0]),   # Dark emerald
        np.array([105.0, 148.0, 68.0]),  # Pale lime
    ]

    # Tip color: dry/warm brown-gold at the very top
    tip_color = np.array([158.0, 148.0, 72.0])

    n_blades = 18000
    blade_x    = np.random.uniform(0, size, n_blades).astype(int)
    blade_w    = np.random.randint(2, 7, n_blades)       # 2-6 px wide
    blade_h    = np.random.randint(int(size*0.26), int(size*0.72), n_blades)  # 26% to 72% tall
    blade_col  = np.random.randint(0, len(palettes), n_blades)
    blade_lean = np.random.uniform(-0.14, 0.14, n_blades)  # horizontal lean per row
    blade_tip  = np.random.uniform(0.05, 0.18, n_blades)   # fraction showing tip browning

    # Ground level: blades start from bottom (y=size-1 going up)
    for i in range(n_blades):
        bx = blade_x[i]
        bw = blade_w[i]
        bh = blade_h[i]
        col = palettes[blade_col[i]].copy()
        lean = blade_lean[i]
        tip_frac = blade_tip[i]

        for row in range(bh):
            # y: bottom=size-1, top toward y=size-1-bh
            y = size - 1 - row
            if y < 0:
                continue

            # Lean: shift x as we go up
            x_shift = int(lean * row)
            xL = max(0, bx + x_shift)
            xR = min(size - 1, bx + x_shift + bw)

            # Color: natural darkening toward base, lightening midway, browning at tip
            blade_frac = row / bh  # 0 at base, 1 at tip

            if blade_frac > (1.0 - tip_frac):
                # Tip browning
                tip_blend = (blade_frac - (1.0 - tip_frac)) / tip_frac
                px_col = col * (1.0 - tip_blend) + tip_color * tip_blend
            elif blade_frac < 0.12:
                # Base darkening (sub-canopy)
                px_col = col * (0.55 + 0.45 * (blade_frac / 0.12))
            else:
                # Mid highlight - brightest at 55% height
                mid_boost = 1.0 + 0.12 * np.sin(blade_frac * np.pi)
                px_col = col * mid_boost

            px_col = np.clip(px_col, 0, 255)

            if xL < xR:
                img[y, xL:xR, :] = px_col

    # Add subtle ambient ground scatter
    noise = np.random.uniform(-6.0, 6.0, (size, size, 3)).astype(np.float32)
    img = np.clip(img + noise, 0, 255).astype(np.uint8)

    out_path = r"D:\NeuroCompanion\Unity\NeuroCompanion\Assets\Textures\Environment\Terrain\Grass_Natural_Albedo.png"
    Image.fromarray(img).save(out_path, quality=96)
    print(f"Saved grass blade albedo: {out_path}")


def gen_grass_normal(size=1024):
    """
    Generates a normal map matching the blade layout:
    blades angled outward from center with upward normals.
    """
    print(f"Generating {size}x{size} grass normal map...")
    np.random.seed(55)

    normal = np.zeros((size, size, 3), dtype=np.float32)
    # Base: pointing straight up (flat ground)
    normal[:, :, 0] = 128.0   # R = Nx neutral
    normal[:, :, 1] = 128.0   # G = Ny neutral
    normal[:, :, 2] = 255.0   # B = Nz up

    n_blades = 10000
    blade_x    = np.random.uniform(0, size, n_blades).astype(int)
    blade_w    = np.random.randint(2, 6, n_blades)
    blade_h    = np.random.randint(int(size*0.26), int(size*0.72), n_blades)
    blade_lean = np.random.uniform(-0.12, 0.12, n_blades)

    for i in range(n_blades):
        bx = blade_x[i]
        bw = blade_w[i]
        bh = blade_h[i]
        lean = blade_lean[i]

        for row in range(bh):
            y = size - 1 - row
            if y < 0:
                continue

            blade_frac = row / bh
            x_shift = int(lean * row)
            xL = max(0, bx + x_shift)
            xR = min(size - 1, bx + x_shift + bw)

            # Blade normal: angled slightly toward camera (Ny tilted forward)
            nx = 128 + int(lean * 60)
            ny = 128 - int(blade_frac * 30)  # Tips angle forward
            nz = 220

            if xL < xR:
                normal[y, xL:xR, 0] = nx
                normal[y, xL:xR, 1] = ny
                normal[y, xL:xR, 2] = nz

    out_path = r"D:\NeuroCompanion\Unity\NeuroCompanion\Assets\Textures\Environment\Terrain\Grass_Natural_Normal.png"
    Image.fromarray(normal.astype(np.uint8)).save(out_path)
    print(f"Saved grass normal map: {out_path}")


if __name__ == "__main__":
    gen_grass_blade_albedo(2048)
    gen_grass_normal(1024)
    print("All grass textures done!")
