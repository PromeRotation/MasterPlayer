# Crucible recorded fixtures

Mechanic events and positions extracted from local CN Duty Recorder files. Actor IDs in these fixtures are encounter-local aliases; no player names are included.

- `eye-fixture.json`: ordinary board 3, 2026-09-19 13:50:22, three paired eye explosions (48506/48508).
- `gaze-fixture.json`: earlier ordinary board 3 recording, tether 195 marks the individual eyes that later resolve 48510; also includes the explosion pairs.
- `fury-613837.json` / `fury-713412.json`: high board 1, 2026-09-19 15:46:32, both sequences of three R6 explosions (48827) and 15m knockbacks (48805), including existing fire pools and their despawn times. Times are relative to the sequence start in milliseconds identified by the filename; event `t` values are in seconds. Positions are `[x,y,z]`.
- `uptime-low1-205342.json`: ordinary board 1, 2026-09-19 20:53:42, Knight Ancient Tomb (46866). Times are relative to cast start; the 4.7s packet cast predicts a 5.0s hit after the existing NPC correction, while the recorded effect arrives at 4.902s. The player originally exited the R6 circle after 0.554s. Includes the source recording SHA-256.
- `dodge-low1-212927.json`: ordinary board 1, 2026-09-19 21:29:27, five staggered Greater Demon cleaves (46879) and four outward Ground Uplift waves (46902–46905). The recording hit the player with the middle cleave at 207.474s and the opening circle at 270.226s. Per-fight times are relative to the first damaging cast start; actor IDs are local aliases.

`BmrLocalSyncRegression` replays eye events and runs the actual PR solver/movement dispatcher for Fury with 150/300ms publication latency and 0.75s knockback movement lock. These are offline regressions, not game-session acceptance results.

`DodgeUptimeRegression` feeds Ancient Tomb into the B01 mechanism engine and output adapter, then runs the actual PR solver and movement dispatcher with auto-dodge alone. With 0/80/200/300ms solver publication latency, it checks delayed departure and exit before the recorded hit, using the normal 0.5m danger inflation and PR time margins. It also checks cast completion, intermediate hazards, emergency routes, mechanism goals, and request expiry. Movement is simulated at 6m/s; live game behavior still requires testing.

`RecordedDodgeRefinementRegression` reproduces both 21:29:27 sequences through their actual mechanism components, PR solver, solution-validity check, and movement dispatcher. It covers auto-dodge alone and the production follow-goal geometry, 80/300ms worker publication latency, and 80ms movement startup delay. Every recorded impact is checked against its actual shape. The pre-fix host reproduces both stationary hits; the paired fix must avoid the entire five-hit/four-hit sequence.
