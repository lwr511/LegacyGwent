Independent root review before acceptance (2026-09-27)
- curved_shell receives Blender-space p from fig_to_world but constructs Vector((p.x,p.y,zfront)), discarding the screen vertical p.z after the C() coordinate conversion. Hair and face shells collapse to a horizontal slab. Fix in C coordinates.
- build_hair_mass solid mask ignores source alpha (transparent pixels are black, not white); require alpha>0.1 so background cannot become a hair rectangle.
- save_rgba float-buffer export gamma-encodes already encoded pixel values: source (183,121,57) became (221,183,130). Preserve exact original RGB when deriving texture source; fix byte PNG export, not arbitrary color tint.
- arm_pose_at sends grip target to sword at ALL times, so the hand follows the falling sword from above instead of waiting and intercepting. Author separate hand trajectory until3.2 and after8.3; rigid grip only while held.
- sword fall ends with150degree extra spin but next frame drops spin tozero. Smooth transient spin must tend to0 at catch.
- finger bone base pose is curled negative38/44/40deg. Current negative CLOSED values counter-rotate and straighten fingers; OPEN adds flex. Confirm with actual mesh tip distances and use physically correct closing curves.
- shader Standard with no lights differs from existing original painted-image material path; reuse production shader and correct depth writing rather than black/grey PBR appearance.
- AnimationUtility.SetCurve is not Unity2019 API; use SetEditorCurve.
- GoldSkin import sRGB false while color texture requires true.
First Blender renders are rejected, not visual acceptance. Root takes over these specific fixes after model worker completes to avoid simultaneous edits.
