using System;

namespace LosSantosStrike
{
    public struct V2
    {
        public float X, Y;
        public V2(float x, float y) { X = x; Y = y; }
        public float Length => (float)Math.Sqrt(X * X + Y * Y);
        public static V2 operator +(V2 a, V2 b) => new V2(a.X + b.X, a.Y + b.Y);
        public static V2 operator *(V2 a, float s) => new V2(a.X * s, a.Y * s);
        public static float Dot(V2 a, V2 b) => a.X * b.X + a.Y * b.Y;
        public V2 Normalized() { float l = Length; return l > 1e-5f ? new V2(X / l, Y / l) : new V2(0, 0); }
        public override string ToString() => $"({X:0.00}, {Y:0.00})";
    }

    /// <summary>
    /// Source-engine ground and air movement (friction, acceleration, air strafing), the model
    /// Counter-Strike 2 uses, with CS2's default tuning. Works in metres per second.
    /// </summary>
    public sealed class CsMovement
    {
        public float Accelerate = 5.5f;      // sv_accelerate
        public float AirAccelerate = 12f;    // sv_airaccelerate
        public float Friction = 5.2f;        // sv_friction
        public float StopSpeed = 80f * CsWeapons.UnitsToMeters;  // sv_stopspeed
        public float AirWishCap = 30f * CsWeapons.UnitsToMeters; // air wish speed clamp
        public const float WalkFraction = 0.52f;   // holding walk (shift in CS)
        public const float DuckFraction = 0.34f;   // crouched
        public const float AccurateFraction = 0.34f; // at or below this share of max speed shots stay accurate

        /// <summary>Advance one frame. wishDir is a unit vector or zero; speeds in m/s.</summary>
        public V2 Step(V2 velocity, V2 wishDir, float wishSpeed, bool onGround, float dt)
        {
            if (dt <= 0f) return velocity;
            if (onGround)
            {
                velocity = ApplyFriction(velocity, dt);
                return Accel(velocity, wishDir, wishSpeed, wishSpeed, Accelerate, dt);
            }
            float capped = Math.Min(wishSpeed, AirWishCap);
            return Accel(velocity, wishDir, capped, wishSpeed, AirAccelerate, dt);
        }

        private V2 ApplyFriction(V2 v, float dt)
        {
            float speed = v.Length;
            if (speed < 0.01f) return new V2(0, 0);
            float control = speed < StopSpeed ? StopSpeed : speed;
            float drop = control * Friction * dt;
            float newSpeed = Math.Max(0f, speed - drop);
            return v * (newSpeed / speed);
        }

        private static V2 Accel(V2 v, V2 wishDir, float wishSpeed, float accelBase, float accel, float dt)
        {
            if (wishSpeed <= 0f || (wishDir.X == 0f && wishDir.Y == 0f)) return v;
            float current = V2.Dot(v, wishDir);
            float add = wishSpeed - current;
            if (add <= 0f) return v;
            float accelSpeed = Math.Min(accel * dt * accelBase, add);
            return v + wishDir * accelSpeed;
        }

        /// <summary>Counter-Strike's top running speed with this gun (knife speed when unarmed).</summary>
        public static float MaxSpeedMeters(CsWeapon weapon, bool armed, bool scoped)
        {
            float units = weapon == null ? (armed ? CsWeapons.UnmappedGunSpeed : CsWeapons.KnifeSpeed)
                                         : (scoped && weapon.ScopedSpeed > 0f ? weapon.ScopedSpeed : weapon.MaxSpeed);
            return units * CsWeapons.UnitsToMeters;
        }

        /// <summary>World-space wish direction from stick/keys (x right, y forward) and camera heading (GTA degrees).</summary>
        public static V2 WishDirection(float inputRight, float inputForward, float cameraHeadingDeg)
        {
            var input = new V2(inputRight, inputForward);
            if (input.Length < 0.05f) return new V2(0, 0);
            double h = cameraHeadingDeg * Math.PI / 180.0;
            var forward = new V2((float)-Math.Sin(h), (float)Math.Cos(h));
            var right = new V2((float)Math.Cos(h), (float)Math.Sin(h));
            return (forward * inputForward + right * inputRight).Normalized();
        }
    }
}
