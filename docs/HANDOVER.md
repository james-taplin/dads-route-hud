# Development status

Current candidate: 0.5.4 on codex/route-hud-junction-clarity, based on the installed 0.5.3
checkpoint 11fe5bc. It stabilizes status/list layout, moves consist details above events and
maps nearest next stations with travel-relative arrows. See the final sections for build/install
evidence. The 20 km range and 0.5.3 performance corrections are preserved; runtime improvement
remains unmeasured. The preserved handoff confirms a 0.5.1 collector stall, not a hardware limit.

The 0.5.0 refactor was developed on codex/route-hud-refactor from the completed 0.4.2 checkpoint
bb92b42, then installed at James's request. Older candidate status and acceptance notes below
are retained as history; the latest installation entry takes precedence.

23 October 2026: James requested keeping the other Route HUD changes while restoring the compact
Dad's Driving HUD list and addressing the alternating datasets. Candidate 0.4.2 implements that
request. The 0.4.1 runtime log now exists; the old instruction to wait for that drive is satisfied.
See the 0.4.2 section below for confirmed evidence and remaining game acceptance.

Runtime caveat, 22 October 2026: a stale UMM shadow copy held 0.3.2 while later DLLs were
installed. The on-disk DLL and regenerated shadow copy were verified to match 0.4.1; James then
requested another cache clear before his next test, and no HUD cache remains. In-game acceptance
of 0.3.3 through 0.4.1 remains outstanding; see the dated handover note below before debugging
or installing another candidate. Logging is enabled for James's next drive.

Version 0.4.1 is a test candidate, built, packaged and installed for testing on 20 October 2026,
as were 0.3.1 to 0.4.0 before it. In-game acceptance of the readable list, the switch markers, the
capture hold and the reverser-led direction is still pending. It combines compact columns and driving strip, independent draggable/resizable main and graph windows, per-window transparency, top-right initial placement and a configurable route horizon up to 50 km. All collected events remain retained; finite-pixel clusters and display filters do not truncate the complete catalogue.

The existing-instance signal-manager lookup performance correction is present. The periodic refresh correction keeps the last valid same-route signal view until its replacement is ready, rather than exposing a temporary native-only catalogue. Changes of route/vehicle/source invalidate old observations. In-game confirmation of the latest publication and GUI behavior remains pending.

Automated checks of the current snapshot: zero-warning installed-reference compilation, 242
route/window/layout/naming checks, 14 conversion checks, and 51/51 metadata contracts. Public CI runs the
two pure suites without proprietary references. Follow VALIDATION.md for actual game acceptance.
Synthetic in-memory timings are not game performance measurements.

Publication intentionally starts from the original MIT HUD history with a source-only import. Koteq's compatibility work remains credited. No game DLLs, private settings, backups or local profiling evidence are included. Neither Rinzler's binary nor its implementation is used. See LICENSING.md for pinned notices.

The public repository is behind this checkout: it holds the source import up to 0.2.0, and the
0.2.1, 0.3.0 and the 0.3.1 to 0.4.1 commits are local only. No tag, release or binary has been published,
and no pull request has been opened upstream. The GitHub default branch therefore does not match
any installed build.

Builds/packages do not install or modify game files. Use one HUD ID at a time in UMM; back up an existing installation and settings before changing it. Optional DVSignals, AI Traffic and DoubleTrack compatibility must not become mandatory dependencies. Multiplayer locomotive-ID ownership remains a stretch goal, not a supported feature.


## Track-ahead clarity update

Version 0.2.1 adds a permanent HUD-header Detach route / Attach route action and an Attach action on the detached Track ahead window. Action hit areas are excluded from header dragging; geometry locking does not disable the action, and mouse/key input stays native when the game cursor is unavailable. Visibility changes apply at the next IMGUI Layout, with settings saved through the existing deferred path. No extra collector, toolbar integration, optional dependency or input binding is added.

The full-range graph now includes a continuous elevation profile across the requested distance. Close-up and five-section atlas are optional repeated views; neither extends/truncates the route. A one-time routeDisplayVersion migration selects the simpler continuous view while preserving range, metrics, geometry, train settings and subsequent user choices. UMM names and the marker legend are clearer; bright signal marker backgrounds use dark text. Source collection, loop protection and the existing native-instance lookup are unchanged. Build/pure tests do not establish actual in-game GUI/input acceptance.


## Three independent sections (0.3.0)

In UMM, **Separate all three sections** detaches metrics and the continuous graph, and selects Graph + events. The main window retains the route event list and train details. Each window is independently movable/resizable, with saved geometry. Use Detach metrics / Attach metrics and Detach route / Attach route in the main header, or Attach in each detached header. No Mod Toolbar integration or additional collector is introduced.

Metrics support **1-8 columns**. Each metric has an Auto / 1-8 column selector; filter the settings by column and use its up/down arrows to set reading order among metrics with that saved assignment. Auto preserves the previous row-major placement. Reducing the count folds higher assignments into the last visible column, without changing their saved choices; increasing restores them. Units, precision, selections and prior ordering are retained. Column assignments are ignored by the horizontal tape, which uses the normal provider reading order.

For a tape along the top or bottom, select Driving strip and resize the detached metrics window wide and shallow (minimum height 80 px). Its own opacity and visibility controls are in UMM. Attached, detached-metrics and details sizes are saved separately so changing metric style does not resize the detached details window. First-open metrics placement keeps existing headers reachable where screen space permits. All windows scroll overflow rather than dropping selected metrics/events. User input/appearance and persistence across a real game restart remain subject to game acceptance.


## Fit route to graph width (0.3.1)

The default graph scale fits the available mapped route to the full width and labels that actual distance. For example, 4.9 km mapped of a requested 20 km uses a 0-4.9 km graph; the status separately retains 20 km requested. UMM's Selected-distance scale restores a fixed 0-requested axis with striped unavailable coverage. Full profile, markers, axes, shading, optional close-up and atlas share the chosen scale. Positive sub-kilometre ranges are supported. Display fitting does not extend collection, alter retained observations, change the requested horizon or bypass loops/dead ends.

The supplied screenshot reported only 4.9/20 km available. Inspection confirmed the previous renderer already used RequestedDistance rather than a fixed 50 km denominator. The cause of that shorter capture is not established by the screenshot. This update implements the explicitly selected fit behavior and preserves visible termination status while refreshing; it does not claim to repair an unverified collection defect. In-game visual acceptance remains pending.


## Named route termination (0.3.2)

A capture that stops before the selected look-ahead now records one diagnostic naming the directed
native track it ended on, the distance reached against the distance selected, and the specific
reason: no further connection along the selected branches, unavailable or unusable native geometry,
a loop closure, an unrepresentable advance, or an unusable start or selected distance. Where that
end is a junction, the diagnostic also names the junction and which of its branches the game has
selected. This distinguishes a switch set into a short stub from a buffer stop or from geometry the
game has not supplied, instead of leaving a short route unexplained. The diagnostic appears in the
UMM inspector beside the existing coverage text, and is also written to the log when logging is
enabled. The status line's termination label is unchanged.

The walker exposes the terminal directed track for this purpose and makes no new native calls while
walking; collection and display are unchanged, and a short route is still reported as reached rather
than padded. Nothing here establishes why any particular route ended short; it reports what the game
returned at that point.

Publication reuses the single position fix already computed each frame rather than scanning every
captured interval a second time. Behaviour and cadence are unchanged. The per-frame topology
revalidation budget is deliberately left as it was, so a switch thrown ahead is still noticed within
a few seconds.

## Readable route list (0.3.3)

The upcoming list no longer shows raw identifiers and enum names. Each row is four measured columns:
distance, one plain word for what it is, what it says, and the game's own short code kept dimmed at
the end so a row still matches a job overview. Yard tracks are named from the identifier parts the
game exposes publicly, with the station's display name resolved through the game's own yard lookup
and cached per yard: "GF-B-5-L" reads as "Goods Factory - B yard, track 5, loading". Nothing here
hardcodes a list of stations or yards, so it follows the game rather than going stale. A track with
no yard identity falls back to its object name, and a track with nothing identifying at all is
reported as unnamed instead of blank. No station is invented where the game supplies none.

Junctions are called switches, matching the chosen wording, and read as "Goods Factory switch -
branch 3 of 4"; an unusable branch count is omitted rather than shown as zero. Speed signs read as
the limit alone, since the row already says "limit". Grade signs state their sense, as "-1.4 %
falling" or "+1.2 % climbing", with "level" at zero. Other native sign types are split into words,
and sign accessories are named rather than prefixed with their raw type.

Collection, retention and ordering are unchanged: every observation is still retained with its own
identity, display filters still remove nothing, and the raw native type and sign text remain in each
observation's source detail for the UMM inspector. Naming happens once per capture, and station
lookups are cached for the session and cleared with the rest of the capture state, so no per-frame
cost is added. In-game acceptance of the new rows is untested.

## Switch side on approach (0.3.4)

A facing switch now carries a marker ahead of its description showing which way it takes the route
relative to the direction of approach: "<<<" to the left, ">>>" to the right, and "|||" where the
selected branch continues straight ahead within three degrees. The side is measured from the native
heading where travel leaves the current track and where it enters the next one the route actually
follows, so the marker always agrees with the captured route rather than with a separately read
branch. The row also states it in words, as "branch 3 of 4, diverging right".

A switch approached from one of its branches converges instead of diverging, so it has no side to
report. Those rows carry no marker and say "trailing, set for your route" or "trailing, set against
you", which states what the game has selected without asserting what the game will do about it. A
switch at the very end of a capture has no following track, so its side is left unestablished and
no marker is shown rather than a guess.

The classification is pure and pinned by tests against real-world geometry, independent of the
engine's handedness: heading north and leaving east is a right turn. Degenerate or unusable headings,
and a straight reversal, are reported as unknown. Markers are this mod's own fixed tokens, never
native text, and are the one field not escaped for display. Collection, retention and the existing
junction event identity are unchanged.

## The list no longer empties while refreshing (0.3.5)

A screen recording of a dead-end yard track showed the route list alternating between the captured
route and nothing at all, spending most of its time empty. The cause was not the data changing: the
capture was being retired and rebuilt, and every path that retired it also emptied what was on
screen, so each rebuild blanked the list until the replacement finished.

Retiring a capture and clearing the display are now separate decisions. Only a change of vehicle,
of direction of travel, or of the selected look-ahead can empty the list, because only those make
the previous capture meaningless. While a replacement is collected the last completed capture stays
on screen, shifted to the live position and marked as retained while refreshing, and is replaced
atomically when the new capture completes. If the vehicle is no longer on that capture the hold does
not apply and the partial walk is reported as before, so nothing stale is presented as current.

Two causes of needless rebuilds are also removed. Regenerated native geometry no longer retires the
capture: the topology is unchanged, so it schedules a fresh capture instead, and it no longer
restarts a walk that is already running, which previously could prevent any capture from completing.
A connection mismatch found at the end of collection asks for another capture in the same way. The
direction of travel is now decided with hysteresis in pure, tested code: measured motion decides
above 0.5 m/s, an already decided direction is kept below 0.15 m/s, and the reverser decides
between the two. Previously a vehicle idling either side of a single 0.15 m/s threshold could keep
changing its reported direction, and each change retired the whole capture.

Each completed capture now records how many captures have been made since loading and what started
the last one, in the UMM inspector's diagnostics, so a repeated rebuild can be attributed rather
than guessed at. Collection, retention, naming and the switch markers are unchanged. Which trigger
caused the recorded loop is not established; the diagnostic exists to establish it.

## Direction of travel follows the reverser (0.3.6)

The route shown is now the road the reverser has selected, not the one the vehicle happens to be
moving along. The reverser decides whenever it sits more than 0.05 from neutral in the control's own
units, where full gear is 0.5 either side; a steam reverser's distance from neutral is its cutoff,
so every normal running cutoff states a direction and only mid-gear and lever float do not. At or
near neutral the lever states nothing and motion above 0.5 m/s decides instead; failing both, the
previous answer stands.

This means the road ahead changes as soon as the reverser is set, before the train moves, and it
stays correct while the train is still rolling the other way after a stop, which is the usual case
when shunting. It also means that a vehicle rolling against its reverser, such as running back down
a grade or being propelled by another locomotive, shows the end the reverser selects rather than the
end it is moving towards. That is the stated intent, not an oversight; making sustained motion
override the lever would be a one-line change if it is wanted.

The decision stays pure and tested, and still retires a capture only when it actually changes.

## One cab supplies direction and position (0.3.7)

Direction, speed and the position anchor are now all taken from a single vehicle: the one the player
occupies if it has a reverser, otherwise the locomotive they last drove. No other locomotive's lever
is read, so a trailing engine in a double-headed consist cannot set the route, and a multiple-unit
arrangement is followed from the cab in use.

This closes a mismatch that mattered more once the reverser led. The reverser was read from the last
locomotive when the player's own car had none, while the direction of travel was applied to that
car's orientation and its bogie supplied the position. A player riding in a wagon coupled facing the
other way would therefore have seen the route from the wrong end. Both now come from the same
vehicle, so a reverser's direction is always applied to the locomotive it belongs to.

Walking back through the train no longer retires the capture either, because the vehicle the capture
is anchored to stops changing as the player moves between cars.

This is structural rather than arithmetic, so the pure suites do not cover it; it needs the game and
a reversed-coupling consist to accept.

## Optional signals stop dropping out every refresh (0.3.8)

Steady running still showed the list changing every couple of seconds after 0.3.5 and 0.3.7. The
remaining cause was a capture completing, not a capture being discarded, and it was deterministic
rather than intermittent.

A capture completes on the scheduled refresh roughly every two seconds. Completing one called
OptionalSignalTelemetry.Reset, which clears that component's bindings: its manager type, so it
reports itself unavailable, and its source assemblies, so their identities become null. The
publication hold that exists precisely to keep the previous view on screen while optional signals
are re-read compares exactly those two things, and both had just been destroyed, so the hold could
never apply at the only moment it was for. The capture was therefore published with no optional
signals at all, and the rows returned about half a second later when the read pass finished. With
DVSignals, AI Traffic and DoubleTrack all active, that is every signal row in the list appearing and
disappearing on a two-second cycle.

Completing a capture no longer resets the optional telemetry. It already re-reads on its own when
the capture's revision changes, so the only effect of the reset was to destroy the state the hold
depends on. A genuine route change still resets it, which is correct, because the previous optional
sample describes a route that no longer applies.

With logging enabled, any change in how many events reach the screen is now logged with the counts,
whether the previous capture was held, and the optional signal status, so a list that still gains or
loses rows can be attributed from Player.log rather than inferred.

## Controls live on the main window (0.3.9)

The detached route and metrics windows no longer carry an Attach action in their headers. Every
attach and detach control is on the main window, which keeps the detached windows to their content
and their own title. Header actions are now optional in the window code rather than always present.
With the main window hidden and a section detached, re-attaching is done from the UMM settings, as
the main window is deliberately the only in-world control surface.

Overlay buttons take the HUD's own font size and proportional padding instead of the default skin's
fixed size, so they shrink with a small font. A single header action is only as wide as its label
needs, rather than holding a 90-pixel minimum. Two actions share their own row, each sized to the
wider of the two labels and centred together, instead of each taking half the window's width. The
page buttons under the route list are styled the same way. The UMM settings panel keeps the default
skin, which is the host's to size.

The dead attach actions left behind by this change are removed rather than left unreferenced.

## Auto columns group like metrics (0.4.0)

A metric left on Auto now follows what it is about instead of being dealt out in turn, so each
column reads as a column of like things. The groups, in reading order, are what the train is doing
(speed, acceleration, grade, altitude, mass, kinetic energy), the driving controls, the brakes,
output, the engine, consumables, and native simulation ports. Brake controls and brake pressures
deliberately share one group, because they are read together. Speed and grade lead rather than
following the controls; that order is one list in MetricGroups and is easy to change.

Only groups that actually have a metric on Auto take a column, so a locomotive without a boiler
does not leave a gap. Present groups spread across whatever columns exist: with fewer columns than
groups, neighbouring groups share one in order; with more, the spare columns are left free for
explicit choices rather than padded. A metric with a saved column still keeps it, and a choice
beyond the visible count still folds into the last column without losing the saved value.

Metrics are matched by exact name, with a keyword rule behind it so a metric added by the game or
another mod still lands somewhere sensible rather than being dropped, and native ports are
recognised by their "Sim: " prefix. The classification is pure and tested, including that every
metric is placed exactly once at one, three, six and eight columns.

The settings panel's own group filter still uses its older Controls/Steam/Performance/Vehicle sets;
those two taxonomies have not been unified.

## Window padding is a setting (0.4.1)

The space between a window's frame and its content was a set of fixed figures scattered through the
window code: fourteen pixels of header, ten and four at the title, eight at each side of the body,
sixteen at the bottom, and a width allowance of thirty-eight. None of them moved with the font, so a
small font left a lot of empty frame around very little text.

Window padding is now a single UMM setting of nought to sixteen pixels, defaulting to four, and
every one of those insets follows it: header height, title inset, body area, and the width a window
loses to its own frame. The gap between columns follows it too, instead of a fixed ten. The width
allowance keeps a fixed twenty-two pixels on top of the padding for the scroll bar and the resize
grip, which exist whatever the padding is, so the grip stays usable at nought.

The previous appearance was equivalent to a padding of eight, so the default of four closes the
windows in on their content noticeably without any change of setting. This affects all three
windows, not only the detached ones, since they share the same window code.

This is a visual change with no arithmetic to pin down, so the pure suites do not cover it. It needs
the game to accept.

## Return handover and stale UMM shadow copy - 22 October 2026

Claude handed editing ownership back to Codex through bridge conversation tidy-hare. At handover,
the public checkout was clean at 49308bf on codex/dads-route-hud, 14 commits ahead of origin.
Claude reported no unfinished edits, active builds/tests, agents or scheduled work. Nothing was
pushed. The private-history dads-route-hud checkout must never be merged into this public tree;
leave dads-driving-hud alone. James runs the game tests; do not launch DV.

Main carries [EnableReloading]. UMM keeps a DadsRouteHUD.dll.<n>.cache shadow copy in the installed
mod directory. Claude reported that the running HUD still showed the old junction/track labels
and detached Attach buttons while the installed DLL was 0.4.1. He removed the stale copy on
22 October, preserving it in artifacts/backups/stale-umm-cache-20261022/. This explains why those
recordings cannot validate the changes in 0.3.3 through 0.4.1. Codex has not independently reviewed
the recording or observed the loaded game assembly.

Codex independently checked the local files during handover:

- The backed-up cache is assembly 0.3.2.0, byte-identical to the DLL in the 0.3.2 ZIP:
  SHA256 70b6cbca879279ab1be032870a4f9f09e4035925bfd0cd45234d5c52d445a4cb.
- The installed DLL and DadsRouteHUD.dll.14135.cache are both assembly 0.4.1.0 and byte-identical
  to the DLL in the 0.4.1 ZIP:
  SHA256 0ea8020b9959d253de25e388d009b9d1289e3316989272bbc667c1f964913e7c.
- Installed Settings.xml contains windowPadding=4 and enableLogging=true. Its backup is at
  artifacts/backups/settings-before-logging-20261022/Settings.xml. Comparing the parsed XML after
  restoring enableLogging in memory gives an exact match: logging false -> true is the only
  XML change. No installed files were changed during these handover checks.

Before any future installation, close the game, preserve the mod/settings and UMM Params.xml,
and remove stale DadsRouteHUD.dll.*.cache files from this mod's directory. After restarting,
check the regenerated cache's assembly version and hash against the installed candidate DLL.
The expected labels and newly persisted setting keys provide supporting runtime evidence;
copying a new DLL alone does not establish that UMM loaded it. Do not remove unrelated mods'
caches. Claude's installer existed only in his session scratchpad and did not clear the cache;
there is no repo-resident installer yet. Add this safeguard before the next installation.

With enableLogging on, Providers/RouteMonitorService.LogPublication logs changes to the published
event count as "Route list N -> M events", conditionally followed by "; previous capture held",
then "; optional signals: <status>". It skips the initial count. If visible movement occurs
without a count-change line, that alone does not identify the cause: rows can change while the
count stays fixed. The UMM inspector also exposes capture count and the last rebuild reason.

Wait for James's next drive with the corrected cache and logging enabled before another flicker
fix. The optional-signal Reset change is Claude's proposed explanation, not a runtime-confirmed
fix. Readable names, switch markers, capture retention/direction behavior, header controls,
grouped Auto columns and padding all still need game acceptance. Earlier release-section cause
descriptions document implementation reasoning, not confirmation that those builds fixed the
recorded symptoms.

Claude also identified possible later cleanup: the long RouteMonitorService.Update method,
PrepareLayout's hand-maintained flags, differing metric grouping taxonomies and apparently unused
Overlay/EventList.cs. These are reported review candidates, not verified defects or authorization
to refactor them. This handover changes documentation only.

### Cache cleared again before James's game test - 22 October 2026

At James's explicit request, Codex checked that DV was closed, backed up and removed
DadsRouteHUD.dll.14135.cache from this mod's installed directory. The verified backup is in
artifacts/backups/cache-cleared-before-game-test-20261022-193751/. This was the already-matching
0.4.1 cache, cleared as a precaution. No DadsRouteHUD.dll.*.cache files remain there; UMM should
regenerate the shadow copy on the next launch. Installed DLL and Settings.xml hashes were
verified unchanged. Logging remains enabled and runtime acceptance is still pending.

## Local history preservation - 22 October 2026

At James's request, Codex recorded the existing returned handover and loaded-candidate checks as a present-day local documentation checkpoint. The starting source commit was `49308bf`, with 14 existing local commits beyond the cached `origin/codex/dads-route-hud` reference. Those commits are retained exactly; no history was rewritten or imported from the private-history HUD checkout.

The complete working folder and Git database, including ignored packages, cache/settings backups and validation receipts, were independently backed up before this checkpoint. Both changed documents matched that backup by SHA256 before this preservation note. Ignored local evidence remains outside the source checkpoint.

No build, game action, installation, push or release was performed. Runtime and visual acceptance remain pending as documented above. This preservation record does not authorise additional HUD changes or publication.

## Compact list and capture-origin correction - 23 October 2026 (0.4.2)

James authorised this follow-up after bridge swift-wren: preserve the other changes, restore the
list format used before Route HUD, and address the apparent alternation between datasets.

Confirmed: the 23 October Player.log contains repeated changes around 141 <-> 85 events, with the
lower count reporting an optional read in progress. The loaded 0.4.1 DLL/cache had already been
verified. The single optional diagnostic's exact runtime text is still unknown; 0.4.2 logs each
distinct optional diagnostic through the existing enableLogging setting, without a profiling mode.

The service computed located/position against retained at the start of Update, then could replace
retained in FinishCapture and reuse that old position in publication. On moving refreshes this
assigned the old capture's offset to the new capture's origin. A later frame could then fail the
0.5 m backwards-position check, trigger a false left-route rebuild and reset optional telemetry.
Publication now reprojects after FinishCapture. A regression compiling the actual service with
native/optional boundary test doubles fails against HEAD 97ad4a8's service and passes with this
correction. This establishes a reproducible code defect, not proof it is the only runtime cause.
Existing complete-snapshot retention and reverser direction behaviour are preserved; no timed delay
or relaxed invalidation is added. Existing logging now includes capture/rebuild attribution and
the hold predicate results once per declined capture, should another cause remain in game.

The list follows dads-driving-hud 0.1.2's measured distance + description layout, read-only reference
only. Short native track codes, coloured signal names, aspects and amber occupancy/reservation text
are restored. Sign source evidence/raw text/prefab names stay in the full inspector instead of each
driving row. Switch approach markers and descriptions remain; the newer geometry classification is
preserved. Current retention, filters, paging, all three windows, metrics, graph, padding, range and
settings are unchanged. This restores the compact format without restoring legacy collection caps
or old redundant-event filters. Generic TrackIDs use native IsGeneric and fall back to the actual
track name; placeholder parts also have a pure naming guard.

Checks: installed-reference build has zero warnings/errors; 250 route-core, 14 driving conversion
and 55 service lifecycle checks pass; 51/51 metadata contracts match. The lifecycle suite covers
repeated moving/stationary refreshes with partial diagnostics, source loss/replacement and reversal.
It uses test doubles at native boundaries and is not a game execution. CI includes that suite.
Package/artifact audit passed: identity/references and all 14 allowlisted files verified. Nothing pushed,
merged, tagged or published; the legacy HUD checkouts were read only. James launches/tests DV.

Install.ps1 is the reproducible local upgrade path: requires a closed game and existing HUD,
checks package hash/allowlist, verifies a full backup, preserves Settings.xml and UMM Params.xml,
clears only this mod's backed-up shadow caches, then verifies all installed package files/version.
It does not enable the mod or launch the game. Check the regenerated cache version/hash after the
next game launch; a copied DLL alone is insufficient evidence of what ran.

### 0.4.2 test installation

Installed with the game closed using Install.ps1. Full verified pre-upgrade mod/settings/cache and
Params.xml backup plus installation-receipt.json:
artifacts/backups/compact-list-0.4.2-20261023-165118Z/. All 14 installed package files were checked;
Settings.xml and UMM Params.xml remain byte-identical. Logging stays enabled. DadsRouteHUD remains
disabled in UMM because James had turned it off after his drive; enable it in UMM for the next test.
Only this HUD's backed-up shadow cache was cleared. No game launch or runtime acceptance occurred.
The final package/installed handover includes this installation note; the receipt records final
package and installed-file hashes. The runtime cause of the remaining partial diagnostic, and
whether any additional flicker cause exists, must still be checked from the next game run.

## Lightweight refactor and independent review - 23 October 2026 (0.5.0)

James requested a full code refactor on a new branch, thorough error/performance checks, and agents
for research and independent review. The finished compact-list/capture-origin fix was checkpointed
as bb92b42 on codex/dads-route-hud before creating codex/route-hud-refactor. The installed 0.4.2,
its Settings.xml and UMM flags are preserved; no refactor installation or game launch is authorised
by the build/package workflow. No history from the private DLL-containing checkout was imported.

Three agents covered capture/optional lifecycle, UI/car-list/settings, and independent native/pure/
installer review. Native SignPlacer/RailTrack sources and working Mapify/DoubleTrack examples were
checked; they support the existing adapters. No new runtime dependency, hook, service, input binding,
profiling mode or observation cap was needed. Small pure helpers that were already clear were kept.

Confirmed defects corrected:

- A changed selected path could keep the old display while collecting a replacement; hard route
  invalidation now clears obsolete facts. Path changes before publication reject that capture.
- Geometry regenerated during collection could produce a mixed sample; it now retries while holding
  only a still-usable same-route capture. Reverser primacy and the 0.4.2 origin correction remain.
- Optional holds compared assemblies that outlive scenes. They now identify actual existing native
  manager/traffic instances, without creating singletons or searching the scene.
- Grouped brake-pipe minima wrote into the reservoir field; the correct field now updates. An
  uncoupled front coupler could throw before the valid rear link was checked; both are nullable.
- Car-list data survived train changes and was collected during IMGUI. Update now captures formatted
  rows at the saved cadence; Layout freezes rows/columns. Endpoints/count/group/train changes refresh
  immediately, while an internal equal-count reorder is reflected at the next saved period.
- Failed deferred layout saves lost the pending change. They now remain pending with a five-second
  retry interval and a forced final attempt on unload/quit. Native sampling waits for world readiness.
- Installer PE/version validation occurred after replacement. It now preflights in staging, backs up
  and verifies the original installation, and restores affected files/caches on a failed upgrade.
  A restore failure accurately reports the retained backup; settings and UMM flags are never written.
- The artifact audit now rejects duplicate ZIP entry names as well as unexpected files/counts.

Performance/structure changes:

- Capture scheduling, incremental collection, publication and error handling have named boundaries.
  Held replacements are not merged/sorted, and status-only views share immutable capture collections.
- Event row formatting/measurement is frozen at Layout, hidden metric windows are not sampled, and
  selected providers are enumerated once. Native car/job collection is outside drawing.
- Provider settings use a private lookup index while the ordered serialized list remains unchanged;
  raw-port discovery no longer repeats full settings scans. All 75 serializable Settings field
  candidates and 76 public instance fields across six classes match installed 0.4.2 exactly.
- Native signs are indexed by known track instead of scanning every global sign for each track.
  Matching uses existing cached content signatures. Exact association, five-second discovery and
  explicit partial coverage remain; no nearest-track guessing or source streaming was added.
- Shifted views binary-search passed events and size only the remaining event list. An initial
  profile/interval preallocation caused a near-tail allocation regression in a synthetic benchmark;
  that preallocation was removed before handoff. Do not repeat it without measuring near-tail cases.
- The unused legacy EventList, TrackIndexer, TrackFollower and TrackEvent hierarchy were removed
  after repository-wide caller checks. SignalTrackEvent is a standalone immutable observation.
  Legacy maxEventCount remains serialized for compatibility and is not a collection cap.

Validation and evidence:

The installed-reference net48 build is warning-free. Eight suites contain 582 checks: 14 driving,
252 route-core, 67 production-service lifecycle, 90 native-geometry boundary, 47 production sign-index,
51 actual optional-adapter, 16 production-CarList and 45 telemetry/registry checks.
The optional/native PE contract audit matches 51/51 members. The
source-only QuantitiesNet test project uses the same pinned MIT source; CI needs no game assemblies.
The installer fixture suite has 12 assertions covering actual package installation into a fake game,
preserved settings/flags, backup/cache behavior, injected-copy rollback and preflight version rejection.
An additional 51 checks compile the actual optional adapter and exercise reflection binding,
continuation/cancellation, source replacement, all/opposing/distant heads, atomic refresh, partial
diagnostics, occupancy/reservations and optional AI enrichment with boundary doubles. No new defect
was found by that harness. Packaging and the identity/reference/exact 14-file artifact audit pass.
Another 45 checks execute the actual telemetry/registry/query providers with native doubles and
source-built QuantitiesNet: identity, component/port caching, raw discovery defaults, finite reads,
resources/controls, acceleration cadence/gaps/rewinds and reset. No production defect was found.
The 47 sign-index checks execute production placement/registration, per-track membership,
captured/recipe precedence, ambiguity quarantine, parts/accessories, exact association, projection
failure, content cache refresh, origin shift, pruning, destroyed objects and reset/unsubscription.
This closes the changed sign-index boundary with test doubles; real native scene acceptance remains.
Validate.ps1 is the single local validation entry point and writes build/validation-receipt.json;
it builds/packages/tests and runs installer fixtures, without touching the actual installation.

The candidate DLL is 187,392 bytes versus installed 0.4.2's 211,456 (about 11% smaller). In the
controlled .NET 8 synthetic workload, status-only wrapping of 50,000 events allocates 104 bytes
instead of roughly 453,201. Shifts allocate roughly 10% less at offsets 1 m and 25 km; the near-tail
case also allocates less after correcting the initial capacity regression. These measurements do
not establish game frame timing or Unity/net48 allocation gains. Baseline/current/provenance and
raw batches remain in the ignored benchmark evidence, rather than a performance guarantee.

Independent review found no remaining concrete introduced regression in the reviewed changes.
Local evidence is under ignored build/: settings-contract-audit.json, refactor-benchmark-*.json and
refactor-benchmark-summary.txt, plus installer fixture folders and the normal build/audit receipts.
Synthetic .NET 8 timings/allocations are not Unity/net48 frame-time measurements. Runtime acceptance
of visuals, optional combinations, real signal stability and frame timing remains James's game test.
Terminal/dead-end/loop closing-edge changes retain the existing scheduled-refresh detection bound;
adjacent captured-edge validation stays incremental. Native/reflection calls cannot be pre-empted.

Final candidate remains local to codex/route-hud-refactor; no push, release, merge or refactor
installation occurred. The installed 0.4.2 DLL remains byte-identical. Latest complete validation
and package hashes are in build/validation-receipt.json, build/unified-artifact-audit.json and the
artifact SHA256 sidecar. Settings field-contract evidence was also rechecked against the final DLL.

## 0.5.0 installed for James's test - 23 October 2026

At James's explicit request, Install.ps1 installed the existing validated 0.5.0 package with the game
closed. All 14 installed package files match their verified hashes; assembly version is 0.5.0.0.
Package SHA256: 9788842B7F1D13DFA694BFD6312CFF4644C37F03A13AFEA88D3DDBC2E1CF4F2C.

Verified backup of the previous complete mod, Settings.xml, cache and original UMM Params.xml:
artifacts/backups/upgrade-0.5.0-1fcd080ca46844cc9df6ad9284cb15c1/.
Its installation-receipt.json records package/installed/settings hashes and before/after UMM flags.
Settings.xml remains byte-identical. A surgical Params.xml edit changed only DadsRouteHUD
Enabled=false -> true and DadsDrivingHUD Enabled=true -> false; parsed XML was compared with the
expected two-attribute change. Every other mod/global setting is preserved. This supersedes the
installer's initial flag-preservation receipt, with final flags/hashes recorded alongside it.

Only this HUD's already-backed-up DadsRouteHUD.dll.*.cache files were cleared; none remain.
The game was not launched. On James's next run, UMM must regenerate a 0.5.0 shadow copy; verify its
version/hash against the installed DLL before attributing runtime results. Visual, signal-stability
and real frame-time acceptance remain pending. The candidate ZIP was not rebuilt for this
post-install documentation entry; its bundled handover correctly records the earlier build state.

## 0.5.1 frozen route correction - 23 October 2026

James reported that flicker had stopped but the HUD no longer worked. His screenshot showed live
metrics, a 0-30 m route graph and one retained event while driving. The installed 0.5.0 DLL and its
UMM shadow cache had matching version/hash, ruling out the earlier stale-cache incident for this run.
Player.log contains 33,998 OnUpdate ArgumentNullException entries with the same CarList task traversal
stack. Passenger Jobs 5.3.1 supplies a valid destination leaf with null nestedTasks; the inherited
traversal called LINQ Select on that null list. The 0.5.0 refactor moved car-list sampling into Update
ahead of RouteMonitorService, promoting this existing traversal defect into route-update starvation.

Version 0.5.1 handles those leaves through their actual car membership, state and destination data,
without a Passenger Jobs reference or hardcoded task type. Row sampling also contains exceptions,
clears stale rows at the next Layout, deduplicates repeated warnings and retries at the configured
cadence with a 0.25 s minimum after failures. Successful updates retain the saved cadence. This keeps
expensive failed reads out of every frame and allows the shared callback to reach route collection.
The compact list and every other refactor change are retained. Native graph direction matches the
game's own transitions; no route geometry workaround was added.

James stopped the other Class150 chat and assigned this build here. On takeover, this checkout and
its only registered worktree were clean at 7f9ee95; the other chat had withdrawn its uncommitted
passenger-task patch. This build recreates that reviewed fix and adds failure-containment coverage.
Two agents reviewed the fault, production boundary and tests. The new car-list suite reproduces the
original exception against HEAD's old implementation, then passes all 33 checks against the fix,
including null-car leaves, exception containment, Layout consistency, retry cadence and recovery.
Adjacent historical Route HUD and Driving HUD checkouts are clean; the provenance scratch folder
contains no additional source fixes.

Full Validate.ps1 now passes with zero build warnings/errors: 599 checks across eight suites,
51/51 metadata contracts, the 14-file package audit and 12 isolated installer assertions. The DLL is
187,904 bytes, 512 bytes larger than 0.5.0; no new runtime dependencies were added. The route-service
test exposed a pre-existing setup timing assumption, independent of CarList: cold JIT exceeded the
collector's real 2 ms budget, so the initial signal could arrive on frame five rather than four.
The original harness failed 2/5 fresh processes. Test setup now allows at most 32 updates at the
original simulated time for native/optional readiness before every original timed assertion; it
passes 5/5 fresh processes. No route-service production code or lifecycle assertion was changed.

Actual installation is recorded separately below once complete. James launches the game; runtime
visual, coverage and performance acceptance remain outstanding.

## 0.5.1 installed for James's retest - 23 October 2026

With Derail Valley closed, Install.ps1 installed the audited 0.5.1 package. All 14 installed files
match the package; assembly version is 0.5.1.0. Package SHA256:
C122C7CD12E47F643E85D522C2862C6EC31FB3484D554305271085FBE94DE395.
Installed DLL SHA256: B8A7B88ACB19800CDA1E01E552FF22643C246F9E875A924A4EC08377C66158E7.

Verified full prior mod/settings/cache and original UMM Params.xml backup:
artifacts/backups/upgrade-0.5.1-8235dee7cf81427bbbbef39d61819f12/.
The installation-receipt.json there includes final enablement verification. Current Settings.xml
remains byte-identical (SHA256 1CA78CE317D97EAE86976B72C0333DC98DAE3E3F41DCAFB0384F6104FB1C5A9D).
Only DadsRouteHUD Enabled=false -> true changed in Params.xml; DadsDrivingHUD was already false and
remains disabled. Parsed XML matches the original with exactly the requested enablement values;
every other setting is preserved. Only backed-up DadsRouteHUD.dll.*.cache files were cleared; none
remain. James launches the next game test and UMM must regenerate its 0.5.1 cache.

The game was not launched and runtime acceptance remains pending. The validated ZIP is unchanged
by this post-installation source handover entry; its bundled handover records the build state.

## 0.5.2 readable route events and source coverage - 23 October 2026

James's 20:58 screenshot shows 27 displayed / 32 retained events on page 1/1, with a 63 m-1.9 km
gap and unreadable structural track/junction names. Installed filters show every event category;
opposing observations are hidden. Thus a page-size cap does not explain that screenshot's gap.
The current 0.5.1 game log has no HUD OnUpdate exceptions or car-list sampling warnings, unlike
the previous 0.5.0 failure. The old freeze is separate from the sign coverage defect below.

Confirmed sign source gap: installed DoubleTrack 2.1.1 replaces DV.Signs.SignPlacer with its own
CustomSignPlacer. Its private PlaceSign does not call the native method watched by the HUD, and the
late-enable fallback previously looked only for native placer components. This omitted the exact
DoubleTrack speed/grade placements. Version 0.5.2 adds an optional checked reflection observer and
read-only recipe matching for that actual implementation. It keeps native support and no static
DoubleTrack reference. Captured track anchors now remain independent of later visible-pole moves;
the installed Toolbox performs clearance adjustments of several metres, which could otherwise
make an already known sign fail the one-metre track projection check. Unknown late-enable matches
still remain partial instead of being assigned to nearby parallel rails.

The compact two-column list now uses human labels with bold type colours, wrapped descriptions and
separate reservation notes. Native station/yard parts give station track names; structural source
labels become plain track descriptions, while raw identifiers stay in the complete inspector.
The driving list has no paging or row cap. One full scroll extent and a binary viewport lookup
paint visible rows only; formatted descriptions and Unity-measured heights are reused across moving
snapshots. Caches retain one generation. Opposing/type-filter counts, mapped extent/termination and
independent sign/signal coverage warnings are always shown in list mode. Inspector paging remains.

Native Junction has connection data but no destination-name field. NativeJunctionNames therefore
maps each branch through native directed potential connections to the first station on each path,
using actual StationController names rather than guessed nearest stations or a manual alias table.
It shows the selected station destination and other branch options; left/right still comes only
from native geometry. Work advances outside IMGUI with a cooperative 24-track / 0.5 ms Update budget.
The 50 km/4096-directed-track destination search bounds report unknown/partial alternatives and do
not cap route-event collection. Visited-track geometry subscriptions invalidate off-route caches;
Reset unsubscribes them. Station-name lookup does not cache temporary empty/partial catalogue misses.

Signal limitation: the log records DVSignals creating exactly 100 buffer-stop lamp controllers,
matching the previous unplaced-controller diagnostic. BufferStopSignalController deliberately
passes null placement; native BufferStop has no universal track/span/direction association, and
DVSignals/AI Traffic's placement-based lookups skip those controllers too. The diagnostic now counts
full controller types and explicitly names terminal buffer lamps, retaining all missing counts and
partial coverage. No guessed buffer-lamp placement is added; source completeness is not claimed.

Three agents implemented and independently cross-reviewed the source/list/native naming changes.
The complete validation passed with zero-warning game-reference builds, 679 checks across nine
suites, 64/64 metadata contracts, the 14-file artifact audit and 12 isolated installer assertions.
The final literal DoubleTrack label cleanup adds one focused naming regression (680 checks total),
with affected suites and package audits repeated afterward. The DLL remains about 196 KiB with
no new runtime dependencies. Actual installation is recorded separately below once complete.
James launches the game. Native visual, physical-board comparison and real frame timing still need
the next drive; source-boundary tests cannot establish those observations.

## 0.5.2 installed for James's retest - 23 October 2026

Installed with the game closed; all 14 files match the audited package and assembly version is
0.5.2.0. Package SHA256: 9C9CF0E66C0B3FE189344487DE51FE0B32152885DF26565D51E36CF0303B4627.
DLL SHA256: 2D870B7A56E9103EA04404AACD01CFBF28C7B1F8C5464656A6B04D5BBA5AB8B5 (200,704 bytes).
Verified full prior installation/cache and UMM Params.xml backup with installation-receipt.json:
artifacts/backups/upgrade-0.5.2-6fc114a301904495b90dda93ab30e13e/.

Current Settings.xml remains byte-identical, SHA256
1CA78CE317D97EAE86976B72C0333DC98DAE3E3F41DCAFB0384F6104FB1C5A9D. Params.xml is also byte-identical:
Route HUD was already enabled and Driving HUD disabled, and both flags remain as requested.
Only backed-up DadsRouteHUD.dll.*.cache files were removed; none remain until UMM regenerates one.
The game was not launched. The audited ZIP is unchanged by this post-installation source record;
its bundled handover records the earlier build state. No push or release was made.

## 0.5.3 recurring collector stall - 23 October 2026

James supplied HUD_BUILD_SESSION_REPORT.md from the Toolbox performance investigation. Preserved
evidence lives under his Toolbox workspace evidence/hitch-attribution-20261023; all eight snapshot
hashes and three exact-tested source hashes were independently verified. Do not overwrite those
0.5.1 sources with current work. Mode 3 broad timers identified OnUpdate -> Collect at about
491-646 ms; callback-only timing and a stationary HUD-off/on control corroborated the mod fault.
At the same (1721,131,4669) position, OFF had no HUD callback and ON2 HUD maxima were 499.28/471.79 ms.
Parent/child timings are inclusive. The tests contain no GPU timing; other hitches remain.

The tested DLL is 0.5.1 (B8A7B88ACB19800CDA1E01E552FF22643C246F9E875A924A4EC08377C66158E7).
The handoff did not runtime-measure 0.5.2, which was installed afterward. Source review confirmed
0.5.2 retained the same periodic invalidation and added the optional placer query. Its previous
automated checks did not measure Unity scene-query cost. Recorded hardware is i7-13700HX, about
32 GB RAM and RTX 4080 Laptop. There is no evidence that the 20 km horizon exceeds hardware capacity.
The collector reads about 800 profile samples at 20 km and yields between samples; whole-scene
sign discovery is independent of route horizon. The range stays 20,000 m for the comparison.

Confirmed redundant work: EnsureTrack ran Prune and whole-scene root discovery before its cache
lookup. Every five seconds Prune discarded all track projection caches and marked the root index
unready, even without membership changes. Origin shifts and new placements could also repeat scene
discovery. Native RailTrack.GetClosestPoint scans its point set, so cache loss repeats projection
work too. This is the strongest source-supported explanation for the measured Collect stall; the
original expensive leaf split remains unmeasured and must not be presented as established fact.

Version 0.5.3 performs initial/optional-availability discovery, then reuses exact placements and
origin-relative spatial cells. Both installed native and DoubleTrack implementations generate
future roots through the already observed PlaceSign; no new target or scanner is introduced.
Late DoubleTrack activation attaches that same checked observer once if PatchAll skipped it while
inactive. Maintenance advances 32 known roots and 16 tracks per call, including one active Update
call per frame, and invalidates affected data after actual content/removal/geometry changes.
Unchanged roots keep caches. Ambiguous candidate removal, raw/accessory edits, destruction,
late native/optional generation and world shifts remain covered; no horizon/event cap was added.

DVPerfProbe 0.2.0 has no supported arbitrary-method filter for these leaves. HudTimings therefore
adds logging-gated fixed counters around HUD Update, collector steps, each discovery query, root
index/refresh, recipes/projections, profile sampling, capture completion, optional telemetry,
publication/merge/shift, junction names and list rebuilds. Sampling has no heap allocation, file IO
or Harmony timer patches. Five-second aggregates report inclusive maxima/totals/calls and over-2-ms
counts, with selected horizon and retained event count. Disable/reset generations prevent old-world
or unfinished scopes leaking into later reports. Existing enableLogging controls all instrumentation.

Three agents audited evidence/source, implemented the cache/lifecycle correction and cross-reviewed
coverage/performance. Cold initial discovery, optional activation and individual native recipe or
projection calls remain synchronous and are separately timed. Full build/test/install evidence and
the necessary fresh stationary runtime comparison follow below; no measured improvement is claimed
before that game run. Probe/FSR/graphics settings are preserved. James launches the game.

Full Validate.ps1 passes: zero game-reference compiler warnings/errors, 733 checks across nine
suites (including 96 actual sign-index and 22 timing-lifecycle checks), 64/64 metadata contracts,
the 14-file package audit and 12 isolated installer assertions. The sign fixture now matches the
actual native BaseSign component type, rather than substituting a GameObject, and game-reference
compilation verifies both component and accessory identity handling. The 2,048-root regression
retains observations while proving unchanged five-second cycles and world shifts perform no
additional scene searches, projections or cache replacement. Other checks cover bounded maintenance,
raw/accessory mutations, removals/ambiguous retry, late optional observation and recipe matching.
These prove source behavior against boundary doubles, not Unity call duration or a measured fix.

## 0.5.3 installed; stationary measurement deferred - 23 October 2026

With Derail Valley closed, the audited package was installed and all 14 file hashes verified.
Version 0.5.3.0; DLL SHA256 78B98572ADF5C6EAC8BC53981DD0493DB60FA447D1CDF50D2AB656BE8D5B3200
(206,336 bytes). ZIP SHA256 BEDD8900D68B894D4ABF406D01E81B8774EC7E3B924FA9D63A152A9F71A2EE32.
Verified prior mod/cache and Params.xml backup with installation-receipt.json:
artifacts/backups/upgrade-0.5.3-46347268462446509b0b40b00fdd9683/.

Settings.xml and UMM Params.xml remain byte-identical; look-ahead is 20,000 m, logging enabled,
Route HUD enabled and Driving HUD disabled. Only backed-up HUD shadow caches were cleared; no
HUD cache remained at verification. DVPerfProbe settings are untouched (SHA256
883284597DD55031B2BB93388DE00C3CC22C1BC7F899F6BECEF76813235131CE); FSR/graphics were not changed.
The game was not launched. Asked to load a fresh stationary 90-second test, James answered
"I'll test later". Stop here; do not claim new timing numbers or an observed reduction, and do not
schedule unsolicited follow-up. The next test should preserve this range and inspect loaded cache,
HUD stage aggregates and coverage as described in VALIDATION.md. The initial/availability census
can still be synchronous; distinguish that from recurring warm stationary stalls.

The validated ZIP is unchanged by this post-install/deferred-test source record. No push or release.

## 0.5.4 stationary list layout and junction clarity

James reported a missing-signal message appearing/disappearing about once per second, requested
one nearest next station per junction branch with travel-relative throw arrows, and asked for
job/loco/consist details above events. His final clarification requires all status messages to
overlay the bottom of the list window on black so they cannot make the rows bump up and down.

Confirmed source causes: OptionalSignalTelemetry replaced the known partial-coverage status with
refresh activity, and the same-route replacement hold also discarded the previous signal coverage
wording. Both retain partial status now, until the newly completed sample actually changes it.
The event header previously used conditional/wrapped status labels, so message changes could move
every row. Status, mapped coverage and filter counts now paint in a fixed four-line opaque black
footer after the scroll view; they do not participate in GUILayout. A constant trailing spacer
allows the last event to scroll clear. The footer leaves the scrollbar/grip visible and window
minimum height reserves readable event space. The heading stays one fixed-height line. Distance
column width follows format/horizon samples, avoiding rewrap when the widest event passes or
distances count down. Actual route/aspect/consist changes can still alter the displayed content.

NativeJunctionNames now visits tracks in rail-distance order and stops at one nearest named station
per branch, rather than collecting all first-station alternatives beyond further forks. In a
station, the approach yard's native identity is excluded so the destination is the next distinct
station. Maps are cached per junction AND approach yard; opposite-direction encounters cannot
cancel each other's searches. Existing native invalidation, directed loop guards and cooperative
24-step/0.5 ms work with 4,096-node/50 km safeguards remain. Partial geometry gives nearest-known
wording, never a claim of proven nearest/full coverage.

Each branch arrow compares the approach travel heading with a chord up to 30 m into that branch's
native kinked geometry. This observes divergence beyond a shared initial tangent. Reversing travel
reverses left/right; branch array order is not directional evidence. The selected option carries
[set], and label/graph marker share one resolved direction. Unknown headings remain explicit.
The existing three-degree ahead threshold remains; very gentle turns and short connectors can
read as ahead. No custom station-name table, dependency, scanner or event/horizon cap was added.

Two agents independently reviewed UI and native mapping. Review caught/fixed small-window footer
occlusion, scrollbar/grip overlap, a premature sign-sample availability claim, label/graph marker
inconsistency and cancellation between approach-yard mappings. Added regressions cover same-source
partial-status retention/clearing, repeated replacement holds, unequal track counts/distances,
cheaper revisits, one station on a tie, branch reorder/reverse/unknown headings, in-station mapping
and simultaneous approach yards. Actual curved forward/reverse geometry compiles from production
source in the geometry suite. The game was not launched; visual and performance acceptance remain
pending as described in VALIDATION.md. Settings, the 20 km range, sign/performance fixes and other
prior changes remain in scope and are preserved.

Full Validate.ps1 passes: zero game-reference compiler warnings/errors, 781 assertions across
nine suites (14 driving, 271 route core, 88 service lifecycle + 22 timing, 102 native geometry,
52 native names, 96 sign index, 58 optional signals, 33 car list, 45 telemetry), 64/64 metadata
contracts, the 14-file package identity/reference audit, 12 isolated installer assertions and
git diff --check. UI acceptance and a fresh stationary performance comparison remain pending;
James launches the game when ready.

0.5.4 installed with the game closed; all 14 package files verified. DLL assembly 0.5.4.0,
208,384 bytes, SHA256 23A05E4F6ECD89F9BE48A4B4C2A3F974D0BD7922F64301216BFF874CD3C84281.
ZIP SHA256 1A390A217AE5D6397D486FF3C99862370BE3FE2D535CA31DECCC73E33F3A7144.
Verified prior mod/cache and Params.xml backup plus installation receipt:
artifacts/backups/upgrade-0.5.4-83d37aab4e2649d08347380f0bee9013/.
Settings.xml and UMM Params.xml remain byte-identical; 20,000 m range and logging retained,
Route HUD enabled, Driving HUD disabled. HUD shadow caches backed up and cleared (zero remain).
DVPerfProbe settings retain SHA256 883284597DD55031B2BB93388DE00C3CC22C1BC7F899F6BECEF76813235131CE.
No graphics/FSR changes or game launch. This post-install record does not alter the audited ZIP.
No push or release; next game visual/performance acceptance remains James's later test.
