# Extending the game

The rule: adding a skill, class or boss means adding files, not editing the framework or a shared file. If a change needs a framework edit, it goes in its own small `core-change` PR.

## What is data and what is code today

| Thing | Where it lives | Status |
|---|---|---|
| Abilities (cooldown, range, width, damage, windup, resource cost) | `AbilityAsset` files in `Assets/Game/Data/Resources/Definitions` | Data. One file each. |
| Classes (role, stats, auto-attack, up to 3 skills, one defensive skill, resource, aggro range, body radius, threat multiplier, boss phases) | `ClassAsset` files in the same folder | Data. One file each. |
| Defensive skills: dodge (dash and brief invulnerability, optional counter rider), block (damage reduction), shield (absorb) | Ability `kind` plus its numbers | Data. The three kinds are code in `GameServer`; a fourth kind is a `core-change`. |
| Class resource (mana, rage, focus): max, start, regen or decay, gain per damage dealt, taken and blocked | `ClassAsset` resource fields | Data. Abilities spend it with `resourceCost`. |
| Windup (delay between starting an attack and it landing) | `windup` on an ability | Data. Clients draw the warning area while it counts down. |
| Healing (`heal` on an ability heals allies in its line, the caster included, clamped to max health; an ability with damage and heal does both) | Ability numbers | Data. |
| Taunt and threat (`taunt` seconds on an ability forces enemies hit to target the caster and lifts the caster's threat above everyone's; `threatMultiplier` on a class scales threat from its damage; enemies attack whoever has the most threat) | Ability and class numbers | Data. |
| Area shape of an ability: line, circle or cone (`shape`, `angle`, `radius`, `range`) | Ability asset | Data. A new shape is a `core-change` (`InArea` in the server and the telegraph mesh on the client). Projectiles are not built yet. |
| Skill variants (own effects plus damage, range, cooldown and cost scales) | `Variants` on the ability asset | Data. Chosen by the player's loadout; the Style button stands in for the skill tree. |
| Skins (look of a character) | `SkinAsset` files in `Assets/Game/Data/Resources/Skins` | Data. One file each. A skin is either a prefab with an Animator or a coloured primitive. |
| Skill effects | `castEffect` and `hitEffect` prefabs on each `AbilityAsset` | Data. Played on every client from use and hit events. |
| Bosses and phases (health thresholds, which attacks to use, speed, minions to spawn) | `Phases` on the class asset | Data. Telegraphs are drawn by every client from the ability's shape and windup. |
| Dungeons (ordered rooms of enemy spawns) | `DungeonAsset` files in the same folder | Data. See the dungeon room example below. |
| Status effects | `Entity.Statuses` (invulnerable, blocking, shield, dashing) | Used by the defensive skills. Generic debuffs are not built yet. |

The loader reads every asset under `Resources/Definitions` and validates the lot. Unknown ability ids, a class with more than 3 skills, non-positive health and similar mistakes fail loudly with a list of every problem.

## Worked example: add a skill

Goal: a new `fighter` skill called `whirl`, a short wide hit.

1. In the editor: **Assets > Create > Game > Ability**, save it as `Assets/Game/Data/Resources/Definitions/whirl.asset`. The file name is the id.
2. Set its numbers in the Inspector: cooldown `4`, range `3`, radius `3`, damage `18`.
3. Open `fighter.asset` and drag `whirl` into **Skills** (the order is the button slot order: slot 1 is the first entry).
4. Press Play. The loader validates on start.

No code changed. Only one existing file was touched (`fighter.asset`, to list the skill). To avoid even that, create a new class that lists the skill, as in the next example.

Tuning an existing value is the same: edit the asset. Reports of the form "value, current, wanted" from the tuning issue template are one-line asset edits.

## Worked example: add a defensive skill

Goal: a `roll` dodge for a new class, with a counter when it is timed well.

1. Create an ability `counter_strike` (kind **Attack**, cooldown `0`, range `6`, radius `1`, damage `30`). This is the rider.
2. Create an ability `roll`: kind **Dodge**, cooldown `3`, **Duration** `0.4` (how long you are invulnerable and dashing), **Dash Distance** `5`, **Perfect Window** `0.15` (a hit that lands this soon after the dodge starts fires the rider), **Rider** `counter_strike`.
3. Set the class **Defense** to `roll`.

A **Block** ability needs a duration and a **Reduction** between 0 and 1. A **Shield** needs a duration and an **Absorb** amount. Both feed the class resource through its **Gain Blocked** value, which is how a tank builds rage and a healer converts absorbed damage into mana.

## Worked example: give a class a resource

On the class asset tick **Has Resource**, then set an id (`mana`, `rage`, `focus`; the HUD colours these three), a max, a start value, **Regen** per second (negative decays), and the three gains. Abilities with a **Resource Cost** need the class to have a resource, and the validator says so if not. A negative cost grants resource instead of spending it.

## Worked example: add a dungeon room

A dungeon is one `DungeonAsset` (**Assets > Create > Game > Dungeon**, in `Assets/Game/Data/Resources/Definitions`). Add a room to **Rooms**, then add spawns: a class (not a player class), a position inside the arena (radius 20) and an optional skin id such as `slime_green`. Rooms run in order. The loader checks that every room has enemies and every class exists. The arena loads the dungeon named in `ArenaBootstrap` (`slime_cave`); a dungeon picker comes with the hub.

On a spawn, tick **Boss** to make it a section boss: the section is cleared when every boss is dead, trash does not have to die, and whatever is left disappears when the last boss falls. A section with no boss spawn is cleared when all enemies are dead. Give spawns the same **Pack** number (1, 2, ...) to make a group that pulls together: when one of them aggros, the rest chase the same target. Pack 0 means alone. On the class asset, **Leash Range** (larger than the aggro range, 0 means none) makes a mob that is dragged that far from where it stood give up, walk home and heal to full. Keep bosses at 0.

Between rooms everyone is fully restored. That is a tuning choice, not a rule; changing it means editing `BeginRoom` in `GameServer`.

## Worked example: add a class

1. **Assets > Create > Game > Class**, save as `Assets/Game/Data/Resources/Definitions/ranger.asset`.
2. Set role, max health, move speed and power. Set **Auto Attack** and up to three **Skills** from existing or new ability assets.
3. To play it, change the class id passed to `AddPlayer` in `ArenaBootstrap` (a class picker comes with the hub).
4. Add a test: load the definitions with `DefinitionLoader.Load` and assert the class and its abilities resolve. See `DefinitionLoaderTests`.

## Worked example: add a skin

1. **Assets > Create > Game > Skin**, save as `Assets/Game/Data/Resources/Skins/mage_frost.asset`. The name must start with the class id and an underscore (`mage_`) to appear in that class's skin list.
2. Either set **Prefab** to a rigged character prefab, or leave it empty and set **Body** and **Accent** materials, **Scale** and **Shape** for a primitive.
3. The skin id is the file name. Players pick it with the Skin button; other players see it because the server relays only its 32-bit id.

If the prefab has an **Animator**, it can use these parameters, and any it lacks are simply ignored: `Speed` (float), `Use` (trigger), `Slot` (int, 0 is the auto-attack), `Hit` (trigger), `Dead` (bool).

## Worked example: swap temporary art for real art

The imported characters, monsters and nature props under `Assets/Game/Art/Temp` are placeholders. To replace one character:

1. Import the new model and its animations. Give the model a Generic or Humanoid rig and set the avatar to be created from the model.
2. Make an Animator Controller with the parameters `Speed` (float), `Use` (trigger), `Slot` (int, 0 is the auto-attack, 4 is the defensive skill), `Hit` (trigger) and `Dead` (bool). `ArtImport` shows one way to build it; any controller with those parameters works.
3. Save a prefab of the model with that Animator, then open the skin asset (for example `tank_warrior`) and set **Prefab** to it. Scale the prefab so the character is about 2 units tall.

Class skins are found by the class id prefix (`tank_`, `healer_`, `mage_`), and skins with a prefab are listed before plain primitives, so a new `mage_` skin with a prefab becomes a choice with no other change.

## Worked example: change what a skill looks like

Open the ability asset (for example `fireball.asset`) and set **Cast Effect** and **Hit Effect** to other prefabs. A particle prefab with a one-shot burst works well. Nothing else changes, and the skill's numbers are untouched. 

## Worked example: add a skill variant

A variant is a changed version of one ability, for the skill tree to unlock later. On the ability asset, add an entry to **Variants**: an **Id** (for example `crimson`), the numbers it changes (**Damage Scale**, **Range Scale**, **Cooldown Scale**, **Cost Scale**; all 1 means no change) and its own **Cast Effect** and **Hit Effect**. A green bolt becoming a red bolt is a variant with a red effect and a damage scale above 1.

- The player's choice is a loadout: the variant id for each ability slot, sent when joining (`GameServer.AddPlayer`, the join message). The server ignores an id the ability does not have.
- The server applies the scales itself, and the use and damage events carry the variant id, so every client plays the right effect.
- For now the **Style** button picks the same variant number for the attack and the first skill (style 1 is the first variant of each). The skill-tree screen replaces that later; nothing else changes.
- Heal and circle size scale with the damage and range scales.

## Worked example: add a boss

A boss is an ordinary class with phases. The seeded `slime_boss` is the reference.

1. Make the boss attacks as abilities. Give each a **Windup** and a **Shape** (circle for a slam around the boss, cone for a sweep, line for a thrust) with the numbers for range, radius and angle. The warning area is drawn from those, so nothing else is needed for the telegraph.
2. Make a class with role None, its health, move speed, **Aggro Range**, **Body Radius** and an auto attack. Add the attacks to **Skills**.
3. Add **Phases** on the class asset. Each phase has **Health Below** (a fraction, highest first), the **Attacks** it chooses from, an optional **Speed Scale**, and optionally **Spawn Class**, **Spawn Count** and **Spawn Skin** for minions that appear when the phase starts.
4. Put it in a dungeon room as a spawn.

The validator checks that phases are ordered and that every attack is one of the class's abilities.

## Tests to write for any addition

- Add the new numbers to a `Game.Core.Tests` case if they change rules (damage, cooldown, hit shape). Core tests run without Unity: `tools\test-core.ps1`.
- Run everything: `tools\run-tests.ps1` and `tools\run-tests.ps1 -Platform PlayMode`.
- Anything about feel, readability or controls: label the PR `needs-device-test` and say exactly what to try.

## Known limits of the foundation (for the review before M2)

These are decisions and gaps, not bugs. They are why the foundation is not frozen.

- Client and server tick clocks are not synchronised. No clock offset estimation yet; needed before two phones play together (M1.5).
- Move commands are one tick each, capped at 2 per tick. Reconciliation replays unacknowledged moves and drops ones older than 1 second.
- Hits are areas (line, circle or cone), instant or after a windup. No projectiles. A windup hit uses the position and direction from when the attack started.
- Enemy behaviour is basic: attack whoever has the most threat (or the nearest hostile in aggro range), use the attacks of the current phase, and obey a taunt. No pathfinding. Enemy decisions live in the one big `GameServer` class (issue 53 splits it).
- A client starts a telegraph when the use event arrives, so a late event restarts the warning and a player who joins mid-windup misses it (issue 54).
- A dodge ignores Move commands while dashing, so the client briefly predicts movement the server discards. It snaps back on the next snapshot.
- Outside a dungeon dead players respawn after 5 seconds. Inside one they stay down until the next room or a restart.
- A dungeon is one arena with several waves (rooms); different layouts per room, doors and travel between rooms are not built.
- State uses floats. The server is authoritative so cross-machine determinism is not required; tests only prove same-seed repeatability on one machine.
- `WireCodec` allocates per message, so the combat allocation budget is not met.
- The networking library is not chosen; everything sits behind `ITransport`. Whether to move to Netcode for GameObjects is an open decision (issue 58).
- Player classes (tank, healer, mage) are temporary: weapons with their own skill trees replace them (issue 50). Mobs and bosses keep class definitions.
- Definitions are copied, not derived: a variant of a monster is a duplicate asset. A "based on" link is planned (issue 50).
- AI-controlled party members do not exist yet; the intended hook is server-side controllers that emit the same commands as a phone.
- Definitions load from `Resources`, which is fine for the prototype. Revisit if content grows large.
