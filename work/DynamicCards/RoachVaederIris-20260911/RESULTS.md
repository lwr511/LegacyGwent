# Roach, Vaedermakar, Iris playback audit

Current main cache tested directly in an independent Unity 2019 process. No main-project assets, runtime code, catalog, or resource packages changed.

- Roach, art 11221000 -> Legacy2017/11221001: intro begins with empty roof, horse descends into view around the 3.1333-second intro. Roach and Geralt meshes move in the 20.9667-second loop; the horse temporarily leaves the roof. Prior delivered Latest/10290101 playback also includes leaving/reappearing. Both previews restart the intro after re-enable.
- Iris von Everec, art 11221500 -> Legacy2017/11221501: actual intro clip plays, then transitions to Loop. Initial low/off-center pose moves into the full card, with body/dress and petals/rays active. Large and small previews run beyond 30 seconds and restart after re-enable.
- Vaedermakar, art 11320800 -> Legacy2017/11320801: snow, rain, clouds and lightning animate. Character and cape use AlphaBlended_wavey; absence of Animator/SkinnedMeshRenderer is expected for this source. Available old and newer conversion/catalog data have no separate skeletal intro. The final isolated render draws each original character/cape submesh and its unmodified source material through a separate camera command buffer, excluding environmental pixels from the motion comparison.

Current/baseline-results.json contains 66 actual-runtime samples, including immediate bind, intro, loop past 30 seconds, and re-enable. Current/Baseline holds the captured frames. PriorRoach contains the earlier-source comparison. VaederIsolated contains the final GPU-only body/cape captures; vaeder-isolated-final.log is the final isolation run. result.json records assertions and main cache hashes against the preceding verified delivery.
