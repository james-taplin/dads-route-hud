# Development status

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
