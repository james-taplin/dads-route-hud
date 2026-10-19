# Project context

Dad's Route HUD 0.3.1 is the combined compact/driving-strip fork of the MIT Heads-up Display for Derail Valley. Public repository: https://github.com/james-taplin/dads-route-hud. It is published as a fork of mspielberg/dv-hud.

The public source branch starts at original MIT revision 9acf926a2d7c8512ddb85cbf637b3e4a9bfa6f6f and imports the current fork source in one commit. The local development lineage includes Koteq b09fe942ab5118e6aa348a153f686a537a483d0d and Dad's Driving HUD. Koteq's compatibility contributions are retained and credited, but its old committed game DLLs are excluded by not importing that commit's history. QuantitiesNet is pinned at 2b6522cb70df8108af0d96dec6ebdc6c35a673aa. See LICENSING.md and CREDITS.md.

The net48 assembly uses UMM lifecycle callbacks and runtime IMGUI. NativeTelemetry supplies operating metrics. RouteWalkBuilder is pure incremental graph traversal; NativeRouteGraph/Geometry adapt selected native track connections. NativeRouteSignIndex preserves sign source placement through a read-only Harmony observer and validated native recipe reads. RouteMonitorService owns retained immutable captures and same-route signal replacement publication. OptionalSignalTelemetry reads existing native/optional source instances without static optional references or a recurring global manager search.

Overlay and HudWindow provide the compact/strip modes and independent saved windows. WindowGeometry handles bounded positioning/resizing. RouteMonitor renders profile, range overview and atlas; the complete inspector retains source details. Display filters do not change collection. No Mod Toolbar registration or cursor-unlock/extra driving binding is present.

Actual source coverage, optional-mod combinations, input/visual behavior and performance still require in-game acceptance. Native iteration budgets cannot pre-empt individual native/reflection calls. Distant signal update age is uncertain; multiplayer ownership remains unimplemented.


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
