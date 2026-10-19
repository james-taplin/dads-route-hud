# Dad's Route HUD — combined compact and driving-strip HUD

One HUD with compact columns or a horizontal driving strip, selected in UMM. Both modes share the retained 50 km route collector, native telemetry and existing-instance signal lookup fix. Based on Zeibach/Koteq's MIT source. The older standalone compact build is retained as a disabled, fixed fallback.

## UMM controls and independent windows

- Choose Driving strip or Compact columns; choose Route graph, Route event list, or Graph + events.
- Toggle the separate route graph window, its visibility, near range, collection range (100–50,000 m), profile height, overview, next-event line and expanded atlas.
- Show/hide tracks, junctions, speed/grade signs, signals, other signs and opposing observations. These are display filters; all observations remain in the complete UMM catalogue.
- Choose separate background and text/graphics opacity for each window. Metric selection/order/units and train details remain configurable.
- With the native game cursor available, drag a header or its lower-right grip. Both windows scroll when content exceeds the chosen size. UMM also provides explicit size fields and an optional geometry lock.
- The main HUD starts at the actual screen's top right with a 10px margin. Geometry is remembered after dragging/resizing. Reset windows restores the default top-right placement; the separate graph starts below where space permits.
- **No Mod Toolbar integration.** No toolbar dependency, buttons or toolbar-owned windows are created.

## Driving strip and atlas

Selected operating metrics run left to right in measured label/value cells, wrapping when necessary. The default selection includes speed, throttle/reverser, train/independent/dynamic brake application, mechanical power demand, tractive effort, brake-pipe pressure and gradient. The inherited metric catalogue, raw native ports, units and ordering remain configurable in Unity Mod Manager (UMM).

Route collection defaults to **50,000 metres** along the currently selected native track connections. The display has a readable near profile and a full-range overview up to 50 km. In UMM, select **Expand the passive route atlas** for five equal range tiles (10 km each at the 50 km setting); select **Inspect every retained route event here** for the complete paginated catalogue, including distance, source ID, direction, aspect, reservation and freshness.

Dense markers combine into exact counts. Clustering and pagination never remove records or select only important events. Native track entries, junctions and every associated physical sign part/accessory are retained, including opposite-facing observations. Geometry samples form the grade profile separately; geometry-derived speed estimates are not presented as posted limits. Profile slopes show relative elevation with stated vertical exaggeration.

The walker has no old 100-track, 64-signal or presentation-row completion cap. Work continues across Update frames. Directed native intervals prevent repeated traversal, including return to the untravelled prefix of the starting track. A loop closes at the first already covered directed span. The display reports reached distance and actual termination, and shades unknown space. A shorter loop, dead end, missing geometry, partial sign association or unavailable signals is never labelled complete 50 km event coverage.

The collector reads native geometry and sign metadata without adding sign colliders or forcing scene streaming. Native placement capture preserves exact source identity. Late enablement validates native sign recipes against observed signs, with unmatched/ambiguous data reported explicitly. See [native sign association](docs/NATIVE_SIGN_ASSOCIATION.md).

## Candidate status

Version **0.2.0** is built and packaged for testing. [The handover](docs/HANDOVER.md) records zero-warning game-reference compilation, 128 route-core checks, 14 conversion checks and 51 metadata-contract checks. These do not establish in-game layout, full-route source coverage or frame timing. Window mouse interaction and visual acceptance remain pending in [VALIDATION.md](docs/VALIDATION.md).

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

Output: artifacts/DadsRouteHUD-0.2.0.zip. QuantitiesNet is built from pinned MIT source. Current game assemblies are local references; game/Unity/UMM/optional DLLs and obsolete upstream lib/ binaries are excluded from packages. [Project context](docs/PROJECT_CONTEXT.md) records architecture and provenance.

## Reading the metrics

- Control percentages describe handle position, not predicted braking or engine output. Reverser displays R/N/F and directional application.
- Air/boiler pressures are gauge pressure: native absolute bar minus the game's 1 bar atmosphere.
- Power is requested tractive effort multiplied by signed forward speed in SI units, displayed as magnitude. It is mechanical demand before adhesion/slip limitations, not measured wheel-contact or shaft power. The inherited 3.6× conversion error is corrected.
- Vehicle mass, kinetic energy and grade resistance describe the selected vehicle, not the consist. Raw port type names are not physical unit metadata. Unsupported/uninitialised readings show --.

## Licence and provenance

Retain LICENSE, QuantitiesNet.LICENSE and THIRD_PARTY_NOTICES.txt. This public fork preserves the original Zeibach MIT history and credits Koteq's compatibility contributions. Its source-only import omits the compatibility branch's committed game DLLs. No Rinzler implementation is reused or distributed. A prior limited declaration/settings comparison means this is not described as a formal clean-room exercise.

## Licence and credits

Dad's Route HUD and its fork additions are distributed under [MIT](LICENSE). Original copyright/permission notices are retained. See [credits](CREDITS.md), [third-party notices](THIRD_PARTY_NOTICES.txt), the separate [QuantitiesNet licence](QuantitiesNet.LICENSE), and the [pinned-source verification](docs/LICENSING.md).
