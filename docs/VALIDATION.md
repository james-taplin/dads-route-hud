# Validation and acceptance

Automated build/core checks pass as recorded in HANDOVER.md. Runtime and visual acceptance are **not run**. This is a compiled test candidate, not a release-readiness claim.

## Repeatable automated checks

```powershell
./Build.ps1 -GamePath 'C:\Program Files (x86)\Steam\steamapps\common\Derail Valley'
dotnet run --project tests/DrivingMath.Tests.csproj --configuration Release
dotnet run --project tests/RouteCore.Tests.csproj --configuration Release
dotnet run --project tools/OptionalContracts/OptionalContracts.csproj --configuration Release -p:NuGetAudit=false -- 'C:\Program Files (x86)\Steam\steamapps\common\Derail Valley\Mods' '.\build\optional-contract-audit.json'
git diff --check
./Package.ps1
```

Expect zero build warnings/errors, 14 driving checks, 224 route/layout/naming checks and 51 metadata matches. Inspect reference/PE audit, package allowlist/identity/hash and preservation manifest. No static or mandatory optional dependencies; no game/Unity/UMM/Harmony/optional DLLs in the ZIP. Building must leave parent source, installed HUD/settings and Params.xml unchanged.

## Native route acceptance — pending

1. On later test installation, select the candidate in UMM and restart. Preserve original HUD/settings. Normal driving keys and cursor behavior must remain native.
2. At 50,000 m, compare the complete catalogue against native tracks/junctions and physical speed/grade/other signs plus optional heads along a route exceeding old 100-track/64-signal limits. Parts/accessories retain separate source identities. Opposing observations remain marked. Include an item exactly at 50,000 m; exclude one just beyond.
3. Expand the five atlas tiles. Their partition counts must sum to the catalogue; tile boundaries appear once. Every ID remains accessible with First/Previous/Next/Last pages, without importance filtering.
4. Move during cold multi-frame capture. Distances must follow the live anchor. Native span-direction changes at valid joins must preserve captures. Actual reverse travel rebuilds. Shifted horizon tails remain shaded/pending until refreshed.
5. Start a looping route midway through a track. Collect its unseen prefix, then report Loop Closed at first covered directed span. No repeated traversal/freeze/fabricated 50 km completion. Passing the old closing point must recapture. Opposite-direction encounters remain distinct.
6. Exercise dead ends/missing geometry, switch changes during/after capture, locomotive/save changes, toggle/unload/reload. Clear old branch/facts; no duplicate panels/subscriptions/probes. Report reached distance and actual termination.
7. Compare profile to signed grades/elevation. Check relative elevation/exaggeration, origin shifts, parallel/inserted DoubleTrack rails, normal startup and late enablement. Unmatched/ambiguous signs must remain diagnostic rather than guessed.
8. Measure cold/warm game collector, recipe, optional pass and publication timing on dense long routes. Iteration budgets are cooperative; check expensive individual calls/GC and continued progress without refresh starvation.

## Layout and metrics — pending

Check strip/expanded atlas/inspector at normal/small resolutions and fonts 10–28: measured cells, wrap, counts, bounds, opacity, unlock/drag/relock and persisted position. Extreme caption clipping must not remove inspector access. Large metric selections remain reachable by scrolling. No normal driving mouse capture or new hotkeys.

Compare handles, gauge pipe/cylinder pressures and engine-specific ports to native gauges on DE2, DE6/DH4/DM3, steam and CCL vehicles. Missing means --. Check SI power demand and saved units/selection/order. Conversion tests do not prove all live port semantics.

## Optional mods — pending

Hitch comparison: keep DVLocoBogieVisuals off and DadsDrivingHUD disabled in UMM. Compare the same saved scene with DadsRouteHUD enabled versus its native UMM toggle disabled; hiding its panel is insufficient for callback attribution. No additional profiling mode is needed. The reported approximately 155 ms Update spikes versus 29 ms ordinary frames are user-supplied baseline evidence, not a measurement of this candidate. The recurring global manager search is removed in 0.2.0; remaining native calls still require runtime observation.

Run base game + HUD; DVSignals + HUD; DoubleTrack + HUD; DVSignals + AI Traffic + HUD; then all three. AI Traffic's dependencies remain its responsibility. Compare heads/aspects/colours/distances and free/occupied/reserved tracks with positive AI ownership. Include red signals with other causes; never infer reservation from colour.

Use >64 signals and a pass spanning multiple frames. Periodic route refresh must wait for active optional reading; hard changes still cancel. Absent/broken contracts clear old facts. Unassociated placements/watchdog failures report partial coverage. Distant aspects have unknown update age and may be stale; no forced updates/reservations.

Confirm signals/switches/NPC trains remain unaffected and absent manager singletons are not created. Multiplayer host/client replication and locomotive-ID attribution remain a separate unimplemented stretch goal.


## Combined window checks — 19 October 2026

The route suite now includes window recovery across changed resolutions, corrupt saved coordinates, minimum/maximum resizing, anchored edge resizing and actual-screen top-right defaults (128 checks in total). Both projects compile against installed references without warnings. Actual GUI input/appearance still requires a game test: switch both metric modes and all three route presentations, dock/pop out, toggle graph sections and event types, drag/resize both windows, change each opacity, save/restart, reset to top right, and test smaller/larger resolutions. Confirm no Mod Toolbar buttons/windows/dependency, no driving input/cursor change, all selected metrics reachable via scrolling, and full catalogue retention when display filters hide markers. Confirm switching modes/windows does not start duplicate collectors. Keep the fixed standalone fallback disabled while using the combined mod.


## Periodic signal publication - pending game confirmation

The supplied recording shows 79 retained items, then 52 with signals missing for approximately 1.65-1.75 seconds, then 79 again by 1.8 seconds. The 128-check suite includes holding the previous valid same-route capture while its signal replacement is collected, atomic replacement and invalidation. In game, observe several periodic refreshes while stopped and moving: markers/counts must not drop to the native-only view during replacement; refreshing status must be explicit. Change route/locomotive or disable the signal source and confirm old signal facts clear. Automated core tests do not execute the Unity service or prove its runtime behavior.


## 0.2.1 continuous route and header actions - game acceptance pending

Confirm the first upgrade displays one continuous profile/marker row/axis across the selected range (20 km remains 20 km; 50 km remains 50 km). The optional close-up and sectioned atlas start off once; enabling either persists across restarts. Changing presentation must not alter retained IDs/counts, horizon, loop termination or collection source.

With the native free cursor, click Detach route, drag and resize the detached graph, scroll its content and click Attach. Repeat with geometry locked, with graph previously hidden, and with event-list-only selected. Header clicks must not start dragging. Buttons remain outside the scrolled body and actions take effect on the next Layout. Check small/large resolutions and fonts, overlap of both windows, bright signal marker tokens, normal driving keys, locked cursor, and persistence after save/restart. The second window uses the same collector. Confirm all previous metric/range/window choices survive migration.


## Three sections and metric columns (0.3.0) - game acceptance pending

The pure suite has 224 checks, including explicit/Auto columns, ordering, empty columns, fold/restore without mutating saved assignments, exact-once retention and shallow tape geometry. In UMM choose Separate all three sections. Verify independent drag/resize, Attach from both detached windows, both main-header actions, overlap/input priority and lock behavior. Set column count 1, 2 and 8, assign and reorder metrics in each, reduce/restore the count, search/filter, and confirm no selection/unit/precision loss. Switch detached metrics between columns and tape: the details window size must stay fixed. Use Top tape / Bottom tape and manually stretch a tape, check scrolling when too short, save/restart, and inspect all three opacities/visibility controls. Route, optional source handling and loop/flicker behavior must remain unchanged with one shared collector.


## Graph fit (0.3.1) - game acceptance pending

Test selected ranges 100 m, 5, 10, 20 and 50 km. Completed ranges must span the full axis. With a shorter available capture (e.g. 4.9/20 km), fit mode fills the width but labels 4.9 km while retaining the 20 km request in status. Toggle Selected-distance scale: unavailable tail becomes striped without changing collection. Confirm profile/markers/axis/atlas agreement, zero-data waiting, loop/dead-end termination, moving/refreshing captures, units, saved settings and no event loss. Pure tests cover these scale boundaries; they do not execute IMGUI or validate live route coverage.

## Named route termination — pending

Select a look-ahead longer than the track actually ahead and open the UMM inspector. One diagnostic
must name the directed native track the capture ended on, the reached and selected distances, and
the reason; at a junction it must also name the junction and the selected branch. Throw that switch
to a through route and confirm the next capture reaches further and the diagnostic disappears once
the horizon is reached. Confirm the status line's termination label is unchanged, that nothing is
logged unless logging is enabled, and that no extra native call is made while walking.

## Readable route list - pending

Drive through a station and confirm each row reads as distance, one plain word, the description, then
the dim code. Check a loading, storage, inbound, outbound, parking and passenger track, and confirm
each station's resolved name matches the game's own station name. Confirm a plain main-line track
reads as a main line, that a track outside any yard falls back to its object name, and that no
station name appears where the game supplies none. Check a switch reports the correct selected branch
of the correct count, that speed rows show the limit alone, and that grade rows state falling or
climbing with the right sign. Narrow the window until the code column is dropped and confirm the
description stays readable. Confirm the retained catalogue in the UMM inspector still lists every
observation with its raw native type and sign text.

## Switch side on approach - pending

Approach a facing switch set to the diverging road and confirm the marker points to the side the
train actually takes, then throw it and confirm the marker follows. Confirm a switch whose selected
branch continues straight shows "|||". Approach the same switch from a branch and confirm no marker
appears and the row reports trailing, with "set for your route" when the switch is set to the track
you are on and "set against you" when it is not. Check both directions of travel over the same
switch, and a switch immediately at the end of the look-ahead, which must show no marker. Verify the
marker column disappears entirely when no row has one, and that distances stay aligned.

## Capture hold while refreshing - pending

Stand on a dead-end yard track and watch the route list for a minute: the displayed and retained
counts must not drop to zero and back. Shunt back and forth across a standstill and confirm the list
only rebuilds on a real reversal, not while creeping. Drive the main line through several origin
shifts and confirm the list stays populated throughout. Confirm the status says the previous capture
is retained while refreshing only while a replacement is collected, and that the new capture replaces
it in one step. Change locomotive, change the look-ahead, and walk away from the captured route, and
confirm each of those does clear the list rather than showing a stale one. Read the capture count and
last rebuild reason in the UMM inspector and report them.

## Direction from the reverser - pending

Stand still and move the reverser from full forward through mid-gear to full back, confirming the
route list switches ends with the lever and does not change while the lever is in mid-gear. Notch a
steam reverser up to a short running cutoff and confirm the direction holds. Stop from a forward
move, set back, and confirm the road behind appears while the train is still rolling forwards. In a
vehicle with no reverser, confirm clear motion still decides. Confirm the list rebuilds once on a
real direction change and not repeatedly while shunting.

## One cab supplies direction and position - pending

Double-head two locomotives, drive from one and confirm the trailing locomotive's reverser has no
effect on the route, in both orientations. Couple a wagon facing the other way, ride in it, and
confirm the route still runs from the locomotive's end rather than reversing. Walk from the cab
through the train and back and confirm the list does not empty or rebuild on the way. Leave the cab
entirely and confirm the route continues from the locomotive last driven.

## Optional signals held across a refresh - pending

With DVSignals active, run steadily on a signalled stretch and watch the displayed event count for a
minute: it must not drop and recover on a cycle. Confirm signal rows stay put across the refresh that
happens about every two seconds. Throw a signal and confirm its aspect still updates. Disable
DVSignals and confirm the list is stable without it and reports signals unavailable. Enable logging,
drive for a minute, and confirm Player.log shows no repeated event-count swings.

## Control layout and button scale - pending

Detach the route and metrics windows and confirm neither header offers an Attach action, that both
still drag and resize by header and grip, and that the main window's own actions attach them. Set
the font to 10, 15 and 28 and confirm the header buttons scale with it, that their labels are not
clipped, and that the two-action row stays centred without crowding the title. Narrow the main window
and confirm the buttons stay legible and inside the frame. Confirm the page buttons under the route
list match.
