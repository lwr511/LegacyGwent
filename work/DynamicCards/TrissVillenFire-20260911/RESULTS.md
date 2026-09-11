# Triss and Villentretenmerth fire repair

## Scope and evidence
- Triss art 11210600, Legacy2017/11210601: main-cache baseline reproduced long fire strips and rectangles around hands and the background. Source RGBA sheet is intact; alphaIsTransparency=1 corrupts packed frame channels. Alpha-off reference playback removes those shapes. Only this texture importer flag changed, not PNG pixels, compression, material or particle settings.
- Villentretenmerth art 11210700: main-cache Legacy2017/11210701 shows broad white ground patches. The prior delivered Latest/10130101 scene has detailed ground fire, forest and animated dragon. Restored that verified scene and its source camera/settings. No speculative old-shader brightness adjustment was made.
- Gold source selection changed during bba82a5fc; the earlier source is also recorded in preferred_card_sources.json. Restore retained the old entry without an art mapping, preserving existing Legacy part groups.
- Dependency restoration copied 38 absent assets with their metadata from BeforeOldSources-20260908. The closure has 50 assets; existing dependencies were reused. restore-plan.json and restore-applied.json record identity and SHA256. catalog-before.json is the rollback snapshot.

## Verification
PASS. Main Unity editor BuildBundle completed at 2026-09-11T11:58:19.9977827Z with material/skin validation. Temporary editor build runner removed afterward.

An independent Unity 2019 process loaded the absolute MAIN cache path, with no runtime material overrides. Both cards completed small/large playback, over 18 seconds, and disable/re-enable: 20 samples. Skinned mesh deltas verify both characters continue animating. Final visual review of large playback and resumed small previews shows the reported long fire strips/rectangle edges removed from Triss and detailed ground fire restored for the dragon.

The actual Triss bundle texture matches the verified alpha-off reference on all 1,048,576 GPU-read pixels: zero mismatches, maximum delta zero. 80 relevant source files and metadata match the delivered cache manifest. 28 payload files total 1,745,510,084 bytes. Five Legacy partitions using the shared texture, the Latest partitions affected by the restored source, and catalog/index changed; all other payload hashes match the preceding delivery. Adding the restored Latest scene crosses the partition-size boundary and creates cards-latest-005.bundle.

Evidence: Delivered/Baseline/*.png, Delivered/baseline-results.json, Delivered/texture-pixel-checks.txt, delivery-audit.json, source-audit.json. No game installer was generated.
