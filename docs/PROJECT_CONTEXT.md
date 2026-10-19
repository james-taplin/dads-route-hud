# Project context

Dad's Route HUD 0.2.1 is the combined compact/driving-strip fork of the MIT Heads-up Display for Derail Valley. Public repository: https://github.com/james-taplin/dads-route-hud. It is published as a fork of mspielberg/dv-hud.

The public source branch starts at original MIT revision 9acf926a2d7c8512ddb85cbf637b3e4a9bfa6f6f and imports the current fork source in one commit. The local development lineage includes Koteq b09fe942ab5118e6aa348a153f686a537a483d0d and Dad's Driving HUD. Koteq's compatibility contributions are retained and credited, but its old committed game DLLs are excluded by not importing that commit's history. QuantitiesNet is pinned at 2b6522cb70df8108af0d96dec6ebdc6c35a673aa. See LICENSING.md and CREDITS.md.

The net48 assembly uses UMM lifecycle callbacks and runtime IMGUI. NativeTelemetry supplies operating metrics. RouteWalkBuilder is pure incremental graph traversal; NativeRouteGraph/Geometry adapt selected native track connections. NativeRouteSignIndex preserves sign source placement through a read-only Harmony observer and validated native recipe reads. RouteMonitorService owns retained immutable captures and same-route signal replacement publication. OptionalSignalTelemetry reads existing native/optional source instances without static optional references or a recurring global manager search.

Overlay and HudWindow provide the compact/strip modes and independent saved windows. WindowGeometry handles bounded positioning/resizing. RouteMonitor renders profile, range overview and atlas; the complete inspector retains source details. Display filters do not change collection. No Mod Toolbar registration or cursor-unlock/extra driving binding is present.

Actual source coverage, optional-mod combinations, input/visual behavior and performance still require in-game acceptance. Native iteration budgets cannot pre-empt individual native/reflection calls. Distant signal update age is uncertain; multiplayer ownership remains unimplemented.


## Track-ahead clarity update

Version 0.2.1 adds a permanent HUD-header Detach route / Attach route action and an Attach action on the detached Track ahead window. Action hit areas are excluded from header dragging; geometry locking does not disable the action, and mouse/key input stays native when the game cursor is unavailable. Visibility changes apply at the next IMGUI Layout, with settings saved through the existing deferred path. No extra collector, toolbar integration, optional dependency or input binding is added.

The full-range graph now includes a continuous elevation profile across the requested distance. Close-up and five-section atlas are optional repeated views; neither extends/truncates the route. A one-time routeDisplayVersion migration selects the simpler continuous view while preserving range, metrics, geometry, train settings and subsequent user choices. UMM names and the marker legend are clearer; bright signal marker backgrounds use dark text. Source collection, loop protection and the existing native-instance lookup are unchanged. Build/pure tests do not establish actual in-game GUI/input acceptance.
