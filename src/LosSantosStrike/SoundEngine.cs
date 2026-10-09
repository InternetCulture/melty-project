using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace LosSantosStrike
{
    /// <summary>Plays the Counter-Strike 2 gun sounds imported from the player's own CS2 install.</summary>
    internal sealed class SoundEngine : IDisposable
    {
        private const int Rate = 44100;
        private const int MaxVoices = 24;

        private sealed class Bank
        {
            public List<float[]> Shots = new List<float[]>();
            public float[] ClipOut, ClipIn;
        }

        private readonly object _lock = new object();
        private Dictionary<string, Bank> _banks = new Dictionary<string, Bank>();
        private readonly Random _rng = new Random();
        private MixingSampleProvider _mixer;
        private VolumeSampleProvider _volume;
        private WaveOutEvent _out;

        public int GunCount { get { lock (_lock) return _banks.Count(b => b.Value.Shots.Count > 0); } }

        public bool Has(string id) { lock (_lock) return _banks.TryGetValue(id, out var b) && b.Shots.Count > 0; }

        public void Start(float volume)
        {
            _mixer = new MixingSampleProvider(WaveFormat.CreateIeeeFloatWaveFormat(Rate, 2)) { ReadFully = true };
            _volume = new VolumeSampleProvider(_mixer) { Volume = volume };
            _out = new WaveOutEvent { DesiredLatency = 70, NumberOfBuffers = 3 };
            _out.Init(_volume);
            _out.Play();
        }

        /// <summary>Loads %LOCALAPPDATA%\LosSantosStrike\sounds\&lt;gun&gt;\shot_N.wav, clipout.wav, clipin.wav.</summary>
        public int Load(string soundsDir, Action<string> log)
        {
            var banks = new Dictionary<string, Bank>();
            if (Directory.Exists(soundsDir))
            {
                foreach (var dir in Directory.GetDirectories(soundsDir))
                {
                    var bank = new Bank();
                    foreach (var f in Directory.GetFiles(dir, "shot_*.wav").OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
                    {
                        var s = Read(f, log);
                        if (s != null) bank.Shots.Add(s);
                    }
                    bank.ClipOut = Read(Path.Combine(dir, "clipout.wav"), log);
                    bank.ClipIn = Read(Path.Combine(dir, "clipin.wav"), log);
                    banks[Path.GetFileName(dir)] = bank;
                }
            }
            lock (_lock) _banks = banks;
            return GunCount;
        }

        public void PlayShot(string id)
        {
            Bank b;
            lock (_lock) if (!_banks.TryGetValue(id, out b) || b.Shots.Count == 0) return;
            Play(b.Shots[_rng.Next(b.Shots.Count)]);
        }

        public void PlayClipOut(string id) { Bank b; lock (_lock) if (_banks.TryGetValue(id, out b)) Play(b.ClipOut); }
        public void PlayClipIn(string id) { Bank b; lock (_lock) if (_banks.TryGetValue(id, out b)) Play(b.ClipIn); }

        private void Play(float[] samples)
        {
            if (samples == null || _mixer == null) return;
            var inputs = _mixer.MixerInputs.ToList();
            if (inputs.Count >= MaxVoices) _mixer.RemoveMixerInput(inputs[0]);
            _mixer.AddMixerInput(new CachedSampleProvider(samples, _mixer.WaveFormat));
        }

        private static float[] Read(string path, Action<string> log)
        {
            if (!File.Exists(path)) return null;
            try
            {
                using (var reader = new WaveFileReader(path))
                {
                    WaveStream pcm = reader;
                    var enc = reader.WaveFormat.Encoding;
                    if (enc != WaveFormatEncoding.Pcm && enc != WaveFormatEncoding.IeeeFloat)
                        pcm = WaveFormatConversionStream.CreatePcmStream(reader); // e.g. MS ADPCM via Windows' codec
                    ISampleProvider sp = pcm.ToSampleProvider();
                    if (sp.WaveFormat.Channels == 1) sp = new MonoToStereoSampleProvider(sp);
                    else if (sp.WaveFormat.Channels != 2) return null;
                    if (sp.WaveFormat.SampleRate != Rate) sp = new WdlResamplingSampleProvider(sp, Rate);

                    var all = new List<float>(Rate * 2);
                    var buf = new float[Rate];
                    int n;
                    while ((n = sp.Read(buf, 0, buf.Length)) > 0)
                    {
                        for (int i = 0; i < n; i++) all.Add(buf[i]);
                        if (all.Count > Rate * 2 * 6) break; // nothing gun-related runs over 6 seconds
                    }
                    if (!ReferenceEquals(pcm, reader)) pcm.Dispose();
                    return all.ToArray();
                }
            }
            catch (Exception e)
            {
                log?.Invoke("Could not read " + path + ": " + e.Message);
                return null;
            }
        }

        public void Dispose()
        {
            try { _out?.Stop(); _out?.Dispose(); } catch { }
            _out = null;
            _mixer = null;
        }

        private sealed class CachedSampleProvider : ISampleProvider
        {
            private readonly float[] _data;
            private int _pos;
            public CachedSampleProvider(float[] data, WaveFormat format) { _data = data; WaveFormat = format; }
            public WaveFormat WaveFormat { get; }
            public int Read(float[] buffer, int offset, int count)
            {
                int n = Math.Min(count, _data.Length - _pos);
                if (n <= 0) return 0;
                Array.Copy(_data, _pos, buffer, offset, n);
                _pos += n;
                return n;
            }
        }
    }
}
