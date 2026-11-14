# Emberclad (SolveTheory Adventure)

A browser-first, single-player 2.5D action-adventure prototype built in Unity (2D URP). Inspired by Castle Crashers' art/combat and Elden Ring's atmosphere.

**Current scope:** a single hand-drawn forest level — responsive movement, a combo-driven combat system, a companion (Khalid the red panda), swarm-AI forest skeletons, several encounters, and a boss fight.

## Project layout

- `Assets/Scripts/` — gameplay code (`Core.cs`, `Actors.cs`, `Enemy.cs`, `Companion.cs`, `World.cs`, `Game.cs`, `Hud.cs`)
- `Assets/Editor/ArtImport.cs` — forces import settings for the procedurally generated art/audio
- `Assets/Resources/Art/` — sprite atlas + background textures (generated, see below)
- `Assets/Resources/Sfx/` — synthesized SFX/music (generated, see below)
- `AgentScripts/art/` — Python art/audio generation pipeline (PIL/numpy). `gen_chars.py`, `gen_fx.py`, `gen_props.py`, `gen_bg.py` draw the sprites; `pack.py` packs them into the runtime atlas; `gen_sfx.py` synthesizes all sound.
- `Packages/`, `ProjectSettings/` — standard Unity project config

`Library/`, `Temp/`, `Logs/`, `UserSettings/` are intentionally not tracked — Unity regenerates them on open.

## Opening the project

Open with Unity 6 (6000.6.9f1 or compatible), 2D URP template. The `ForestLevel` scene is the entry point (set in Build Settings).

## Regenerating art/audio

```
cd AgentScripts/art
python3 gen_chars.py && python3 gen_fx.py && python3 gen_props.py && python3 gen_bg.py && python3 pack.py
python3 gen_sfx.py
```

This rewrites `Assets/Resources/Art/atlas.png` + `atlas_map.txt` and `Assets/Resources/Sfx/*.wav`. Unity's `ArtImport.cs` postprocessor applies correct import settings automatically.
