# Extending the game

The rule: adding a skill, class or boss means adding files, not editing the framework or a shared file. If a change needs a framework edit, it goes in its own small `core-change` PR.

## What is data and what is code today

| Thing | Where it lives | Status |
|---|---|---|
| Abilities (cooldown, range, width, damage) | `AbilityAsset` files in `Assets/Game/Data/Resources/Definitions` | Data. One file each. |
| Classes (role, stats, auto-attack, up to 3 skills) | `ClassAsset` files in the same folder | Data. One file each. |
| Ability behaviour beyond "damage along a line" (dash, shield, heal, projectile) | `Game.Core` | Not built yet. Needs an effect extension point (see issues #19, #20). |
| Bosses, telegraph shapes, phases | `Game.Core` | Not built yet. Milestone M2. |
| Status effects | `Entity.Statuses` exists; no effects use it yet | Stub. |

The loader reads every asset under `Resources/Definitions` and validates the lot. Unknown ability ids, a class with more than 3 skills, non-positive health and similar mistakes fail loudly with a list of every problem.

## Worked example: add a skill

Goal: a new `fighter` skill called `whirl`, a short wide hit.

1. In the editor: **Assets > Create > Game > Ability**, save it as `Assets/Game/Data/Resources/Definitions/whirl.asset`. The file name is the id.
2. Set its numbers in the Inspector: cooldown `4`, range `3`, radius `3`, damage `18`.
3. Open `fighter.asset` and drag `whirl` into **Skills** (the order is the button slot order: slot 1 is the first entry).
4. Press Play. The loader validates on start.

No code changed. Only one existing file was touched (`fighter.asset`, to list the skill). To avoid even that, create a new class that lists the skill, as in the next example.

Tuning an existing value is the same: edit the asset. Reports of the form "value, current, wanted" from the tuning issue template are one-line asset edits.

## Worked example: add a class

1. **Assets > Create > Game > Class**, save as `Assets/Game/Data/Resources/Definitions/ranger.asset`.
2. Set role, max health, move speed and power. Set **Auto Attack** and up to three **Skills** from existing or new ability assets.
3. To play it, change the class id passed to `AddPlayer` in `ArenaBootstrap` (a class picker comes with the hub).
4. Add a test: load the definitions with `DefinitionLoader.Load` and assert the class and its abilities resolve. See `DefinitionLoaderTests`.

## Worked example: add a boss (planned, M2)

Bosses do not exist yet, so this describes the intended shape and will be replaced by a real example when the first boss lands:

- A boss is an ordinary entity from a `ClassDef`, spawned with `GameServer.SpawnNpc` on the enemy team.
- Its phases and attack pattern are data (a list of phases, each with telegraphs and timings), driven by a boss controller in `Game.Core` that issues the same commands an ability use would.
- A new telegraph shape implements one interface and registers by id; it is not a new code path in the server.

## Tests to write for any addition

- Add the new numbers to a `Game.Core.Tests` case if they change rules (damage, cooldown, hit shape). Core tests run without Unity: `tools\test-core.ps1`.
- Run everything: `tools\run-tests.ps1` and `tools\run-tests.ps1 -Platform PlayMode`.
- Anything about feel, readability or controls: label the PR `needs-device-test` and say exactly what to try.

## Known limits of the foundation (for the review before M2)

These are decisions and gaps, not bugs. They are why the foundation is not frozen.

- Client and server tick clocks are not synchronised. No clock offset estimation yet; needed before two phones play together (M1.5).
- Move commands are one tick each, capped at 2 per tick. Reconciliation replays unacknowledged moves and drops ones older than 1 second.
- Hits are instant line checks. No projectiles, no area shapes other than a wide line.
- State uses floats. The server is authoritative so cross-machine determinism is not required; tests only prove same-seed repeatability on one machine.
- `WireCodec` allocates per message, so the combat allocation budget is not met.
- The networking library is not chosen; everything sits behind `ITransport`.
- AI-controlled party members do not exist yet; the intended hook is server-side controllers that emit the same commands as a phone.
- Definitions load from `Resources`, which is fine for the prototype. Revisit if content grows large.
