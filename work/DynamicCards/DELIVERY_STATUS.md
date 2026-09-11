# Dynamic card delivery status

Code, complete converted source inventory, and tested Windows runtime packages are installed in the primary Unity project. Full main-game UI, all-card visual acceptance, and mobile hardware remain unverified.

- Three read-only source installations: Native 255, Legacy 461, Latest 1279; all 1995 source scene variants are present. These are not 1995 unique cards.
- Current game coverage: 650 of 671 art IDs; 21 without reliable matches remain static. Future/unselected source variants are retained. See client_card_coverage.json and the CSV inventories.
- Client GUID audit: 32235 inspected files, missing 0, duplicate 0. External compatibility dependencies are isolated under DynamicCards.
- Structural audit: 1995 scenes, 33166 renderers, 20302 particle systems, 3493 animators, migration issues 0. This is not a visual guarantee. Source empty slots and authoring exceptions have separate records.
- Source animation curves, default state graphs, unconditional transitions, material curves and pointer switches are restored. Long sampling truncation and missing weighted bones were repaired where source evidence existed. See README for original-source limitations.
- Complete partition generation: premium_full_bundle_generation.log. Unity's slow final managed hashing was interrupted only after all parts and index had been written; no part was rebuilt or discarded. snapshot_probe_manifest.py generated a SHA-256 manifest externally with before/after size and mtime checks.
- FinalPackageValidation.Run re-opened every complete package: premium_full_bundle.log PASS, cards=1995, bundles=65, bytes=9700633209.
- Full-package queue: premium_full_queue.log PASS, including preview priority/background suspension, visual ordering, cross-part isolation, in-flight cancellation, unload and reload. Queue test source was refreshed and rerun to include the newest cancellation assertion.
- Full-package Geralt: geralt_full_partition_timeline.log PASS, source layers, intact-to-cut boundary, intro to persistent loop, restart.
- Actual focused standalone player and optional include/exclude build staging: focused_partition_player.log and focused_partition_stage.log PASS. All stage files restored.
- Installed 66 payload files (65 bundles plus index), 9.70 GB / 9.03 GiB into primary Library/DynamicCardsBundles/StandaloneWindows64. Final manifest covers 132942 files. Ready marker written last. Backup transaction: BeforeFinalPremiumCache/transaction.json. Installer: install_premium_cache.py. Final evidence: final_delivery_verification.json.
- Production runtime and build scripts match tested Probe versions; the content audit helper intentionally differs. git diff --check passed (line-ending warnings only).
- Main Unity was left running and was importing the enlarged asset set. Stop old Play and start through Login after import finishes. Main-editor compilation and complete gameplay have not been used as acceptance proof.

Limitations: not every one of the 1995 variants was visually accepted frame by frame. Source conditional transitions, AvatarMask, proprietary scripts and audio events have incomplete coverage. Legacy 12230611 source bone 114 is missing and only its bind pose was restored. Lambert's black foreground also appears in the original standalone source scene; original full game UI was not reproduced for that comparison. Mobile performance has not been measured.

Storage: an automatic approval review rejected deletion of three helper Library folders; that deletion was not performed or retried. Lossless NTFS compression is partial. Source installations were not modified. No helper build process needs resuming for this delivery.
