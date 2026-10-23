# Dad's Route HUD — combined compact and driving-strip HUD

One HUD with compact columns or a horizontal driving strip, selected in UMM. Both modes share the retained 50 km route collector, native telemetry and existing-instance signal lookup fix. Based on Zeibach/Koteq's MIT source. The older standalone compact build is retained as a disabled, fixed fallback.

## UMM controls and independent windows

- Choose Driving strip or Compact columns; choose Route graph, Route event list, or Graph + events.
- Click **Detach route** in the HUD header to move the graph into its own window. Click **Attach** in that window (or **Attach route** in the HUD) to return it. Header actions stay visible while scrolling and remain usable with geometry locked. Use the native free cursor for these controls.
- UMM also provides an **Attached to HUD / Detached window** selector, visibility, collection range (100–50,000 m), profile height and displayed content.
- The route list scrolls through every matching event, with wrapped, bold colour-coded names and no page-size limit. Station tracks use the game's station names; mainline junctions show connected station destinations and the selected branch. Unresolved destinations remain explicit.
- Show/hide tracks, junctions, speed/grade signs, signals, other signs and opposing observations. Hidden counts and incomplete coverage are stated beside the list; all observations remain in the complete UMM catalogue.
- Choose separate background and text/graphics opacity for each window. Metric selection/order/units and train details remain configurable.
- With the native game cursor available, drag a header or its lower-right grip. Both windows scroll when content exceeds the chosen size. UMM also provides explicit size fields and an optional geometry lock.
- The main HUD starts at the actual screen's top right with a 10px margin. Geometry is remembered after dragging/resizing. Reset windows restores the default top-right placement; the separate graph starts below where space permits.
- **No Mod Toolbar integration.** No toolbar dependency, buttons or toolbar-owned windows are created.

## Driving strip and atlas

Selected operating metrics run left to right in measured label/value cells, wrapping when necessary. The default selection includes speed, throttle/reverser, train/independent/dynamic brake application, mechanical power demand, tractive effort, brake-pipe pressure and gradient. The inherited metric catalogue, raw native ports, units and ordering remain configurable in Unity Mod Manager (UMM).

Route collection defaults to **50,000 metres** along the currently selected native track connections. The default display is one continuous grade profile, marker row and distance axis across the selected range, up to 50 km. A 5 km close-up and five-section atlas are optional duplicate views of that same route, not additional coverage. Enable them separately in UMM if wanted; select **Inspect every retained route event here** for the complete paginated catalogue, including distance, source ID, direction, aspect, reservation and freshness.

Dense markers combine into exact counts. Clustering and pagination never remove records or select only important events. Native track entries, junctions and every associated physical sign part/accessory are retained, including opposite-facing observations. Geometry samples form the grade profile separately; geometry-derived speed estimates are not presented as posted limits. Profile slopes show relative elevation with stated vertical exaggeration. S/V/J/G/T identify signal/speed/junction/grade/track markers; numbers identify groups of retained events. Tokens use contrasting text on bright signal backgrounds.

On the first load of 0.2.1, the simpler continuous view is selected once: optional close-up and five-section atlas are turned off. Range, window placement, metric choices and other settings are preserved. Subsequent display choices are remembered.

The walker has no old 100-track, 64-signal or presentation-row completion cap. Work continues across Update frames. Directed native intervals prevent repeated traversal, including return to the untravelled prefix of the starting track. A loop closes at the first already covered directed span. The display reports reached distance and actual termination, and shades unknown space. A shorter loop, dead end, missing geometry, partial sign association or unavailable signals is never labelled complete 50 km event coverage.

The collector reads native geometry and sign metadata without adding sign colliders or forcing scene streaming. Native placement capture preserves exact source identity. Late enablement validates native sign recipes against observed signs, with unmatched/ambiguous data reported explicitly. See [native sign association](docs/NATIVE_SIGN_ASSOCIATION.md).

## Candidate status

Candidate **0.5.2** makes the route list readable and fully scrollable, names junction destinations
from native track connections, and supports DoubleTrack's separate speed/grade sign placer.
Exact captured sign anchors also survive later pole-clearance adjustments. It retains the 0.5.1
passenger-task freeze fix, graph, window settings and other refactor changes. See
[the handover](docs/HANDOVER.md) for evidence, independent review, exact checks and runtime acceptance
still required. No runtime dependencies were added; optional mods remain optional.
Contributors can run `./Validate.ps1 -GamePath '<game path>'` for the complete local checks;
this builds/packages/tests without changing the real installation.

Candidate **0.4.2** restores the earlier compact distance + description list while retaining Route
HUD's graph, independent windows, grouped metrics and full event catalogue. It corrects reuse of a
previous capture's position after a moving refresh, which could trigger another rebuild and remove
optional signals. Build, pure and service-boundary regressions pass; in-game stability and appearance
still require James's test. See [the current handover](docs/HANDOVER.md) and [validation](docs/VALIDATION.md).
Use `./Install.ps1 -GamePath '<your Derail Valley path>'` only with the game closed, after building,
testing and packaging. It backs up the current mod, preserves settings/UMM flags and clears this
mod's shadow cache. The earlier candidate notes below are historical.

Version **0.3.1** is built and packaged for testing. [The handover](docs/HANDOVER.md) records zero-warning game-reference compilation, 152 route/layout checks, 14 conversion checks and 51 metadata-contract checks. These do not establish in-game layout, full-route source coverage or frame timing. Window mouse interaction and visual acceptance remain pending in [VALIDATION.md](docs/VALIDATION.md).

The candidate has its own DadsRouteHUD mod ID, DLL and settings. Building and packaging never install it. For a later test installation, select the desired HUD in UMM and restart. Dad's Driving HUD and previous Rinzler files/settings are not migrated or overwritten.

## Optional mods

DVSignals, AI Traffic and DoubleTrack remain optional, without static assembly references or mandatory requirements. The HUD reads existing local signal/controller and reservation state; it does not request reservations, change signals/switches or create a traffic manager. Signal names use observed aspect colours where available. AI ownership requires positive identification. A red aspect alone does not establish its cause.

All discoverable heads on associated DVSignals controller placements are retained, including opposing and embedded distant heads. Their shared controller placement anchors distance. Installed DVSignals normally updates controllers within 1.5 km of the camera; distant aspects may be stale and source update ages are unknown. See [the adapter documentation](docs/OPTIONAL_SIGNAL_ADAPTER.md).

Multiplayer locomotive-ID/track/signal-lock attribution remains a stretch goal. Shared runtime IDs alone do not establish replication or ownership. No player-name or nearby-train tracker is implemented.

## Build and package

Clone with submodules, install a .NET 8 SDK, and use a Derail Valley installation containing Unity Mod Manager:

```powershell
./Build.ps1 -GamePath 'C:\Program Files (x86)\Steam\steamapps\common\Derail Valley'
dotnet run --project tests/DrivingMath.Tests.csproj --configuration Release
dotnet run --project tests/RouteCore.Tests.csproj --configuration Release
./Package.ps1
```

Output: artifacts/DadsRouteHUD-0.3.1.zip. QuantitiesNet is built from pinned MIT source. Current game assemblies are local references; game/Unity/UMM/optional DLLs and obsolete upstream lib/ binaries are excluded from packages. [Project context](docs/PROJECT_CONTEXT.md) records architecture and provenance.

## Reading the metrics

- Control percentages describe handle position, not predicted braking or engine output. Reverser displays R/N/F and directional application.
- Air/boiler pressures are gauge pressure: native absolute bar minus the game's 1 bar atmosphere.
- Power is requested tractive effort multiplied by signed forward speed in SI units, displayed as magnitude. It is mechanical demand before adhesion/slip limitations, not measured wheel-contact or shaft power. The inherited 3.6× conversion error is corrected.
- Vehicle mass, kinetic energy and grade resistance describe the selected vehicle, not the consist. Raw port type names are not physical unit metadata. Unsupported/uninitialised readings show --.

## Licence and provenance

Retain LICENSE, QuantitiesNet.LICENSE and THIRD_PARTY_NOTICES.txt. This public fork preserves the original Zeibach MIT history and credits Koteq's compatibility contributions. Its source-only import omits the compatibility branch's committed game DLLs. No Rinzler implementation is reused or distributed. A prior limited declaration/settings comparison means this is not described as a formal clean-room exercise.

## Licence and credits

Dad's Route HUD and its fork additions are distributed under [MIT](LICENSE). Original copyright/permission notices are retained. See [credits](CREDITS.md), [third-party notices](THIRD_PARTY_NOTICES.txt), the separate [QuantitiesNet licence](QuantitiesNet.LICENSE), and the [pinned-source verification](docs/LICENSING.md).


## Three independent sections (0.3.0)

In UMM, **Separate all three sections** detaches metrics and the continuous graph, and selects Graph + events. The main window retains the route event list and train details. Each window is independently movable/resizable, with saved geometry. Use Detach metrics / Attach metrics and Detach route / Attach route in the main header, or Attach in each detached header. No Mod Toolbar integration or additional collector is introduced.

Metrics support **1-8 columns**. Each metric has an Auto / 1-8 column selector; filter the settings by column and use its up/down arrows to set reading order among metrics with that saved assignment. Auto preserves the previous row-major placement. Reducing the count folds higher assignments into the last visible column, without changing their saved choices; increasing restores them. Units, precision, selections and prior ordering are retained. Column assignments are ignored by the horizontal tape, which uses the normal provider reading order.

For a tape along the top or bottom, use **Top tape / Bottom tape** in UMM, or select Driving strip and resize the detached metrics window wide and shallow (minimum height 80 px). Its own opacity and visibility controls are in UMM. Attached, detached-metrics and details sizes are saved separately so changing metric style does not resize the detached details window. First-open metrics placement keeps existing headers reachable where screen space permits. All windows scroll overflow rather than dropping selected metrics/events. User input/appearance and persistence across a real game restart remain subject to game acceptance.


## Fit route to graph width (0.3.1)

The default graph scale fits the available mapped route to the full width and labels that actual distance. For example, 4.9 km mapped of a requested 20 km uses a 0-4.9 km graph; the status separately retains 20 km requested. UMM's Selected-distance scale restores a fixed 0-requested axis with striped unavailable coverage. Full profile, markers, axes, shading, optional close-up and atlas share the chosen scale. Positive sub-kilometre ranges are supported. Display fitting does not extend collection, alter retained observations, change the requested horizon or bypass loops/dead ends.

The supplied screenshot reported only 4.9/20 km available. Inspection confirmed the previous renderer already used RequestedDistance rather than a fixed 50 km denominator. The cause of that shorter capture is not established by the screenshot. This update implements the explicitly selected fit behavior and preserves visible termination status while refreshing; it does not claim to repair an unverified collection defect. In-game visual acceptance remains pending.
