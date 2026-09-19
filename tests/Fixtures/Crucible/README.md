# Crucible recorded fixtures

Mechanic events and positions extracted from local CN Duty Recorder files. Actor IDs in these fixtures are encounter-local aliases; no player names are included.

- `eye-fixture.json`: ordinary board 3, 2026-09-19 13:50:22, three paired eye explosions (48506/48508).
- `gaze-fixture.json`: earlier ordinary board 3 recording, tether 195 marks the individual eyes that later resolve 48510; also includes the explosion pairs.
- `fury-613837.json` / `fury-713412.json`: high board 1, 2026-09-19 15:46:32, both sequences of three R6 explosions (48827) and 15m knockbacks (48805), including existing fire pools and their despawn times. Times are relative to the sequence start in milliseconds identified by the filename; event `t` values are in seconds. Positions are `[x,y,z]`.

`BmrLocalSyncRegression` replays eye events and runs the actual PR solver/movement dispatcher for Fury with 150/300ms publication latency and 0.75s knockback movement lock. These are offline regressions, not game-session acceptance results.
