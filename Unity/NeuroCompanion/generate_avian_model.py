import math
import os

out_dir = r"D:\NeuroCompanion\Unity\NeuroCompanion\Assets\Models\Environment\Birds"
tex_dir = r"D:\NeuroCompanion\Unity\NeuroCompanion\Assets\Textures\Environment\Birds"
os.makedirs(out_dir, exist_ok=True)
os.makedirs(tex_dir, exist_ok=True)

# ─────────────────────────────────────────────────────────────────────────────
# CONTINUOUS SEAMLESS S-CURVE AVIAN ANATOMY (Solid, Closed, Water-tight)
# ─────────────────────────────────────────────────────────────────────────────
# (z_center, y_center, rx, ry_top, ry_bot)
rings = [
    # 0-2: Distinct Beak
    ( 0.060, 0.067, 0.0010, 0.0010, 0.0010), # 0: Beak tip
    ( 0.053, 0.067, 0.0035, 0.0035, 0.0035), # 1: Mid bill
    ( 0.045, 0.068, 0.0065, 0.0060, 0.0055), # 2: Bill base / gape
    # 3-5: Rounded Head & Skull (Eyes on ring 4)
    ( 0.037, 0.070, 0.0120, 0.0105, 0.0090), # 3: Face / forehead
    ( 0.027, 0.074, 0.0160, 0.0145, 0.0125), # 4: Crown & skull (Eyes here!)
    ( 0.017, 0.067, 0.0145, 0.0135, 0.0120), # 5: Nape & upper throat
    # 6: Anatomical Neck Inward Dip
    ( 0.009, 0.058, 0.0140, 0.0125, 0.0140), # 6: Neck hollow / throat dip
    # 7-9: Swelling Breast & Deep Keel (Breast curves forward in Z!)
    ( 0.015, 0.048, 0.0195, 0.0140, 0.0210), # 7: Shoulder junction
    ( 0.019, 0.041, 0.0235, 0.0160, 0.0255), # 8: Upper breast (swells forward)
    ( 0.013, 0.035, 0.0265, 0.0175, 0.0270), # 9: Mid breast & deep keel
    # 10-12: Full Torso & Folded Wing Flanks
    ( 0.000, 0.031, 0.0275, 0.0175, 0.0255), # 10: Widest body point
    (-0.016, 0.027, 0.0250, 0.0160, 0.0225), # 11: Mid belly & flanks
    (-0.033, 0.023, 0.0210, 0.0140, 0.0185), # 12: Lower belly & rump
    # 13: Undertail Coverts / Vent
    (-0.049, 0.019, 0.0160, 0.0105, 0.0125), # 13: Undertail vent
    # 14-17: Flat Tail Retrices Fan (Angled downward)
    (-0.065, 0.015, 0.0140, 0.0060, 0.0060), # 14: Tail base
    (-0.082, 0.010, 0.0150, 0.0035, 0.0035), # 15: Mid tail fan
    (-0.100, 0.006, 0.0160, 0.0022, 0.0022), # 16: Pre-tip tail fan
    (-0.114, 0.002, 0.0150, 0.0012, 0.0012), # 17: Tail tip
]

sectors = 24
verts = []
uvs = []
normals = []
faces = []

# Front Apex Vertex (Closes beak tip completely)
front_apex = (0.0, 0.067, 0.062)
verts.append(front_apex)
uvs.append((0.5, 0.0))
normals.append((0.0, 0.0, 1.0))
front_apex_idx = 1

# Generate rings
ring_start_indices = []
for r_idx, (z_c, y_c, rx, ry_t, ry_b) in enumerate(rings):
    v = float(r_idx) / (len(rings) - 1)
    ring_start_indices.append(len(verts) + 1)
    for s in range(sectors + 1):
        u = float(s) / sectors
        theta = u * 2.0 * math.pi
        cos_t = math.cos(theta)
        sin_t = math.sin(theta)

        # Flank fold widening for folded wings on rings 8, 9, 10, 11
        rx_eff = rx
        if 8 <= r_idx <= 11:
            rx_eff += 0.0025 * (sin_t ** 2)

        x = rx_eff * sin_t
        y = y_c + (ry_t if cos_t >= 0 else ry_b) * cos_t
        z = z_c

        nx = sin_t
        ny = cos_t
        nz = 0.3 if r_idx <= 4 else (-0.2 if r_idx >= 12 else 0.0)
        len_n = math.sqrt(nx*nx + ny*ny + nz*nz)
        if len_n > 1e-6:
            nx /= len_n; ny /= len_n; nz /= len_n

        verts.append((x, y, z))
        uvs.append((u, v))
        normals.append((nx, ny, nz))

# Back Apex Vertex (Closes tail tip completely)
back_apex = (0.0, 0.002, -0.116)
verts.append(back_apex)
uvs.append((0.5, 1.0))
normals.append((0.0, 0.0, -1.0))
back_apex_idx = len(verts)

# 1. Front Cap Faces (Fan from front apex to ring 0)
stride = sectors + 1
r0_start = ring_start_indices[0]
for s in range(sectors):
    p0 = front_apex_idx
    p1 = r0_start + s
    p2 = r0_start + s + 1
    faces.append((p0, p1, p2))

# 2. Quad Body Faces between all consecutive rings
for r in range(len(rings) - 1):
    curr_start = ring_start_indices[r]
    next_start = ring_start_indices[r + 1]
    for s in range(sectors):
        p0 = curr_start + s
        p1 = curr_start + s + 1
        p2 = next_start + s + 1
        p3 = next_start + s
        faces.append((p0, p2, p1))
        faces.append((p0, p3, p2))

# 3. Back Cap Faces (Fan from last ring to back apex)
last_start = ring_start_indices[-1]
for s in range(sectors):
    p0 = back_apex_idx
    p1 = last_start + s + 1
    p2 = last_start + s
    faces.append((p0, p1, p2))

# Write closed OBJ file
obj_path = os.path.join(out_dir, "Avian_Bird_Body.obj")
with open(obj_path, "w") as f:
    f.write("# NeuroCompanion Seamless Closed S-Curve Avian Model\n")
    f.write("o Avian_Bird_Body\n")
    for x, y, z in verts:
        f.write(f"v {x:.5f} {y:.5f} {z:.5f}\n")
    for u, v in uvs:
        f.write(f"vt {u:.5f} {v:.5f}\n")
    for nx, ny, nz in normals:
        f.write(f"vn {nx:.5f} {ny:.5f} {nz:.5f}\n")
    f.write("s 1\n")
    for p0, p1, p2 in faces:
        f.write(f"f {p0}/{p0}/{p0} {p1}/{p1}/{p1} {p2}/{p2}/{p2}\n")

print(f"Created seamless closed avian OBJ: {obj_path} ({len(verts)} verts, {len(faces)} faces)")

# ─────────────────────────────────────────────────────────────────────────────
# 2. TEXTURE GENERATION (Matching 18-ring coordinates)
# ─────────────────────────────────────────────────────────────────────────────
def write_tga_bottom_up(filename, width, height, pixels):
    with open(filename, "wb") as f:
        header = bytearray(18)
        header[2] = 2  # uncompressed true-color
        header[12] = width & 0xFF
        header[13] = (width >> 8) & 0xFF
        header[14] = height & 0xFF
        header[15] = (height >> 8) & 0xFF
        header[16] = 24  # 24 bits
        header[17] = 0   # bottom-left origin
        f.write(header)
        raw = bytearray()
        for r, g, b in pixels:
            raw.append(int(max(0, min(255, b))))
            raw.append(int(max(0, min(255, g))))
            raw.append(int(max(0, min(255, r))))
        f.write(raw)
    print(f"Created TGA texture: {filename}")

W, H = 512, 512

def generate_texture(name, species):
    pixels = []
    for y in range(H):
        v = float(y) / (H - 1)
        # v ~ 0.0 - 0.14: Beak
        # v ~ 0.15 - 0.32: Head & Eyes (Ring 4 is at v = 4/17 = 0.235)
        # v ~ 0.33 - 0.40: Nape & Neck Dip (Dove collar)
        # v ~ 0.41 - 0.68: Breast, Folded Wings & Mantle
        # v ~ 0.69 - 0.78: Belly & Undertail Vent (Bulbul ruby vent)
        # v ~ 0.79 - 1.00: Tail Fan
        for x in range(W):
            u = float(x) / (W - 1)
            dist_back = min(u, 1.0 - u) * 2.0 # 0 at back, 1 at belly

            r, g, b = 130, 120, 110

            if species == "sparrow":
                if v < 0.14:
                    r, g, b = 42, 38, 34 # Dark bill
                elif v < 0.34:
                    if dist_back < 0.35:
                        r, g, b = 136, 140, 144 # Grey crown
                    elif dist_back > 0.65:
                        r, g, b = 24, 22, 20     # Black bib
                    else:
                        if 0.40 < dist_back < 0.60:
                            r, g, b = 158, 76, 38 # Chestnut post-ocular stripe
                        else:
                            r, g, b = 224, 220, 212 # Clean white cheek
                elif v < 0.74:
                    if dist_back < 0.58:
                        streak = math.sin(u * 44.0) * math.cos(v * 32.0)
                        if streak > 0.18:
                            r, g, b = 35, 24, 16 # Dark mantle streaks
                        else:
                            r, g, b = 172, 88, 44 # Warm rufous mantle
                        if 0.48 < v < 0.54 and 0.24 < dist_back < 0.50:
                            r, g, b = 242, 240, 235 # White wing bar
                    else:
                        r, g, b = 196, 190, 178 # Buff belly
                else:
                    r, g, b = 55, 44, 36 # Dark retrices

            elif species == "bulbul":
                if v < 0.14:
                    r, g, b = 24, 22, 22 # Black bill
                elif v < 0.34:
                    r, g, b = 22, 20, 19 # Glossy black head
                elif v < 0.72:
                    scallop = math.sin(u * 34.0) * math.sin(v * 38.0)
                    base = 48 + int(scallop * 10)
                    r, g, b = base, base - 2, base - 4 # Dark brown body
                elif v < 0.82:
                    if dist_back > 0.55:
                        r, g, b = 230, 20, 20 # BRILLIANT RUBY RED UNDERTAIL VENT!
                    else:
                        r, g, b = 42, 38, 36
                else:
                    if v > 0.94:
                        r, g, b = 242, 242, 242 # White tail tip
                    else:
                        r, g, b = 28, 26, 25

            elif species == "dove":
                if v < 0.14:
                    r, g, b = 42, 40, 38 # Slender dark bill
                elif 0.30 < v < 0.36 and dist_back < 0.65:
                    if 0.32 < v < 0.34:
                        r, g, b = 14, 14, 14 # Black collar
                    else:
                        r, g, b = 248, 248, 248 # White border
                elif v < 0.50:
                    r, g, b = 212, 200, 194 # Pinkish buff breast
                elif v < 0.80:
                    r, g, b = 198, 194, 188 # Pearl grey plumage
                else:
                    if dist_back > 0.50:
                        r, g, b = 238, 238, 234 # White outer tail
                    else:
                        r, g, b = 182, 178, 172

            elif species == "myna":
                if v < 0.14:
                    r, g, b = 252, 205, 15 # Yellow bill
                elif v < 0.36:
                    if 0.20 < v < 0.28 and 0.34 < dist_back < 0.60:
                        r, g, b = 255, 214, 16 # BRIGHT YELLOW EYE PATCH!
                    else:
                        r, g, b = 20, 18, 18   # Black hood
                elif v < 0.78:
                    if 0.50 < v < 0.60 and 0.28 < dist_back < 0.48:
                        r, g, b = 248, 248, 248 # White wing patch
                    else:
                        r, g, b = 108, 68, 44   # Chocolate brown body
                else:
                    if dist_back > 0.55:
                        r, g, b = 245, 245, 245 # White undertail
                    else:
                        if v > 0.94:
                            r, g, b = 242, 242, 242 # White tail tip
                        else:
                            r, g, b = 28, 24, 22

            elif species == "babbler":
                if v < 0.14:
                    r, g, b = 224, 208, 164 # Pale bill
                elif v < 0.80:
                    streak = math.sin(u * 44.0) * math.cos(v * 26.0)
                    base = 146 + int(streak * 16)
                    r, g, b = base, base - 10, base - 18 # Mottled plumage
                else:
                    r, g, b = 112, 102, 90 # Dark tail

            # Eye iris and pupil on ring 4 (v ~ 0.235, dist_back ~ 0.48)
            if 0.21 < v < 0.26 and 0.40 < dist_back < 0.56 and species != "myna":
                dv = (v - 0.235) / 0.022
                du = (dist_back - 0.48) / 0.07
                if dv*dv + du*du < 1.0:
                    if species == "dove":
                        r, g, b = 185, 24, 24   # Ruby-red iris
                    elif species == "babbler":
                        r, g, b = 242, 236, 188 # Pale staring iris
                    else:
                        r, g, b = 14, 14, 14    # Dark iris
                    if dv*dv + du*du < 0.42:
                        r, g, b = 8, 8, 8       # Pupil
                    if dv > 0.18 and du > 0.18:
                        r, g, b = 255, 255, 255 # Highlight

            pixels.append((r, g, b))

    tga_path = os.path.join(tex_dir, f"{name}.tga")
    write_tga_bottom_up(tga_path, W, H, pixels)

generate_texture("Sparrow_Texture", "sparrow")
generate_texture("Bulbul_Texture", "bulbul")
generate_texture("Dove_Texture", "dove")
generate_texture("Myna_Texture", "myna")
generate_texture("Babbler_Texture", "babbler")
print("Anatomical S-curve bird model and bottom-up aligned textures successfully generated!")