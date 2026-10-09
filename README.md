# Los Santos Strike

All of GTA V's story mode and open world, played in first person, with Counter-Strike 2's movement, guns, spray recoil and gun sounds.

## What you get

- **First person only.** The camera stays in first person on foot and in every vehicle. The camera-switch button does nothing.
- **Counter-Strike movement.**
  - You run at Counter-Strike speeds, and how fast you can run depends on the gun in your hands (250 units/s with no gun, 215 with an AK-47, 200 with an AWP, 100 while scoped in).
  - Movement is quick and snappy: you speed up fast, stop fast, and can counter-strafe and air-strafe.
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

Everything else in GTA V stays the same: missions, cars, the map and the police. This is single player only, because mods don't run in GTA Online.

## You need

- **Grand Theft Auto V** for PC, story mode.
- **Counter-Strike 2**, installed through Steam. It's free, and the mod takes its gun sounds from your copy.
- **Script Hook V** and **ScriptHookVDotNet 3** in your GTA V folder. Melty installs these for you.

## Playing

1. Start GTA V's story mode.
2. The first time you play, the mod finds Counter-Strike 2 through Steam and copies its gun sounds to `%LOCALAPPDATA%\LosSantosStrike`. A notification tells you when they're loaded.
3. Press **F10** to turn the whole mod on or off.

All the settings are in `scripts/LosSantosStrike.ini`:

- first person
- movement
- sprint-walks
- gun stats
- recoil
- sounds, the suppressor and the volume
- the Counter-Strike 2 folder, if Steam can't find it

## Files

| In your GTA V folder | What it is |
|---|---|
| `scripts/LosSantosStrike.dll` | the mod |
| `scripts/LosSantosStrike.ini` | settings |
| `scripts/NAudio.dll` | sound playback (NAudio, MIT) |
| `scripts/LosSantosStrike/CsSoundImporter.exe` | reads the gun sounds from your Counter-Strike 2 install |

## Building

You need the .NET 10 SDK. Run `./build.sh`. It runs the tests, builds the script and the importer, and writes `dist/LosSantosStrike-<version>.zip`.

- `src/Shared`: movement, spray, gun table and sound matching. This is plain C#, and the unit tests in `tests/` cover it.
- `src/LosSantosStrike`: the ScriptHookVDotNet 3 script that runs inside GTA V (.NET Framework 4.8).
- `src/CsSoundImporter`: reads Counter-Strike 2's `pak01_dir.vpk` with ValveResourceFormat and writes the gun sounds as WAV files.

You can run the importer by hand:

```
CsSoundImporter.exe [--cs2 "<Counter-Strike 2 folder>"] [--force]
```

## Credits

See `packaging/THIRD-PARTY-NOTICES.txt` for the open-source libraries this mod uses.
