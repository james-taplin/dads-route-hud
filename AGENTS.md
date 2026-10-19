# Contributor instructions

This is a net48 Unity Mod Manager DLL mod, not a Unity Editor project. Read README.md, docs/PROJECT_CONTEXT.md and docs/VALIDATION.md before changes. Preserve MIT notices and credits. Keep optional mods optional and do not redistribute game/Unity/mod-loader/optional reference assemblies.

Prefer existing native functionality and the smallest evidence-supported fix. State what is confirmed, inferred or unknown. Collection stays outside IMGUI; all windows share one collector. Retain every collected route observation without ordinary count/importance caps. Use directed interval loop protection and explicit incomplete coverage. Do not guess units, sign associations, reservation ownership or multiplayer replication.

Build with Build.ps1 against the contributor's installed game/UMM; never use committed lib/ assemblies. Run both pure test suites, the metadata contract audit when optional references are available, git diff --check, Package.ps1 and the artifact audit. Keep test intermediates isolated. Building/testing does not install or publish. Report runtime/visual acceptance separately from automated checks.
