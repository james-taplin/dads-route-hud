# Project context

Dad's Route HUD 0.5.2 is the combined compact/driving-strip fork of the MIT Heads-up Display for Derail Valley. Public repository: https://github.com/james-taplin/dads-route-hud. It is published as a fork of mspielberg/dv-hud.

The public source branch starts at original MIT revision 9acf926a2d7c8512ddb85cbf637b3e4a9bfa6f6f and imports the current fork source in one commit. The local development lineage includes Koteq b09fe942ab5118e6aa348a153f686a537a483d0d and Dad's Driving HUD. Koteq's compatibility contributions are retained and credited, but its old committed game DLLs are excluded by not importing that commit's history. QuantitiesNet is pinned at 2b6522cb70df8108af0d96dec6ebdc6c35a673aa. See LICENSING.md and CREDITS.md.

The net48 assembly uses UMM lifecycle callbacks and runtime IMGUI. NativeTelemetry supplies operating metrics. RouteWalkBuilder is pure incremental graph traversal; NativeRouteGraph/Geometry adapt selected native track connections. NativeRouteSignIndex preserves sign source placement through a read-only Harmony observer and validated native recipe reads. RouteMonitorService owns retained immutable captures and same-route signal replacement publication. OptionalSignalTelemetry reads existing native/optional source instances without static optional references or a recurring global manager search.

Overlay and HudWindow provide the compact/strip modes and independent saved windows. WindowGeometry handles bounded positioning/resizing. RouteMonitor renders profile, range overview and atlas; the complete inspector retains source details. Display filters do not change collection. No Mod Toolbar registration or cursor-unlock/extra driving binding is present.

Actual source coverage, optional-mod combinations, input/visual behavior and performance still require in-game acceptance. Native iteration budgets cannot pre-empt individual native/reflection calls. Distant signal update age is uncertain; multiplayer ownership remains unimplemented.

In 0.4.2 the driving list returns to the pre-Route-HUD distance + compact description format, while
collection/inspector/graph/window features remain. Publication reprojects the live position when a
completed capture replaces retained, so offsets from different capture origins cannot trigger a
false route invalidation. Existing snapshot retention provides refresh stability. Service lifecycle
regressions compile production service code with native boundary test doubles; game acceptance is
still required. Install.ps1 preserves settings/flags and clears backed-up UMM shadow caches.

The 0.5.0 refactor was installed for James's test on 23 October. RouteMonitorService separates scheduling,
collection and publication; hard invalidation clears obsolete facts and same-route replacements
retain complete snapshots. Optional sources are identified by actual existing native instance.
CarList samples native state in Update and freezes formatted rows at Layout. Private metric settings
lookups, per-track sign membership, status sharing and passed-event binary search reduce repeated
work. The unused inherited track/list path is removed. No runtime dependencies or features were
added; serialized settings fields and the complete observation model remain compatible.

The 0.5.0 game log confirmed repeated CarList.GetNextDestinationTrack exceptions when a passenger-job
leaf supplied no nestedTasks. Because CarList now samples in the shared Update before route collection,
that error froze the route while metrics continued. Version 0.5.1 uses supplied car/destination data
for such leaves without depending on Passenger Jobs. Car-list row sampling now catches failures,
clears stale rows at the next Layout, deduplicates warnings and retries at the saved cadence (at least
0.25 seconds after a failure). Other Update consumers can continue. Native route direction matches the
game's Bogie transitions; no graph workaround is justified by this fault. Game acceptance remains pending.

Version 0.5.2 keeps two route-list columns but uses full station/track names, bold type colours and
wrapped descriptions. The main list has no paging or count cap: one full-height scroll extent and
binary viewport lookup paint only visible rows. Moving snapshots reuse formatted descriptions and
measured heights. Opposing/filter counts, mapped extent and source-coverage warnings remain visible.
The complete inspector retains its separate pagination and original source identifiers.

NativeJunctionNames maps each junction branch through directed native potential connections to the
first stations on its paths. Junction has no native destination-name field; this incremental cached
lookup is the confirmed gap filled by the helper. Work is limited to 24 tracks / 0.5 ms per Update;
unresolved or bounded results are explicit. Branch indices never imply left/right. Destination work
does not cap route collection or depend on optional mods. Native station names replace the need for
a manually maintained station alias table.

DoubleTrack 2.1.1 replaces the native sign placer with CustomSignPlacer. Version 0.5.2 observes that
optional placer's exact track/anchor/direction metadata and validates its read-only recipes for late
enablement. Captured anchors survive later Toolbox clearance moves of the visible poles. No nearest
track guessing or static DoubleTrack dependency is introduced. The known unplaced signal-controller
population includes terminal buffer lamps with no native placement; their source limitation remains
explicit rather than fabricating positions.


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
