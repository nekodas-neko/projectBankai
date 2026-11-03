# Extending the game

The rule: adding a skill, class or boss means adding files, not editing the framework or a shared file. If a change needs a framework edit, it goes in its own small `core-change` PR.

## What is data and what is code today

| Thing | Where it lives | Status |
|---|---|---|
| Abilities (cooldown, range, width, damage, windup, resource cost) | `AbilityAsset` files in `Assets/Game/Data/Resources/Definitions` | Data. One file each. |
| Classes, which for players are weapons (display name, skin prefix, playable flag, stats, auto-attack, up to 3 skills, one defensive skill, resource, aggro range, body radius, threat multiplier, boss phases) | `ClassAsset` files in the same folder | Data. One file each. |
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

## The ability button is gated by the weapon's resource

The skill next to the attack has only a short cooldown (about half a second, so one tap cannot fire it twice); what limits it is the weapon's resource, and the ring around the button shows how far the resource is from the cost. The pattern, in data only:

- the weapon's resource starts full (for example 100 rage),
- the skill has a positive **Resource Cost** (30), so it can be used three times in a row,
- the auto attack has a **negative** Resource Cost (-10), which gives resource back on every use, so two auto attacks earn the skill again.

The ring on the button is the larger of the cooldown left and the missing part of the cost (`ActionButton.RingFill`). Seeded numbers: sword and shield rage 100 (the attack gives 10, the bash costs 30), staff focus 100 (the bolt gives 8, the fireball costs 35), healing mace mana 100 (the bolt gives 6, the pulse costs 30, plus a slow regen).

## Boss mechanics: ground circles and other timed attacks

A boss phase can have **Mechanics**: attacks on a timer that run alongside its normal attack cycle. Each one has an **Ability** (one of the boss's skills), an **Interval** in seconds, a **First Delay** after the phase starts, a **Count** (how many at once, 1 to 4, never twice on the same player) and a **Target** rule:

- **Farthest:** the player farthest from the boss, which is how a boss reaches the ranged players.
- **Random:** any living player.
- **Not Top Threat:** anyone except the player the boss is mainly fighting (usually the tank).

On the ability, tick **At Target** to make its area centre on the chosen player's position at the moment it starts, instead of on the boss. The warning is drawn on the ground there for the whole windup, and the boss keeps fighting while it winds up, so the player's answer is to walk out of it. Make it red (**Avoidable** off) for the "get out" version. Up to four mechanics per phase; the ability has to be one of the boss's own attacks (put it in the class's **Skills**).

The seeded `slime_bomb` (a red 3.8-unit ground circle, 0.8 s warning) is used by all three bosses on the farthest player from the first phase (the mushroom warden every 4.5 s, the brood mother every 7 s, the slime king every 10 s), and again later: the brood mother also drops one on someone other than its main target, and the slime king adds a faster one from 65% health and a random one from 30%. Every number can be changed from the tuning file, for example `class.slime_boss.phases.1.mechanics.0.intervalSeconds = 5`.

## Boss recovery: a pause after every attack

A class has a **Recovery Seconds** field (0 by default). After one of its attacks starts, the enemy stays planted for the attack's windup plus this many seconds before it moves or attacks again. Timed mechanics (ground circles) keep running while it recovers. Bosses use it so they do not chase and swing without a break: there is a window to stand and hit, which is how an MMO boss feels. The tuning file can change it, for example `class.slime_boss.recoverySeconds = 1.5`.

## Red and orange attacks

Every attack warning is **orange** or **red**. An ability has an **Avoidable** tickbox (on by default):

- **Avoidable (orange):** block, a dodge's invulnerability and shields all work against it, and a block pressed just after it still counts.
- **Not avoidable (red):** it ignores block, dodge and shields, and a late block does not help. The only answer is to be somewhere else when it lands. It is drawn in red so players know to move.

The slime king's smash (the big circle around the boss) is the first red attack. A rule of thumb for fairness: attacks aimed at the people standing next to the boss are mostly orange (the tank has its answers), and the red ones should have a safe side (a cone, a line) or give enough time to step out and back without losing the rhythm.

## Worked example: give a dungeon a map (zones and gates)

A dungeon with **Zones** is one continuous map instead of a row of separate arenas. On the `DungeonAsset`:

- **Zones:** rectangles (a **Min** and a **Max** corner, in world units, Y up the screen). Everything walkable is inside a zone, and bodies stay about half a unit from the edge. The players start near (0, 0), so the first zone has to contain that.
- **Gates:** a rectangle that overlaps the end of two zones, like a corridor between them. It is a wall (drawn as a gold barrier) until the section named by **Open After** is cleared (0 is the first section, so the first gate opens when the first boss dies). A gate has to overlap two zones and open after a section that is not the last.
- **Rooms** become sections: each room has an **Offset** that is added to all its spawn positions, so each section's enemies are authored in their own coordinates and placed in their own zone. Spawns have to end up inside a zone (the loader checks).

How it plays: every section's enemies are in the map from the start, and the section is cleared when its boss dies (or when everything dies, if it has no boss). That opens the next gate. Nobody is moved or fully restored; downed players get back up with half health, whatever the boss summoned disappears, and the rest of the trash stays. Killing the last boss is the victory; if everyone is down it is a defeat, and Play again puts the whole cave back.

The server and every client use the same walls (`WorldMap`), so movement prediction agrees with the server. A client that does not share the server's map object (a joining phone) builds its own from the dungeon asset and learns which gates are open from the snapshot's section number. The client draws the floors, walls and barriers from the same data (`MapView`), copies the arena's trees and rocks along the walls (`MapScenery`), and shows the mini map (`MiniMap`). A dungeon with no zones still plays as before: a round arena, one room at a time.

A map has many more enemies in view than a round room did, so a snapshot may not fit in one network message (about 1,400 bytes). A connection that cannot carry it all gets every player and the nearest other entities, so far-away enemies appear as you approach them.

## Worked example: add a dungeon room

A dungeon is one `DungeonAsset` (**Assets > Create > Game > Dungeon**, in `Assets/Game/Data/Resources/Definitions`). Add a room to **Rooms**, then add spawns: a class (not a player class), a position inside the arena (radius 20) and an optional skin id such as `slime_green`. Rooms run in order. The loader checks that every room has enemies and every class exists. The arena loads the dungeon named in `ArenaBootstrap` (`slime_cave`); a dungeon picker comes with the hub.

On a spawn, tick **Boss** to make it a section boss: the section is cleared when every boss is dead, trash does not have to die, and whatever is left disappears when the last boss falls. A section with no boss spawn is cleared when all enemies are dead. Give spawns the same **Pack** number (1, 2, ...) to make a group that pulls together: when one of them aggros, the rest chase the same target. Pack 0 means alone. On the class asset, **Leash Range** (larger than the aggro range, 0 means none) makes a mob that is dragged that far from where it stood give up, walk home and heal to full. Keep bosses at 0.

Between rooms everyone is fully restored. That is a tuning choice, not a rule; changing it means editing `BeginRoom` in `GameServer`.

## Worked example: add a class

1. **Assets > Create > Game > Class**, save as `Assets/Game/Data/Resources/Definitions/ranger.asset`.
2. Set max health, move speed and power (tick **Playable** only for a weapon a player can pick). Set **Auto Attack** and up to three **Skills** from existing or new ability assets.
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

A weapon's skins are found by its **Skin Prefix** on the class asset (`tank` for the sword and shield, `healer` for the healing mace, `mage` for the staff, so skins are named `tank_steel`, `mage_wizard` and so on; the prefix defaults to the weapon id for new weapons), and skins with a prefab are listed before plain primitives, so a new `mage_` skin with a prefab becomes a choice with no other change.

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
2. Make a class (not **Playable**) with its health, move speed, **Aggro Range**, **Body Radius** and an auto attack. Add the attacks to **Skills**.
3. Add **Phases** on the class asset. Each phase has **Health Below** (a fraction, highest first), the **Attacks** it chooses from, an optional **Speed Scale**, and optionally **Spawn Class**, **Spawn Count** and **Spawn Skin** for minions that appear when the phase starts.
4. Put it in a dungeon room as a spawn.

The validator checks that phases are ordered and that every attack is one of the class's abilities.

## Where server rules live

`GameServer` owns the public API and the fixed tick, and calls focused systems (all in `Assets/Game/Core`, plain C#):

| System | Owns |
|---|---|
| `World` | the entities, spawning, nearest-hostile queries, body separation, the event buffers |
| `Session` | connections, command queues, sequence numbers, sending snapshots |
| `AbilityBook` | looking abilities up and applying a variant (the place a "based on" template would be resolved) |
| `Combat` | using abilities, windups, hit areas, damage, healing, dodge, block and shield |
| `NpcBrain` | enemy targeting, chasing, leash, pack calls, boss phases and minions |
| `DungeonDirector` | rooms, boss-gated clears, victory, defeat, restart, the respawn rule |
| `SnapshotBuilder` | turning the world into the snapshot sent to clients |

A new boss mechanic goes in `NpcBrain` (decisions) or `Combat` (what an ability does); a new dungeon rule goes in `DungeonDirector`. The tick order in `GameServer.StepTick` is part of the behaviour: commands, enemy decisions, dashes, separation, windups, timers and respawns, dungeon, snapshot.

## Tests to write for any addition

- Add the new numbers to a `Game.Core.Tests` case if they change rules (damage, cooldown, hit shape). Core tests run without Unity: `tools\test-core.ps1`.
- Run everything: `tools\run-tests.ps1` and `tools\run-tests.ps1 -Platform PlayMode`.
- Anything about feel, readability or controls: label the PR `needs-device-test` and say exactly what to try.

## Known limits of the foundation (for the review before M2)

These are decisions and gaps, not bugs. They are why the foundation is not frozen.

- Client and server tick clocks are not synchronised. No clock offset estimation yet; needed before two phones play together (M1.5).
- Move commands are one tick each, capped at 2 per tick. Reconciliation replays unacknowledged moves and drops ones older than 1 second.
- Hits are areas (line, circle or cone), instant or after a windup. No projectiles. A windup hit uses the position and direction from when the attack started.
- Enemy behaviour is basic: attack whoever has the most threat (or the nearest hostile in aggro range), use the attacks of the current phase, and obey a taunt. No pathfinding. Enemy decisions live in `NpcBrain`, ability rules in `Combat`, dungeon progression in `DungeonDirector`; `GameServer` only coordinates them (see below).
- Every snapshot lists the windups still going on (who, ability, origin, direction, time left), and the client draws telegraphs from that list. A late or joining client shows only the time left, and a warning the server stops sending disappears. Dodges and attack effects are still not predicted locally (issue 56).
- A dodge ignores Move commands while dashing, so the client briefly predicts movement the server discards. It snaps back on the next snapshot.
- Outside a dungeon dead players respawn after 5 seconds. Inside one they stay down until the next room or a restart.
- A dungeon is one arena with several waves (rooms); different layouts per room, doors and travel between rooms are not built.
- State uses floats. The server is authoritative so cross-machine determinism is not required; tests only prove same-seed repeatability on one machine.
- `WireCodec` allocates per message, so the combat allocation budget is not met.
- The networking library is not chosen; everything sits behind `ITransport`. Whether to move to Netcode for GameObjects is an open decision (issue 58).
- The player kits are named for their weapons (`sword_and_shield`, `healing_mace`, `staff`) and are marked **Playable**; there are no roles. They are still `ClassDef` assets; weapons gain skill trees next (issue 50). The skin names still use the old prefixes. Mobs and bosses keep class definitions.
- Definitions are copied, not derived: a variant of a monster is a duplicate asset. A "based on" link is planned (issue 50).
- AI-controlled party members do not exist yet; the intended hook is server-side controllers that emit the same commands as a phone.
- Definitions load from `Resources`, which is fine for the prototype. Revisit if content grows large.
