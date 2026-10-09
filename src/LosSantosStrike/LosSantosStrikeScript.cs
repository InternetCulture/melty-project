using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using GTA;
using GTA.Math;
using GTA.Native;

namespace LosSantosStrike
{
    /// <summary>
    /// GTA V story mode, always in first person, with Counter-Strike 2 movement, guns, spray
    /// recoil and (from the player's own CS2 install) gun sounds.
    /// </summary>
    public class LosSantosStrikeScript : Script
    {
        private const string ModName = "Los Santos Strike";

        // Settings (LosSantosStrike.ini next to the script)
        private bool _forceFirstPerson, _csMovement, _shiftToWalk, _csGuns, _csRecoil, _csSounds, _quietGtaGunfire;
        private float _volume;
        private Keys _toggleKey;
        private string _cs2Folder;

        private readonly CsMovement _move = new CsMovement();
        private readonly SprayRecoil _recoil = new SprayRecoil();
        private readonly SoundEngine _sounds = new SoundEngine();
        private readonly Random _rng = new Random();
        private readonly string _dataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LosSantosStrike");
        private string _logPath;

        private bool _enabled = true;
        private float _time;

        // movement
        private V2 _vel;
        private bool _controlling;
        private float _speed;

        // guns
        private uint _weaponHash;
        private int _lastClip = -1;
        private float _lastShot = -100f, _lastClipDrop = -100f;
        private bool _wasReloading, _wasShooting, _clipInPlayed;
        private float _reloadStart = -100f, _lastForcedReload = -100f;
        private CsWeapon _reloadWeapon;
        private readonly Dictionary<uint, int> _shotsSinceReload = new Dictionary<uint, int>();
        private readonly Dictionary<uint, float> _baseDamage = new Dictionary<uint, float>();
        private float _nextDamageRefresh;
        private readonly List<KeyValuePair<uint, uint>> _addedSuppressors = new List<KeyValuePair<uint, uint>>();

        private volatile string _notice;
        private static readonly uint Unarmed = Joaat.Hash("WEAPON_UNARMED");
        private static readonly uint[] Suppressors =
        {
            Joaat.Hash("COMPONENT_AT_PI_SUPP_02"), Joaat.Hash("COMPONENT_AT_PI_SUPP"),
            Joaat.Hash("COMPONENT_AT_AR_SUPP_02"), Joaat.Hash("COMPONENT_AT_AR_SUPP"),
            Joaat.Hash("COMPONENT_AT_SR_SUPP"),
        };

        public LosSantosStrikeScript()
        {
            _logPath = Path.Combine(BaseDirectory ?? ".", "LosSantosStrike.log");
            LoadSettings();
            Interval = 0;
            Tick += OnTick;
            KeyDown += OnKeyDown;
            Aborted += OnAborted;
            Log("Started.");

            if (_csSounds)
            {
                try { _sounds.Start(_volume); }
                catch (Exception e) { Log("Audio output failed: " + e.Message); }
                Task.Run(() => ImportAndLoadSounds());
            }
        }

        private void LoadSettings()
        {
            var s = Settings;
            _forceFirstPerson = s.GetValue("Camera", "ForceFirstPerson", true);
            _csMovement = s.GetValue("Movement", "CounterStrikeMovement", true);
            _shiftToWalk = s.GetValue("Movement", "SprintKeyWalks", true);
            _csGuns = s.GetValue("Guns", "CounterStrikeGuns", true);
            _csRecoil = s.GetValue("Guns", "SprayRecoil", true);
            _csSounds = s.GetValue("Sounds", "CounterStrikeSounds", true);
            _quietGtaGunfire = s.GetValue("Sounds", "QuietGtaGunfire", true);
            _volume = Math.Max(0f, Math.Min(2f, s.GetValue("Sounds", "Volume", 0.8f)));
            _cs2Folder = s.GetValue("Sounds", "CounterStrike2Folder", "");
            _toggleKey = s.GetValue("General", "ToggleKey", Keys.F10);
        }

        // ---------------------------------------------------------------- sounds

        private void ImportAndLoadSounds()
        {
            try
            {
                string exe = Path.Combine(BaseDirectory, "LosSantosStrike", "CsSoundImporter.exe");
                if (File.Exists(exe))
                {
                    string args = "--auto";
                    if (!string.IsNullOrWhiteSpace(_cs2Folder)) args += " --cs2 \"" + _cs2Folder.Trim().Trim('"') + "\"";
                    var psi = new ProcessStartInfo(exe, args)
                    {
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        WindowStyle = ProcessWindowStyle.Hidden,
                        WorkingDirectory = Path.GetDirectoryName(exe),
                    };
                    using (var p = Process.Start(psi))
                    {
                        if (p != null && !p.WaitForExit(180000)) { try { p.Kill(); } catch { } }
                        Log("Sound importer exited with " + (p != null && p.HasExited ? p.ExitCode.ToString() : "timeout"));
                    }
                }
                else
                {
                    Log("Sound importer not found at " + exe);
                }

                int guns = _sounds.Load(Path.Combine(_dataDir, "sounds"), Log);
                Log("Loaded Counter-Strike 2 sounds for " + guns + " guns.");
                string err = Path.Combine(_dataDir, "import.err");
                if (guns > 0)
                    _notice = "~g~" + ModName + "~s~: Counter-Strike 2 sounds loaded for " + guns + " guns.";
                else if (File.Exists(err))
                    _notice = "~y~" + ModName + "~s~: " + File.ReadAllText(err).Trim() + " GTA gun sounds stay on.";
                else
                    _notice = "~y~" + ModName + "~s~: no Counter-Strike 2 sounds found. GTA gun sounds stay on.";
            }
            catch (Exception e)
            {
                Log("Sound import failed: " + e);
                _notice = "~y~" + ModName + "~s~: Counter-Strike 2 sounds could not be loaded.";
            }
        }

        // ---------------------------------------------------------------- main loop

        private void OnTick(object sender, EventArgs e)
        {
            float dt = Math.Min(0.1f, Math.Max(0f, Game.LastFrameTime));
            _time += dt;

            var notice = _notice;
            if (notice != null) { _notice = null; GTA.UI.Notification.Show(notice); }
            if (!_enabled) return;

            Ped ped = Game.Player.Character;
            if (ped == null || !ped.Exists()) return;

            bool active = PlayerHasControl();
            if (_forceFirstPerson && active) ForceFirstPerson(ped);

            uint weapon = (uint)ped.Weapons.Current.Hash;
            CsWeapon w = _csGuns ? CsWeapons.ByGtaHash(weapon) : null;

            if (_csGuns)
            {
                RefreshDamage();
                HandleGun(ped, w, weapon, dt);
            }

            if (_csMovement) HandleMovement(ped, w, weapon, active, dt);
        }

        private bool PlayerHasControl()
        {
            if (Game.IsPaused || Game.IsLoading) return false;
            if (Function.Call<bool>(N.IS_PAUSE_MENU_ACTIVE)) return false;
            if (Function.Call<bool>(N.IS_CUTSCENE_PLAYING)) return false;
            if (Function.Call<bool>(N.IS_PLAYER_SWITCH_IN_PROGRESS)) return false;
            if (!Function.Call<bool>(N.IS_PLAYER_CONTROL_ON, Game.Player)) return false;
            return true;
        }

        // ---------------------------------------------------------------- camera

        private void ForceFirstPerson(Ped ped)
        {
            Game.DisableControlThisFrame(GTA.Control.NextCamera);
            Game.DisableControlThisFrame(GTA.Control.VehicleCinCam);
            Function.Call(N.SET_CINEMATIC_BUTTON_ACTIVE, false);

            if (Function.Call<bool>(N.IS_PED_IN_ANY_VEHICLE, ped, false))
            {
                int context = Function.Call<int>(N.GET_CAM_ACTIVE_VIEW_MODE_CONTEXT);
                if (Function.Call<int>(N.GET_CAM_VIEW_MODE_FOR_CONTEXT, context) != N.CamViewFirstPerson)
                    Function.Call(N.SET_CAM_VIEW_MODE_FOR_CONTEXT, context, N.CamViewFirstPerson);
            }
            else if (Function.Call<int>(N.GET_FOLLOW_PED_CAM_VIEW_MODE) != N.CamViewFirstPerson)
            {
                Function.Call(N.SET_FOLLOW_PED_CAM_VIEW_MODE, N.CamViewFirstPerson);
            }
        }

        // ---------------------------------------------------------------- movement

        private bool CanCsMove(Ped ped)
        {
            if (!Function.Call<bool>(N.IS_PED_ON_FOOT, ped)) return false;
            if (Function.Call<bool>(N.IS_PED_DEAD_OR_DYING, ped, true)) return false;
            if (Function.Call<bool>(N.IS_PED_RAGDOLL, ped)) return false;
            if (Function.Call<bool>(N.IS_PED_CLIMBING, ped)) return false;
            if (Function.Call<bool>(N.IS_PED_VAULTING, ped)) return false;
            if (Function.Call<bool>(N.IS_PED_IN_COVER, ped, false)) return false;
            if (Function.Call<bool>(N.IS_PED_GOING_INTO_COVER, ped)) return false;
            if (Function.Call<bool>(N.IS_PED_SWIMMING, ped)) return false;
            if (Function.Call<bool>(N.IS_PED_GETTING_INTO_A_VEHICLE, ped)) return false;
            if (Function.Call<bool>(N.IS_PED_IN_MELEE_COMBAT, ped)) return false;
            if (Function.Call<int>(N.GET_PED_PARACHUTE_STATE, ped) != -1) return false;
            if (Function.Call<bool>(N.IS_PED_USING_ANY_SCENARIO, ped)) return false;
            if (Function.Call<bool>(N.GET_IS_TASK_ACTIVE, ped, N.TaskClimbLadder)) return false;
            if (Function.Call<bool>(N.IS_PLAYER_BEING_ARRESTED, Game.Player, true)) return false;
            return true;
        }

        private void HandleMovement(Ped ped, CsWeapon w, uint weapon, bool active, float dt)
        {
            if (!active || !CanCsMove(ped))
            {
                if (_controlling) Function.Call(N.SET_PED_MOVE_RATE_OVERRIDE, ped, 1f);
                _controlling = false;
                _speed = 0f;
                return;
            }

            Vector3 actual = ped.Velocity;
            var actualH = new V2(actual.X, actual.Y);
            if (!_controlling) { _vel = actualH; _controlling = true; }
            else if (_vel.Length > 1f && actualH.Length < _vel.Length * 0.5f) _vel = actualH; // ran into something

            bool onGround = !Function.Call<bool>(N.IS_PED_JUMPING, ped)
                         && !Function.Call<bool>(N.IS_PED_FALLING, ped)
                         && !Function.Call<bool>(N.IS_ENTITY_IN_AIR, ped);

            float right = Game.GetControlValueNormalized(GTA.Control.MoveLeftRight);
            float forward = -Game.GetControlValueNormalized(GTA.Control.MoveUpDown);
            float input = Math.Min(1f, new V2(right, forward).Length);

            bool walk = false;
            if (_shiftToWalk)
            {
                Game.DisableControlThisFrame(GTA.Control.Sprint);
                walk = Function.Call<bool>(N.IS_DISABLED_CONTROL_PRESSED, 0, (int)GTA.Control.Sprint);
            }
            bool duck = Function.Call<bool>(N.GET_PED_STEALTH_MOVEMENT, ped);
            bool armed = weapon != Unarmed && Function.Call<bool>(N.IS_PED_ARMED, ped, 4);
            bool scoped = Function.Call<bool>(N.IS_PLAYER_FREE_AIMING, Game.Player);

            float max = CsMovement.MaxSpeedMeters(w, armed, scoped);
            float wishSpeed = max * input * (duck ? CsMovement.DuckFraction : walk ? CsMovement.WalkFraction : 1f);
            V2 wishDir = CsMovement.WishDirection(right, forward, GameplayCamera.Rotation.Z);

            _vel = _move.Step(_vel, wishDir, wishSpeed, onGround, dt);
            _speed = _vel.Length;

            if (input < 0.05f && _speed < 0.05f && onGround)
            {
                // Standing still: leave the ped to GTA (idles, interactions, mission prompts).
                _vel = new V2(0, 0);
                Function.Call(N.SET_PED_MOVE_RATE_OVERRIDE, ped, 1f);
                return;
            }

            ped.Velocity = new Vector3(_vel.X, _vel.Y, actual.Z);
            float rate = Math.Max(0.6f, Math.Min(1.3f, _speed / 5.2f));
            Function.Call(N.SET_PED_MOVE_RATE_OVERRIDE, ped, rate);
        }

        // ---------------------------------------------------------------- guns

        private void RefreshDamage()
        {
            if (_time < _nextDamageRefresh) return;
            _nextDamageRefresh = _time + 5f;
            foreach (var w in CsWeapons.All)
            {
                uint h = w.GtaHash;
                if (!_baseDamage.TryGetValue(h, out float baseDmg))
                {
                    baseDmg = Function.Call<float>(N.GET_WEAPON_DAMAGE, h, 0);
                    if (baseDmg <= 0f) continue;
                    _baseDamage[h] = baseDmg;
                }
                Function.Call(N.SET_WEAPON_DAMAGE_MODIFIER, h, w.Damage / baseDmg);
            }
        }

        private void ResetDamage()
        {
            foreach (var w in CsWeapons.All) Function.Call(N.SET_WEAPON_DAMAGE_MODIFIER, w.GtaHash, 1f);
            _nextDamageRefresh = 0f;
        }

        private void HandleGun(Ped ped, CsWeapon w, uint weapon, float dt)
        {
            var current = ped.Weapons.Current;
            int clip = current.AmmoInClip;
            bool reloading = Function.Call<bool>(N.IS_PED_RELOADING, ped);
            bool shooting = Function.Call<bool>(N.IS_PED_SHOOTING, ped);

            if (weapon != _weaponHash)
            {
                _weaponHash = weapon;
                _lastClip = clip;
                _wasReloading = reloading;
                _wasShooting = shooting;
                _recoil.Reset();
                if (w != null) EnsureSuppressor(ped, w);
                return;
            }

            if (w != null && reloading && !_wasReloading)
            {
                _shotsSinceReload[weapon] = 0;
                _reloadStart = _time;
                _reloadWeapon = w;
                _clipInPlayed = false;
                if (_csSounds) _sounds.PlayClipOut(w.Id);
            }
            _wasReloading = reloading;
            if (_reloadWeapon != null && _reloadWeapon == w && !_clipInPlayed && _time - _reloadStart > w.ReloadTime * 0.6f)
            {
                _clipInPlayed = true;
                if (_csSounds) _sounds.PlayClipIn(w.Id);
            }

            int shots = 0;
            if (_lastClip >= 0 && clip < _lastClip && !reloading)
            {
                shots = Math.Min(_lastClip - clip, 5);
                _lastClipDrop = _time;
            }
            else if (clip > _lastClip)
            {
                _shotsSinceReload[weapon] = 0;
            }
            else if (shooting && !_wasShooting && _time - _lastClipDrop > 1f)
            {
                shots = 1; // infinite-ammo cheats never empty the clip
            }
            _lastClip = clip;
            _wasShooting = shooting;

            if (w == null) return;

            if (shots > 0)
            {
                bool onGround = !Function.Call<bool>(N.IS_PED_JUMPING, ped) && !Function.Call<bool>(N.IS_PED_FALLING, ped);
                bool scoped = Function.Call<bool>(N.IS_PLAYER_FREE_AIMING, Game.Player);
                float speed = _csMovement && _controlling ? _speed : new V2(ped.Velocity.X, ped.Velocity.Y).Length;
                float inaccuracy = SprayRecoil.Inaccuracy(w, speed, CsMovement.MaxSpeedMeters(w, true, scoped), onGround, scoped);
                for (int i = 0; i < shots; i++)
                {
                    if (_csSounds) _sounds.PlayShot(w.Id);
                    if (_csRecoil) Kick(_recoil.OnShot(w, _time, inaccuracy, () => (float)_rng.NextDouble()));
                }
                _shotsSinceReload.TryGetValue(weapon, out int fired);
                _shotsSinceReload[weapon] = fired + shots;
                _lastShot = _time;
            }
            else if (_csRecoil)
            {
                Kick(_recoil.Update(w, _time, dt));
            }

            // Counter-Strike fire rate, magazine size and reload time.
            bool block = _time - _lastShot < w.CycleTime - 0.01f;
            if (_reloadWeapon == w && _time - _reloadStart < w.ReloadTime) block = true;
            _shotsSinceReload.TryGetValue(weapon, out int sinceReload);
            if (sinceReload >= w.MagSize)
            {
                block = true;
                if (!reloading && clip > 0 && current.Ammo > clip && _time - _lastForcedReload > 1f)
                {
                    _lastForcedReload = _time;
                    Function.Call(N.MAKE_PED_RELOAD, ped);
                }
            }
            if (block)
            {
                Game.DisableControlThisFrame(GTA.Control.Attack);
                Game.DisableControlThisFrame(GTA.Control.Attack2);
            }
        }

        private static void Kick(SprayPoint d)
        {
            if (d.Pitch == 0f && d.Yaw == 0f) return;
            GameplayCamera.RelativePitch += d.Pitch;
            GameplayCamera.RelativeHeading -= d.Yaw; // GTA headings turn counter-clockwise
        }

        private void EnsureSuppressor(Ped ped, CsWeapon w)
        {
            if (!_csSounds || !_quietGtaGunfire || !_sounds.Has(w.Id)) return;
            uint h = w.GtaHash;
            foreach (var comp in Suppressors)
            {
                if (!Function.Call<bool>(N.DOES_WEAPON_TAKE_WEAPON_COMPONENT, h, comp)) continue;
                if (!Function.Call<bool>(N.HAS_PED_GOT_WEAPON_COMPONENT, ped, h, comp))
                {
                    Function.Call(N.GIVE_WEAPON_COMPONENT_TO_PED, ped, h, comp);
                    _addedSuppressors.Add(new KeyValuePair<uint, uint>(h, comp));
                }
                return;
            }
        }

        private void RemoveAddedSuppressors()
        {
            Ped ped = Game.Player.Character;
            if (ped == null || !ped.Exists()) return;
            foreach (var kv in _addedSuppressors)
                Function.Call(N.REMOVE_WEAPON_COMPONENT_FROM_PED, ped, kv.Key, kv.Value);
            _addedSuppressors.Clear();
        }

        // ---------------------------------------------------------------- toggle / shutdown

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != _toggleKey) return;
            _enabled = !_enabled;
            if (!_enabled)
            {
                ResetDamage();
                RemoveAddedSuppressors();
                Ped ped = Game.Player.Character;
                if (ped != null && ped.Exists()) Function.Call(N.SET_PED_MOVE_RATE_OVERRIDE, ped, 1f);
                _controlling = false;
            }
            _weaponHash = 0;
            GTA.UI.Notification.Show(ModName + (_enabled ? " ~g~on" : " ~r~off") + "~s~ (" + _toggleKey + ")");
        }

        private void OnAborted(object sender, EventArgs e)
        {
            try
            {
                ResetDamage();
                RemoveAddedSuppressors();
            }
            catch { }
            _sounds.Dispose();
            Log("Stopped.");
        }

        private void Log(string message)
        {
            try { File.AppendAllText(_logPath, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + message + Environment.NewLine); }
            catch { }
        }
    }
}
