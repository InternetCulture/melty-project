using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace LosSantosStrike
{
    /// <summary>Picks a gun's firing and reload sounds out of Counter-Strike 2's sound file list.</summary>
    public static class SoundMatcher
    {
        public sealed class Match
        {
            public List<string> Shots = new List<string>();
            public string ClipOut, ClipIn;
        }

        private static readonly string[] NotAShot =
        {
            "clip", "bolt", "draw", "deploy", "reload", "slide", "pump", "insert", "zoom", "empty", "dry",
            "distant", "dist_", "_far", "tail", "foley", "cock", "hammer", "screw", "unsil", "silencer_on",
            "silencer_off", "detach", "attach", "mag", "cylinder", "shell", "catch", "lever", "hit", "impact",
            "inspect", "holster", "idle", "touch", "lookat", "release", "click", "drop", "pickup", "rattle",
            "handle", "grab", "button", "switch", "barrel", "chamber", "cloth", "move", "sight", "scope",
        };

        private static readonly Regex Numbered = new Regex(@"(^|[_\-])(0?\d{1,2})$", RegexOptions.CultureInvariant);

        /// <param name="paths">sound paths inside the CS2 package, like "sounds/weapons/ak47/ak47_01.vsnd_c"</param>
        public static Match Find(CsWeapon w, IEnumerable<string> paths)
        {
            var inFolder = new List<(string path, string name)>();
            var list = paths as IList<string> ?? paths.ToList();
            foreach (var folder in w.SoundFolders)
            {
                string f = folder.ToLowerInvariant();
                foreach (var p in list)
                {
                    string lp = p.Replace('\\', '/').ToLowerInvariant();
                    int slash = lp.LastIndexOf('/');
                    if (slash < 0) continue;
                    string dir = lp.Substring(0, slash);
                    if (!dir.Contains("weapons/")) continue;
                    string lastDir = dir.Substring(dir.LastIndexOf('/') + 1);
                    if (lastDir != f) continue;
                    inFolder.Add((p, StripExt(lp.Substring(slash + 1))));
                }
                if (inFolder.Count > 0) break;
            }

            var result = new Match();
            if (inFolder.Count == 0) return result;

            Func<string, bool> allowed = name =>
                (w.SoundRequire.Length == 0 || w.SoundRequire.Any(r => name.Contains(r))) &&
                !w.SoundExclude.Any(x => name.Contains(x));

            var shots = inFolder
                .Where(e => allowed(e.name) && !NotAShot.Any(k => e.name.Contains(k)))
                .OrderByDescending(e => Numbered.IsMatch(e.name))
                .ThenBy(e => e.name.Length)
                .ThenBy(e => e.name, StringComparer.Ordinal)
                .Select(e => e.path)
                .Take(4)
                .ToList();
            result.Shots = shots;

            // Reload sounds are shared by the silenced/unsilenced variants, so only the exclusions apply.
            Func<string, bool> notExcluded = name => !w.SoundExclude.Any(x => name.Contains(x));
            result.ClipOut = inFolder.Where(e => notExcluded(e.name) && (e.name.Contains("clipout") || e.name.Contains("magout") || e.name.Contains("clip_out")))
                                     .OrderBy(e => e.name.Length).Select(e => e.path).FirstOrDefault();
            result.ClipIn = inFolder.Where(e => notExcluded(e.name) && (e.name.Contains("clipin") || e.name.Contains("magin") || e.name.Contains("clip_in")))
                                    .OrderBy(e => e.name.Length).Select(e => e.path).FirstOrDefault();
            return result;
        }

        private static string StripExt(string name)
        {
            int dot = name.IndexOf('.');
            return dot >= 0 ? name.Substring(0, dot) : name;
        }
    }
}
