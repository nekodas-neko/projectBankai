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

The everyday ground circle is the orange `slime_spit` (4.6-unit circle, 1.2 s warning, 18 damage; block, dodge or walk out). It is deliberately infrequent so ranged players are not on the move all the time: the mushroom warden drops one on its farthest player every 8 s, the brood mother every 10 s, and the slime king every 14 s, then every 12 s, then every 10 s plus a random one every 16 s in its later phases. Red attacks are rarer still and only ever aimed at players who can walk: the brood mother's red `slime_bomb` (every 18 s from its second phase). The slime king's big smash (cooldown 14 s) is orange, so a tank holding its ground can block it. Every number can be changed from the tuning file, for example `class.slime_boss.phases.1.mechanics.0.intervalSeconds = 5`.

## Boss recovery: a pause after every attack

A class has a **Recovery Seconds** field (0 by default). After one of its attacks starts, the enemy stays planted for the attack's windup plus this many seconds before it moves or attacks again. Timed mechanics (ground circles) keep running while it recovers. Bosses use it so they do not chase and swing without a break: there is a window to stand and hit, which is how an MMO boss feels. The tuning file can change it, for example `class.slime_boss.recoverySeconds = 1.5`.

## Casts, interrupts and adds

Three fields on an ability make a classic boss moment:

- **Interruptible** marks the ability as a *cast*. While it winds up the caster stands still (so it cannot be a ground circle) and the client can show a cast bar. Give it a windup of a second or more, a small **Radius** (0.01) when it should not hurt anyone, and put it on the boss as a phase **mechanic** (a timer), which fires it whether or not anyone is in reach. A heal cast is just **Heal** on that ability; the healing area includes the boss itself.
- **Interrupt** (seconds) goes on the *player's* ability. When it hits an enemy that is winding up an interruptible cast, the cast is cancelled (it does nothing) and the enemy is **Staggered** for that long: it cannot move, attack, cast or fire mechanics. The hit sends an `Interrupted` event. An interrupt on a hit that is not catching a cast does nothing extra.
- **Summon Class / Summon Count / Summon Skin** make the ability add that many of a class around the caster when it resolves. The adds are fixed on the *farthest* player (the ranged or healing one) for 12 seconds, which is what makes the tank use a taunt. A taunt sets the forced target and pulls them. Adds count as summoned: they are removed when the boss dies, and they get the party-size health scale like everything else.

So an interruptible summon is "interrupt the cast or deal with the adds", and an interruptible heal is "interrupt it or the fight gets longer". The validator checks a cast has a windup of at least half a second and is not a ground circle, that an interrupt is 0 to 10 seconds, and that a summon names a real class.

### The shipped casts

The mushroom warden casts **Devour** (2.2 s, every 18 s from 10 s in): if nobody stops it, it heals 140 health. The brood mother casts **Brood call** (2.5 s, every 22 s): three slimes appear and go for the farthest player. Both show a blue cast bar under the boss frame counting down; a stagger shows as "Boss - staggered!". **Shield bash** (2 s stagger) and **fireball** (1.5 s) are the interrupts; the healing mace has none, so a mace player lets the tank or the mage stop casts, and a taunt pulls the adds. The balance sim's bots save an interrupt skill for a cast when the boss has any, and its table has an "ints" column (casts stopped) so you can see who is stopping what.

The slime king has no free skill slot (a class has three), so its cast is a swap: its cone attack is **King cleave**, a 2 s charge-up (24 damage, every 15 s, orange) that can be interrupted. Stopping it staggers the king; otherwise block it or leave the cone. A cast does not have to be harmless, any attack with a windup can be marked interruptible.

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

**Shaping the map.** Zones are only rectangles, but they can overlap, so a dungeon does not have to be a straight line. A narrow zone that overlaps the end of two bigger ones is a passage; a zone that hangs off the side of a path is a side cave; turning a corner is two zones that overlap at the bend. The seeded cave winds north to the warden, east and north to the brood mother, then west and north to the king, with an optional cave off each side of the first room and a nook in the brood tunnels. Keep side caves from touching the next section's zones (even a 2 x 2 corner overlap is a shortcut around a gate). `CaveLayoutTests` walks the shipped map and fails if a pack cannot be reached or something can be reached around a closed gate, so run it after reshaping a map. Trash in a section is optional: only the boss ends the section.

A map has many more enemies in view than a round room did, so a snapshot may not fit in one network message (about 1,400 bytes). A connection that cannot carry it all gets every player and the nearest other entities, so far-away enemies appear as you approach them.

## Map-wide attacks and shelter patches

An environment puzzle is an attack the whole room has to answer by going somewhere. Two pieces of data make one:

- **Everyone** (on the ability): the attack hits every hostile in the room wherever it stands, shape and range ignored. Give it a windup of at least 0.5 s so the room can react, and Avoidable off (red) so a block or a dodge does not answer it; the answer is the shelter. It runs as a boss **mechanic** (a timer on a phase), like a ground circle.
- **Shelter tag and reduction** (on the ability): damage is cut by the reduction (0 to 1) for a target standing on a map **patch** with that tag. A patch is a rectangle on the dungeon (`DungeonDef.Patches`: a name, a tag, a Min and a Max corner). Patches never block movement, and their middle has to be inside a zone.

So a fire wave is an ability with Everyone on, shelter tag `water` and reduction 0.8, and the dungeon has a few `water` patches in that boss's room. Several tags can exist in one dungeon (water, cover); each ability names the one it cares about. The client draws patches from the same data, so players can see where to stand (`ShelterTests` shows the rules).

## Party size: enemies get tougher with each player

A dungeon has **Health Per Extra Player** (0 means no scaling; the cave uses 0.8). Every enemy's health is multiplied by 1 + that number x (players - 1): a solo player meets the normal health, two players 1.8 times, three players 2.6 times. It follows the party while the dungeon runs: someone joining or leaving changes the maximum, and an enemy that was hurt keeps the same share of its health left; anything spawned later (a boss's minions) is scaled too. The design target is three players, tank, damage and healer, with solo and duo possible for a skilled or well-geared player, so the number is a tuning knob: if three players finish a boss too fast, raise it.

## The local profile and weapon mastery

The character's own progress lives in a `PlayerProfile` (`Game.Core`): the weapons it owns (`OwnedWeapon`: a unique id, a weapon type, a power scale and at most one modifier), which one is equipped, and a `WeaponMastery` per weapon **type**. Mastery belongs to the character, so two staffs share one XP bar. `ProfileStore` (`Game.Client`) keeps it in `profile.txt` under the device's data folder as a small text file (`ProfileText`); no network and no accounts. A file this build cannot read is moved to `profile.damaged.txt` and a new character starts, so a bad file never overwrites progress. Lines a newer build adds are skipped, and a file from a newer version is refused rather than misread.

How mastery is earned is data: the `mastery` asset (`MasteryAsset`, in `Assets/Game/Data/Resources/Definitions`) lists the XP each of the 20 levels costs, the points a level gives (1) and what a kill (10) and a boss kill (150) are worth. `WeaponMastery.AddXp` fills the bar, `TryChoose` spends points on a tree node (the skill trees are not built yet, so a node is just an id), and `Reset` gives every point back for free.

Kills train the weapon in hand. `KillTracker` watches the snapshots: an enemy that shows up with no health is a kill (a boss when it carries the boss flag), counted once, and enemies already down when the client joins are not. Enemies a boss summoned (the `Summoned` flag) are skipped, so a summoning boss cannot be farmed. A fallen enemy stays in the snapshot, so a lost packet loses nothing. `MasteryProgress` turns kills into XP for the equipped weapon type, saves the profile at once on a level-up or boss kill and otherwise every 20 seconds, and when the game is paused or closed. The test overlay shows the weapon's mastery level and bar above the build number. Every player's own client counts its own kills, so each profile fills in co-op.

### Weapon power

An owned weapon has a power scale (`OwnedWeapon.Power`, 1.0 for the starter weapons). It rides in the join message as thousandths (protocol version 10; the class hash made it 11) and `GameServer.AddPlayer` multiplies the player's `Stats.Power` by it, which scales damage, healing and shield strength. The server clamps it to 0.8 to 1.3 (`MinWeaponPower`, `MaxWeaponPower`), so a client cannot claim more than a drop can roll. The scale is fixed for the whole session, like the variant loadout.

### Boss loot and the weapon bag

Every boss that falls gives each player a drop of their own (no rolling against each other). `LootTableDef` lists the weapon types a drop can be and the power range (0.95 to 1.15 for the shipped `boss_drop` asset in `Definitions/Loot`; the validator keeps it inside what the server accepts). `LootRoller.Roll` picks a type and a power from a seeded random, and `MasteryProgress` adds the weapon to the profile, saves at once and shows `Dropped: Staff +8%` for five seconds. The roll number is saved with the profile (`loot` line) so every weapon id is unique. The bag holds 30 weapons; a drop that would not fit is lost and says so. Which table an enemy rolls on is data on its class (see the next section). A drop can also carry a modifier and a set of armor (next section).

The **Character** screen (the Menu button, then Character) shows the bag: five weapons or armor sets a page, tap one to carry it. The session restarts because the kit, skills and numbers are sent when it begins. Mastery and the skill tree belong to the weapon **type**, so a dropped staff uses the staff's points. Choosing a weapon type through `HudSettings.ClassName` keeps the weapon already in hand if it is that type, otherwise takes the strongest one owned.

### Gear: modifiers, armor, gold and the Character screen

A weapon can carry one **modifier** and a character wears one **armor set**. Both are data with scales on power, health and speed (kept inside 0.8 to 1.3): `WeaponModifierDef` assets in `Definitions/Modifiers` (Sturdy +10% health, Swift +10% speed, Keen +5% power) and `ArmorSetDef` assets in `Definitions/Armor` (Wanderer, the starter set that gives nothing, Stone Warden, Ember Guard, Moss Cloak; the `Look` field is reserved for outfit art). To add one, copy an asset, change the numbers and list its name in a loot table (`Modifiers` and `ModifierChance`, `ArmorSets` and `ArmorChance`; boss drops carry a modifier half the time and an armor set 40% of the time). `Gear.Of(weapon, armor, modifiers, sets)` multiplies the weapon power, the modifier and the armor into three numbers. They are sent when joining next to each other (protocol 15: power, health scale, speed scale) and the server clamps each to 0.8 to 1.3, so a client cannot claim more than a drop can give; the client's own movement prediction uses the same speed.

**Gold** is a number in the profile (`gold N`, capped at 999,999) that kills, chests and taking things apart add to. Every enemy class has `GoldReward`. Nothing is sold for it yet; it is the currency for the screens that come next.

The **Character** screen (`CharacterScreen`, built from `CharacterInfo`) shows the weapon in hand with its mastery, the armor worn, the health, speed and power they add up to, gold and potions, and the four skills (with any skill tree variant). Two tabs list the weapon bag and the armor bag. Tap a row to carry it (the session restarts); the right button takes it apart for gold (`PlayerProfile.DismantleValue`: more for a stronger weapon and a modifier, 20 for armor). The weapon in hand, the worn set and the last weapon of a type cannot be taken apart.

### The home screen

The game starts on the **Home** scene (`HomeBootstrap`, created by `Game > Seed home scene`, first in the build). `HomeScreen` shows the character card (the same text as the Character screen: weapon, armor, numbers, gold), a button to open the Character screen (equipping or taking things apart here changes nothing running, so there is no restart) and the game modes. `HomeScreen.Modes` is the list: only `dungeon` is available, and it loads the Arena scene in solo mode (host or join from Menu, Session once inside). Arena, Idle grind and Tower are disabled "coming soon" entries. To add a mode, add an entry and handle its id in `HomeBootstrap.Play`. In the dungeon, Menu, Home saves the profile and returns here.

### Skill trees

A weapon type has a `SkillTreeDef` (its id is the weapon type). Each `SkillNodeDef` has an id, a name, a **slot** (0 auto attack, 1 skill, 2 ultimate, 4 defence), the **variant** it switches that slot's ability to, a point **cost** and an optional node it **requires**. A slot holds one choice: taking another node in the same slot swaps it and gives the old cost back (refused when another chosen node requires the one being replaced). `SkillTreeRules.TryChoose` does this, `SkillTreeRules.Loadout` turns the choices into the variant ids the join message already carries, and `Sanitize` drops choices a changed tree no longer has. `SkillTreeValidator` refuses a tree for a weapon that is not playable, a slot with no skill, a missing variant, a bad cost, or requirements that loop or point at the same slot. Because a node is "this ability's variant", a new choice is data: add a variant to the ability, then a node that points at it.

The shipped trees are `SkillTreeAsset`s in `Assets/Game/Data/Resources/Definitions/Trees`, one per weapon, named after the weapon type (at most six nodes: the panel shows six rows). To add a choice: add a variant to the ability asset (a variant with no effects of its own is numbers only and borrows the base skill's look), then add a node in the weapon's tree asset. `SkillTreeCatalog` validates every tree at start-up and ignores them all, with an error in the log, if one is wrong. The **Skills** button opens `SkillTreePanel` for the equipped weapon: tap a node to take it, **Reset points** gives everything back. The variants are sent when a session starts, so a change restarts the session (weapons will only change in the hub later). The old Style test button is gone; the tree replaces it.

### Enemy rewards: the template for a mob or a boss

What a kill is worth is three fields on the enemy's class asset, so a new enemy never needs code:

| Field | Meaning | Mob (`slime`) | Boss (`slime_boss`) |
| --- | --- | --- | --- |
| **Xp Reward** | mastery XP for the weapon in hand; 0 = the default in the `mastery` asset (10 for a mob, 150 for a boss) | 10 | 150 |
| **Loot Table** | id of a loot table asset in `Definitions/Loot` | `mob_drop` | `boss_drop` |
| **Drop Chance** | 0 to 1, rolled separately for every player | 0.03 | 1 |

Every snapshot entity carries `ClassHash` (the `StableId` of its class id), so each client looks up what the dead enemy was worth with `RewardBook` and applies it to its own profile. An enemy the client does not know gives the default XP and no drop. A boss whose class names no table falls back to `boss_drop`. A weapon class cannot carry a reward (the validator refuses it), and a drop chance needs a table.

To add a mob or a boss with rewards: copy `slime` (or `slime_boss`), change the numbers, and set the three fields above. To add a new kind of drop (an elite, a dungeon's own boss weapons), copy `mob_drop` or `boss_drop` in `Definitions/Loot`, rename it, change the weapon types and the power range (inside 0.8 to 1.3, which the validator enforces), and point the class at it. The test `EveryShippedEnemyHasARewardAndItsTableExists` fails when an enemy class has no XP value of its own or names a table that does not exist.

Not built yet: the equipped weapon still comes from the Weapon test button (the profile follows it), modifiers on dropped weapons, and a drop that depends on the dungeon or the party size.

## The run summary

`FightStats` (`Game.Client`) adds up the damage events the client already receives: damage dealt to enemies, healing, damage taken, and how many hits each player blocked or dodged and how many casts they stopped. Victory and Defeat show it under the Play again button, best damage first, with the run time (the clock only runs while the dungeon is fighting or between bosses, and a new run starts a fresh count). It is each client's own view: a hit that the client never saw because it was cut from its snapshot is not counted, so the numbers are close but not exact. Use it to compare weapons and to see who really took the hits.

## Aggro rings

An enemy with an **Aggro Range** that has not noticed anyone yet is drawn with a thin yellow ring on the ground at that distance (`AggroRings`): step inside it and the enemy, and its pack, come for you. The ring disappears once the enemy has a target. The server sends the range in the snapshot (`EntityState.AggroRange`), so any class with an aggro range gets a ring without code; rings only draw within 10 units of the ring's edge so a big map stays readable.

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
