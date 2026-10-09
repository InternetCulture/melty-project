using System;
using System.Collections.Generic;

namespace LosSantosStrike
{
    /// <summary>
    /// Counter-Strike style spray patterns: where each bullet of a held spray lands, as cumulative
    /// (yaw right, pitch up) offsets in degrees from where the first bullet went. Recreated by hand
    /// from the shape of the in-game patterns (first shots climb, then the spray snakes sideways).
    /// </summary>
    public static class SprayPatterns
    {
        // AK-47: climbs straight for ~9 shots, swings left, then right, then settles side to side.
        private static readonly SprayPoint[] Ak = Points(
            0.00f, 0.00f,  0.00f, 0.60f,  0.05f, 1.30f,  0.10f, 2.20f,  0.05f, 3.20f,
            0.20f, 4.20f,  0.40f, 5.00f,  0.30f, 5.70f,  0.00f, 6.10f, -0.80f, 6.30f,
           -1.60f, 6.40f, -2.20f, 6.50f, -2.00f, 6.70f, -1.40f, 6.80f, -0.40f, 6.80f,
            0.80f, 6.70f,  1.80f, 6.80f,  2.40f, 6.90f,  2.60f, 7.00f,  2.20f, 7.10f,
            1.40f, 7.00f,  0.60f, 7.10f,  0.40f, 7.20f,  1.20f, 7.20f,  2.00f, 7.10f,
            2.60f, 7.20f,  2.40f, 7.30f,  1.60f, 7.20f,  0.60f, 7.30f, -0.20f, 7.30f);

        // M4A4: climbs a little less, drifts right, then left, then wanders.
        private static readonly SprayPoint[] M4 = Points(
            0.00f, 0.00f,  0.00f, 0.50f,  0.05f, 1.10f, -0.05f, 1.90f,  0.05f, 2.80f,
            0.15f, 3.60f,  0.30f, 4.30f,  0.55f, 4.90f,  0.90f, 5.20f,  1.30f, 5.40f,
            1.50f, 5.60f,  1.20f, 5.80f,  0.60f, 5.90f, -0.20f, 6.00f, -1.00f, 6.00f,
           -1.60f, 6.10f, -1.90f, 6.20f, -1.70f, 6.30f, -1.10f, 6.30f, -0.40f, 6.40f,
            0.30f, 6.40f,  0.90f, 6.50f,  1.20f, 6.50f,  0.90f, 6.60f,  0.30f, 6.60f,
           -0.30f, 6.70f, -0.70f, 6.70f, -0.50f, 6.80f, -0.10f, 6.80f,  0.30f, 6.80f);

        private static readonly Dictionary<string, SprayPoint[]> Cache = new Dictionary<string, SprayPoint[]>();

        public static SprayPoint[] For(CsWeapon w)
        {
            lock (Cache)
            {
                SprayPoint[] p;
                if (!Cache.TryGetValue(w.Id, out p))
                {
                    p = Build(w);
                    Cache[w.Id] = p;
                }
                return p;
            }
        }

        private static SprayPoint[] Build(CsWeapon w)
        {
            int n = Math.Max(1, w.MagSize);
            var result = new SprayPoint[n];
            switch (w.Class)
            {
                case CsWeaponClass.Rifle:
                case CsWeaponClass.Smg:
                case CsWeaponClass.MachineGun:
                {
                    var src = w.Id == "m4a4" || w.Id == "m4a1_silencer" || w.Id == "famas" ? M4 : Ak;
                    float scale = w.Id == "ak47" || w.Id == "m4a4" ? 1f : w.RecoilScale;
                    var last = src[src.Length - 1];
                    for (int i = 0; i < n; i++)
                    {
                        if (i < src.Length)
                        {
                            result[i] = new SprayPoint(src[i].Yaw * scale, src[i].Pitch * scale);
                        }
                        else
                        {
                            // Long magazines keep snaking sideways at the top of the spray.
                            float t = i - src.Length + 1;
                            result[i] = new SprayPoint(
                                (last.Yaw + 2.2f * (float)Math.Sin(t * 0.55f)) * scale,
                                (last.Pitch + 0.05f * t) * scale);
                        }
                    }
                    break;
                }
                case CsWeaponClass.Pistol:
                    // Each tap kicks straight up; spamming walks it up with a slight wobble.
                    for (int i = 0; i < n; i++)
                        result[i] = new SprayPoint(0.25f * (float)Math.Sin(i * 1.7f) * w.RecoilScale * 2f,
                                                   i * 1.6f * w.RecoilScale);
                    break;
                default:
                    // Snipers and shotguns: one big kick per shot.
                    for (int i = 0; i < n; i++)
                        result[i] = new SprayPoint(0f, i * 3.0f * w.RecoilScale);
                    break;
            }
            return result;
        }

        private static SprayPoint[] Points(params float[] yawPitch)
        {
            var r = new SprayPoint[yawPitch.Length / 2];
            for (int i = 0; i < r.Length; i++) r[i] = new SprayPoint(yawPitch[i * 2], yawPitch[i * 2 + 1]);
            return r;
        }
    }
}
