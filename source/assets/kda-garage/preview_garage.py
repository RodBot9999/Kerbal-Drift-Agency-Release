"""Render a small software preview of the generated MU for visual inspection."""
from pathlib import Path
import sys
import argparse

import numpy as np
from PIL import Image, ImageDraw

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[2]
sys.path.insert(0, str(ROOT / "source/tools/vendor/io_object_mu"))
import mu

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--asset', type=Path, default=ROOT / "GameData/KDAGarageBuilding/Garage")
parser.add_argument('--output', type=Path, default=HERE / 'garage-preview.png')
args = parser.parse_args()
asset = args.asset
model = mu.Mu().read(str(asset / "garage.mu"))
textures = [np.asarray(Image.open(asset / (texture.name + ".png")).convert("RGB")) for texture in model.textures]
width, height = 1080, 760
sky = np.zeros((height, width, 3), dtype="uint8")
for y in range(height):
    sky[y] = (np.array((88, 137, 177)) * (1 - y / height) + np.array((168, 184, 187)) * y / height).astype("uint8")
sky[450:] = np.array((135, 144, 139), dtype="uint8")
canvas = Image.fromarray(sky)
draw = ImageDraw.Draw(canvas)
draw.ellipse((165, 574, 1005, 704), fill=(82, 94, 88))
color = np.asarray(canvas).copy()
depth = np.full((height, width), np.inf, dtype="float32")

camera = np.array((10.5, 7., 15.5))
target = np.array((0., 2.0, 0.))
forward = target - camera
forward /= np.linalg.norm(forward)
right = np.cross(forward, (0, 1, 0))
right /= np.linalg.norm(right)
up = np.cross(right, forward)
light = np.array((-.35, .80, .65))
light /= np.linalg.norm(light)


def project(vertices):
    offset = vertices - camera
    z = offset @ forward
    x = width * .50 + 850 * (offset @ right) / z
    y = height * .58 - 850 * (offset @ up) / z
    return np.stack((x, y, z), axis=1)


for obj in model.obj.children:
    if not hasattr(obj, "shared_mesh"):
        continue
    mesh = obj.shared_mesh
    vertices = np.asarray(mesh.verts, dtype="float32")[:, [0, 2, 1]]
    normals = np.asarray(mesh.normals, dtype="float32")[:, [0, 2, 1]]
    uvs = np.asarray(mesh.uvs, dtype="float32")
    screen = project(vertices)
    material = model.materials[obj.renderer.materials[0]]
    texture = textures[material.textureProperties["_MainTex"].index]
    th, tw = texture.shape[:2]
    for a, b, c in mesh.submeshes[0]:
        p = screen[[a, b, c]]
        if min(p[:, 2]) <= .05:
            continue
        x0 = max(0, int(np.floor(min(p[:, 0]))))
        x1 = min(width - 1, int(np.ceil(max(p[:, 0]))))
        y0 = max(0, int(np.floor(min(p[:, 1]))))
        y1 = min(height - 1, int(np.ceil(max(p[:, 1]))))
        if x1 < x0 or y1 < y0:
            continue
        denominator = (p[1, 1] - p[2, 1]) * (p[0, 0] - p[2, 0]) + (p[2, 0] - p[1, 0]) * (p[0, 1] - p[2, 1])
        if abs(denominator) < .0001:
            continue
        yy, xx = np.mgrid[y0:y1 + 1, x0:x1 + 1]
        u = ((p[1, 1] - p[2, 1]) * (xx - p[2, 0]) + (p[2, 0] - p[1, 0]) * (yy - p[2, 1])) / denominator
        v = ((p[2, 1] - p[0, 1]) * (xx - p[2, 0]) + (p[0, 0] - p[2, 0]) * (yy - p[2, 1])) / denominator
        w = 1 - u - v
        z = u * p[0, 2] + v * p[1, 2] + w * p[2, 2]
        region = depth[y0:y1 + 1, x0:x1 + 1]
        mask = (u >= 0) & (v >= 0) & (w >= 0) & (z < region)
        if not np.any(mask):
            continue
        uv = u[..., None] * uvs[a] + v[..., None] * uvs[b] + w[..., None] * uvs[c]
        tx = np.mod(uv[..., 0], 1)
        ty = np.mod(1 - uv[..., 1], 1)
        texel = texture[np.minimum(th - 1, (ty * th).astype(int)), np.minimum(tw - 1, (tx * tw).astype(int))]
        brightness = .56 + .43 * max(0, float(normals[a] @ light))
        shaded = np.clip(texel.astype("float32") * brightness, 0, 255).astype("uint8")
        color[y0:y1 + 1, x0:x1 + 1][mask] = shaded[mask]
        region[mask] = z[mask]

Image.fromarray(color).save(args.output)
print(args.output)
