using System;
using System.Collections.Generic;

namespace LosSantosStrike
{
    public enum CsWeaponClass { Pistol, Smg, Rifle, Sniper, Shotgun, MachineGun }

    /// <summary>A Counter-Strike 2 gun and the GTA V gun that carries it.</summary>
    public sealed class CsWeapon
    {
        public string Id;            // sound folder id under %LOCALAPPDATA%\LosSantosStrike\sounds
        public string Name;          // Counter-Strike name shown to the player
        public string GtaWeapon;     // GTA V weapon name, e.g. WEAPON_ASSAULTRIFLE
        public CsWeaponClass Class;
        public float Damage;         // per bullet (per pellet for shotguns), before armor/headshot
        public float CycleTime;      // seconds between shots
        public int MagSize;
        public float ReloadTime;     // seconds
        public float MaxSpeed;       // units/second while holding it
        public float ScopedSpeed;    // units/second while aiming (snipers), 0 = same as MaxSpeed
        public float RecoilScale;    // multiplier on the class spray pattern
        public float MoveInaccuracy; // degrees of extra aim kick at full speed
        public string[] SoundFolders;     // CS2 folders under sounds/weapons/
        public string[] SoundRequire = new string[0]; // file name must contain one of these (if any)
        public string[] SoundExclude = new string[0]; // file name must contain none of these

        public uint GtaHash => Joaat.Hash(GtaWeapon);

        /// <summary>Time for the spray to fully reset after the trigger is released.</summary>
        public float RecoveryTime => Math.Max(0.3f, Math.Min(1.2f, CycleTime * 6f));

        /// <summary>Cumulative aim offsets (pitch up, yaw right) in degrees for shot 1..n.</summary>
        public SprayPoint[] Pattern => SprayPatterns.For(this);
    }

    public struct SprayPoint
    {
        public float Pitch, Yaw;
        public SprayPoint(float yaw, float pitch) { Yaw = yaw; Pitch = pitch; }
    }

    public static class CsWeapons
    {
        // Counter-Strike 2 movement units are inches.
        public const float UnitsToMeters = 0.0254f;
        public const float KnifeSpeed = 250f;
        public const float UnmappedGunSpeed = 220f;

        public static readonly IReadOnlyList<CsWeapon> All = new List<CsWeapon>
        {
            new CsWeapon { Id = "glock", Name = "Glock-18", GtaWeapon = "WEAPON_PISTOL", Class = CsWeaponClass.Pistol,
                Damage = 30, CycleTime = 0.15f, MagSize = 20, ReloadTime = 2.27f, MaxSpeed = 240, RecoilScale = 0.30f, MoveInaccuracy = 1.2f,
                SoundFolders = new[] { "glock18", "glock" } },
            new CsWeapon { Id = "usp_silencer", Name = "USP-S", GtaWeapon = "WEAPON_COMBATPISTOL", Class = CsWeaponClass.Pistol,
                Damage = 35, CycleTime = 0.17f, MagSize = 12, ReloadTime = 2.17f, MaxSpeed = 240, RecoilScale = 0.35f, MoveInaccuracy = 1.2f,
                SoundFolders = new[] { "usp", "usp_silencer", "usp_s" } },
            new CsWeapon { Id = "p250", Name = "P250", GtaWeapon = "WEAPON_SNSPISTOL", Class = CsWeaponClass.Pistol,
                Damage = 38, CycleTime = 0.15f, MagSize = 13, ReloadTime = 2.2f, MaxSpeed = 240, RecoilScale = 0.40f, MoveInaccuracy = 1.4f,
                SoundFolders = new[] { "p250" } },
            new CsWeapon { Id = "fiveseven", Name = "Five-SeveN", GtaWeapon = "WEAPON_HEAVYPISTOL", Class = CsWeaponClass.Pistol,
                Damage = 32, CycleTime = 0.15f, MagSize = 20, ReloadTime = 2.2f, MaxSpeed = 240, RecoilScale = 0.35f, MoveInaccuracy = 1.3f,
                SoundFolders = new[] { "fiveseven", "five_seven" } },
            new CsWeapon { Id = "deagle", Name = "Desert Eagle", GtaWeapon = "WEAPON_PISTOL50", Class = CsWeaponClass.Pistol,
                Damage = 53, CycleTime = 0.225f, MagSize = 7, ReloadTime = 2.2f, MaxSpeed = 230, RecoilScale = 1.10f, MoveInaccuracy = 2.6f,
                SoundFolders = new[] { "deagle", "desert_eagle" } },
            new CsWeapon { Id = "tec9", Name = "Tec-9", GtaWeapon = "WEAPON_MACHINEPISTOL", Class = CsWeaponClass.Pistol,
                Damage = 33, CycleTime = 0.12f, MagSize = 18, ReloadTime = 2.5f, MaxSpeed = 240, RecoilScale = 0.45f, MoveInaccuracy = 1.0f,
                SoundFolders = new[] { "tec9" } },

            new CsWeapon { Id = "mac10", Name = "MAC-10", GtaWeapon = "WEAPON_MICROSMG", Class = CsWeaponClass.Smg,
                Damage = 29, CycleTime = 0.075f, MagSize = 30, ReloadTime = 2.6f, MaxSpeed = 240, RecoilScale = 0.55f, MoveInaccuracy = 0.8f,
                SoundFolders = new[] { "mac10" } },
            new CsWeapon { Id = "mp5sd", Name = "MP5-SD", GtaWeapon = "WEAPON_SMG", Class = CsWeaponClass.Smg,
                Damage = 27, CycleTime = 0.08f, MagSize = 30, ReloadTime = 3.0f, MaxSpeed = 235, RecoilScale = 0.45f, MoveInaccuracy = 0.8f,
                SoundFolders = new[] { "mp5", "mp5sd" } },
            new CsWeapon { Id = "p90", Name = "P90", GtaWeapon = "WEAPON_ASSAULTSMG", Class = CsWeaponClass.Smg,
                Damage = 26, CycleTime = 0.07f, MagSize = 50, ReloadTime = 3.3f, MaxSpeed = 230, RecoilScale = 0.45f, MoveInaccuracy = 0.9f,
                SoundFolders = new[] { "p90" } },

            new CsWeapon { Id = "ak47", Name = "AK-47", GtaWeapon = "WEAPON_ASSAULTRIFLE", Class = CsWeaponClass.Rifle,
                Damage = 36, CycleTime = 0.1f, MagSize = 30, ReloadTime = 2.43f, MaxSpeed = 215, RecoilScale = 1.0f, MoveInaccuracy = 3.0f,
                SoundFolders = new[] { "ak47" } },
            new CsWeapon { Id = "m4a4", Name = "M4A4", GtaWeapon = "WEAPON_CARBINERIFLE", Class = CsWeaponClass.Rifle,
                Damage = 33, CycleTime = 0.09f, MagSize = 30, ReloadTime = 3.07f, MaxSpeed = 225, RecoilScale = 0.80f, MoveInaccuracy = 2.6f,
                SoundFolders = new[] { "m4a1", "m4a4" }, SoundExclude = new[] { "silencer", "m4a1s", "unsil" } },
            new CsWeapon { Id = "m4a1_silencer", Name = "M4A1-S", GtaWeapon = "WEAPON_SPECIALCARBINE", Class = CsWeaponClass.Rifle,
                Damage = 38, CycleTime = 0.1f, MagSize = 20, ReloadTime = 3.07f, MaxSpeed = 225, RecoilScale = 0.70f, MoveInaccuracy = 2.4f,
                SoundFolders = new[] { "m4a1_silencer", "m4a1s", "m4a1" }, SoundRequire = new[] { "silencer", "m4a1s", "m4a1_s" } },
            new CsWeapon { Id = "sg556", Name = "SG 553", GtaWeapon = "WEAPON_ADVANCEDRIFLE", Class = CsWeaponClass.Rifle,
                Damage = 30, CycleTime = 0.09f, MagSize = 30, ReloadTime = 2.8f, MaxSpeed = 210, RecoilScale = 0.95f, MoveInaccuracy = 3.0f,
                SoundFolders = new[] { "sg556", "sg553" } },
            new CsWeapon { Id = "famas", Name = "FAMAS", GtaWeapon = "WEAPON_BULLPUPRIFLE", Class = CsWeaponClass.Rifle,
                Damage = 26, CycleTime = 0.09f, MagSize = 25, ReloadTime = 3.3f, MaxSpeed = 220, RecoilScale = 0.70f, MoveInaccuracy = 2.4f,
                SoundFolders = new[] { "famas" } },
            new CsWeapon { Id = "galilar", Name = "Galil AR", GtaWeapon = "WEAPON_COMPACTRIFLE", Class = CsWeaponClass.Rifle,
                Damage = 30, CycleTime = 0.09f, MagSize = 35, ReloadTime = 3.0f, MaxSpeed = 215, RecoilScale = 0.80f, MoveInaccuracy = 2.6f,
                SoundFolders = new[] { "galilar", "galil" } },

            new CsWeapon { Id = "awp", Name = "AWP", GtaWeapon = "WEAPON_HEAVYSNIPER", Class = CsWeaponClass.Sniper,
                Damage = 115, CycleTime = 1.455f, MagSize = 5, ReloadTime = 3.67f, MaxSpeed = 200, ScopedSpeed = 100, RecoilScale = 1.0f, MoveInaccuracy = 6.0f,
                SoundFolders = new[] { "awp" } },
            new CsWeapon { Id = "ssg08", Name = "SSG 08", GtaWeapon = "WEAPON_SNIPERRIFLE", Class = CsWeaponClass.Sniper,
                Damage = 88, CycleTime = 1.25f, MagSize = 10, ReloadTime = 3.7f, MaxSpeed = 230, RecoilScale = 0.6f, MoveInaccuracy = 3.5f,
                SoundFolders = new[] { "ssg08", "scout" } },

            new CsWeapon { Id = "nova", Name = "Nova", GtaWeapon = "WEAPON_PUMPSHOTGUN", Class = CsWeaponClass.Shotgun,
                Damage = 26, CycleTime = 0.88f, MagSize = 8, ReloadTime = 3.5f, MaxSpeed = 220, RecoilScale = 1.0f, MoveInaccuracy = 1.5f,
                SoundFolders = new[] { "nova" } },
            new CsWeapon { Id = "xm1014", Name = "XM1014", GtaWeapon = "WEAPON_ASSAULTSHOTGUN", Class = CsWeaponClass.Shotgun,
                Damage = 20, CycleTime = 0.35f, MagSize = 7, ReloadTime = 3.0f, MaxSpeed = 215, RecoilScale = 0.8f, MoveInaccuracy = 1.5f,
                SoundFolders = new[] { "xm1014" } },

            new CsWeapon { Id = "negev", Name = "Negev", GtaWeapon = "WEAPON_MG", Class = CsWeaponClass.MachineGun,
                Damage = 35, CycleTime = 0.075f, MagSize = 150, ReloadTime = 5.7f, MaxSpeed = 150, RecoilScale = 0.9f, MoveInaccuracy = 3.5f,
                SoundFolders = new[] { "negev" } },
            new CsWeapon { Id = "m249", Name = "M249", GtaWeapon = "WEAPON_COMBATMG", Class = CsWeaponClass.MachineGun,
                Damage = 32, CycleTime = 0.08f, MagSize = 100, ReloadTime = 5.7f, MaxSpeed = 195, RecoilScale = 0.8f, MoveInaccuracy = 3.5f,
                SoundFolders = new[] { "m249" } },
        };

        private static Dictionary<uint, CsWeapon> _byHash;

        public static CsWeapon ByGtaHash(uint hash)
        {
            if (_byHash == null)
            {
                var map = new Dictionary<uint, CsWeapon>();
                foreach (var w in All) map[w.GtaHash] = w;
                _byHash = map;
            }
            CsWeapon found;
            return _byHash.TryGetValue(hash, out found) ? found : null;
        }
    }
}
