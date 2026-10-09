# CS:GTA

All of GTA V's story mode and open world, played in first person, with Counter-Strike 2's movement, guns, spray recoil and gun sounds.

## What you get

- **First person only.** The camera stays in first person on foot and in every vehicle. The camera-switch button does nothing.
- **Counter-Strike movement.**
  - You run at Counter-Strike speeds, and how fast you can run depends on the gun in your hands (250 units/s with no gun, 215 with an AK-47, 200 with an AWP, 100 while scoped in).
  - Movement is quick and snappy: you speed up fast, stop fast, and can counter-strafe and air-strafe.
  - Bunny hopping has no speed cap. Air-strafe to build speed, then jump again right as you land to keep it.
  - The sprint key walks quietly at 52% speed, like Shift in Counter-Strike. Crouched (stealth) movement is 34% speed.
- **Counter-Strike guns.** GTA's guns carry Counter-Strike 2's numbers: damage per bullet, fire rate, magazine size and reload time.

  | Counter-Strike gun | GTA V gun |
  |---|---|
  | Glock-18 | Pistol |
  | USP-S | Combat Pistol |
  | P250 | SNS Pistol |
  | Five-SeveN | Heavy Pistol |
  | Desert Eagle | Pistol .50 |
  | Tec-9 | Machine Pistol |
  | MAC-10 | Micro SMG |
  | MP5-SD | SMG |
  | P90 | Assault SMG |
  | AK-47 | Assault Rifle |
  | M4A4 | Carbine Rifle |
  | M4A1-S | Special Carbine |
  | SG 553 | Advanced Rifle |
  | FAMAS | Bullpup Rifle |
  | Galil AR | Compact Rifle |
  | AWP | Heavy Sniper |
  | SSG 08 | Sniper Rifle |
  | Nova | Pump Shotgun |
  | XM1014 | Assault Shotgun |
  | Negev | MG |
  | M249 | Combat MG |

- **Spray recoil.** Holding the trigger walks your aim along a Counter-Strike-style spray pattern, and you learn to pull against it.
  - The AK-47 climbs, then swings left, then right.
  - Moving, jumping, or firing an unscoped sniper rifle throws your shots off.
  - When you let go, your aim settles back.
- **Counter-Strike 2 gun sounds.** Shots and reloads use Counter-Strike 2's own sounds, read from **your** Counter-Strike 2 install the first time you play. No Counter-Strike files ship with this mod. Where a GTA gun takes a suppressor, one is fitted so GTA's own shot doesn't drown out the Counter-Strike one. You can turn that off in the settings.

Everything else in GTA V stays the same: missions, cars, the map and the police. This is single player, story mode only: CS:GTA switches itself off whenever GTA Online is running. It never touches BattlEye.

## You need

- **Grand Theft Auto V Enhanced** for PC, story mode.
- **Counter-Strike 2**, installed through Steam. It's free, and the mod takes its gun sounds from your copy.
- **Ultimate ASI Loader**, which loads the mod into GTA V. Melty installs it for you.

## Playing

1. Start GTA V's story mode.
2. Before the first start, Melty runs the sound importer, which copies Counter-Strike 2's gun sounds from your copy to `%LOCALAPPDATA%\CSGTA`. A notification in game says how many guns have Counter-Strike sounds.
3. Press **F10** to turn the whole mod on or off.

All the settings are in `CSGTA/CSGTA.ini` in your GTA V folder:

- first person
- movement
- sprint-walks
- gun stats
- recoil
- sounds, the suppressor and the volume
- the Counter-Strike 2 folder, if Steam can't find it

## Files

| Where | What it is |
|---|---|
| `CSGTA.asi` in the GTA V folder | the mod, loaded by Ultimate ASI Loader |
| `CSGTA/CSGTA.ini` in the GTA V folder | settings |
| `%LOCALAPPDATA%\CSGTA\bin\CsSoundImporter.exe` | reads the gun sounds from your Counter-Strike 2 install |
| `%LOCALAPPDATA%\CSGTA` | imported sounds and `CSGTA.log` |

## Building

You need MinGW-w64 (`x86_64-w64-mingw32-g++-posix`) and the .NET 10 SDK. Run `./build.sh`. It runs the tests, builds the plugin and the importer, and writes `dist/CSGTA-<version>.zip`.

- `asi/src`: the plugin (C++17).
  - `cs_logic.*` holds the movement, spray and gun rules. The tests in `asi/tests` cover it.
  - `mod.cpp` runs every frame.
  - `audio.cpp` plays the sounds.
  - `game_bridge_enhanced.cpp` is the connection to GTA V Enhanced's native functions.
- `src/CsSoundImporter` and `src/Shared`: the C# tool that reads Counter-Strike 2's `pak01_dir.vpk` with ValveResourceFormat and writes the gun sounds as WAV files. The tests in `tests/` cover its sound matching.

You can run the importer by hand:

```
CsSoundImporter.exe [--cs2 "<Counter-Strike 2 folder>"] [--force]
```

## Credits

See `packaging/THIRD-PARTY-NOTICES.txt` for the open-source libraries this mod uses.
