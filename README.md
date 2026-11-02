# projectBankai

Working title. A portrait-mode mobile game that keeps the MMO core (tank, DPS and healer roles, telegraphed boss fights, dungeons) but plays with one thumb on a phone.

First target: two players clearing a small dungeon together. Built client/server from the start, so solo, 2-player and 4-player play share one code path.

## Status
Early setup. Nothing playable yet.

## Requirements
- Unity 6000.3.27f2 (6.3 LTS) with Android Build Support
- Git LFS (`git lfs install` after cloning)
- Android phone with USB debugging

## Setup
```bash
git clone https://github.com/nekodas-neko/projectBankai.git
cd projectBankai
git lfs install
```

Open the folder in Unity Hub with the editor version above.

Optional: register Unity's merge tool so scene and prefab merges work.
```bash
git config merge.unityyamlmerge.name "Unity SmartMerge"
git config merge.unityyamlmerge.driver "'<Unity install>/Editor/Data/Tools/UnityYAMLMerge' merge -h -p --force --fallback none %O %B %A %A"
```

## Contributing
Read `CLAUDE.md` for the architecture rules and branch workflow. Work goes through issues and PRs into `dev`.
