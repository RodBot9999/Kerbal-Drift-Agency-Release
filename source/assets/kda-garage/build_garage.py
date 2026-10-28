"""Build the original KDA garage exterior for Kerbal Konstructs.

All geometry and texture pixels are generated here. No third-party model or
texture is read. The catalog name and front plane match existing placements.
"""
from pathlib import Path
import json
import sys
import argparse

import numpy as np
from PIL import Image, ImageDraw

SOURCE = Path(__file__).resolve()
ROOT = SOURCE.parents[3]
sys.path.insert(0, str(ROOT / "source/tools/vendor/io_object_mu"))
import mu

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--output', type=Path, default=ROOT / "GameData/KDAGarageBuilding/Garage")
args = parser.parse_args()
OUT = args.output.resolve()
OUT.mkdir(parents=True, exist_ok=True)
RNG = np.random.default_rng(22042)


def save_texture(name, array):
    Image.fromarray(np.clip(array, 0, 255).astype("uint8"), "RGB").save(OUT / (name + ".png"))


def noise_texture(base, spread=8):
    small = RNG.normal(0, spread, (64, 64)).astype("float32")
    broad = np.asarray(Image.fromarray(small, "F").resize((512, 512), Image.Resampling.BICUBIC))
    grain = RNG.normal(0, spread * .45, (512, 512))
    return np.array(base, dtype="float32")[None, None, :] + broad[:, :, None] + grain[:, :, None]


concrete = noise_texture((126, 139, 145), 7)
for x in (0, 255, 511):
    concrete[:, max(0, x - 2):x + 2] *= .68
for y in (0, 255, 511):
    concrete[max(0, y - 2):y + 2] *= .72
save_texture("kda_concrete", concrete)

shutter = noise_texture((37, 55, 66), 3)
for y in range(0, 512, 28):
    shutter[y:y + 3] *= 1.35
    shutter[y + 3:y + 7] *= .72
    shutter[y + 23:y + 27] *= .82
save_texture("kda_shutter", shutter)

metal = noise_texture((42, 56, 65), 3)
metal[:, ::7] += np.array([6, 8, 8])
save_texture("kda_metal", metal)

roof = noise_texture((75, 84, 86), 6)
for x in range(0, 512, 50):
    roof[:, x:x + 3] *= .63
    roof[:, x + 3:x + 7] *= 1.16
save_texture("kda_roof", roof)

orange = noise_texture((207, 97, 39), 2)
save_texture("kda_orange", orange)

sign = Image.new("RGB", (512, 256), (18, 29, 39))
draw = ImageDraw.Draw(sign)
draw.rectangle((10, 10, 501, 245), outline=(84, 105, 110), width=5)
draw.rectangle((30, 205, 482, 215), fill=(224, 104, 43))
ink = (234, 239, 231)
draw.line([(75, 65), (75, 179)], fill=ink, width=20)
draw.line([(145, 65), (78, 123), (148, 179)], fill=ink, width=20, joint="curve")
draw.line([(193, 65), (193, 179), (252, 179), (279, 151), (279, 93), (252, 65), (193, 65)], fill=ink, width=20, joint="curve")
draw.line([(325, 179), (364, 65), (389, 65), (428, 179)], fill=ink, width=20, joint="curve")
draw.line([(339, 139), (415, 139)], fill=ink, width=17)
sign.save(OUT / "kda_sign.png")

texture_names = ["kda_concrete", "kda_shutter", "kda_metal", "kda_roof", "kda_orange", "kda_sign"]
textures = []
materials = []
for name in texture_names:
    tex = mu.MuTexture()
    tex.name = name
    tex.type = 0
    textures.append(tex)
    mat = mu.MuMaterial()
    mat.name = name
    mat.shaderName = "KSP/Diffuse"
    mat.colorProperties["_Color"] = (1, 1, 1, 1)
    binding = mu.MuMatTex()
    binding.index = len(textures) - 1
    binding.scale = (1, 1)
    binding.offset = (0, 0)
    mat.textureProperties["_MainTex"] = binding
    materials.append(mat)


def node(name):
    obj = mu.MuObject()
    obj.transform = mu.MuTransform()
    obj.transform.name = name
    obj.transform.localPosition = (0, 0, 0)
    obj.transform.localRotation = (1, 0, 0, 0)
    obj.transform.localScale = (1, 1, 1)
    obj.tag_and_layer = mu.MuTagLayer()
    obj.tag_and_layer.tag = "Untagged"
    obj.tag_and_layer.layer = 0
    return obj


root = node("KDAGarage")
faces = [
    ((0, 1, 2, 3), (0, 0, 1), (0, 1)),
    ((4, 7, 6, 5), (0, 0, -1), (0, 1)),
    ((3, 2, 6, 7), (0, 1, 0), (0, 2)),
    ((0, 4, 5, 1), (0, -1, 0), (0, 2)),
    ((1, 5, 6, 2), (1, 0, 0), (2, 1)),
    ((0, 3, 7, 4), (-1, 0, 0), (2, 1)),
]


def box(name, center, size, material, tile=1.7):
    cx, cy, cz = center
    sx, sy, sz = (v / 2 for v in size)
    corners = [
        (cx - sx, cy - sy, cz + sz), (cx + sx, cy - sy, cz + sz),
        (cx + sx, cy + sy, cz + sz), (cx - sx, cy + sy, cz + sz),
        (cx - sx, cy - sy, cz - sz), (cx + sx, cy - sy, cz - sz),
        (cx + sx, cy + sy, cz - sz), (cx - sx, cy + sy, cz - sz),
    ]
    verts, normals, uvs, triangles = [], [], [], []
    for order, normal, axes in faces:
        points = [corners[i] for i in order]
        origin = points[0]
        start = len(verts)
        for point in points:
            verts.append(point)
            normals.append(normal)
            if material == 5 and normal == (0, 0, 1):
                uvs.append(((point[0] - (cx - sx)) / size[0],
                            (point[1] - (cy - sy)) / size[1]))
            else:
                uvs.append(((point[axes[0]] - origin[axes[0]]) / tile,
                            (point[axes[1]] - origin[axes[1]]) / tile))
        triangles.extend([(start, start + 1, start + 2), (start, start + 2, start + 3)])
    obj = node(name)
    mesh = mu.MuMesh()
    mesh.verts = verts
    mesh.normals = normals
    mesh.uvs = uvs
    mesh.submeshes = [triangles]
    obj.shared_mesh = mesh
    obj.renderer = mu.MuRenderer()
    obj.renderer.materials = [material]
    root.children.append(obj)


def collision(name, center, size):
    obj = node(name)
    obj.collider = mu.MuColliderBox(True)
    obj.collider.isTrigger = 0
    obj.collider.center = center
    obj.collider.size = size
    root.children.append(obj)


# The front remains at +5.65 m because the garage's return/launch logic uses
# that plane. The closed door is recessed by 24 cm inside the concrete frame.
box("left wall", (-3.49, 2.05, 0), (.22, 4.10, 11.16), 0)
box("right wall", (3.49, 2.05, 0), (.22, 4.10, 11.16), 0)
box("back wall", (0, 2.05, -5.53), (7.16, 4.10, .24), 0)
box("front left pier", (-3.09, 2.05, 5.53), (.98, 4.10, .24), 0)
box("front right pier", (3.09, 2.05, 5.53), (.98, 4.10, .24), 0)
box("front lintel", (0, 3.70, 5.53), (5.25, .80, .24), 0)
box("foundation", (0, .06, 0), (7.22, .12, 11.22), 2)
box("roof cap", (0, 4.17, 0), (7.42, .18, 11.43), 3)
box("front fascia", (0, 4.10, 5.70), (7.50, .27, .25), 2)
box("door recess", (0, 1.65, 5.40), (5.31, 3.25, .15), 2)
box("main shutter", (0, 1.60, 5.48), (5.07, 3.05, .08), 1, tile=1.2)
for side in (-1, 1):
    box("door rail " + str(side), (side * 2.64, 1.62, 5.64), (.12, 3.35, .18), 2)
    box("corner upright " + str(side), (side * 3.58, 2.10, 5.20), (.11, 4.20, .16), 2)
    box("hazard stripe " + str(side), (side * 2.83, .40, 5.68), (.13, .72, .06), 4)
box("shutter head", (0, 3.24, 5.63), (5.42, .15, .20), 2)
box("KDA sign", (0, 3.69, 5.684), (2.8, .56, .055), 5, tile=2.8)
box("sign left accent", (-1.48, 3.69, 5.69), (.08, .60, .07), 4)
box("sign right accent", (1.48, 3.69, 5.69), (.08, .60, .07), 4)
for side in (-1, 1):
    box("side dark base " + str(side), (side * 3.61, .33, 0), (.035, .62, 10.7), 2)
    for z in (-3.4, 0, 3.4):
        box("side seam %s %s" % (side, z), (side * 3.615, 2.25, z), (.035, 3.1, .065), 2)

# A low extractor housing on the roof, with a deep mouth and four louvers.
box("extractor plinth", (0, 4.38, 1.55), (1.94, .27, 1.45), 2)
box("extractor housing", (0, 4.85, 1.55), (1.72, .72, 1.23), 2)
box("extractor dark mouth", (0, 4.86, 2.20), (1.48, .51, .055), 1)
for y in (4.68, 4.80, 4.92, 5.04):
    box("extractor louver %.2f" % y, (0, y, 2.245), (1.54, .045, .11), 3)
box("extractor cap", (0, 5.25, 1.57), (1.94, .13, 1.50), 3)

collision("solid front and door", (0, 2.05, 5.53), (7.16, 4.10, .24))
collision("solid back", (0, 2.05, -5.53), (7.16, 4.10, .24))
collision("solid left", (-3.49, 2.05, 0), (.22, 4.10, 11.16))
collision("solid right", (3.49, 2.05, 0), (.22, 4.10, 11.16))
collision("solid roof", (0, 4.17, 0), (7.42, .18, 11.43))
collision("solid foundation", (0, .06, 0), (7.22, .12, 11.22))

model = mu.Mu("KDAGarage")
# Geometry above uses Unity's Y-up, Z-forward convention. The MU writer's
# public API expects Blender vectors, and swaps Y/Z and triangle winding.
# Convert once at the boundary, including colliders and the entrance marker.
def writer_vector(vector):
    return vector[0], vector[2], vector[1]

for obj in root.children:
    if hasattr(obj, 'shared_mesh'):
        mesh = obj.shared_mesh
        mesh.verts = [writer_vector(v) for v in mesh.verts]
        mesh.normals = [writer_vector(n) for n in mesh.normals]
        mesh.submeshes = [[(a, c, b) for a, b, c in sub] for sub in mesh.submeshes]
    if hasattr(obj, 'collider'):
        obj.collider.center = writer_vector(obj.collider.center)
        obj.collider.size = writer_vector(obj.collider.size)
entrance = node('KDAEntrance')
entrance.transform.localPosition = writer_vector((0, 0, 5.65))
root.children.append(entrance)
model.obj = root
model.materials = materials
model.textures = textures
model.write(str(OUT / "garage.mu"))
roundtrip = mu.Mu().read(str(OUT / "garage.mu"))
render_nodes = [o for o in roundtrip.obj.children if hasattr(o, "shared_mesh")]
collider_nodes = [o for o in roundtrip.obj.children if hasattr(o, "collider")]
assert len(render_nodes) == 37 and len(collider_nodes) == 6
assert len(roundtrip.materials) == len(roundtrip.textures) == 6
assert all((OUT / (texture.name + ".png")).is_file() for texture in roundtrip.textures)
for obj in render_nodes:
    vertices = np.asarray(obj.shared_mesh.verts)
    normals = np.asarray(obj.shared_mesh.normals)
    for a, b, c in obj.shared_mesh.submeshes[0]:
        assert np.dot(np.cross(vertices[b] - vertices[a], vertices[c] - vertices[a]), normals[a]) > 0
geometry = np.vstack([o.shared_mesh.verts for o in render_nodes])[:, [0, 2, 1]]
assert geometry.min(axis=0)[1] >= -.06  # door-rail feet extend 5.5 cm below the slab
assert np.allclose(next(o for o in roundtrip.obj.children if o.transform.name == 'KDAEntrance').transform.localPosition, (0, 5.65, 0))
dimensions = (geometry.max(axis=0) - geometry.min(axis=0)).round(3).tolist()
report = {
    "provenance": "Original procedural KDA geometry and textures generated by this script; no external model or image inputs",
    "dimensions_metres": dimensions,
    "front_plane_z_metres": 5.65,
    "unity_axes": "Y up, Z outward through the garage door",
    "render_meshes": len(render_nodes),
    "triangles": sum(len(o.shared_mesh.submeshes[0]) for o in render_nodes),
    "colliders": len(collider_nodes),
    "textures": [name + ".png" for name in texture_names],
}
(OUT / "build-report.json").write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
if OUT == (ROOT / "GameData/KDAGarageBuilding/Garage").resolve():
    (SOURCE.parent / "build-report.json").write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
print(json.dumps(report, indent=2))
