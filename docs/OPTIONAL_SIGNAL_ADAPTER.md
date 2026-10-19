# Optional signal adapter

The route fork consumes the single retained native route interval list. It does not walk its own route and does not use AI Traffic's approximately 2.5 km discovery helper. Every discoverable signal head on a projected controller placement is retained, including distinct heads sharing names or a controller, opposing-facing heads, and embedded distant heads that the native AllSignals list does not itself enumerate. Stable source identity is the native Signal.Id; controller placement is the shared anchor, not a claim of per-lamp distance.

Route interval projection is inclusive at the configured horizon, up to 50,000 metres. Root collection excludes an already encountered loop-closing endpoint when the walk reports LoopClosed. Track reference and directed span determine association; names and nearby-world-position guesses do not.

Observed controllers without exact native placement are counted in a partial-coverage diagnostic. They may be outside the requested route, but their membership cannot be established safely; they are never guessed onto a nearby track.

Work continues across main-thread Update calls with a nominal 2 ms iteration budget. There is no normal 100-track, 64-signal, 4,096-controller or 256-block-track truncation. A one-million-step corrupt-enumeration watchdog reports partial data and retains observations; it never claims complete coverage. An individual reflection call or final snapshot publication cannot be pre-empted by this iteration budget, so actual frame timings still require game measurement.

Each completed sample exposes UpcomingSignals, per-track reservation text, IsAvailable, IsComplete, IsReading, Status, SampledAt (Unity realtime seconds), and Diagnostics. IsReading indicates an active continuation; periodic baseline refresh waits for it so a large pass cannot repeatedly be cancelled before publishing. Hard car/direction/topology/geometry changes still invalidate obsolete work. No optional mods is an unavailable data source, not a required dependency. A native geometry horizon is not evidence that signal-controller data is complete.

All aspect update ages are unverified. Installed DVSignals 1.1.3 normally updates controllers within 1.5 km of the camera; queued propagation can update farther. The adapter flags observed distant aspects as potentially stale and never invokes controller updates, activation, aspect changes, reservations or switch alignment. Local occupancy/reservation tables are sampled rather than described as authoritative network state.

AI Traffic is optional enrichment for names, aspect labels/semantic colours and positively established reservation ownership. Existing TrafficManager/RailGraph backing fields are read instead of singleton getters that create objects. Unknown reservation owners stay unknown. No player names, locomotive identity tracking, train reveal, or multiplayer ownership claim is added.

## Existing signal-manager lookup — 0.1.1

The previous adapter synchronously called Unity Object.FindObjectOfType for SignalManager during each half-second refresh. Version 0.1.1 reads the existing private static _instance slot on the native closed SingletonBehaviour<SignalManager> base instead. Binding verifies the exact native generic base, type argument and field type. A missing, destroyed or incompatible existing instance is unavailable; there is no global-search fallback and the auto-creating Instance getter is never called.

This removes a confirmed recurring scene-wide lookup. It does not establish that the reported approximately 155 ms half-second hitch came from that call, and no runtime improvement has been measured. The sign index still performs its existing global catalogue discovery on its five-second refresh/other invalidations, and individual native recipe calls remain synchronous. This change does not redesign those paths or add a profiling mode.

## Metadata validation

Run from the repository:

```powershell
dotnet run --project tools/OptionalContracts/OptionalContracts.csproj --configuration Release -p:NuGetAudit=false -- "C:\Program Files (x86)\Steam\steamapps\common\Derail Valley\Mods" ".\build\optional-contract-audit.json"
```

The standalone net8 tool reads PE metadata and never loads inspected assemblies for execution. All 51 contracts matched: 33 optional members/enum constants, 16 native sign-association contracts, and two existing-singleton binding contracts (SignalManager's exact constructed base and the native generic base's static instance field). The JSON records source SHA256 hashes, member visibility, arity/static shape and raw signatures. This is a compatibility declaration check, not a runtime or visual test.

In-game acceptance remains: absent/present optional-mod combinations, more than 64 signals, exact distant controller coverage, actual reservation changes, camera/scene reload behaviour, pass cancellation, runtime freshness and cold/warm frame timings.
