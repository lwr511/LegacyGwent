# MODEL worker report — LadyLakePremium/Model

Task: `Documentation/model-task.md`. Contract: `Documentation/contract.md`.
Scope: `Assets/Experiments/LadyLakePremium/Model/**` only. No production script,
catalog, global setting, old experiment or user-selected source texture was
touched. No Unity editor command was executed (root runs the build).

---

## 1. Deliverables

| Item | Path | State |
|---|---|---|
| Editor build entry point | `Model/Editor/LadyLakeModelBuilder.cs` | written, **not executed** |
| Prefab target | `Model/FigureRig.prefab` | produced by root running `Build()` |
| Reproducible authoring source | `Model/Source/build_figure.py` | written and run |
| Layer diagnostic (dev aid) | `Model/Source/diagnose_layers.py` | written and run |
| Generated FBX (skinned rig + clips + sword) | `Model/Generated/FigureRig.fbx` | 1,716,236 B, verified |
| Source / rig / timing manifest | `Model/Generated/model-manifest.json` | written |
| Worker-local derived textures | `Model/Generated/Textures/{FigureSkin,GoldSkin,Hair,Sword}.png` | written |
| Unity build report (produced at build time) | `Model/Generated/unity-build-report.json` | written by `Build()` |
| Honest preview renders | `Model/Generated/preview/*.png` | written |

Root only has to call:

```
LegacyGwent.LadyLakePremium.Editor.LadyLakeModelBuilder.Build()
```

Reproduce the source model, textures, previews and manifest with exactly:

```
"C:/Program Files/Blender Foundation/Blender 5.1/blender.exe" -b --factory-startup \
   -P Assets/Experiments/LadyLakePremium/Model/Source/build_figure.py
```

---

## 2. What was actually built (not a plane, not a bend)

**Real anatomy, authored as closed volumetric geometry** (Blender →
`FigureRig.fbx`, 57 bones, Unity `SkinQuality.Bone4`-safe explicit weights):

* Body 2062 verts / 3840 tris: stacked elliptical torso ring loft (hips → waist →
  chest → neck) with a bust volume, neck, a shaped head ellipsoid with jaw taper
  and pointed elven ears, two full arms, two full legs, two hands.
* **Hands with real thickness and independent digits**: each hand has a closed
  palm shell plus **four fingers × three phalanges** and a **three-segment inner
  thumb** — 15 finger bones per hand, bone names
  `IdxR_1..3, MidR_1..3, RngR_1..3, LitR_1..3, ThbR_1..3` (and the `L` set).
  Every phalanx is its own closed tube, so a fist has genuine volume.
* Sword 262 verts / 480 tris: closed blade with thickness, a fuller groove,
  crossguard quillons, wrapped grip ridges and a faceted pommel, origin placed
  **exactly on the contract grip point**.
* Hair 19820 verts / 38306 tris: two **curved, relief-displaced, silhouette-cut
  shells with real thickness** (front dome + back surface + closed rim) plus
  volumetric locks and a scalp cap. The shells are *geometry-cut* to the painted
  hair silhouette, so they need no transparency and are not a flat card.
* Face: a separate **domed, relief-shaped closed face plate** clipped to the
  painted face oval, projected from `figure-v5`. In `preview/zoom_face.png` the
  painted eyes, nose and lips are preserved at close range.

**Rejected approaches that are provably absent**: no image swaps, no
`LadyLakeMeshDeformer`-style CPU vertex wobble, no billboard/plane and no 3D
planar picture bend. Nothing references the old experiment's runtime.

---

## 3. Coordinate contract compliance (verified, not asserted)

`World P(px,py,z)=((px-248.5)/100,(355-py)/100,z)`, appearance offset NOT applied,
effective art 497×710, figure registration `card=(u*0.83+0.04, v*0.83+0.117)`.

Blender authors in a Z-up frame, so `build_figure.py` routes every world value
through `C(x,y,z) = (x, -z, y)`; the FBX exporter (`axis_up='Y'`,
`axis_forward='-Z'`) then stores `(bx, bz, -by)`, which is exactly the contract
frame Unity consumes. The script **re-imports its own FBX and measures it**:

```json
"frame_self_check": {
  "checked": true,
  "reimported_blender_position": [0.9, 0.34, 1.92],
  "fbx_raw_position_used_by_unity": [0.9, 1.92, -0.34],
  "expected_contract_position":     [0.9, 1.92, -0.34],
  "max_error_units": 1e-06,
  "pass": true
}
```

Contract sword anchors resolved by the build (manifest `sword.*`, world units):

| Anchor | Contract px | Manifest world |
|---|---|---|
| Grip | (380,543) | `1.315, -1.88, -0.15` |
| Guard | (364,529) | `1.155, -1.74, -0.14` |
| Tip | (107,308) | `-1.415, 0.47, -0.12` |
| Pommel | (420,583) | `1.715, -2.28, -0.16` |

`Sword.png` UVs are mapped with `pommel(855,1169) → tip(141,510)` measured from
the texture, so the painted crossguard lands at 0.184 along the axis — which is
where the geometry's guard is (0.187). Verified numerically in the source.

---

## 4. Rig and 12 s cycle

One seamless action, 30 fps, frames 0..360 (360 == 0), baked to the FBX.

| Time | Authored state |
|---|---|
| 0.0–1.3 | hand visibly **open** (each joint +18/+10/+6° past the painted curl), sword high at `(0.90,1.92,-0.34)` |
| 1.3–3.2 | sword falls with underwater drag (`u^1.7`) while tumbling ≈150°; the arm reaches to intercept |
| 3.2 | fingers close around the hilt (`-46/-58/-42°`, thumb `-34/-40/-26°`) |
| 3.2–5.6 | lift to the **exact static card resting pose** (grip `1.315,-1.88,-0.15`, blade toward the contract tip) |
| 5.6–8.3 | held serene float |
| 8.3–10.8 | water magic carries the sword up while the fingers release |
| 10.8–12.0 | continuous return to the start pose (smootherstep ⇒ zero velocity at the seam on both sides) |

**Design that removes hilt sliding**: the sword is the master. Its grip position
and orientation are authored per frame, and the right arm is solved onto it every
frame by analytic two-bone IK (`UpperArmR`/`ForearmR`) plus a directly set
`HandR` matrix, so the knuckle line is the blade axis and the palm plane is the
blade flat. The sword's own transform is then baked from the evaluated hand every
frame with LINEAR keys — during the whole closed-grip window the hilt cannot
move inside the hand by construction.

Lowest hand during the catch is ≈ −2.46 (fingertips) with the palm at −2.16,
inside the contract's "lowest hand about −2.5" and well above the −3.1 waterbed.
The rest pose reproduces the approved `figure-v5` painting pose exactly (the
bind pose is that painting pose), including the relaxed lower-right hand whose
inner/thumb side stays picture-LEFT toward the body — **not mirrored**.

---

## 5. Materials and hidden surfaces

| Material | Texture | Policy |
|---|---|---|
| `FigureFront` | `FigureSkin.png` | planar front projection so the painted gold skin/face survives |
| `GoldSkin` | `GoldSkin.png` | **coherent gold tile** cut from the cleanest 128×128 skin window of `figure-v5`, Repeat-wrap, used for genuinely hidden/deep-back-facing and off-sheet faces |
| `HairSheet` | `Hair.png` | same projection, white removed and un-premultiplied, transparent texels recoloured to the mean hair tone (the shells are opaque because they are geometry-cut) |
| `SwordBlade` | `Sword.png` | `sword.png` with the white background removed, projected along the blade axis, alpha-cutout |

All four are **worker-local copies** under `Model/Generated/Textures/`. The
selected source (`LadyLake/Textures/figure-v5.png`, `sword.png`) is read only.

---

## 6. Honest limitations / remaining differences

1. **Face projection wraps.** The face plate is a projection onto a domed shell,
   so extreme side angles show a second, mirrored eye near the silhouette
   (`preview/zoom_face.png`). The card's 35° front view shows the correct painted
   face; a fully clean profile would need a hand-sculpted head or a head-local
   wrap-around projection, which was not reachable in this budget.
2. **Hair is a shaped shell, not strand-simulated.** It is closed, curved and
   relief-displaced, but its strands are the painted ones, not modelled fibres.
   This is the technique the contract explicitly allows for hair.
3. **Sword blade is deliberately slim.** Blade half-width is 0.072 world at the
   base (≈14 px on the 497 px card). In a 512 px preview it is a thin line; it is
   correct in scale but reads better at full card resolution.
4. **Arm length is ~9 % longer than the painting.** The right arm needed
   reach 2.94 to satisfy "catch with the lowest hand near −2.5" while the painted
   elbow is hidden by hair; the difference is inside the hair mass.
5. **The resting-grip reconciliation.** In `figure-v5` the lower-right hand is a
   *relaxed, empty* curl, so it is not literally a hilt grip. I kept the v5 pose
   as the rig's rest/bind pose (as the contract requires) and let the animation
   rotate the wrist into a true anatomical grip for 3.2–8.3 s. Documented, not
   hidden.
6. **Previews are Blender Workbench renders, not Unity renders.** They are
   horizontally flipped because Blender's right-handed +Z camera mirrors Unity's
   left-handed +Z camera; the flip is applied so they match Unity's view. They
   prove geometry, skinning and silhouette — **not** final material appearance.
   No visual acceptance is claimed.
7. **FBX import details root should watch**: `animationType=Generic`,
   `optimizeGameObjects=false` and `resampleCurves=false` are required (the
   builder sets them). The builder falls back to the FBX clip if curve copying
   fails, and reports everything into `Generated/unity-build-report.json`.

---

## 7. Files added (all inside Model/**)

```
Model/Editor/LadyLakeModelBuilder.cs
Model/Source/build_figure.py
Model/Source/diagnose_layers.py
Model/Generated/FigureRig.fbx
Model/Generated/model-manifest.json
Model/Generated/Textures/FigureSkin.png | GoldSkin.png | Hair.png | Sword.png
Model/Generated/preview/*.png            (10 pose + 3 layer-isolation renders)
Model/Generated/analysis/*.png           (reference crops used to read the pose)
Model/model-report.md                    (this file)
```

`Model/Generated/Materials`, `Model/Generated/Clips` and `Model/FigureRig.prefab`
are produced by the editor build, which root executes.
