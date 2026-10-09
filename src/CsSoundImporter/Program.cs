using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using SteamDatabase.ValvePak;
using ValveResourceFormat;
using ValveResourceFormat.ResourceTypes;

namespace LosSantosStrike.Importer
{
    /// <summary>
    /// Copies Counter-Strike 2's gun sounds out of the player's own CS2 install into
    /// %LOCALAPPDATA%\LosSantosStrike\sounds so the GTA V script can play them.
    /// Nothing from Counter-Strike 2 ships with the mod.
    /// </summary>
    public static class Program
    {
        public const int FormatVersion = 1;

        public static int Main(string[] args)
        {
            string cs2 = null, outDir = null;
            bool force = false;
            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--cs2" when i + 1 < args.Length: cs2 = args[++i]; break;
                    case "--out" when i + 1 < args.Length: outDir = args[++i]; break;
                    case "--force": force = true; break;
                    case "--auto": break;
                    case "-h":
                    case "--help":
                        Console.WriteLine("CsSoundImporter [--cs2 <Counter-Strike 2 folder>] [--out <folder>] [--force]");
                        return 0;
                }
            }

            outDir ??= DefaultOutDir();
            Directory.CreateDirectory(outDir);
            var log = new StringBuilder();
            void Log(string s) { Console.WriteLine(s); log.AppendLine(s); }

            try
            {
                string vpk = Cs2Locator.FindPak(cs2, Log);
                if (vpk == null)
                {
                    WriteStatus(outDir, false, "Counter-Strike 2 was not found. Install it from Steam, then start the game again.", log);
                    return 2;
                }

                string stamp = Stamp(vpk);
                string okFile = Path.Combine(outDir, "import.ok");
                if (!force && File.Exists(okFile) && File.ReadAllText(okFile).Trim() == stamp)
                {
                    Log("Sounds already imported from " + vpk);
                    return 0;
                }

                int imported = Import(vpk, Path.Combine(outDir, "sounds"), Log);
                if (imported == 0)
                {
                    WriteStatus(outDir, false, "No Counter-Strike 2 gun sounds could be read from " + vpk, log);
                    return 3;
                }
                File.WriteAllText(okFile, stamp);
                File.Delete(Path.Combine(outDir, "import.err"));
                File.WriteAllText(Path.Combine(outDir, "import.log"), log.ToString());
                Log($"Imported {imported} sounds.");
                return 0;
            }
            catch (Exception e)
            {
                WriteStatus(outDir, false, "Sound import failed: " + e.Message, log.AppendLine(e.ToString()));
                return 3;
            }
        }

        public static string DefaultOutDir() =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LosSantosStrike");

        private static string Stamp(string vpk)
        {
            var fi = new FileInfo(vpk);
            return $"v{FormatVersion}|{fi.FullName}|{fi.Length}|{fi.LastWriteTimeUtc.Ticks}";
        }

        private static void WriteStatus(string outDir, bool ok, string message, StringBuilder log)
        {
            Console.Error.WriteLine(message);
            File.WriteAllText(Path.Combine(outDir, "import.err"), message);
            File.WriteAllText(Path.Combine(outDir, "import.log"), log.ToString() + message + Environment.NewLine);
        }

        public static int Import(string vpkPath, string soundsDir, Action<string> log)
        {
            using var package = new Package();
            package.Read(vpkPath);
            if (package.Entries == null || !package.Entries.TryGetValue("vsnd_c", out var sounds))
            {
                log("The package has no compiled sounds: " + vpkPath);
                return 0;
            }

            var byPath = new Dictionary<string, PackageEntry>(StringComparer.OrdinalIgnoreCase);
            foreach (var e in sounds)
            {
                if (e.DirectoryName != null && e.DirectoryName.Contains("weapons", StringComparison.OrdinalIgnoreCase))
                    byPath[e.GetFullPath().Replace('\\', '/')] = e;
            }
            log($"Found {byPath.Count} weapon sounds in {vpkPath}");

            int count = 0;
            var tmp = soundsDir + ".new";
            if (Directory.Exists(tmp)) Directory.Delete(tmp, true);
            Directory.CreateDirectory(tmp);

            foreach (var w in CsWeapons.All)
            {
                var match = SoundMatcher.Find(w, byPath.Keys);
                if (match.Shots.Count == 0)
                {
                    log($"{w.Name}: no firing sound found");
                    continue;
                }
                var dir = Path.Combine(tmp, w.Id);
                Directory.CreateDirectory(dir);
                int n = 1;
                foreach (var p in match.Shots)
                    if (Extract(package, byPath[p], Path.Combine(dir, $"shot_{n}.wav"), log)) { n++; count++; }
                if (match.ClipOut != null && Extract(package, byPath[match.ClipOut], Path.Combine(dir, "clipout.wav"), log)) count++;
                if (match.ClipIn != null && Extract(package, byPath[match.ClipIn], Path.Combine(dir, "clipin.wav"), log)) count++;
                log($"{w.Name}: {string.Join(", ", match.Shots)}{(match.ClipOut != null ? " + reload" : "")}");
            }

            if (count > 0)
            {
                if (Directory.Exists(soundsDir)) Directory.Delete(soundsDir, true);
                Directory.Move(tmp, soundsDir);
            }
            else
            {
                Directory.Delete(tmp, true);
            }
            return count;
        }

        private static bool Extract(Package package, PackageEntry entry, string wavPath, Action<string> log)
        {
            try
            {
                package.ReadEntry(entry, out byte[] raw);
                using var ms = new MemoryStream(raw);
                using var resource = new Resource();
                resource.Read(ms, verifyFileSize: false);
                if (resource.DataBlock is not Sound sound)
                {
                    log($"  {entry.GetFullPath()}: not a sound resource");
                    return false;
                }
                using var audio = sound.GetSoundStream();
                switch (sound.SoundType)
                {
                    case Sound.AudioFileType.WAV:
                        using (var f = File.Create(wavPath)) audio.CopyTo(f);
                        return true;
                    case Sound.AudioFileType.MP3:
                        AudioConvert.Mp3ToWav(audio, wavPath);
                        return true;
                    default:
                        log($"  {entry.GetFullPath()}: unsupported audio type {sound.SoundType}");
                        return false;
                }
            }
            catch (Exception e)
            {
                log($"  {entry.GetFullPath()}: {e.Message}");
                return false;
            }
        }
    }

    public static class Cs2Locator
    {
        private const string Cs2Folder = "Counter-Strike Global Offensive";

        /// <summary>Find game/csgo/pak01_dir.vpk from a hint (CS2 root, game, csgo folder or the vpk) or from Steam.</summary>
        public static string FindPak(string hint, Action<string> log)
        {
            if (!string.IsNullOrWhiteSpace(hint))
            {
                var found = FromHint(hint.Trim().Trim('"'));
                if (found != null) return found;
                log("Counter-Strike 2 not found at " + hint + "; looking in Steam libraries.");
            }
            foreach (var lib in SteamLibraries())
            {
                var p = FromHint(Path.Combine(lib, "steamapps", "common", Cs2Folder));
                if (p != null) return p;
            }
            return null;
        }

        public static string FromHint(string hint)
        {
            if (File.Exists(hint) && hint.EndsWith(".vpk", StringComparison.OrdinalIgnoreCase)) return Path.GetFullPath(hint);
            if (!Directory.Exists(hint)) return null;
            foreach (var rel in new[] { "game/csgo/pak01_dir.vpk", "csgo/pak01_dir.vpk", "pak01_dir.vpk" })
            {
                var p = Path.Combine(hint, rel.Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(p)) return Path.GetFullPath(p);
            }
            return null;
        }

        public static IEnumerable<string> SteamLibraries()
        {
            var roots = new List<string>();
            if (OperatingSystem.IsWindows())
            {
                foreach (var (hive, key, value) in new[]
                {
                    (Microsoft.Win32.Registry.CurrentUser, @"Software\Valve\Steam", "SteamPath"),
                    (Microsoft.Win32.Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath"),
                    (Microsoft.Win32.Registry.LocalMachine, @"SOFTWARE\Valve\Steam", "InstallPath"),
                })
                {
                    try
                    {
                        using var k = hive.OpenSubKey(key);
                        if (k?.GetValue(value) is string s && Directory.Exists(s)) roots.Add(s.Replace('/', '\\'));
                    }
                    catch { }
                }
                roots.Add(@"C:\Program Files (x86)\Steam");
            }
            else
            {
                var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                roots.Add(Path.Combine(home, ".steam", "steam"));
                roots.Add(Path.Combine(home, ".local", "share", "Steam"));
            }

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var root in roots)
            {
                if (!Directory.Exists(root)) continue;
                if (seen.Add(Path.GetFullPath(root))) yield return root;
                var vdf = Path.Combine(root, "steamapps", "libraryfolders.vdf");
                if (!File.Exists(vdf)) continue;
                foreach (var lib in ParseLibraryFolders(File.ReadAllText(vdf)))
                    if (Directory.Exists(lib) && seen.Add(Path.GetFullPath(lib))) yield return lib;
            }
        }

        public static IEnumerable<string> ParseLibraryFolders(string vdf)
        {
            foreach (Match m in Regex.Matches(vdf, "\"path\"\\s+\"([^\"]+)\""))
                yield return m.Groups[1].Value.Replace(@"\\", @"\");
        }
    }

    public static class AudioConvert
    {
        /// <summary>Decode MP3 to 16-bit PCM WAV so the game script never needs an MP3 codec.</summary>
        public static void Mp3ToWav(Stream mp3, string wavPath)
        {
            using var mpeg = new NLayer.MpegFile(mp3);
            int channels = mpeg.Channels, rate = mpeg.SampleRate;
            var pcm = new MemoryStream();
            var buffer = new float[4096 * channels];
            int read;
            while ((read = mpeg.ReadSamples(buffer, 0, buffer.Length)) > 0)
            {
                for (int i = 0; i < read; i++)
                {
                    float s = Math.Clamp(buffer[i], -1f, 1f);
                    short v = (short)Math.Round(s * short.MaxValue);
                    pcm.WriteByte((byte)(v & 0xFF));
                    pcm.WriteByte((byte)((v >> 8) & 0xFF));
                }
            }
            WriteWav(wavPath, pcm.ToArray(), channels, rate);
        }

        public static void WriteWav(string path, byte[] pcm16, int channels, int sampleRate)
        {
            using var f = File.Create(path);
            using var w = new BinaryWriter(f);
            w.Write(Encoding.ASCII.GetBytes("RIFF"));
            w.Write(36 + pcm16.Length);
            w.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
            w.Write(16);
            w.Write((short)1);
            w.Write((short)channels);
            w.Write(sampleRate);
            w.Write(sampleRate * channels * 2);
            w.Write((short)(channels * 2));
            w.Write((short)16);
            w.Write(Encoding.ASCII.GetBytes("data"));
            w.Write(pcm16.Length);
            w.Write(pcm16);
        }
    }
}
