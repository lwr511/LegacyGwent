# Global camera framing correction

This supersedes the earlier three-card calibration. DynamicCardFraming no longer branches on card IDs. Every dynamic card, including future catalog entries, uses a shared portrait-space vertical framing baseline. Existing top trim is credited to avoid applying the upward shift twice. Art UV crop and camera adjustment now share the same region calculation.

Production files: Assets/DynamicCards/Runtime/DynamicCardFraming.cs and DynamicCardView.cs. The correction runs before the first frame and remains in place through intro, loop and dragging. Perspective scale and drag limits are unchanged. The existing optional content packages remain valid; no content rebuild is needed.

Verification: camera_alignment_global_final.log passed projection validity and thumbnail-size invariance for all 1995 catalog variants. 1994 require additional lift; one already has sufficient top trim. All entries run through the same rule. Twelve visual samples cover Native, Legacy and Latest sources plus all current factions. First and loop comparisons are in GlobalFinal: left static, middle original dynamic, right corrected dynamic, with both dynamic columns at the same animation time. This is not per-frame visual acceptance of all 1995 variants.

Earlier Global images show the intermediate candidate before top-trim compensation; use GlobalFinal for the delivered result. BeforeGlobal contains backups of the prior three-card implementation.

Trial of the Grasses still has a separate bright/blocky particle issue. Its geometry comparison temporarily hides particles for inspection only; production particles are unchanged.
