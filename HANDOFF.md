# CS:GTA: where things stand

This is for the Claude session that continues on the player's Windows PC.

## Agreed design (don't re-ask)

- **What it is:** GTA V Enhanced story mode, fully playable, locked to first person.
- **Counter-Strike movement:** uncapped bunny hopping, and the sprint key walks.
- **Counter-Strike 2 guns:** damage, fire rate, magazine size and reload time, on 21 GTA guns.
- **Spray recoil:** Counter-Strike-style patterns.
- **Counter-Strike 2 gun sounds:** read from the player's own Counter-Strike 2.
- **Solo only.** The mod must never run in GTA Online, and it must never touch, disable or hide from BattlEye.

## State

- The plugin is `asi/`, a C++17 ASI loaded by Ultimate ASI Loader, which Melty installs automatically. Logic tests pass with `./asi/build.sh test`.
- The sound importer is `src/CsSoundImporter`. Melty runs it before the first start (see `packaging/recipe.json`).
- **The Melty draft listing exists:** modId `0512af70-3c3b-40ac-8249-0f52b2521431`, slug `cs-gta`, linked to this repo. Studio: https://melty.gg/studio/0512af70-3c3b-40ac-8249-0f52b2521431
- `packaging/recipe.json` passes validate_recipe and one_click_check (yes).

## Still to do

1. **Implement `asi/src/game_bridge_enhanced.cpp`** against the installed GTA V Enhanced:
   - find the native registration table and resolve documented native hashes to handlers;
   - run `mod::Tick` once per frame on the game's script thread;
   - pin the game build in the recipe (`games[0].version`).
2. **Build `CSGTA.asi`** (MinGW-w64 or MSVC) and install it through Melty or by hand. Then play-test everything listed above, and check `%LOCALAPPDATA%\CSGTA\CSGTA.log`.
3. **Take a real in-game screenshot or clip** showing first person and Counter-Strike behaviour. Show only the game window.
4. **Get the license, remix choice and credit name** from the user. They haven't answered yet.
5. **Upload and publish:**
   - `./build.sh`
   - start_upload, PUT, finish_upload
   - submit_release with `packaging/recipe.json`
   - add_screenshot
   - show the user a summary and ask before publish
   - after the user agrees, save the recipe as `melty.json` at the repo root

The Melty token is never stored in this repo. Get a fresh "Publish" prompt from Melty Studio to connect.
