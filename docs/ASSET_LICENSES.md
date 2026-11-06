# Asset licenses

Every third-party asset goes here before it is used. Only assets with a clear commercial-use license are allowed (CC0 is safest). If the license is unclear, do not use the asset.

| Name | Source URL | License | Date added | Added by |
|---|---|---|---|---|
| Quaternius RPG Characters: Warrior, Monk, Cleric, Ranger, Wizard, Rogue (TEMPORARY placeholder) | https://quaternius.com/packs/rpgcharacters.html | CC0 1.0, stated in the download's License.txt (copy kept in `Assets/Game/Art/Temp/Characters`) | 2026-11-02 | Claude, files downloaded by Delan |
| Quaternius Cute Animated Monsters: GreenDemon, Ghost, Skull, Mushroom, YellowDragon, Cthulhu (TEMPORARY placeholder) | https://quaternius.com/packs/cutemonsters.html | CC0, stated on the pack page. The download has no License.txt, so the page is the record. | 2026-11-02 | Claude, files downloaded by Delan |
| Quaternius Stylized Nature MegaKit, free Standard version: trees, bushes, grass, flowers, rocks (TEMPORARY placeholder) | https://quaternius.com/packs/stylizednaturemegakit.html | CC0 1.0, stated in the download's License_Standard.txt (copy kept in `Assets/Game/Art/Temp/Nature`) | 2026-11-02 | Claude, files downloaded by Delan |

## Temporary art

Everything under `Assets/Game/Art/Temp` is placeholder art, chosen so the game has something to look at while the real look is decided. It is meant to be replaced.

- Characters and monsters are wired in as skins (`SkinAsset` prefabs with an Animator). Replacing one means pointing the skin at a new prefab; gameplay does not depend on these models, their proportions or their animation clips.
- Nature props are decoration placed by `Game > Seed everything`. Nothing depends on them.
- Slimes and the arena floor are still plain primitives.
- The originals stay in the git-ignored `_art-incoming` folder, outside the build.

