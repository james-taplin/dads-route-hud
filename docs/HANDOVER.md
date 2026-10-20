# Development status

Version 0.3.8 is a test candidate, built, packaged and installed for testing on 20 October 2026,
as were 0.3.1 to 0.3.7 before it. In-game acceptance of the readable list, the switch markers, the
capture hold and the reverser-led direction is still pending. It combines compact columns and driving strip, independent draggable/resizable main and graph windows, per-window transparency, top-right initial placement and a configurable route horizon up to 50 km. All collected events remain retained; finite-pixel clusters and display filters do not truncate the complete catalogue.

The existing-instance signal-manager lookup performance correction is present. The periodic refresh correction keeps the last valid same-route signal view until its replacement is ready, rather than exposing a temporary native-only catalogue. Changes of route/vehicle/source invalidate old observations. In-game confirmation of the latest publication and GUI behavior remains pending.

Automated checks of the current snapshot: zero-warning installed-reference compilation, 224
route/window/layout/naming checks, 14 conversion checks, and 51/51 metadata contracts. Public CI runs the
two pure suites without proprietary references. Follow VALIDATION.md for actual game acceptance.
Synthetic in-memory timings are not game performance measurements.

Publication intentionally starts from the original MIT HUD history with a source-only import. Koteq's compatibility work remains credited. No game DLLs, private settings, backups or local profiling evidence are included. Neither Rinzler's binary nor its implementation is used. See LICENSING.md for pinned notices.

The public repository is behind this checkout: it holds the source import up to 0.2.0, and the
0.2.1, 0.3.0 and the 0.3.1 to 0.3.8 commits are local only. No tag, release or binary has been published,
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
