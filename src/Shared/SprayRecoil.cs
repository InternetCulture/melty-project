using System;

namespace LosSantosStrike
{
    /// <summary>
    /// Tracks a spray: which bullet of the pattern comes next, how far the aim has been pushed,
    /// and how it settles back once the trigger is released. Returns camera deltas in degrees.
    /// </summary>
    public sealed class SprayRecoil
    {
        public const float DecayExp = 8f;    // weapon_recoil_decay2_exp
        public const float DecayLinear = 18f; // weapon_recoil_decay2_lin, degrees/second

        private float _shotIndex;
        private float _lastShot = -100f;
        private float _punchPitch, _punchYaw;
        private string _weaponId;

        public float ShotIndex => _shotIndex;
        public float PunchPitch => _punchPitch;
        public float PunchYaw => _punchYaw;

        /// <param name="now">seconds</param>
        /// <param name="inaccuracy">0 = standing still, 1 = full speed, more when airborne</param>
        /// <param name="rand">uniform random in [0,1)</param>
        public SprayPoint OnShot(CsWeapon w, float now, float inaccuracy, Func<float> rand)
        {
            if (_weaponId != w.Id) { _weaponId = w.Id; _shotIndex = 0f; }
            var pattern = w.Pattern;
            int k = (int)Math.Round(_shotIndex);
            float dPitch, dYaw;
            if (k + 1 < pattern.Length)
            {
                dPitch = pattern[k + 1].Pitch - pattern[k].Pitch;
                dYaw = pattern[k + 1].Yaw - pattern[k].Yaw;
            }
            else
            {
                dPitch = 0.15f * w.RecoilScale;
                dYaw = (rand() - 0.5f) * 1.2f * w.RecoilScale;
            }

            if (inaccuracy > 0f)
            {
                // Running, jumping or unscoped sniping throws the shot off in a random direction.
                double angle = rand() * Math.PI * 2.0;
                float radius = (float)Math.Sqrt(rand()) * w.MoveInaccuracy * inaccuracy;
                dPitch += radius * (float)Math.Sin(angle);
                dYaw += radius * (float)Math.Cos(angle);
            }

            _shotIndex += 1f;
            _lastShot = now;
            _punchPitch += dPitch;
            _punchYaw += dYaw;
            return new SprayPoint(dYaw, dPitch);
        }

        /// <summary>Call every frame; returns the camera delta that settles the aim back.</summary>
        public SprayPoint Update(CsWeapon w, float now, float dt)
        {
            if (w == null || dt <= 0f) return new SprayPoint(0, 0);
            if (now - _lastShot < w.CycleTime * 1.2f) return new SprayPoint(0, 0); // still spraying

            if (_shotIndex > 0f)
            {
                float rate = Math.Max(1f, w.Pattern.Length) / w.RecoveryTime;
                _shotIndex = Math.Max(0f, _shotIndex - rate * dt);
            }

            float newPitch = Decay(_punchPitch, dt);
            float newYaw = Decay(_punchYaw, dt);
            var delta = new SprayPoint(newYaw - _punchYaw, newPitch - _punchPitch);
            _punchPitch = newPitch;
            _punchYaw = newYaw;
            return delta;
        }

        private static float Decay(float v, float dt)
        {
            if (v == 0f) return 0f;
            float mag = Math.Abs(v);
            mag *= (float)Math.Exp(-DecayExp * dt);
            mag -= DecayLinear * dt;
            if (mag <= 0.001f) return 0f;
            return Math.Sign(v) * mag;
        }

        public void Reset() { _shotIndex = 0f; _punchPitch = 0f; _punchYaw = 0f; _lastShot = -100f; }

        /// <summary>How much the current movement should throw shots off (0 = accurate).</summary>
        public static float Inaccuracy(CsWeapon w, float horizontalSpeed, float maxSpeed, bool onGround, bool scoped)
        {
            if (w == null) return 0f;
            float factor = 0f;
            if (!onGround) factor = 2.5f;
            else if (maxSpeed > 0f)
            {
                float frac = horizontalSpeed / maxSpeed;
                if (frac > CsMovement.AccurateFraction)
                    factor = Math.Min(1f, (frac - CsMovement.AccurateFraction) / (1f - CsMovement.AccurateFraction));
            }
            if (w.Class == CsWeaponClass.Sniper && !scoped) factor += 1f;
            return factor;
        }
    }
}
