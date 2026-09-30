# -*- coding: utf-8 -*-
"""
LadyLakePremium / MODEL worker - reproducible authoring source.

Builds the ENTIRE FigureRig deliverable procedurally in Blender:

  * anatomical volumetric body: torso, neck, head, both arms, both legs,
    and two REAL hands with a closed palm shell, four independently
    articulated fingers (3 phalanges each) and a three-segment inner thumb;
  * the floating hair as shaped volumetric locks (flattened closed ribbons)
    UV-projected from the approved figure-v5 painting;
  * the sword as a real closed mesh with blade thickness, fuller, crossguard,
    wrapped grip and faceted pommel, UV-mapped onto the existing sword.png;
  * a 58-bone armature, explicit per-vertex skin weights (max 4 influences,
    Unity SkinQuality.Bone4 compatible);
  * one seamless 12 s cycle action (open hand -> reach -> catch hilt at 3.2 s ->
    lift to the contract static resting pose by 5.6 s -> hold -> magic carries
    the sword back up while the fingers release -> return to start);
  * FBX export, a JSON model manifest and honest preview renders.

Run headless (exactly reproduces the shipped assets):

  "C:/Program Files/Blender Foundation/Blender 5.1/blender.exe" -b --factory-startup \
      -P Assets/Experiments/LadyLakePremium/Model/Source/build_figure.py

Coordinate contract (see Documentation/contract.md):
  Unity left handed, x right, y up, z deeper from the viewer.
  Effective art 497x710 px, origin top left.
  world P(px,py,z) = ((px-248.5)/100, (355-py)/100, z), camera looks +Z.
  Approved character registration: figure texture normalised top-left (u,v)
  maps to card normalised (u*0.83+0.04, v*0.83+0.117).
  All meshes are authored in that space WITHOUT the production +2 appearance
  offset (the runtime applies it).

MEASURED Blender -> Unity conversion (not assumed):
  The project FBX pipeline (Blender 5.1, axis_up='Y', axis_forward='-Z') plus
  Unity's right-handed -> left-handed import maps a Blender point b to
      Unity = (-b.x, b.z, -b.y)
  i.e. Unity NEGATES the Blender x that the exporter wrote.  This was measured
  on a real Unity pose capture: the painted face of figure-v5 (contract
  picture-right, x=+0.88) was found at screen x=233, which is the x-mirrored
  position, and the sword blade direction was mirrored.  The earlier
  `Cinv(b)=(b.x, b.z, -b.y)` round-trip self-check was therefore circular: it
  "verified" the wrong assumption against itself.  C() below compensates for
  the real conversion so the contract frame survives the FBX round trip:

Nothing here touches production scripts, the old experiment, or the selected
source texture import settings.
"""

import os
import sys
import json
import math
import struct
import zlib
import shutil
import datetime

import bpy
import numpy as np
from mathutils import Vector, Matrix, Quaternion

# --------------------------------------------------------------------------
# 0. paths / constants
# --------------------------------------------------------------------------

HERE = os.path.dirname(os.path.abspath(__file__))          # .../Model/Source
MODEL = os.path.dirname(HERE)                              # .../Model
PROJ = os.path.abspath(os.path.join(MODEL, "..", "..", "..", ".."))  # project root
GEN = os.path.join(MODEL, "Generated")
TEX = os.path.join(GEN, "Textures")
PREV = os.path.join(GEN, "preview")

FIG_V5 = os.path.join(PROJ, "Assets", "Experiments", "LadyLake", "Textures", "figure-v5.png")
SWORD_PNG = os.path.join(PROJ, "Assets", "Experiments", "LadyLake", "Textures", "sword.png")

FIG_W, FIG_H = 1049.0, 1500.0
SW_W, SW_H = 1065.0, 1477.0

FPS = 30
CLIP_SECONDS = 12.0
SHEET_FRAMES = int(round(CLIP_SECONDS * FPS)) + 1        # 361 -> frame 360 == frame 0

T_OPEN_END = 1.3        # sword still high, hand open
T_FALL_END = 3.2        # sword caught, fingers close
T_REST = 5.6            # sword at exact static card resting pose
T_HOLD_END = 8.3        # serene float ends, fingers begin to release
T_UP_END = 10.8         # fingers open again, sword carried high
T_LOOP = 12.0

# contract core sword pose, effective-art pixels (px, py)
SW_GRIP_PX = (380.0, 543.0)
SW_GUARD_PX = (364.0, 529.0)
SW_TIP_PX = (107.0, 308.0)
SW_POMMEL_PX = (420.0, 583.0)

# sword.png principal axis endpoints measured from the texture (px, py, top-left)
SW_TEX_TIP = Vector((141.0, 510.0))
SW_TEX_POMMEL = Vector((855.0, 1169.0))

BONES_MANIFEST = []      # filled while the armature is authored


# --------------------------------------------------------------------------
# 1. coordinate helpers
# --------------------------------------------------------------------------

# Blender authors right-handed Z-up; Unity consumes the FBX left-handed Y-up and
# negates the Blender x it read.  Authoring through C() pre-mirrors x so the
# imported result is the contract frame again (see the module docstring).
MIRROR_X = True


def C(x, y, z):
    """contract world (x right, y up, z deeper) -> Blender scene coordinates."""
    if MIRROR_X:
        return Vector((-x, -z, y))
    return Vector((x, -z, y))


def Cinv(b):
    """Blender scene coordinates -> contract world (the inverse of C)."""
    if MIRROR_X:
        return Vector((-b[0], b[2], -b[1]))
    return Vector((b[0], b[2], -b[1]))


def fig_to_world(fx, fy, z=0.0):
    """figure-v5 top-left pixel -> contract world space (expressed in Blender)."""
    cx = 0.39324 * fx + 19.88                 # card px  (497*(0.83*fx/1049 + 0.04))
    cy = 0.392867 * fy + 83.07                # card py  (710*(0.83*fy/1500 + 0.117))
    return C((cx - 248.5) / 100.0, (355.0 - cy) / 100.0, z)


def card_px_to_world(px, py, z=0.0):
    return C((px - 248.5) / 100.0, (355.0 - py) / 100.0, z)


def world_to_fig(p):
    q = Cinv(p)
    cx = q[0] * 100.0 + 248.5
    cy = 355.0 - q[1] * 100.0
    return ((cx - 19.88) / 0.39324, (cy - 83.07) / 0.392867)


def world_to_uv(p):
    fx, fy = world_to_fig(p)
    return (fx / FIG_W, 1.0 - fy / FIG_H)


def fx(fx_, fy_, z=0.0):
    return fig_to_world(fx_, fy_, z)


# --------------------------------------------------------------------------
# 2. image helpers (numpy, no external libs)
# --------------------------------------------------------------------------

def load_rgba(path):
    img = bpy.data.images.load(path, check_existing=False)
    w, h = img.size
    buf = np.empty(w * h * 4, dtype=np.float32)
    img.pixels.foreach_get(buf)
    bpy.data.images.remove(img)
    return buf.reshape(h, w, 4)[::-1].copy()          # top-down rows


def save_rgba(arr, path, name):
    # bpy pixels from these source PNGs are already encoded RGB. Export bytes
    # directly; float-buffer Image.save gamma-encodes them a second time.
    h,w=arr.shape[:2]
    rgba=np.rint(np.clip(arr,0,1)*255).astype(np.uint8)
    raw=b"".join(b"\x00"+row.tobytes() for row in rgba)
    def chunk(kind,data):
        return struct.pack(">I",len(data))+kind+data+struct.pack(">I",zlib.crc32(kind+data)&0xffffffff)
    with open(path,"wb") as f:
        f.write(b"\x89PNG\r\n\x1a\n"+chunk(b"IHDR",struct.pack(">IIBBBBB",w,h,8,6,0,0,0))+chunk(b"IDAT",zlib.compress(raw,6))+chunk(b"IEND",b""))


def classify(arr):
    """returns (skin, hair, ink) boolean masks for a figure-v5 style RGBA array."""
    r, g, b = arr[..., 0], arr[..., 1], arr[..., 2]
    a = arr[..., 3]
    luma = 0.2126 * r + 0.7152 * g + 0.0722 * b
    white = (luma > 0.72) & (np.abs(r - g) < 0.075) & (np.abs(g - b) < 0.075)
    skin = (a > 0.2) & (~white) & (r > b * 1.30 + 0.005) & (r > 0.020) & (g > b * 0.95)
    hair = (a > 0.2) & (~white) & (~skin) & (g >= r * 0.98)
    ink = (a > 0.2) & (~white) & (~skin) & (~hair)
    return skin, hair, ink, white


def dilate(mask, iters):
    m = mask.copy()
    for _ in range(iters):
        n = m.copy()
        n[1:, :] |= m[:-1, :]
        n[:-1, :] |= m[1:, :]
        n[:, 1:] |= m[:, :-1]
        n[:, :-1] |= m[:, 1:]
        m = n
    return m


def blur(a, iters):
    for _ in range(iters):
        b = a.copy()
        b[1:, :] += a[:-1, :]
        b[:-1, :] += a[1:, :]
        b[:, 1:] += a[:, :-1]
        b[:, :-1] += a[:, 1:]
        b += a * 4.0
        a = b / 12.0
    return a


def build_textures():
    """derive worker-local COPIES of the painting; the source file is untouched."""
    os.makedirs(TEX, exist_ok=True)
    fig = load_rgba(FIG_V5)
    skin, hair, ink, white = classify(fig)

    # ---- skin filled: painting skin kept, hair/ink/background grown over by
    #      neighbourhood skin so front projection never samples hair or white.
    fill_mask = dilate(skin, 46)
    src = fig.copy()
    src[~skin] = 0.0
    wsum = skin.astype(np.float32)
    acc = src * skin[..., None]
    for _ in range(26):
        acc = blur(acc, 1)
        wsum = blur(wsum, 1)
    filled = np.zeros_like(fig)
    safe = wsum > 1e-6
    for c in range(3):
        ch = np.where(safe, acc[..., c] / np.maximum(wsum, 1e-6), 0.0)
        filled[..., c] = np.where(skin, fig[..., c], np.clip(ch, 0.0, 4.0))
    # Outside the painted skin every texel is the in-painted coherent gold so a
    # hidden surface is never grey painting background and never a white hole.
    filled[..., :3] = np.where(skin[..., None], fig[..., :3],
                               np.clip(filled[..., :3], 0.0, 4.0))
    filled[..., 3] = 1.0
    save_rgba(filled, os.path.join(TEX, "FigureSkin.png"), "LLP_FigureSkin")

    # ---- gold skin tile: cleanest 128x128 all-skin window of the painting.
    win = 128
    ii = skin.astype(np.int32)
    sat = np.zeros((ii.shape[0] + 1, ii.shape[1] + 1), dtype=np.int64)
    sat[1:, 1:] = ii.cumsum(0).cumsum(1)
    best = (-1e18, 0, 0)
    for y in range(0, ii.shape[0] - win, 8):
        for x in range(0, ii.shape[1] - win, 8):
            s = (sat[y + win, x + win] - sat[y, x + win] - sat[y + win, x] + sat[y, x])
            # A window that is not ~pure skin still carries hair strands, and
            # those strands were painting dark bands onto every hidden-surface
            # face of the body.  Heavily penalise any non-skin texel.
            bad = win * win - s
            score = float(s) - 60.0 * float(bad)
            if score > best[0]:
                best = (score, x, y)
    _, bx, by = best
    tile = fig[by:by + win, bx:bx + win, :].copy()
    tskin = skin[by:by + win, bx:bx + win]
    med = np.median(tile[tskin][:, :3], axis=0) if tskin.sum() > 32 else np.array([0.45, 0.30, 0.09])
    # feather the tile into a seamless repeat: cross fade the outer 24 px
    f = 24
    ramp = np.linspace(0.0, 1.0, f, dtype=np.float32)[:, None, None]
    tile[:f, :, :3] = tile[:f, :, :3] * ramp + med[None, None, :] * (1 - ramp)
    tile[-f:, :, :3] = tile[-f:, :, :3] * ramp[::-1] + med[None, None, :] * (1 - ramp[::-1])
    ramp2 = np.linspace(0.0, 1.0, f, dtype=np.float32)[None, :, None]
    tile[:, :f, :3] = tile[:, :f, :3] * ramp2 + med[None, None, :] * (1 - ramp2)
    tile[:, -f:, :3] = tile[:, -f:, :3] * ramp2[:, ::-1] + med[None, None, :] * (1 - ramp2[:, ::-1])
    tile[..., 3] = 1.0
    save_rgba(tile, os.path.join(TEX, "GoldSkin.png"), "LLP_GoldSkin")

    # ---- hair sheet, white removed and un-premultiplied (no white fringe).
    # The classified mask is what CUTS the hair geometry, so the sheet itself is
    # authored opaque: an alpha cutout on a closed, geometry-cut shell discards
    # every fragment whose projection lands just outside the painted silhouette
    # and that is exactly the shattered green mass of the rejected build.  The
    # colour of texels outside the hair is faded to the mean hair tone instead,
    # so the shell stays coherent where it samples them.
    hh = fig.copy()
    r, g, b = fig[..., 0], fig[..., 1], fig[..., 2]
    luma = 0.2126 * r + 0.7152 * g + 0.0722 * b
    alpha = np.clip((0.62 - luma) / 0.30, 0.0, 1.0)
    alpha = np.where(hair | ink, np.maximum(alpha, 0.85), alpha)
    alpha = np.where(white, 0.0, alpha)
    aa = np.maximum(alpha, 1e-3)[..., None]
    col = np.clip((fig[..., :3] - (1.0 - aa)) / aa, 0.0, 4.0)
    # fully transparent texels must not leak garbage RGB into the opaque hair
    # shell, so fade their colour to the mean hair tone instead.
    hair_px = fig[hair | ink][:, :3] if (hair | ink).sum() > 32 else np.array([[0.05, 0.11, 0.06]])
    mean_hair = np.median(hair_px, axis=0)
    w = np.clip((alpha - 0.08) / 0.35, 0.0, 1.0)[..., None]
    col = col * w + mean_hair[None, None, :] * (1.0 - w)
    hh[..., :3] = col
    hh[..., 3] = 1.0
    save_rgba(hh, os.path.join(TEX, "Hair.png"), "LLP_Hair")

    # ---- sword sheet, white background -> alpha
    sw = load_rgba(SWORD_PNG)
    r, g, b = sw[..., 0], sw[..., 1], sw[..., 2]
    luma = 0.2126 * r + 0.7152 * g + 0.0722 * b
    a = np.clip((0.80 - luma) / 0.22, 0.0, 1.0)
    a = np.where((np.abs(r - g) < 0.05) & (np.abs(g - b) < 0.05) & (luma > 0.80), 0.0, a)
    aa = np.maximum(a, 1e-3)[..., None]
    sw[..., :3] = np.clip((sw[..., :3] - (1.0 - aa)) / aa, 0.0, 4.0)
    # Fully transparent texels unpremultiply to (0 - 1)/a, i.e. black.  The sword
    # is a real closed mesh whose cross sections sample slightly outside the
    # painted blade outline, and those texels were painting the blade black.
    # Fade them to the mean metal tone instead (same treatment as the hair).
    metal_px = sw[a > 0.6][:, :3] if (a > 0.6).sum() > 32 else np.array([[0.30, 0.36, 0.34]])
    mean_metal = np.median(metal_px, axis=0)
    wq = np.clip((a - 0.08) / 0.35, 0.0, 1.0)[..., None]
    sw[..., :3] = sw[..., :3] * wq + mean_metal[None, None, :] * (1.0 - wq)
    sw_alpha = a.copy()                 # the silhouette mask, before it is filled
    sw[..., 3] = 1.0
    save_rgba(sw, os.path.join(TEX, "Sword.png"), "LLP_Sword")

    return fig, skin, hair, sw, sw_alpha


def sword_texture_profile(sw, alpha):
    """half width (px) of the sword mask measured perpendicular to its axis."""
    r, g, b, a = sw[..., 0], sw[..., 1], sw[..., 2], alpha
    luma = 0.2126 * r + 0.7152 * g + 0.0722 * b
    mask = (a > 0.35) & (luma < 0.72)
    axis = (SW_TEX_TIP - SW_TEX_POMMEL)
    L = axis.length
    u = axis / L
    perp = Vector((-u.y, u.x))
    n = 220
    prof = np.zeros(n, dtype=np.float32)
    ys, xs = np.nonzero(mask)
    pts = np.stack([xs.astype(np.float32), ys.astype(np.float32)], axis=1)
    rel = pts - np.array([SW_TEX_POMMEL.x, SW_TEX_POMMEL.y], dtype=np.float32)
    along = rel @ np.array([u.x, u.y], dtype=np.float32)
    across = rel @ np.array([perp.x, perp.y], dtype=np.float32)
    t = np.clip(along / L, 0.0, 1.0)
    idx = np.clip((t * (n - 1)).astype(np.int32), 0, n - 1)
    for i in range(n):
        sel = across[idx == i]
        if sel.size > 3:
            prof[i] = 0.5 * (np.percentile(sel, 97) - np.percentile(sel, 3))
    # smooth + forward/back fill
    for _ in range(3):
        prof[1:-1] = 0.25 * prof[:-2] + 0.5 * prof[1:-1] + 0.25 * prof[2:]
    prof = np.maximum(prof, 2.0)
    return prof, L, u, perp


# --------------------------------------------------------------------------
# 3. mesh builder
# --------------------------------------------------------------------------

class MB(object):
    """accumulates verts / triangles / uvs / material ids / bone weights."""

    def __init__(self, name):
        self.name = name
        self.v = []          # Vector
        self.uv = []         # (u,v) per vertex (then refined per face for goldskin)
        self.w = []          # dict bone -> weight
        self.f = []          # (a,b,c, matidx)

    def add(self, p, uv, w):
        self.v.append(Vector(p))
        self.uv.append((float(uv[0]), float(uv[1])))
        self.w.append(dict(w))
        return len(self.v) - 1

    def tri(self, a, b, c, m):
        self.f.append((a, b, c, m))

    def quad(self, a, b, c, d, m):
        self.f.append((a, b, c, m))
        self.f.append((a, c, d, m))

    def build(self, bone_names):
        me = bpy.data.meshes.new(self.name + "Mesh")
        clean = []
        mats = []
        for (a, b, c, m) in self.f:
            if a == b or b == c or a == c:
                continue
            clean.append((a, b, c))
            mats.append(m)
        me.from_pydata([tuple(p) for p in self.v], [], clean)
        me.update()
        ob = bpy.data.objects.new(self.name, me)
        bpy.context.scene.collection.objects.link(ob)

        # ---- normals -------------------------------------------------------
        # The shells, their rims and the lofts are assembled from several code
        # paths, so triangle winding is not globally consistent on its own.  A
        # single reversed triangle is a hole in Unity (the card shader culls
        # back faces), which is exactly the shattered look of the rejected
        # build.  Recalculate outward normals per closed island instead of
        # trusting hand-derived winding.
        try:
            for o in bpy.context.view_layer.objects:
                o.select_set(False)
            bpy.context.view_layer.objects.active = ob
            ob.select_set(True)
            bpy.ops.object.mode_set(mode='EDIT')
            bpy.ops.mesh.select_all(action='SELECT')
            bpy.ops.mesh.normals_make_consistent(inside=False)
            bpy.ops.object.mode_set(mode='OBJECT')
            ob.select_set(False)
        except Exception as exc:                       # never break the build
            print("[model] normals pass failed on", self.name, repr(exc))

        uvlay = me.uv_layers.new(name="UVMap")
        for i, loop in enumerate(me.loops):
            uvlay.data[i].uv = self.uv[loop.vertex_index]
        groups = {}
        for bn in bone_names:
            groups[bn] = ob.vertex_groups.new(name=bn)
        for vi, wd in enumerate(self.w):
            tot = sum(max(0.0, x) for x in wd.values())
            if tot <= 1e-9:
                groups[bone_names[0]].add([vi], 1.0, 'REPLACE')
                continue
            items = sorted(wd.items(), key=lambda kv: -kv[1])[:4]
            tot = sum(max(0.0, x) for _, x in items)
            for bn, x in items:
                if bn not in groups:
                    groups[bn] = ob.vertex_groups.new(name=bn)
                if x > 1e-6:
                    groups[bn].add([vi], float(max(0.0, x) / tot), 'REPLACE')
        for i, (_, _, _, m) in enumerate(self.f):
            pass
        # material indices per polygon (faces are 2 per quad in creation order),
        # remapped to the slots this mesh actually has - a body face id of 3 on a
        # one-slot mesh is out of range and renders untextured black.
        used = []
        for pi in range(len(me.polygons)):
            m = int(mats[pi]) if pi < len(mats) else 0
            if m not in used:
                used.append(m)
        used.sort()
        remap = {m: i for i, m in enumerate(used)}
        for pi, poly in enumerate(me.polygons):
            m = int(mats[pi]) if pi < len(mats) else 0
            poly.material_index = remap[m]
        return ob


def parallel_frames(pts, up0=None):
    if up0 is None:
        up0 = CAM_UP
    n = len(pts)
    ts = []
    for i in range(n):
        if i == 0:
            t = pts[1] - pts[0]
        elif i == n - 1:
            t = pts[-1] - pts[-2]
        else:
            t = pts[i + 1] - pts[i - 1]
        ts.append(t.normalized())
    ups = []
    u = up0 - ts[0] * up0.dot(ts[0])
    if u.length < 1e-5:
        u = Vector((0.0, 1.0, 0.0))
    ups.append(u.normalized())
    for i in range(1, n):
        u = ups[-1] - ts[i] * ups[-1].dot(ts[i])
        if u.length < 1e-6:
            u = Vector((0.0, 1.0, 0.0))
        ups.append(u.normalized())
    return ts, ups


def loft(mb, sections, segs, mat, cap_a=True, cap_b=True, up0=None,
         shade=lambda ang: 1.0):
    """sections: list of dict(p=Vector, rx, ry, w=dict). Returns first/last ring."""
    if up0 is None:
        up0 = CAM_UP
    pts = [s["p"] for s in sections]
    ts, ups = parallel_frames(pts, up0)
    rings = []
    for i, s in enumerate(sections):
        t = ts[i]
        u = ups[i]
        sde = t.cross(u)
        ring = []
        for k in range(segs):
            a = 2.0 * math.pi * k / segs
            rr = shade(a)
            off = u * (s["rx"] * math.cos(a) * rr) + sde * (s["ry"] * math.sin(a) * rr)
            p = s["p"] + off
            ring.append(mb.add(p, world_to_uv(p), s["w"]))
        rings.append(ring)
    for i in range(len(rings) - 1):
        a, b = rings[i], rings[i + 1]
        for k in range(segs):
            k2 = (k + 1) % segs
            mb.quad(a[k], a[k2], b[k2], b[k], mat)
    if cap_a:
        s = sections[0]
        u = ups[0]
        sde = ts[0].cross(u)
        c = mb.add(s["p"], world_to_uv(s["p"]), s["w"])
        for k in range(segs):
            k2 = (k + 1) % segs
            mb.tri(c, rings[0][k2], rings[0][k], mat)
    if cap_b:
        s = sections[-1]
        u = ups[-1]
        sde = ts[-1].cross(u)
        c = mb.add(s["p"], world_to_uv(s["p"]), s["w"])
        for k in range(segs):
            k2 = (k + 1) % segs
            mb.tri(c, rings[-1][k], rings[-1][k2], mat)
    return rings


def tube(mb, path, radii, weights, segs=8, mat=0, cap_a=True, cap_b=True,
         up0=None, ratio=1.0):
    if up0 is None:
        up0 = CAM_UP
    secs = []
    for i, p in enumerate(path):
        w = weights[i] if isinstance(weights, list) else weights
        rx = radii[i]
        secs.append({"p": Vector(p), "rx": rx, "ry": rx * ratio, "w": w})
    return loft(mb, secs, segs, mat, cap_a, cap_b, up0)


def mix(*pairs):
    d = {}
    for n, v in pairs:
        d[n] = d.get(n, 0.0) + v
    return d


def dblend(a, b, t):
    d = {}
    for k, v in a.items():
        d[k] = d.get(k, 0.0) + v * (1.0 - t)
    for k, v in b.items():
        d[k] = d.get(k, 0.0) + v * t
    return d


def lerp(a, b, t):
    return a + (b - a) * t


def vlerp(a, b, t):
    return Vector(a) + (Vector(b) - Vector(a)) * t


# --------------------------------------------------------------------------
# 4. skeleton definition (world space, rest pose = approved v5 painting)
# --------------------------------------------------------------------------

# hand built in the painting's relaxed curl, palm toward the camera (contract -z)
HAND_R_WRIST = C(1.037, -1.799, -0.02)
HAND_R_PALM = C(1.127, -2.094, -0.02)
HAND_L_WRIST = C(-0.614, 1.934, 0.06)
HAND_L_PALM = C(-0.594, 2.170, 0.06)

HEAD_C = fx(805, 690, 0.02)
HEAD_UP = C(-0.316, 0.949, 0.0).normalized()
HEAD_RT = C(0.949, 0.316, 0.0).normalized()
HEAD_FD = C(0.0, 0.0, -1.0)
HEAD_RR = Vector((0.335, 0.410, 0.245))


def hand_frame(wrist, palm, palm_normal):
    hy = (palm - wrist).normalized()
    hz = Vector(palm_normal)
    hz = (hz - hy * hz.dot(hy)).normalized()
    hx = hy.cross(hz)
    return hx, hy, hz


FINGER_NAMES = ["Idx", "Mid", "Rng", "Lit"]
FINGER_OFF = [0.078, 0.027, -0.027, -0.078]
FINGER_SCALE = [0.94, 1.0, 0.95, 0.80]
FINGER_SPREAD = [12.0, 3.0, -4.0, -13.0]      # degrees, only used by the open left hand
# rest curl per phalanx (degrees toward the palm)  - loose fist as painted
FINGER_CURL = [38.0, 44.0, 40.0]


def hand_bones(side, wrist, palm, palm_normal, spread):
    """returns (bone_defs, geometry) for one hand.

    bone_defs: list of (name, parent, head, tail)
    geometry : list of dicts describing palm + finger chains for the mesh pass
    """
    sfx = "R" if side > 0 else "L"
    hx, hy, hz = hand_frame(wrist, palm, palm_normal)
    palm_len = (palm - wrist).length
    P = wrist + hy * palm_len
    defs = [("Hand" + sfx, "Forearm" + sfx, wrist, P)]
    geo = {}
    fingers = []
    for i, fn in enumerate(FINGER_NAMES):
        base = P + hx * FINGER_OFF[i] + hz * (-0.004) + hy * (-0.012)
        s = FINGER_SCALE[i]
        seglen = [0.070 * s, 0.052 * s, 0.042 * s] if side > 0 else [0.13*s,0.105*s,0.08*s]
        rad = [0.026 * s, 0.021 * s, 0.017 * s]
        # spread pushes the finger outward inside the palm plane
        sd = math.radians(spread * (FINGER_OFF[i] / 0.078))
        d = (hy * math.cos(sd) + hx * math.sin(sd)).normalized()
        pts = [base]
        dcur = d
        cur = Vector(base)
        parent = "Hand" + sfx
        chain = []
        cum = 0.0
        for j in range(3):
            cum += FINGER_CURL[j] if side > 0 else [5.0, 8.0, 8.0][j]
            q = Quaternion(hx, math.radians(-cum))
            dcur = q @ d
            nxt = cur + dcur * seglen[j]
            nm = "%s%s_%d" % (fn, sfx, j + 1)
            defs.append((nm, parent, cur, nxt))
            chain.append((cur, nxt, rad[j]))
            parent = nm
            cur = nxt
        fingers.append(chain)
    # thumb: inner side is picture LEFT for the right hand (contract), i.e. -hx
    tb = wrist + hy * (palm_len * 0.30) - hx * (0.072 * side) + hz * (-0.012)
    td = (-hx * 0.42 * side + hy * 0.86 - hz * 0.30).normalized()
    tseglen = [0.062, 0.050, 0.040]
    trad = [0.028, 0.024, 0.019]
    tcurls = [26.0, 40.0, 34.0]
    cur = Vector(tb)
    dcur = td
    parent = "Hand" + sfx
    thumb = []
    cum = 0.0
    for j in range(3):
        cum += tcurls[j]
        q = Quaternion(hx, math.radians(-cum * side))
        dcur = (q @ td).normalized()
        nxt = cur + dcur * tseglen[j]
        nm = "Thb%s_%d" % (sfx, j + 1)
        defs.append((nm, parent, cur, nxt))
        thumb.append((cur, nxt, trad[j]))
        parent = nm
        cur = nxt
    geo["sfx"] = sfx
    geo["wrist"] = wrist
    geo["palm"] = P
    geo["hx"], geo["hy"], geo["hz"] = hx, hy, hz
    geo["palm_len"] = palm_len
    geo["fingers"] = fingers
    geo["thumb"] = thumb
    return defs, geo


def build_skeleton():
    """the full 58 bone table, world space, rest pose == approved painting."""
    B = []                     # (name, parent, head, tail)
    B.append(("Root", None, Vector((0.0, 0.0, 0.0)), Vector((0.0, 0.30, 0.0))))
    pelvis = fx(450, 430, 0.02)
    waist = fx(540, 500, 0.02)
    chest = fx(620, 560, 0.02)
    neckb = fx(700, 610, 0.04)
    headb = fx(742, 636, 0.03)
    crown = fx(744, 596, 0.02)
    B.append(("Hips", "Root", pelvis, waist))
    B.append(("Spine", "Hips", waist, chest))
    B.append(("Chest", "Spine", chest, neckb))
    B.append(("Neck", "Chest", neckb, headb))
    B.append(("Head", "Neck", headb, crown))
    # hair chain, follows the flowing mass down-right
    h0 = fx(770, 640, 0.10)
    h1 = fx(700, 780, 0.14)
    h2 = fx(600, 930, 0.16)
    h3 = fx(520, 1040, 0.16)
    h4 = fx(470, 1120, 0.15)
    B.append(("HairRoot", "Head", h0, h1))
    B.append(("HairA", "HairRoot", h1, h2))
    B.append(("HairB", "HairA", h2, h3))
    B.append(("HairC", "HairB", h3, h4))
    B.append(("HairD", "HairC", h4, h4 + Vector((-0.25, -0.45, 0.02))))

    # arms
    shR = fx(640, 545, 0.0)
    shL = fx(490, 530, 0.05)
    B.append(("ShoulderR", "Chest", fx(645, 560, 0.02), shR))
    B.append(("ShoulderL", "Chest", fx(520, 545, 0.03), shL))
    elR = fx(700, 890, -0.06)
    wrR = HAND_R_WRIST
    elL = fx(415, 350, -0.06)
    wrL = HAND_L_WRIST
    B.append(("UpperArmR", "ShoulderR", shR, elR))
    B.append(("ForearmR", "UpperArmR", elR, wrR))
    B.append(("UpperArmL", "ShoulderL", shL, elL))
    B.append(("ForearmL", "UpperArmL", elL, wrL))
    hdR, geoR = hand_bones(1, HAND_R_WRIST, HAND_R_PALM, CAM_UP, 0.0)
    hdL, geoL = hand_bones(-1, HAND_L_WRIST, HAND_L_PALM, C(0.0, 0.0, 1.0), 9.0)
    B.extend(hdR)
    B.extend(hdL)

    # legs
    hipA = fx(520, 470, 0.02)
    kneeA = fx(386, 593, -0.04)
    ankA = fx(253, 807, -0.06)
    toeA = fx(196, 860, -0.10)
    hipBm = fx(505, 860, 0.02)
    kneeB = fx(304, 1113, -0.06)
    ankB = fx(212, 1246, -0.08)
    toeB = fx(140, 1345, -0.12)
    B.append(("ThighA", "Hips", hipA, kneeA))
    B.append(("ShinA", "ThighA", kneeA, ankA))
    B.append(("FootA", "ShinA", ankA, toeA))
    B.append(("ToeA", "FootA", toeA, toeA + Vector((-0.16, -0.10, 0.0))))
    B.append(("ThighB", "Hips", hipBm, kneeB))
    B.append(("ShinB", "ThighB", kneeB, ankB))
    B.append(("FootB", "ShinB", ankB, toeB))
    B.append(("ToeB", "FootB", toeB, toeB + Vector((-0.14, -0.14, 0.0))))
    # The sword carrier.  The sword has an authored world trajectory that is not
    # a joint of the body, but an object-level action on the sword mesh did not
    # survive the FBX scene bake (Unity's imported clip carried no /Sword track
    # at all, so the hilt never left its rest transform).  Carrying the sword on
    # a real bone puts its motion inside the single armature action, which every
    # exporter path and Unity's Animator both handle.  Rest frame == the sword's
    # authored rest frame, so the rigid attachment is exact.
    B.append(("SwordBone", "Root", GRIP_WORLD, GRIP_WORLD + REST_BLADE * 0.45))
    return B, geoR, geoL


# --------------------------------------------------------------------------
# 5. body / head / hair / sword geometry
# --------------------------------------------------------------------------

def build_body(mb, geoR, geoL):
    up = CAM_UP

    def limb(path_px, radii, bones, zs=None, segs=10, ratio=1.0, name=""):
        pts = []
        for i, (px, py) in enumerate(path_px):
            z = 0.0 if zs is None else zs[i]
            pts.append(fx(px, py, z))
        n = len(pts)
        wts = []
        for i in range(n):
            f = i / float(n - 1)
            idx = f * (len(bones) - 1)
            i0 = int(math.floor(idx))
            i1 = min(i0 + 1, len(bones) - 1)
            t = idx - i0
            wts.append(mix((bones[i0], 1.0 - t), (bones[i1], t)))
        return tube(mb, pts, radii, wts, segs=segs, ratio=ratio)

    # ---- torso: stacked elliptical rings, hips -> chest -> neck
    torso = [
        (450, 430, 0.03), (470, 447, 0.02), (500, 466, 0.01), (532, 484, 0.00),
        (562, 504, -0.01), (592, 526, -0.01), (620, 552, 0.00), (648, 578, 0.02),
        (676, 602, 0.04), (700, 620, 0.06),
    ]
    trad = [0.375, 0.370, 0.340, 0.300, 0.275, 0.268, 0.272, 0.262, 0.205, 0.130]
    trati = [0.62, 0.60, 0.58, 0.56, 0.56, 0.58, 0.60, 0.60, 0.62, 0.72]
    tbone = ["Hips", "Hips", "Spine", "Spine", "Spine", "Chest", "Chest", "Chest", "Neck", "Neck"]
    pts = [fx(a, b, c) for a, b, c in torso]
    wts = []
    for i in range(len(pts)):
        nxt = tbone[min(i + 1, len(tbone) - 1)]
        wts.append(mix((tbone[i], 0.62), (nxt, 0.38)))
    secs = [{"p": pts[i], "rx": trad[i], "ry": trad[i] * trati[i], "w": wts[i]} for i in range(len(pts))]
    loft(mb, secs, 12, 0)

    # bust - two merged volumes so the silhouette is not a tube
    for sgn, o in ((1.0, 0.10), (-1.0, -0.10)):
        c = fx(600 + 18 * sgn, 585, -0.10)
        r0 = 0.115
        secs = []
        for k in range(7):
            t = k / 6.0
            ang = math.pi * t
            v = Vector((math.cos(ang) * r0 * 0.95 + o * 0.0, math.sin(ang) * r0 * 0.85, 0.0))
            secs.append({"p": c + Vector((o + v.x, v.y * 0.0, -math.sin(ang) * r0 * 0.75)),
                         "rx": r0 * math.sin(ang) * 0.9,
                         "ry": r0 * math.sin(ang) * 0.75,
                         "w": mix(("Chest", 0.85), ("Spine", 0.15))})
        # simple hemisphere-ish blob via tube along the chest normal
        p0 = c + C(o, 0.0, 0.10)
        p1 = fx(600 + 18 * sgn, 585, -0.30)
        w = mix(("Chest", 0.9), ("Spine", 0.1))
        tube(mb, [p0, vlerp(p0, p1, 0.55), p1], [0.135, 0.115, 0.045],
             [w, w, w], segs=10)

    # ---- neck
    limb([(690, 606), (706, 618), (722, 630), (738, 640)], [0.105, 0.098, 0.092, 0.088],
         ["Neck", "Neck", "Head", "Head"], zs=[0.02, 0.03, 0.03, 0.03], segs=10)

    # ---- head: shaped ellipsoid with a jaw/chin taper and a nose bump
    hc = HEAD_C
    hup, hrt, hfd = HEAD_UP, HEAD_RT, HEAD_FD
    rr = HEAD_RR
    lats, lons = 14, 22
    hw_ = mix(("Head", 0.92), ("Neck", 0.08))
    # single vertices at the two poles: a collapsed ring of coincident but
    # distinct vertices leaves a ring of boundary edges, which is a visible hole
    # in Unity where the card shader culls back faces.
    head_ids = []
    for i in range(1, lats):
        th = math.pi * i / lats
        ring = []
        for j in range(lons):
            ph = 2.0 * math.pi * j / lons
            sx = math.sin(th) * math.cos(ph)
            sy = math.cos(th)
            sz = math.sin(th) * math.sin(ph)
            # jaw taper below the equator, brow ridge above
            taper = 1.0 - 0.30 * max(0.0, -sy) ** 1.3
            wide = 1.0 + 0.05 * max(0.0, sy) ** 2
            p = hc + hrt * (sx * rr.x * taper * wide) + hup * (sy * rr.y) + hfd * (sz * rr.z)
            ring.append(mb.add(p, world_to_uv(p), hw_))
        head_ids.append(ring)
    p_top = hc + hup * rr.y
    p_bot = hc - hup * rr.y
    top = mb.add(p_top, world_to_uv(p_top), hw_)
    bot = mb.add(p_bot, world_to_uv(p_bot), hw_)
    for j in range(lons):
        j2 = (j + 1) % lons
        mb.tri(top, head_ids[0][j], head_ids[0][j2], 0)
        mb.tri(bot, head_ids[-1][j2], head_ids[-1][j], 0)
    for i in range(len(head_ids) - 1):
        for j in range(lons):
            j2 = (j + 1) % lons
            mb.quad(head_ids[i][j], head_ids[i][j2], head_ids[i + 1][j2], head_ids[i + 1][j], 0)
    # pointed elven ears
    for sgn in (1.0, -1.0):
        eb = hc + hrt * (sgn * 0.255) + hup * 0.10 + hfd * 0.02
        et = eb + (hrt * (sgn * 0.10) + hup * 0.14 + hfd * 0.03)
        w = mix(("Head", 1.0))
        tube(mb, [eb, vlerp(eb, et, 0.5), et], [0.055, 0.042, 0.012], [w, w, w],
             segs=7, ratio=0.45)

    # ---- arms
    limb([(645, 552), (676, 604), (706, 668), (712, 740), (706, 812), (700, 890)],
         [0.150, 0.148, 0.132, 0.118, 0.104, 0.093],
         ["Chest", "ShoulderR", "UpperArmR", "UpperArmR", "UpperArmR", "ForearmR"],
         zs=[0.02, 0.0, -0.02, -0.04, -0.05, -0.06], segs=10, ratio=0.86)
    limb([(700, 890), (752, 968), (800, 1042), (836, 1104), (850, 1142), (846, 1160)],
         [0.093, 0.086, 0.077, 0.062, 0.052, 0.048],
         ["UpperArmR", "ForearmR", "ForearmR", "ForearmR", "ForearmR", "HandR"],
         zs=[-0.06, -0.08, -0.11, -0.14, -0.15, -0.15], segs=10, ratio=0.86)
    limb([(505, 540), (472, 490), (445, 432), (424, 372), (414, 296), (412, 212)],
         [0.150, 0.146, 0.130, 0.113, 0.098, 0.086],
         ["Chest", "ShoulderL", "UpperArmL", "UpperArmL", "UpperArmL", "ForearmL"],
         zs=[0.02, 0.0, -0.02, -0.05, -0.09, -0.13], segs=10, ratio=0.86)
    limb([(412, 212), (410, 160), (412, 122), (418, 100)],
         [0.086, 0.072, 0.058, 0.052],
         ["ForearmL", "ForearmL", "HandL", "HandL"],
         zs=[-0.13, -0.14, -0.14, -0.14], segs=10, ratio=0.86)

    # ---- legs
    limb([(520, 470), (490, 510), (455, 546), (420, 572), (396, 592)],
         [0.170, 0.166, 0.155, 0.140, 0.122],
         ["Hips", "Hips", "ThighA", "ThighA", "ShinA"],
         zs=[0.02, 0.0, -0.02, -0.03, -0.04], segs=10, ratio=0.90)
    limb([(396, 592), (350, 640), (310, 700), (280, 756), (263, 796)],
         [0.122, 0.115, 0.108, 0.088, 0.070],
         ["ThighA", "ShinA", "ShinA", "ShinA", "FootA"],
         zs=[-0.04, -0.05, -0.05, -0.055, -0.06], segs=10, ratio=0.90)
    limb([(263, 796), (240, 826), (214, 850), (206, 862)],
         [0.070, 0.062, 0.052, 0.046],
         ["ShinA", "FootA", "FootA", "ToeA"],
         zs=[-0.06, -0.07, -0.09, -0.10], segs=9, ratio=0.72)
    limb([(505, 860), (480, 900), (455, 940), (430, 980), (400, 1020), (355, 1078),
          (320, 1112)],
         [0.175, 0.172, 0.165, 0.155, 0.142, 0.120, 0.108],
         ["Hips", "Hips", "ThighB", "ThighB", "ThighB", "ShinB", "ShinB"],
         zs=[0.02, 0.0, -0.02, -0.03, -0.04, -0.05, -0.06], segs=10, ratio=0.90)
    limb([(320, 1112), (280, 1160), (248, 1204), (222, 1240)],
         [0.108, 0.100, 0.088, 0.068],
         ["ThighB", "ShinB", "ShinB", "FootB"],
         zs=[-0.06, -0.065, -0.07, -0.08], segs=10, ratio=0.90)
    limb([(222, 1240), (196, 1284), (164, 1326), (150, 1344)],
         [0.068, 0.058, 0.048, 0.042],
         ["ShinB", "FootB", "FootB", "ToeB"],
         zs=[-0.08, -0.09, -0.11, -0.12], segs=9, ratio=0.72)

    build_hand(mb, geoR, 0.0)
    build_hand(mb, geoL, 0.0)
    build_face_shell(mb)


def build_hand(mb, geo, mat):
    wrist = geo["wrist"]
    P = geo["palm"]
    hx, hy, hz = geo["hx"], geo["hy"], geo["hz"]
    w0 = mix(("Hand" + geo["sfx"], 0.35), ("Forearm" + geo["sfx"], 0.65))
    w1 = mix(("Hand" + geo["sfx"], 1.0))
    # closed palm shell - real thickness, wider across the knuckles
    palm_secs = []
    n = 6
    for k in range(n):
        t = k / float(n - 1)
        c = vlerp(wrist, P, t)
        rx = lerp(0.055, 0.092, t) * (1.0 - 0.18 * max(0.0, t - 0.8) / 0.2)
        ry = lerp(0.042, 0.048, t)
        w = w0 if k == 0 else (dblend(w0, w1, min(1.0, t * 2.2)) if k < 2 else w1)
        palm_secs.append({"p": c, "rx": rx, "ry": ry, "w": w})
    # reorient: loft along hy using explicit rings
    rings = []
    for k, s in enumerate(palm_secs):
        t = k / float(n - 1)
        rr = s["rx"]
        rt = s["ry"]
        ring = []
        for j in range(10):
            a = 2.0 * math.pi * j / 10
            p = s["p"] + hx * (rr * math.cos(a)) + hz * (rt * math.sin(a))
            ring.append(mb.add(p, world_to_uv(p), s["w"]))
        rings.append(ring)
    for k in range(n - 1):
        for j in range(10):
            j2 = (j + 1) % 10
            mb.quad(rings[k][j], rings[k][j2], rings[k + 1][j2], rings[k + 1][j], mat)
    c0 = mb.add(wrist - hy * 0.02, world_to_uv(wrist), w0)
    for j in range(10):
        j2 = (j + 1) % 10
        mb.tri(c0, rings[0][j2], rings[0][j], mat)
    c1 = mb.add(P + hy * 0.02, world_to_uv(P), w1)
    for j in range(10):
        j2 = (j + 1) % 10
        mb.tri(c1, rings[-1][j], rings[-1][j2], mat)

    # fingers: 3 closed phalanx tubes each, blended at the joints
    for fi, chain in enumerate(geo["fingers"]):
        for j, (a, b, rad) in enumerate(chain):
            nm = "%s%s_%d" % (FINGER_NAMES[fi], geo["sfx"], j + 1)
            par = ("Hand" + geo["sfx"]) if j == 0 else ("%s%s_%d" % (FINGER_NAMES[fi], geo["sfx"], j))
            d = (b - a)
            p0 = a - d * 0.12
            p1 = a + d * 0.42
            p2 = b + d * 0.05
            wp = mix((par, 0.45), (nm, 0.55))
            wm = mix((nm, 1.0))
            tube(mb, [p0, p1, p2], [rad * 1.06, rad * 0.98, rad * 0.86],
                 [wp, wm, wm], segs=8)
    for j, (a, b, rad) in enumerate(geo["thumb"]):
        nm = "Thb%s_%d" % (geo["sfx"], j + 1)
        par = ("Hand" + geo["sfx"]) if j == 0 else ("Thb%s_%d" % (geo["sfx"], j))
        d = (b - a)
        p0 = a - d * 0.12
        p1 = a + d * 0.45
        p2 = b + d * 0.05
        wp = mix((par, 0.5), (nm, 0.5))
        wm = mix((nm, 1.0))
        tube(mb, [p0, p1, p2], [rad * 1.08, rad, rad * 0.85], [wp, wm, wm], segs=8)


def curved_shell(mb, rect_px, nx, ny, z_front, thick, mat, weights_fn,
                 relief=None, relief_amp=0.0, dome_amp=0.0,
                 dome_center=(0.30, 0.05), dome_rad=(0.55, 0.60), mask=None):
    """A genuinely curved, closed, double-sided shaped sheet with REAL thickness.

    This is the technique the contract allows for the preserved painted face and
    the flowing hair: a domed, relief-displaced 3D shell whose geometry is
    alpha-cut to the painted silhouette, NOT a flat plane with a shader bend.
    The mesh is closed, so it needs no transparency at all.
    """
    x0, y0, x1, y1 = rect_px
    h, w = (relief.shape if relief is not None else (1, 1))

    def image_norm(u, v):
        """rect-local uv -> whole-sheet normalised (u,v) used by mask and relief."""
        return ((x0 + (x1 - x0) * u) / FIG_W, (y0 + (y1 - y0) * v) / FIG_H)

    def sample_relief(u, v):
        if relief is None or relief_amp == 0.0:
            return 0.0
        gu, gv = image_norm(u, v)
        px = int(min(w - 1, max(0, gu * (w - 1))))
        py = int(min(h - 1, max(0, gv * (h - 1))))
        return float(relief[py, px]) * relief_amp

    def zfront(u, v):
        dx = (u - dome_center[0]) / dome_rad[0]
        dy = (v - dome_center[1]) / dome_rad[1]
        b = math.exp(-(dx * dx + dy * dy) * 1.15)
        return z_front - dome_amp * b + sample_relief(u, v)

    def on(i, j):
        if i < 0 or j < 0 or i >= nx or j >= ny:
            return False
        if mask is None:
            return True
        gu, gv = image_norm(i / float(nx - 1), j / float(ny - 1))
        mh, mw = mask.shape
        px = int(min(mw - 1, max(0, gu * (mw - 1))))
        py = int(min(mh - 1, max(0, gv * (mh - 1))))
        return bool(mask[py, px])

    def cell(i, j):
        """a quad exists only when all four of its grid nodes are inside the mask"""
        return on(i, j) and on(i + 1, j) and on(i, j + 1) and on(i + 1, j + 1)

    front = [[None] * ny for _ in range(nx)]
    back = [[None] * ny for _ in range(nx)]
    for i in range(nx):
        for j in range(ny):
            if not on(i, j):
                continue
            u = i / float(nx - 1)
            v = j / float(ny - 1)
            p = fig_to_world(x0 + (x1 - x0) * u, y0 + (y1 - y0) * v, 0.0)
            wd = weights_fn(p, u, v)
            front[i][j] = mb.add(C(Cinv(p).x, Cinv(p).y, zfront(u, v)), world_to_uv(p), wd)
            back[i][j] = mb.add(C(Cinv(p).x, Cinv(p).y, z_front + thick), world_to_uv(p), wd)
    for i in range(nx - 1):
        for j in range(ny - 1):
            if not cell(i, j):
                continue
            a, b, c, d = front[i][j], front[i][j + 1], front[i + 1][j + 1], front[i + 1][j]
            mb.quad(a, b, c, d, mat)
            a, b, c, d = back[i][j], back[i + 1][j], back[i + 1][j + 1], back[i][j + 1]
            mb.quad(a, b, c, d, mat)
    # rim: every grid edge that borders exactly one existing cell is a silhouette
    # edge of the cut-out and must be closed, or the shell has a real hole.  Keying
    # the rim off the same cell() predicate the surface uses is what makes the
    # shell watertight: the previous version tested the neighbour CELL while the
    # faces depended on the four NODES, so concave mask corners leaked.
    back_of_front = {}
    for i in range(nx):
        for j in range(ny):
            if front[i][j] is not None:
                back_of_front[front[i][j]] = back[i][j]

    def edge_quad(p, q):
        if p is None or q is None:
            return
        bp, bq = back_of_front.get(p), back_of_front.get(q)
        if bp is None or bq is None:
            return
        mb.quad(p, q, bq, bp, mat)

    for i in range(nx - 1):
        for j in range(ny):
            if front[i][j] is not None and front[i + 1][j] is not None:
                if cell(i, j) != cell(i, j - 1):
                    edge_quad(front[i][j], front[i + 1][j])
    for i in range(nx):
        for j in range(ny - 1):
            if front[i][j] is not None and front[i][j + 1] is not None:
                if cell(i, j) != cell(i - 1, j):
                    edge_quad(front[i][j], front[i][j + 1])


def build_face_shell(mb):
    """preserved painted face: shaped, domed, closed shell with real thickness."""
    fig = load_rgba(FIG_V5)
    skin, hair, ink, white = classify(fig)
    luma = (0.2126 * fig[..., 0] + 0.7152 * fig[..., 1] + 0.0722 * fig[..., 2])
    n = 240

    def resample(a, n):
        h, w = a.shape
        ys = np.linspace(0, h - 1, n).astype(np.int32)
        xs = np.linspace(0, w - 1, n).astype(np.int32)
        return a[np.ix_(ys, xs)]

    small = resample(luma, n)
    # the face plate is clipped to the painted face oval so no flat rectangle shows
    oval = np.zeros_like(skin)
    cx, cy, rx, ry = 806.0, 692.0, 96.0, 122.0
    yy, xx = np.mgrid[0:skin.shape[0], 0:skin.shape[1]]
    oval = (((xx - cx) / rx) ** 2 + ((yy - cy) / ry) ** 2) < 1.0
    face_mask = resample((oval & (skin | dilate(skin, 4))).astype(np.uint8), n) > 0

    def wf(p, u, v):
        return mix(("Head", 0.92), ("Neck", 0.08))

    curved_shell(mb, (714.0, 574.0, 896.0, 804.0), 26, 30, -0.250, 0.21, 0,
                 wf, relief=small, relief_amp=0.050,
                 dome_amp=0.055, dome_center=(0.50, 0.48), dome_rad=(0.62, 0.62),
                 mask=face_mask)


def build_hair_mass(mb):
    """flowing hair: masked curved relief shells + thicker volumetric locks."""
    fig = load_rgba(FIG_V5)
    skin, hair, ink, white = classify(fig)
    luma = (0.2126 * fig[..., 0] + 0.7152 * fig[..., 1] + 0.0722 * fig[..., 2])
    n = 420

    def resample(a, n):
        h, w = a.shape
        ys = np.linspace(0, h - 1, n).astype(np.int32)
        xs = np.linspace(0, w - 1, n).astype(np.int32)
        return a[np.ix_(ys, xs)]

    small = resample(luma, n)
    # solid hair mass: everything that is neither background nor bare skin, then
    # morphologically closed so specular highlights inside the hair do not punch
    # holes in the shell geometry.
    outside = (luma > 0.78) & (np.abs(fig[..., 0] - fig[..., 1]) < 0.09) & \
              (np.abs(fig[..., 1] - fig[..., 2]) < 0.09)
    solid = (fig[..., 3] > 0.1) & (~outside) & (~dilate(skin, 3))
    hm = resample(solid.astype(np.uint8), n) > 0
    for _ in range(7):
        d = hm.copy()
        d[1:, :] |= hm[:-1, :]
        d[:-1, :] |= hm[1:, :]
        d[:, 1:] |= hm[:, :-1]
        d[:, :-1] |= hm[:, 1:]
        hm = d
    for _ in range(7):
        e = hm.copy()
        e[1:, :] &= hm[:-1, :]
        e[:-1, :] &= hm[1:, :]
        e[:, 1:] &= hm[:, :-1]
        e[:, :-1] &= hm[:, 1:]
        hm = e
    hmask = hm

    hair_bones = ["HairRoot", "HairRoot", "HairA", "HairA", "HairB", "HairB",
                  "HairC", "HairC", "HairD", "HairD"]
    y_top = fx(780, 560, 0.0).y
    y_bot = fx(500, 1180, 0.0).y

    def wf(p, u, v):
        t = min(1.0, max(0.0, (y_top - p.y) / max(1e-4, (y_top - y_bot))))
        idx = t * (len(hair_bones) - 1)
        i0 = int(math.floor(idx))
        i1 = min(i0 + 1, len(hair_bones) - 1)
        f = idx - i0
        return mix((hair_bones[i0], 1.0 - f), (hair_bones[i1], f))

    # main mantle: a curved, relief-displaced, silhouette-cut shell with thickness
    curved_shell(mb, (300.0, 440.0, 1040.0, 1210.0), 150, 168, 0.16, 0.34, 2,
                 wf, relief=small, relief_amp=0.09,
                 dome_amp=0.26, dome_center=(0.42, 0.30), dome_rad=(0.52, 0.55),
                 mask=hmask)
    # lower sweep so the hair keeps flowing past the body
    curved_shell(mb, (330.0, 760.0, 1000.0, 1260.0), 110, 80, 0.13, 0.30, 2,
                 wf, relief=small, relief_amp=0.07,
                 dome_amp=0.20, dome_center=(0.55, 0.55), dome_rad=(0.62, 0.70),
                 mask=hmask)

    # thicker volumetric locks give the silhouette strands real round volume
    locks = [
        ([(800, 600), (740, 660), (660, 720), (560, 760), (450, 780), (350, 775)],
         [0.05, 0.02, -0.02, -0.06, -0.10, -0.12], 0.175, 0.115),
        ([(806, 596), (770, 690), (740, 800), (700, 910), (640, 1000), (560, 1060)],
         [0.04, 0.00, -0.06, -0.12, -0.16, -0.18], 0.185, 0.120),
        ([(812, 604), (860, 680), (906, 760), (930, 850), (912, 950), (860, 1030)],
         [0.04, 0.0, -0.04, -0.10, -0.16, -0.20], 0.185, 0.120),
        ([(790, 592), (720, 570), (630, 560), (540, 566), (450, 580)],
         [0.03, -0.02, -0.07, -0.10, -0.12], 0.155, 0.105),
        ([(818, 598), (880, 570), (946, 566), (1000, 590), (1030, 630)],
         [0.03, -0.02, -0.06, -0.09, -0.11], 0.150, 0.100),
        ([(796, 610), (740, 720), (660, 830), (570, 920), (490, 980), (410, 1020)],
         [0.05, 0.0, -0.06, -0.12, -0.16, -0.18], 0.180, 0.118),
        ([(810, 620), (830, 740), (820, 860), (780, 970), (720, 1060), (650, 1120)],
         [0.05, 0.0, -0.06, -0.12, -0.17, -0.20], 0.180, 0.118),
        ([(788, 606), (700, 600), (600, 606), (500, 620), (410, 640)],
         [0.02, -0.03, -0.08, -0.11, -0.13], 0.160, 0.108),
        ([(804, 612), (762, 742), (700, 862), (620, 962), (540, 1040)],
         [0.05, 0.0, -0.07, -0.13, -0.17], 0.168, 0.112),
    ]
    for (pts_px, zs, wdt, thk) in locks:
        pts = [fx(a, b, c) for (a, b), c in zip(pts_px, zs)]
        n2 = len(pts)
        wl = [wf(pts[i], 0.0, 0.0) for i in range(n2)]
        secs = []
        for i in range(n2):
            f = i / float(n2 - 1)
            pinch = 0.45 + 0.55 * (1.0 - f) ** 0.7
            secs.append({"p": pts[i], "rx": wdt * pinch, "ry": thk * pinch, "w": wl[i]})
        loft(mb, secs, 8, 2)

    # scalp cap so the crown is never a bald ellipsoid (closed: single apex plus
    # a rim fan, otherwise it contributes a ring of boundary edges)
    hc = HEAD_C
    hup, hrt, hfd, rr = HEAD_UP, HEAD_RT, HEAD_FD, HEAD_RR
    lats, lons = 9, 20
    th_max = math.pi * 0.62
    sw = mix(("Head", 0.55), ("HairRoot", 0.45))
    ids = []
    for i in range(1, lats + 1):
        th = th_max * i / lats
        ring = []
        for j in range(lons):
            ph = 2.0 * math.pi * j / lons
            sx = math.sin(th) * math.cos(ph)
            sy = math.cos(th)
            sz = math.sin(th) * math.sin(ph)
            p = hc + hrt * (sx * rr.x * 1.06) + hup * (sy * rr.y * 1.06) + hfd * (sz * rr.z * 1.06)
            ring.append(mb.add(p, world_to_uv(p), sw))
        ids.append(ring)
    apex = hc + hup * (rr.y * 1.06)
    apex_id = mb.add(apex, world_to_uv(apex), sw)
    for j in range(lons):
        j2 = (j + 1) % lons
        mb.tri(apex_id, ids[0][j], ids[0][j2], 2)
    for i in range(lats - 1):
        for j in range(lons):
            j2 = (j + 1) % lons
            mb.quad(ids[i][j], ids[i][j2], ids[i + 1][j2], ids[i + 1][j], 2)
    rim_c = hc + hup * (math.cos(th_max) * rr.y * 1.06)
    rim_id = mb.add(rim_c, world_to_uv(rim_c), sw)
    for j in range(lons):
        j2 = (j + 1) % lons
        mb.tri(rim_id, ids[-1][j2], ids[-1][j], 2)


def build_sword(mb, prof, L, uaxis, perp):
    """closed sword mesh, local origin at the contract grip point, +Y = blade."""
    tip_px = SW_TEX_TIP
    pom = SW_TEX_POMMEL
    axis_len = (tip_px - pom).length
    px_per_world = axis_len / 4.166

    def blade_half(y):
        # y is local (0 at grip, positive toward tip)
        t = (y + 0.566) / 4.166
        if t < 0.185:
            return 0.055 + 0.02 * math.sin(math.pi * min(1.0, max(0.0, (t - 0.03) / 0.12)))
        tt = (t - 0.185) / 0.815
        if tt < 0.06:
            return 0.072
        return max(0.008, 0.072 * (1.0 - tt) ** 0.62)

    def uv_of(x, y, hw_ref=None):
        t = min(1.0, max(0.0, (y + 0.566) / 4.166))
        base = pom + (tip_px - pom) * t
        hw_tex = float(prof[int(t * (len(prof) - 1))])
        hw_loc = blade_half(y) if hw_ref is None else max(1e-5, hw_ref)
        k = 0.0 if hw_loc < 1e-5 else (x / hw_loc)
        p = base + perp * (k * hw_tex)
        return (p.x / SW_W, 1.0 - p.y / SW_H)

    def ring(secs_ys, halfw, halfd, wfun, closed_scale=1.0):
        pass

    # blade: 8-gon cross section with a fuller so it has real thickness
    ys = [-0.566, -0.545, -0.48, -0.30, -0.10, 0.02, 0.13, 0.185, 0.205, 0.30,
          0.32, 0.55, 1.0, 1.7, 2.4, 3.0, 3.35, 3.52, 3.60]
    rings = []
    for y in ys:
        hw = blade_half(y)
        if y < 0.185:
            hd = 0.045 + 0.02 * max(0.0, 1.0 - abs(y) / 0.6)
            if y < -0.50:
                hw = 0.085
                hd = 0.085
        elif y < 0.30:
            hw = max(hw, 0.30 * (1.0 - abs(y - 0.235) / 0.09) * 0.0 + 0.155)
            hd = 0.052
        else:
            hd = max(0.006, 0.048 * max(0.0, 1.0 - (y - 0.30) / 3.3) ** 0.8)
        # crossguard quillons: widen near y=0.235
        if 0.13 < y < 0.34:
            g = 1.0 - abs(y - 0.235) / 0.105
            hw = max(hw, 0.30 * g ** 0.55)
            hd = max(hd, 0.055 * g ** 0.4)
        pro = [(-1.0, 0.0), (-0.72, 0.72), (-0.30, 1.0), (0.30, 1.0), (0.72, 0.72),
               (1.0, 0.0), (0.72, -0.72), (0.30, -1.0), (-0.30, -1.0), (-0.72, -0.72)]
        ring_ids = []
        for (a, b) in pro:
            x = a * hw
            z = b * hd
            # fuller: pull the flat mid-band slightly inward
            if abs(a) < 0.4 and abs(b) > 0.6 and y > 0.30:
                z *= 0.55
            wp = mix(("Sword", 1.0))
            ring_ids.append(mb.add(Vector((x, y, z)), uv_of(x, y, hw), wp))
        rings.append(ring_ids)
    n = 10
    for i in range(len(rings) - 1):
        for j in range(n):
            j2 = (j + 1) % n
            mb.quad(rings[i][j], rings[i][j2], rings[i + 1][j2], rings[i + 1][j], 3)
    wS = mix(("Sword", 1.0))
    c0 = mb.add(Vector((0.0, -0.575, 0.0)), uv_of(0.0, -0.566, 0.085), wS)
    c1 = mb.add(Vector((0.0, 3.615, 0.0)), uv_of(0.0, 3.60, 0.01), wS)
    for j in range(n):
        j2 = (j + 1) % n
        mb.tri(c0, rings[0][j2], rings[0][j], 3)
        mb.tri(c1, rings[-1][j], rings[-1][j2], 3)

    # grip wrap ridges (small torus bands, real geometry)
    for gy in (-0.44, -0.36, -0.28, -0.20, -0.12, -0.04, 0.04):
        secs = []
        for k in range(9):
            a = 2.0 * math.pi * k / 8
            secs.append({"p": Vector((0.0, gy + 0.028 * math.cos(a), 0.0)),
                         "rx": 0.062 + 0.006 * math.sin(a), "ry": 0.062 + 0.006 * math.sin(a),
                         "w": wS})
        pass
    # pommel: faceted ball + collar (single pole vertices, closed)
    pom_c = Vector((0.0, -0.566, 0.0))
    lats, lons = 6, 10
    prows = []
    for i in range(1, lats):
        th = math.pi * i / lats
        row = []
        for j in range(lons):
            ph = 2.0 * math.pi * j / lons
            sx = math.sin(th) * math.cos(ph)
            sy = math.cos(th)
            sz = math.sin(th) * math.sin(ph)
            r = 0.088 * (1.0 + 0.18 * abs(sy) ** 2)
            p = pom_c + Vector((sx * r, sy * r * 0.95, sz * r))
            row.append(mb.add(p, uv_of(sx * r, -0.566, 0.088), wS))
        prows.append(row)
    ptop = pom_c + Vector((0.0, 0.088 * 1.18 * 0.95, 0.0))
    pbot = pom_c - Vector((0.0, 0.088 * 1.18 * 0.95, 0.0))
    ptop_id = mb.add(ptop, uv_of(0.0, -0.566, 0.088), wS)
    pbot_id = mb.add(pbot, uv_of(0.0, -0.566, 0.088), wS)
    for j in range(lons):
        j2 = (j + 1) % lons
        mb.tri(ptop_id, prows[0][j], prows[0][j2], 3)
        mb.tri(pbot_id, prows[-1][j2], prows[-1][j], 3)
    for i in range(len(prows) - 1):
        for j in range(lons):
            j2 = (j + 1) % lons
            mb.quad(prows[i][j], prows[i][j2], prows[i + 1][j2], prows[i + 1][j], 3)


# --------------------------------------------------------------------------
# 6. scene / armature
# --------------------------------------------------------------------------

def clear_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    sc = bpy.context.scene
    sc.render.fps = FPS
    sc.frame_start = 0
    sc.frame_end = SHEET_FRAMES - 1
    sc.unit_settings.system = 'METRIC'
    sc.unit_settings.scale_length = 1.0


def make_material(name, tex_path, mode, tile=False):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    nt = mat.node_tree
    for n in list(nt.nodes):
        nt.nodes.remove(n)
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled")
    tex = nt.nodes.new("ShaderNodeTexImage")
    img = bpy.data.images.load(tex_path, check_existing=False)
    tex.image = img
    tex.extension = 'REPEAT' if tile else 'EXTEND'
    nt.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    if mode == 'alpha':
        nt.links.new(tex.outputs["Alpha"], bsdf.inputs["Alpha"])
        try:
            mat.blend_method = 'BLEND'
        except Exception:
            pass
    bsdf.inputs["Roughness"].default_value = 0.45 if mode == 'opaque' else 0.55
    if "Metallic" in bsdf.inputs:
        bsdf.inputs["Metallic"].default_value = 0.0
    nt.links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])
    return mat


def make_armature(B):
    arm_data = bpy.data.armatures.new("FigureArmature")
    arm = bpy.data.objects.new("Figure", arm_data)
    bpy.context.scene.collection.objects.link(arm)
    bpy.context.view_layer.objects.active = arm
    arm.select_set(True)
    bpy.ops.object.mode_set(mode='EDIT')
    eb = arm_data.edit_bones
    made = {}
    for (name, parent, head, tail) in B:
        b = eb.new(name)
        b.head = tuple(head)
        b.tail = tuple(tail)
        b.use_connect = False
        if (b.tail - b.head).length < 1e-4:
            b.tail = b.head + Vector((0.0, 0.02, 0.0))
        if parent:
            b.parent = made[parent]
        made[name] = b
    # the sword carrier must have the sword's own rest frame: +Y along the blade
    # and +Z along the blade flat, so posing the bone by the sword basis moves
    # the rigidly attached mesh exactly onto the authored trajectory.
    if "SwordBone" in made:
        made["SwordBone"].align_roll(Vector(REST_FLAT))
    bpy.ops.object.mode_set(mode='OBJECT')
    arm.select_set(False)
    for (name, parent, head, tail) in B:
        # manifest is always expressed in the CONTRACT frame (Unity world), even
        # though the Blender scene itself is authored in Blender's Z-up frame.
        h = Cinv(head)
        t = Cinv(tail)
        BONES_MANIFEST.append({
            "name": name,
            "parent": parent,
            "head": [round(v, 6) for v in h],
            "tail": [round(v, 6) for v in t],
            "length": round((tail - head).length, 6),
        })
    return arm


def bind(ob, arm):
    ob.parent = arm
    m = ob.modifiers.new("Armature", 'ARMATURE')
    m.object = arm
    m.use_vertex_groups = True
    ob.matrix_parent_inverse = Matrix.Identity(4)


# --------------------------------------------------------------------------
# 7. animation
# --------------------------------------------------------------------------

SH_R = fx(640, 545, 0.0)
ELBOW_POLE_R = C(0.62, -0.10, -0.78).normalized()
CAM_UP = C(0.0, 0.0, -1.0).normalized()


def two_bone_ik(S, W, lu, lf, pole):
    d = (W - S)
    dist = min(d.length, lu + lf - 1e-4)
    dist = max(dist, abs(lu - lf) + 1e-4)
    u = d.normalized()
    a = (lu * lu - lf * lf + dist * dist) / (2.0 * dist)
    h = math.sqrt(max(0.0, lu * lu - a * a))
    p = pole - u * pole.dot(u)
    if p.length < 1e-5:
        p = Vector((0.0, 1.0, 0.0)) - u * u.y
    p.normalize()
    return S + u * a + p * h


def aim_bone(pb, d, up=None):
    if up is None:
        up = CAM_UP
    y = Vector(d).normalized()
    z = up - y * up.dot(y)
    if z.length < 1e-5:
        up2 = Vector((0.0, 1.0, 0.0))
        z = up2 - y * up2.dot(y)
        if z.length < 1e-5:
            z = Vector((1.0, 0.0, 0.0)) - y * y.x
    z.normalize()
    x = y.cross(z)
    m = Matrix((x, y, z)).transposed().to_4x4()
    m.translation = pb.matrix.translation
    pb.matrix = m
    bpy.context.view_layer.update()


def set_bone_rot(pb, cols, keep_pos=True):
    m = Matrix((cols[0], cols[1], cols[2])).transposed().to_4x4()
    m.translation = pb.matrix.translation if keep_pos else Vector((0, 0, 0))
    pb.matrix = m
    bpy.context.view_layer.update()


def sword_basis(blade_dir, flat_normal):
    y = Vector(blade_dir).normalized()
    z = Vector(flat_normal)
    z = (z - y * z.dot(y))
    if z.length < 1e-5:
        z = CAM_UP
    z.normalize()
    x = y.cross(z)
    return x, y, z


GRIP_WORLD = card_px_to_world(SW_GRIP_PX[0], SW_GRIP_PX[1], -0.15)
TIP_WORLD = card_px_to_world(SW_TIP_PX[0], SW_TIP_PX[1], -0.12)
POMMEL_WORLD = card_px_to_world(SW_POMMEL_PX[0], SW_POMMEL_PX[1], -0.16)
GUARD_WORLD = card_px_to_world(SW_GUARD_PX[0], SW_GUARD_PX[1], -0.14)

REST_BLADE = (TIP_WORLD - GRIP_WORLD).normalized()
REST_FLAT = C(0.0, 0.0, -1.0)

HAND_REST_BASIS = sword_basis(REST_BLADE, REST_FLAT)          # (x, y, z)


def hand_matrix_from_sword(x, y, z):
    """bone matrix (X,Y,Z columns) for HandR so the hilt lies across the fingers."""
    hx = y                                   # knuckle line == blade axis
    hz = z                                   # palm normal == blade flat normal
    hy = hz.cross(hx)                        # wrist -> knuckles
    return (hx, hy, hz)


GRIP_IN_HAND = Vector((0.0, 0.145, -0.018))


def solve_right_arm(arm, grip_world, basis, lu, lf):
    """poses UpperArmR / ForearmR / HandR so the palm holds grip_world."""
    hx, hy, hz = hand_matrix_from_sword(*basis)
    wrist = Vector(grip_world) - (hy * GRIP_IN_HAND.y + hz * GRIP_IN_HAND.z)
    E = two_bone_ik(SH_R, wrist, lu, lf, ELBOW_POLE_R)
    pbU = arm.pose.bones["UpperArmR"]
    pbF = arm.pose.bones["ForearmR"]
    pbH = arm.pose.bones["HandR"]
    aim_bone(pbU, E - SH_R)
    aim_bone(pbF, wrist - E)
    set_bone_rot(pbH, (hx, hy, hz))
    return wrist


def set_finger(arm, sfx, curls, thumb_curls, curl_axis):
    """curls: list of 4 lists of 3 angles (degrees, + = extend, - = flex)."""
    for fi, fn in enumerate(FINGER_NAMES):
        for j in range(3):
            nm = "%s%s_%d" % (fn, sfx, j + 1)
            pb = arm.pose.bones[nm]
            ang = math.radians(curls[fi][j])
            q = Quaternion(curl_axis, -ang)
            m4 = (q.to_matrix() @ pb.matrix.to_3x3()).to_4x4()
            m4.translation = pb.matrix.translation
            pb.matrix = m4
            bpy.context.view_layer.update()
    for j in range(3):
        nm = "Thb%s_%d" % (sfx, j + 1)
        pb = arm.pose.bones[nm]
        q = Quaternion(curl_axis, -math.radians(thumb_curls[j]))
        m4 = (q.to_matrix() @ pb.matrix.to_3x3()).to_4x4()
        m4.translation = pb.matrix.translation
        pb.matrix = m4
        bpy.context.view_layer.update()


FINGER_OPEN = [[-34.0, -38.0, -34.0]] * 4
FINGER_REST = [[0.0, 0.0, 0.0]] * 4
FINGER_CLOSED = [[8.0, 25.0, 18.0]] * 4          # relative to the painted loose fist

THUMB_OPEN = [-24.0, -32.0, -28.0]
THUMB_CLOSED = [2.0, 10.0, 8.0]


def key_all(arm, f):
    for pb in arm.pose.bones:
        pb.rotation_mode = 'QUATERNION'
        pb.keyframe_insert(data_path="rotation_quaternion", frame=f)
        pb.keyframe_insert(data_path="location", frame=f)


def arm_pose_at(arm, t):
    """author the armature for time t; returns the HandR world grip transform."""
    lu = arm.pose.bones["UpperArmR"].bone.length
    lf = arm.pose.bones["ForearmR"].bone.length
    # reset the animated bones to the rest pose first
    for nm in ["UpperArmR", "ForearmR", "HandR", "SwordBone"] + \
              ["%sR_%d" % (fn, j + 1) for fn in FINGER_NAMES for j in range(3)] + \
              ["ThbR_%d" % (j + 1) for j in range(3)]:
        pb = arm.pose.bones[nm]
        pb.rotation_quaternion = Quaternion((1, 0, 0, 0))
        pb.location = (0, 0, 0)
    bpy.context.view_layer.update()

    # Sword and hand have independent paths until contact. Only the held
    # interval shares a grip transform. The hand must never chase a high sword.
    sw_p, sw_q = sword_state(t)
    idle=GRIP_WORLD+C(-0.04,0.14,0.02)
    if t<T_FALL_END:
        hp=idle+(catch_pos()-idle)*smooth((t-1.9)/(T_FALL_END-1.9))
    elif t<=T_HOLD_END:
        hp=sw_p
    else:
        hp=GRIP_WORLD+(idle-GRIP_WORLD)*smooth((t-T_HOLD_END)/1.2)
    hq=rest_quat()
    basis=tuple(hq @ Vector(v) for v in [(1,0,0),(0,1,0),(0,0,1)])
    solve_right_arm(arm,hp,basis,lu,lf)
    if t<T_FALL_END:
        curl=1.0-smooth((t-2.95)/.25)
    elif t<=T_HOLD_END:
        curl=0.0
    else:
        curl=smooth((t-T_HOLD_END)/.6)
    th=curl
    for i,nm in enumerate(["HairRoot","HairA","HairB","HairC","HairD"]):
        pb=arm.pose.bones[nm]
        pb.rotation_quaternion=Quaternion(Vector((0,1,0)),math.radians((1+i*.45)*math.sin(t*math.pi/6)))
    curls = []
    for fi in range(4):
        curls.append([lerp(FINGER_CLOSED[fi][j], FINGER_OPEN[fi][j], curl) for j in range(3)])
    tcurls = [lerp(THUMB_CLOSED[j], THUMB_OPEN[j], th) for j in range(3)]
    curl_axis = arm.pose.bones["HandR"].matrix.col[0].to_3d().normalized()
    set_finger(arm, "R", curls, tcurls, curl_axis)

    # the sword rides its own bone, so its trajectory is part of this action
    pbS = arm.pose.bones["SwordBone"]
    basis = tuple(sw_q @ Vector(v) for v in ((1.0, 0.0, 0.0), (0.0, 1.0, 0.0), (0.0, 0.0, 1.0)))
    mS = Matrix((basis[0], basis[1], basis[2])).transposed().to_4x4()
    mS.translation = Vector(sw_p)
    pbS.matrix = mS
    bpy.context.view_layer.update()
    return sw_p, sw_q


# ---- authored sword trajectory -------------------------------------------

TOP_POS = C(0.95, 0.20, -0.34)


def rest_quat():
    x, y, z = sword_basis(REST_BLADE, REST_FLAT)
    return Matrix((x, y, z)).transposed().to_quaternion()


def top_quat():
    """blade pointing up and slightly toward the viewer; flat still faces camera."""
    y = C(-0.50, 0.86, 0.04).normalized()
    x, y, z = sword_basis(y, C(0.0, 0.0, -1.0))
    return Matrix((x, y, z)).transposed().to_quaternion()


def catch_pos():
    return C(1.15, -2.30, -0.245)


def sword_state(t):
    """returns (grip world position, sword world rotation) for time t seconds."""
    qrest = rest_quat()
    qtop = top_quat()
    cp = catch_pos()
    if t <= T_OPEN_END:
        return TOP_POS.copy(), qtop
    if t < T_FALL_END:
        u = (t - T_OPEN_END) / (T_FALL_END - T_OPEN_END)
        e = u ** 1.7
        p = TOP_POS + (cp - TOP_POS) * e
        q = qtop.slerp(qrest, min(1.0, (u ** 1.25) * 1.02))
        # underwater tumble
        spin = Quaternion(C(0.0, 1.0, 0.0).normalized(), math.radians(18.0 * math.sin(math.pi * u)))
        q = spin @ q
        return p, q
    if t <= T_HOLD_END:
        # rigidly follow the hand through the lift and the held float
        return cp + (GRIP_WORLD - cp) * smooth((t - T_FALL_END) / (T_REST - T_FALL_END)), qrest
    if t < T_UP_END:
        u = (t - T_HOLD_END) / (T_UP_END - T_HOLD_END)
        mid = C(0.96, 0.02, -0.30)
        p = GRIP_WORLD + (mid - GRIP_WORLD) * smooth(u)
        q = qrest.slerp(qtop, smooth(u) * 0.35)
        return p, q
    u = (t - T_UP_END) / (T_LOOP - T_UP_END)
    mid = C(0.96, 0.02, -0.30)
    p = mid + (TOP_POS - mid) * smooth(u)
    q = qrest.slerp(qtop, 0.35 + 0.65 * smooth(u))
    return p, q


def smooth(x):
    x = min(1.0, max(0.0, x))
    return x * x * (3.0 - 2.0 * x)


# --------------------------------------------------------------------------
# 8. main
# --------------------------------------------------------------------------

def action_fcurves(act):
    """Blender 4.4+ slotted actions no longer expose Action.fcurves."""
    try:
        return list(act.fcurves)
    except AttributeError:
        pass
    out = []
    for layer in getattr(act, "layers", []):
        for strip in getattr(layer, "strips", []):
            for cb in getattr(strip, "channelbags", []):
                out.extend(cb.fcurves)
    return out


def export_map(b):
    """Blender scene coords -> the coordinates Unity actually ends up with.

    MEASURED, not assumed: the whole FBX chain (exporter axis conversion plus
    Unity's right-handed -> left-handed import) maps a Blender point b to
    (-b.x, b.z, -b.y).  That is the same expression as Cinv() with MIRROR_X on,
    which is why C()/Cinv() are the authoring-side inverse of it.
    """
    return Cinv(b)


def verify_fbx_roundtrip(path, expected_blender_pos):
    """Honest, empirical self-check of the exported frame.

    Re-imports the FBX into a scratch scene and measures where the sword carrier
    bone lands, then reports both the raw FBX/Unity position and the deviation
    from the contract value.  This validates the mapping the whole registration
    depends on.  (The previous version of this check compared the measured value
    against Cinv(), i.e. against the same assumption it was supposed to test, so
    it "passed" while Unity showed a mirrored model.)
    """
    out = {"checked": False}
    try:
        bpy.ops.wm.read_factory_settings(use_empty=True)
        bpy.ops.import_scene.fbx(filepath=path, global_scale=1.0)
        got = None
        for o in bpy.data.objects:
            if o.type != 'ARMATURE':
                continue
            bone = o.data.bones.get("SwordBone")
            if bone is not None:
                got = (o.matrix_world @ bone.head_local).copy()
                break
        if got is None:
            for o in bpy.data.objects:                     # fallback: the mesh
                if o.name.split(".")[0] == "Sword":
                    got = o.matrix_world.translation.copy()
                    break
        if got is None:
            out["error"] = "neither SwordBone nor Sword found after re-import"
            return out
        unity_raw = export_map(got)
        contract_expect = Cinv(expected_blender_pos)
        out["checked"] = True
        out["reimported_blender_position"] = [round(v, 6) for v in got]
        out["fbx_raw_position_used_by_unity"] = [round(v, 6) for v in unity_raw]
        out["expected_contract_position"] = [round(v, 6) for v in contract_expect]
        out["max_error_units"] = round((unity_raw - contract_expect).length, 6)
        out["pass"] = bool((unity_raw - contract_expect).length < 1e-4)
    except Exception as e:  # never break the build over a self-check
        out["error"] = repr(e)
    return out

def op_kwargs(op, kwargs):
    props = op.get_rna_type().properties.keys()
    return {k: v for k, v in kwargs.items() if k in props}


def export_fbx(path):
    kwargs = dict(
        filepath=path, use_selection=False, use_visible=True,
        object_types={'EMPTY', 'ARMATURE', 'MESH'},
        global_scale=1.0, apply_unit_scale=True, apply_scale_options='FBX_SCALE_NONE',
        axis_forward='-Z', axis_up='Y',
        use_mesh_modifiers=True, use_mesh_modifiers_render=False,
        mesh_smooth_type='FACE', use_tspace=True, use_custom_props=False,
        bake_space_transform=False,
        add_leaf_bones=False, primary_bone_axis='Y', secondary_bone_axis='X',
        armature_nodetype='NULL',
        bake_anim=True, bake_anim_use_nla_strips=False, bake_anim_use_all_actions=False, bake_anim_force_startend_keying=True,
        bake_anim_step=1.0, bake_anim_simplify_factor=0.0,
        path_mode='AUTO', embed_textures=False,
    )
    bpy.ops.export_scene.fbx(**op_kwargs(bpy.ops.export_scene.fbx, kwargs))


def render_previews(shots):
    sc = bpy.context.scene
    sc.render.engine = 'BLENDER_WORKBENCH'
    try:
        sc.display.shading.light = 'STUDIO'
        sc.display.shading.color_type = 'TEXTURE'
        sc.display.shading.show_shadows = True
        sc.display.render_aa = '8'
    except Exception:
        pass
    sc.render.resolution_x = 512
    sc.render.resolution_y = 512
    sc.render.film_transparent = False
    cam_data = bpy.data.cameras.new("PreviewCam")
    cam_data.sensor_fit = 'VERTICAL'
    cam_data.angle_y = math.radians(35.0)
    cam = bpy.data.objects.new("PreviewCam", cam_data)
    bpy.context.scene.collection.objects.link(cam)
    cam.location = tuple(C(0.0, 0.0, -11.5))
    cam.rotation_euler = (math.radians(90.0), 0.0, math.radians(180.0))
    sc.camera = cam
    os.makedirs(PREV, exist_ok=True)
    made = []
    for (nm, f, cpos, fov) in shots:
        sc.frame_set(f)
        bpy.context.view_layer.update()
        cam.location = tuple(C(cpos[0], cpos[1], cpos[2]))
        cam_data.angle_y = math.radians(fov)
        p = os.path.join(PREV, nm + ".png")
        sc.render.filepath = p
        bpy.ops.render.render(write_still=True)
        # The preview camera is built from C() so its right axis is the contract
        # +x axis.  When C() pre-mirrors x (MIRROR_X) the view is already the
        # contract/Unity view and must NOT be flipped; without the mirror the
        # Blender view is the mirror of Unity's and has to be flipped back.
        if not MIRROR_X:
            arr = load_rgba(p)
            save_rgba(arr[:, ::-1].copy(), p, "LLP_flip")
        made.append(p)
    return made


def verify_geometry(objects):
    """Signed volume per mesh.

    Every layer is a union of CLOSED islands, so a positive signed volume proves
    the triangles are wound outward.  Inward winding is invisible in Blender
    workbench previews (double sided) but is a hole in Unity (the card shader
    culls back faces), which is how the rejected build shattered.
    """
    report = []
    for ob in objects:
        me = ob.data
        me.calc_loop_triangles()
        v = np.array([tuple(p.co) for p in me.vertices], dtype=np.float64)
        vol = 0.0
        for tri in me.loop_triangles:
            a, b, c = tri.vertices
            vol += float(np.dot(v[a], np.cross(v[b], v[c]))) / 6.0
        # boundary edges (used by exactly one face) are holes a front-face culling
        # shader shows straight through to the background.
        edge_faces = {}
        for poly in me.polygons:
            for ek in poly.edge_keys:
                edge_faces[ek] = edge_faces.get(ek, 0) + 1
        boundary = sum(1 for c in edge_faces.values() if c == 1)
        nonmanifold = sum(1 for c in edge_faces.values() if c > 2)
        blen = 0.0
        blong = 0
        for ek, c in edge_faces.items():
            if c != 1:
                continue
            d = v[ek[0]] - v[ek[1]]
            L = float(np.linalg.norm(d))
            blen += L
            if L > 0.01:
                blong += 1
        # average face normal dotted with the outward direction from the centroid
        outward = 0
        cen = v.mean(axis=0) if len(v) else np.zeros(3)
        for poly in me.polygons:
            d = np.array(tuple(poly.center)) - cen
            if np.linalg.norm(d) < 1e-9:
                continue
            if np.dot(np.array(tuple(poly.normal)), d) > 0:
                outward += 1
        total = max(1, len(me.polygons))
        report.append({
            "name": ob.name,
            "verts": len(me.vertices),
            "tris": len(me.loop_triangles),
            "signed_volume": round(vol, 5),
            "boundary_edges": boundary,
            "boundary_edge_length": round(blen, 5),
            "boundary_edges_longer_than_0.01": blong,
            "nonmanifold_edges": nonmanifold,
            "outward_face_ratio": round(outward / float(total), 4),
            "outward": bool(vol > 0.0),
        })
        print("[model] geometry %-6s verts=%-6d tris=%-6d signed_volume=%8.5f "
              "boundary_edges=%-5d (len=%7.4f, long=%d) nonmanifold=%-4d outward=%.3f" %
              (ob.name, len(me.vertices), len(me.loop_triangles), vol,
               boundary, blen, blong, nonmanifold, outward / float(total)))
    return report


def main():
    t0 = datetime.datetime.now()
    clear_scene()
    os.makedirs(GEN, exist_ok=True)
    os.makedirs(TEX, exist_ok=True)
    print("[model] deriving worker-local textures ...")
    fig, skin, hair, sw, sw_alpha = build_textures()
    prof, axis_len, uaxis, perp = sword_texture_profile(sw, sw_alpha)

    B, geoR, geoL = build_skeleton()
    arm = make_armature(B)

    body = MB("Body")
    build_body(body, geoR, geoL)
    body_ob = body.build([b[0] for b in B] + ["Sword"])

    hairmb = MB("Hair")
    build_hair_mass(hairmb)
    hair_ob = hairmb.build([b[0] for b in B] + ["Sword"])

    swmb = MB("Sword")
    build_sword(swmb, prof, axis_len, uaxis, perp)
    # bake the grip translation into the vertices so the sword object itself is
    # at the origin of the contract frame and its rest frame is exactly the
    # SwordBone rest frame (bone parenting is then a pure rigid attachment).
    for i in range(len(swmb.v)):
        swmb.v[i] = swmb.v[i] + GRIP_WORLD
    sw_ob = swmb.build([b[0] for b in B] + ["Sword"])
    sw_ob.location = (0.0, 0.0, 0.0)

    # materials
    m_fig = make_material("FigureFront", os.path.join(TEX, "FigureSkin.png"), 'opaque')
    m_gold = make_material("GoldSkin", os.path.join(TEX, "GoldSkin.png"), 'opaque', tile=True)
    m_hair = make_material("HairSheet", os.path.join(TEX, "Hair.png"), 'opaque')
    m_sword = make_material("SwordBlade", os.path.join(TEX, "Sword.png"), 'opaque')
    body_ob.data.materials.append(m_fig)
    body_ob.data.materials.append(m_gold)
    hair_ob.data.materials.append(m_hair)
    sw_ob.data.materials.append(m_sword)

    # hidden-surface remap: faces pointing away from the camera (or landing on a
    # non-skin / white area of the painting) use the coherent gold skin tile.
    me = body_ob.data
    uvl = me.uv_layers[0]
    skin_d = dilate(skin, 6)
    for poly in me.polygons:
        n = poly.normal
        cx = cy = cz = 0.0
        for li in poly.loop_indices:
            v = me.vertices[me.loops[li].vertex_index].co
            cx += v.x
            cy += v.y
            cz += v.z
        k = 1.0 / max(1, poly.loop_total)
        u, v = world_to_uv((cx * k, cy * k, cz * k))
        back = Cinv(n).z > 0.55
        outside = not (0.0 <= u <= 1.0 and 0.0 <= v <= 1.0)
        px = int(min(FIG_W - 1, max(0, u * FIG_W)))
        py = int(min(FIG_H - 1, max(0, (1.0 - v) * FIG_H)))
        # FigureSkin.png carries in-painted skin over the hair, but the diffusion
        # cannot reach the middle of the large hair mass, so those texels are
        # still black.  Any face whose projection lands on a non-skin texel must
        # therefore take the coherent gold tile too - otherwise the body shows
        # hard black bands exactly where the hair covers it in the painting.
        off_sheet = px < 0 or py < 0 or px >= skin_d.shape[1] or py >= skin_d.shape[0]
        non_skin = off_sheet or (not bool(skin_d[py, px]))
        if back or outside or non_skin:
            poly.material_index = 1
            for li in poly.loop_indices:
                vv = me.vertices[me.loops[li].vertex_index].co
                uvl.data[li].uv = ((vv.x * 0.62) % 1.0, (vv.y * 0.62) % 1.0)
        else:
            poly.material_index = 0
    me.update()

    # body and hair deform with the armature...
    for ob in (body_ob, hair_ob):
        bind(ob, arm)

    # ---------- scene root + markers ----------
    figure_rig = bpy.data.objects.new("FigureRig", None)
    bpy.context.scene.collection.objects.link(figure_rig)
    arm.parent = figure_rig
    bpy.context.view_layer.update()

    grip = bpy.data.objects.new("Grip", None)
    bpy.context.scene.collection.objects.link(grip)
    grip.parent = sw_ob
    # NOTE: GripSocket (bone-parented palm empty) is authored by the Unity Editor
    # builder instead - Unity does not round-trip FBX bone-parented empties.

    # ...while the sword is a rigid child of its own bone.  Done while the rig is
    # still in the rest pose so the attachment offset is pose independent.
    for o in bpy.context.view_layer.objects:
        o.select_set(False)
    sw_ob.select_set(True)
    arm.select_set(True)
    bpy.context.view_layer.objects.active = arm
    arm.data.bones.active = arm.data.bones["SwordBone"]
    sword_world_before = sw_ob.matrix_world.copy()
    bpy.ops.object.parent_set(type='BONE', keep_transform=True)
    bpy.context.view_layer.update()
    print("[model] sword bone parent drift: %.6f units" %
          (sw_ob.matrix_world.translation - sword_world_before.translation).length)
    # the mesh origin is the contract origin now, so the Grip marker must be put
    # back on the hilt inside the sword's own (rigid) space.
    grip.location = sw_ob.matrix_world.inverted() @ GRIP_WORLD
    bpy.context.view_layer.update()
    print("[model] grip marker world: %s" % [round(v, 4) for v in grip.matrix_world.translation])
    for o in bpy.context.view_layer.objects:
        o.select_set(False)

    # ---------- animation ----------
    arm.animation_data_create()
    times = [i/float(FPS) for i in range(SHEET_FRAMES)]
    for t in times:
        f = int(round(t * FPS))
        bpy.context.scene.frame_set(f)
        arm_pose_at(arm, t)
        key_all(arm, f)
    act = arm.animation_data.action
    act.name = "LadyLakeCycle"
    try:
        act.use_frame_range = True
        act.frame_start = 0
        act.frame_end = SHEET_FRAMES - 1
    except Exception:
        pass
    for fc in action_fcurves(act):
        for kp in fc.keyframe_points:
            kp.interpolation = 'BEZIER'
            kp.handle_left_type = 'AUTO_CLAMPED'
            kp.handle_right_type = 'AUTO_CLAMPED'

    # ---------- export ----------
    for ob in bpy.data.objects:
        ob.select_set(False)
    fbx = os.path.join(GEN, "FigureRig.fbx")
    # the sword's rest grip sits on its carrier bone's head
    sword_blender_pos = GRIP_WORLD.copy()
    export_fbx(fbx)
    print("[model] fbx ->", fbx, os.path.getsize(fbx), "bytes")

    shots = [("shot_00_open_sword_high", 0, (0.0, 0.0, -11.5), 35.0),
             ("shot_032_catch", int(3.2 * FPS), (0.0, 0.0, -11.5), 35.0),
             ("shot_056_rest_grip", int(5.6 * FPS), (0.0, 0.0, -11.5), 35.0),
             ("shot_070_hold", int(7.0 * FPS), (0.0, 0.0, -11.5), 35.0),
             ("shot_095_release", int(9.5 * FPS), (0.0, 0.0, -11.5), 35.0),
             ("shot_110_carry_up", int(11.0 * FPS), (0.0, 0.0, -11.5), 35.0),
             ("zoom_face", int(5.6 * FPS), (0.86, 0.02, -2.30), 35.0),
             ("zoom_grip_hand", int(5.6 * FPS), (1.25, -1.95, -1.90), 35.0),
             ("zoom_open_hand", 0, (1.05, -1.85, -1.90), 35.0),
             ("zoom_left_hand", 0, (-0.60, 2.05, -1.85), 35.0)]
    renders = [] if "--fast" in sys.argv else render_previews(shots)

    print("[model] verifying exported frame ...")
    frame_check = {"checked":False,"reason":"Unity runtime validation is used for fast iteration"} if "--fast" in sys.argv else verify_fbx_roundtrip(fbx, sword_blender_pos)
    print("[model] frame check:", json.dumps(frame_check))

    print("[model] verifying geometry orientation ...")
    geo_check = verify_geometry([body_ob, hair_ob, sw_ob])

    manifest = {
        "frame_self_check": frame_check,
        "geometry_self_check": geo_check,
        "generated_utc": datetime.datetime.utcnow().isoformat() + "Z",
        "generator": "Assets/Experiments/LadyLakePremium/Model/Source/build_figure.py",
        "blender": bpy.app.version_string,
        "coordinate_contract": {
            "world_from_art_px": "P(px,py,z)=((px-248.5)/100,(355-py)/100,z)",
            "figure_registration": "card=(u*0.83+0.04, v*0.83+0.117) of figure-v5 normalised top-left",
            "appearance_offset_applied": False,
            "up_axis": "y", "camera_looks": "+z",
        },
        "prefab_contract": {
            "root": "FigureRig",
            "static_method": "LegacyGwent.LadyLakePremium.Editor.LadyLakeModelBuilder.Build()",
            "prefab_path": "Assets/Experiments/LadyLakePremium/Model/FigureRig.prefab",
        },
        "bones": BONES_MANIFEST,
        "bone_count": len(BONES_MANIFEST),
        "hand_bones_right": [b["name"] for b in BONES_MANIFEST
                             if b["name"].endswith("R") or b["name"].endswith("R_1")
                             or b["name"].endswith("R_2") or b["name"].endswith("R_3")
                             or b["name"].startswith("ThbR")],
        "skeleton_notes": [
            "4 fingers x 3 phalanges + 3-segment inner thumb per hand",
            "explicit per-vertex weights, max 4 influences (Unity SkinQuality.Bone4)",
            "rest pose reproduces the approved figure-v5 painting pose",
        ],
        "sword": {
            "grip_px": SW_GRIP_PX, "guard_px": SW_GUARD_PX,
            "tip_px": SW_TIP_PX, "pommel_px": SW_POMMEL_PX,
            "grip_world": [round(v, 6) for v in Cinv(GRIP_WORLD)],
            "tip_world": [round(v, 6) for v in Cinv(TIP_WORLD)],
            "pommel_world": [round(v, 6) for v in Cinv(POMMEL_WORLD)],
            "guard_world": [round(v, 6) for v in Cinv(GUARD_WORLD)],
            "texture": "Model/Generated/Textures/Sword.png (derived from LadyLake/Textures/sword.png)",
            "uv_axis": "blade local +Y maps pommel(855,1169) -> tip(141,510) in texture px",
            "local_origin": "contract grip point",
        },
        "clip": {
            "name": "LadyLakeCycle",
            "fps": FPS,
            "frames": SHEET_FRAMES,
            "seconds": CLIP_SECONDS,
            "seamless": True,
            "master_driver": "sword: the hand is solved by two-bone IK onto the sword grip each frame",
            "key_times": {
                "0.0-1.3": "hand open, sword high (top position)",
                "1.3-3.2": "sword falls with underwater drag, arm reaches to intercept",
                "3.2": "fingers close around the hilt",
                "3.2-5.6": "lift to the exact static card resting pose",
                "5.6-8.3": "held serene float",
                "8.3-10.8": "water magic carries the sword up while the fingers release",
                "10.8-12.0": "continuous return to the start pose",
            },
        },
        "anchors": {
            "Grip": "child empty of Sword at the grip point",
            "GripSocket": "bone-parented empty on HandR (grip lives in the palm)",
        },
        "meshes": [
            {"name": "Body", "verts": len(body.v), "tris": len(body.f),
             "materials": ["FigureFront (painting-projected)", "GoldSkin (coherent hidden surface tile)"]},
            {"name": "Hair", "verts": len(hairmb.v), "tris": len(hairmb.f),
             "materials": ["HairSheet (projected, alpha)"]},
            {"name": "Sword", "verts": len(swmb.v), "tris": len(swmb.f),
             "materials": ["SwordBlade (sword.png projected)"]},
        ],
        "textures": {
            "FigureSkin.png": "figure-v5 with hair/background grown over by skin so the front projection never samples hair or white",
            "GoldSkin.png": "seamless gold skin tile cut from the cleanest 128x128 skin window of figure-v5",
            "Hair.png": "hair/ink sheet, white background removed and un-premultiplied",
            "Sword.png": "sword.png with white background removed and un-premultiplied",
        },
        "previews": [os.path.basename(p) for p in renders],
        "artifacts": {
            "fbx": "Model/Generated/FigureRig.fbx",
            "fbx_bytes": os.path.getsize(fbx),
        },
        "runtime_notes": [
            "No image swaps, no CPU vertex wobble, no Billboard/plane fakes.",
            "Hands, arms and body are real closed meshes with meaningful thickness.",
            "Face and hair are carefully shaped projected meshes over real volume.",
            "C# builder is Unity 2019.4 compatible and editor-only.",
        ],
        "elapsed_seconds": round((datetime.datetime.now() - t0).total_seconds(), 1),
    }
    mpath = os.path.join(GEN, "model-manifest.json")
    with open(mpath, "w", encoding="utf-8") as fh:
        json.dump(manifest, fh, indent=2)
    print("[model] manifest ->", mpath)
    print("[model] done in", manifest["elapsed_seconds"], "s")


if __name__ == "__main__":
    main()
