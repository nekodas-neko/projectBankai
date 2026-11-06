# Tuning numbers without a rebuild

Every ability and class number is data, but changing an asset needs a rebuild and an install. For balance work there is a shortcut: a plain text file that overrides any number when a **development build** or the editor starts.

## On a phone

1. Copy `docs/tuning.example.txt` to `tuning.txt` in the repo root (it is ignored by git), and edit it.
2. Plug the phone in and run `tools\tune.ps1`. It pushes the file to the game's folder and restarts the game.
3. The build label in the bottom-left corner shows `tuning: N overrides` when the file is applied, or `tuning IGNORED: ...` with the first problem when it is not.
4. `tools\tune.ps1 -Clear` removes the file and restarts; `tools\tune.ps1 -Show` prints what is on the phone.

In the editor the game also reads `tuning.txt` from the project root.

## The format

```
# comments start with a hash
ability.king_smash.damage = 26
class.staff.stats.maxHealth = 80
ability.fireball.variants.frost.damageScale = 0.9
class.slime_boss.phases.2.speedScale = 1.4
```

- A line is `ability.<id>.<setting>` or `class.<id>.<setting>`, an equals sign and a value. Letter case does not matter for setting names.
- Settings are the property names of the definitions (`Damage`, `Windup`, `ResourceCost`, `ChargeFromDealt`, `Stats.MaxHealth`, `Resource.Regen` and so on; see `Definitions.cs`).
- A setting that leads to a list takes the next part as the entry's id (a variant) or its position (a boss phase, counting from 0).
- Values are numbers, `true` or `false`, or the name of an option (`ability.x.shape = cone`). Decimals always use a dot.
- The id of a definition cannot be changed.

## Safety

- The file is read only in development builds and the editor; a release build ignores it.
- The numbers go through the same validator as the shipped ones. If any line is wrong or breaks a rule, the **whole file is ignored**, the problem is logged and shown on the build label, and the game runs with the shipped numbers.
- The server decides everything that matters, so when two phones play together the host's numbers are the ones that count. Use the same file on both so the buttons and the warnings agree.

## Checking balance with the bots

`toolsalance-report.ps1` (needs Unity in batch mode, so close the editor) plays every weapon against every boss with bots and prints a table: who wins, how long it takes, health left, damage taken and dealt, and how many ultimates were fired. It uses the shipped numbers plus `tuning.txt`, so the loop for tuning is: edit `tuning.txt`, run the report, read the table, repeat, and when it looks right copy the numbers into the assets (and the matching lines in `SeedContent.cs`).

The bots are perfect solo players, so real fights will be longer and harder; read the table as weapon against weapon. Each weapon is judged on the way it is meant to be played: the sword and shield on **stand and block**, the staff and the healing mace on **kite**. `stand still` is a control.

The report ends with the **targets missed** (the goals live in `ShippedBalanceTests`, so change them there):

- a boss fight should last at least about 30 s (mushroom warden), 60 s (brood mother) and 90 s (slime king) for a perfect solo bot, and no more than 180 s;
- no more than 1.5 ultimates a minute;
- played the intended way, a weapon should finish with at least 30% health.

Two principles are also tests, so tuning cannot break them: a ranged weapon takes less damage kiting than standing still, and blocking cuts the tank's damage rate.
