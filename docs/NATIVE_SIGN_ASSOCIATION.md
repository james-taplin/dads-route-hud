# Native sign association

The native SignPlacer creates a private SignData recipe containing an exact RailTrack reference, a Bezier placement point and direction. Its generated SignGeneratorData retains the raw sign parts but discards that route association. This is the confirmed gap addressed by a passive Harmony postfix on the existing PlaceSign method; no extra DLL, scene object, collider, raycast or gameplay mutation is added.

Normal-startup capture retains the actual returned GameObject ID, exact native track association, approach direction (!flip), track anchor and every raw SignParameters part. The anchor is stored independently of the visible pole in the origin-relative world frame, so Toolbox clearance adjustments do not move a known track observation off its rails. Metadata on the actual returned object takes precedence over the recipe. Each native part and each accessory becomes an individual retained route observation. Unsupported or nonnumeric text remains OtherSign; opposing-facing items remain records. Native speed-board text is in tens of km/h; native grade text already contains signed percent. Unknown readings are not made into speed limits.

DoubleTrack 2.1.1 replaces SignPlacer with its separate DV.Signs.CustomSignPlacer rather than calling
the native PlaceSign. Version 0.5.2 observes the optional private PlaceSign with a checked reflection
contract for its exact SignData, and discovers its existing live components for read-only late-enable
recipes. Only the loaded active DoubleTrack mod is consulted; no optional assembly reference is
added. Inactive or absent DoubleTrack leaves native support intact. Late enablement after a large
pole relocation can still fail the strict observed-root match and remains explicitly partial.

A late-enabled mod cannot recover the discarded association by reading SignGeneratorData alone. The fallback asks the existing live SignPlacer for its read-only per-track/per-junction recipe and accepts a recipe only when exactly one actual SignDebug or SignGeneratorData root matches its expected pole position (within 0.25 m), flattened native rotation (within 1 degree) and raw part signature (types, text, prefab references and accessory references), or the native visible SignDebug text when raw metadata is absent. The actual placer flags, local component scope, native ShouldIncludeTrack result and noSignsTrackNameMarks are honoured. Recipe generation never calls Make, PlaceSign or PlaceSigns. A recipe by itself never becomes an observation.

Candidates are indexed spatially for matching the already known native recipe; this is not a nearest-track association. Multiple matches, cross-track recipe disagreement, unmatched recipes or unavailable native contracts are rejected and reported as partial coverage. Each retained source is projected only onto its already established track. NativeRouteGeometry rejects projection farther than one metre from that track. Position offsets are evaluated in the current scene frame so world-origin shifts do not select parallel rails.

Track-point subscriptions invalidate relevant caches, and reset unsubscribes them. Version 0.5.3
performs an initial scene census once, plus discovery when the optional placer first becomes
available. Both supported placers route future generation through their existing observed PlaceSign;
late optional activation attaches that same checked observer if it was unavailable at HUD load.
Known-root maintenance starts at the existing five-second cadence and advances at most 32 roots
and 16 tracks per call, including one active-HUD Update call per frame. Unchanged roots retain
their per-track projection caches. Raw edits, accessory changes, destroyed or moved roots and track
geometry invalidate affected data; removing an ambiguous candidate retries unresolved recipes.
Spatial cells use the origin-relative frame, so world shifts need no new scene census.
Captured associations survive ordinary HUD toggles and LoadingFinished; full reset is reserved for
unload. Another native recipe pass is skipped when every observed root already has an association.

Initial discovery and individual native recipe/projection calls still cannot be pre-empted. With
HUD logging enabled, stage timings separate each scene query, root indexing/refresh, recipes and
projection from route collection/publication and list layout. Reports are aggregate and inclusive;
parent and child times must not be added. Runtime performance remains a separate acceptance check.

Coverage explicitly describes observed roots, validated recipe matches, unmatched plans and unassociated scene roots. Some unassociated roots may be outside the requested route; they still prevent an unsupported global completeness claim. Diagnostics are retained since reset, including historical failures, so a previously observed problem remains visible rather than silently disappearing.

## Validation

The metadata-only contract tool checks 16 native sign member declarations, 13 DoubleTrack placer
declarations, 33 other optional-mod declarations and two signal-manager singleton-binding contracts.
It records source assembly hashes/signature blobs and executes none of the inspected target code.
The current audit also checks four native car-registry/motion declarations for map traffic;
all 68 contracts matched the installed assemblies. Native speed events retain typed primary and
alternate km/h values alongside raw source evidence, so map colouring never parses a UI label.

Read-only source/metadata review confirmed native local/global scope, filters, junction ownership, direction, pole offset/rotation, speed/grade text units and origin-shift conventions. Compilation and runtime evidence are recorded in the main validation handover. Sign matching, distant modified-route coverage, late enablement, origin changes and cold native-recipe timing still require in-game observations. A per-track native recipe call is synchronous and cannot be pre-empted inside that native call by the route collector's work budget.
