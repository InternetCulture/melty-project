using System;
using System.Linq;
using CSGTA;
using Xunit;

public class JoaatTests
{
    [Theory]
    [InlineData("WEAPON_PISTOL", 0x1B06D571u)]
    [InlineData("WEAPON_ASSAULTRIFLE", 0xBFEFFF6Du)]
    [InlineData("WEAPON_CARBINERIFLE", 0x83BF0278u)]
    [InlineData("WEAPON_HEAVYSNIPER", 0x0C472FE2u)]
    [InlineData("COMPONENT_AT_PI_SUPP", 0xC304849Au)]
    [InlineData("COMPONENT_AT_AR_SUPP_02", 0xA73D4664u)]
    public void MatchesGtaHashes(string name, uint expected) => Assert.Equal(expected, Joaat.Hash(name));
}

public class WeaponTableTests
{
    [Fact]
    public void EveryGtaGunIsMappedOnce()
    {
        var hashes = CsWeapons.All.Select(w => w.GtaHash).ToList();
        Assert.Equal(hashes.Count, hashes.Distinct().Count());
        Assert.Equal("AK-47", CsWeapons.ByGtaHash(Joaat.Hash("WEAPON_ASSAULTRIFLE")).Name);
        Assert.Null(CsWeapons.ByGtaHash(Joaat.Hash("WEAPON_UNARMED")));
    }

    [Fact]
    public void PatternsCoverTheMagazine()
    {
        foreach (var w in CsWeapons.All)
        {
            Assert.Equal(w.MagSize, w.Pattern.Length);
            Assert.Equal(0f, w.Pattern[0].Pitch);
            Assert.All(w.Pattern, p => Assert.True(!float.IsNaN(p.Pitch) && !float.IsNaN(p.Yaw)));
        }
    }

    [Fact]
    public void AkSprayClimbsThenSwingsLeftThenRight()
    {
        var ak = CsWeapons.All.First(w => w.Id == "ak47").Pattern;
        Assert.True(ak[8].Pitch > 5.5f && Math.Abs(ak[8].Yaw) < 0.5f);
        Assert.True(ak[11].Yaw < -1.5f);
        Assert.True(ak[18].Yaw > 2f);
    }
}

public class MovementTests
{
    const float Dt = 1f / 60f;
    static float Ms(float units) => units * CsWeapons.UnitsToMeters;

    [Fact]
    public void RunsUpToKnifeSpeedAndNoFurther()
    {
        var m = new CsMovement();
        var v = new V2(0, 0);
        var wish = new V2(0, 1);
        float top = Ms(250);
        for (int i = 0; i < 180; i++) v = m.Step(v, wish, top, true, Dt);
        Assert.InRange(v.Length, top * 0.97f, top * 1.001f);
    }

    [Fact]
    public void ReachesTopSpeedQuicklyLikeCs()
    {
        var m = new CsMovement();
        var v = new V2(0, 0);
        float top = Ms(250);
        int frames = 0;
        while (v.Length < top * 0.9f && frames < 600) { v = m.Step(v, new V2(1, 0), top, true, Dt); frames++; }
        Assert.InRange(frames * Dt, 0.2f, 0.7f);
    }

    [Fact]
    public void StopsFastWhenKeysReleased()
    {
        var m = new CsMovement();
        var v = new V2(Ms(250), 0);
        int frames = 0;
        while (v.Length > 0.01f && frames < 600) { v = m.Step(v, new V2(0, 0), 0, true, Dt); frames++; }
        Assert.InRange(frames * Dt, 0.2f, 0.8f);
    }

    [Fact]
    public void CounterStrafeBecomesAccurateFasterThanReleasing()
    {
        var m = new CsMovement();
        float top = Ms(215);
        float accurate = top * CsMovement.AccurateFraction;
        int release = 0, counter = 0;
        var v = new V2(top, 0);
        while (v.Length > accurate) { v = m.Step(v, new V2(0, 0), 0, true, Dt); release++; }
        v = new V2(top, 0);
        while (v.X > accurate) { v = m.Step(v, new V2(-1, 0), top, true, Dt); counter++; }
        Assert.True(counter < release, $"counter {counter} vs release {release}");
    }

    [Fact]
    public void AirStrafingTurnsWithoutGainingMuchWishSpeed()
    {
        var m = new CsMovement();
        var v = new V2(0, Ms(250));
        for (int i = 0; i < 30; i++) v = m.Step(v, new V2(1, 0), Ms(250), false, Dt);
        Assert.True(v.X > 0.3f, "air strafe should bend the path");
        Assert.True(v.Y >= Ms(250) * 0.999f, "no air friction");
    }

    [Fact]
    public void BunnyHoppingHasNoSpeedCap()
    {
        var m = new CsMovement();
        float run = Ms(250);
        var v = new V2(0, run);
        // Strafe-jump: air strafe each hop, land and jump again inside the window (no friction).
        for (int hop = 0; hop < 10; hop++)
        {
            for (int i = 0; i < 40; i++)
            {
                // Optimal strafe: keep the wish direction just under the air cap's worth off the velocity.
                double off = Math.Acos(Math.Min(1.0, (m.AirWishCap * 0.5) / v.Length));
                double a = Math.Atan2(v.Y, v.X) + off * (hop % 2 == 0 ? -1 : 1);
                v = m.Step(v, new V2((float)Math.Cos(a), (float)Math.Sin(a)), run, false, Dt);
            }
            v = m.Step(v, new V2(0, 0), 0, true, Dt, skipFriction: true);
        }
        Assert.True(v.Length > run * 1.3f, $"speed {v.Length} should exceed run speed {run}");
    }

    [Fact]
    public void WishDirectionFollowsGtaHeading()
    {
        var north = CsMovement.WishDirection(0, 1, 0);
        Assert.InRange(north.Y, 0.99f, 1.01f);
        var west = CsMovement.WishDirection(0, 1, 90);
        Assert.InRange(west.X, -1.01f, -0.99f);
        var strafeRightFacingNorth = CsMovement.WishDirection(1, 0, 0);
        Assert.InRange(strafeRightFacingNorth.X, 0.99f, 1.01f);
    }
}

public class RecoilTests
{
    static CsWeapon Ak => CsWeapons.All.First(w => w.Id == "ak47");

    [Fact]
    public void StandingSprayFollowsPatternExactly()
    {
        var r = new SprayRecoil();
        float pitch = 0, yaw = 0, t = 0;
        var pattern = Ak.Pattern;
        for (int i = 0; i < 29; i++)
        {
            var d = r.OnShot(Ak, t, 0f, () => 0.5f);
            pitch += d.Pitch; yaw += d.Yaw; t += Ak.CycleTime;
            Assert.InRange(pitch, pattern[i + 1].Pitch - 1e-3f, pattern[i + 1].Pitch + 1e-3f);
            Assert.InRange(yaw, pattern[i + 1].Yaw - 1e-3f, pattern[i + 1].Yaw + 1e-3f);
        }
    }

    [Fact]
    public void AimSettlesBackAfterRelease()
    {
        var r = new SprayRecoil();
        float t = 0;
        for (int i = 0; i < 10; i++) { r.OnShot(Ak, t, 0f, () => 0.5f); t += Ak.CycleTime; }
        float pitch = r.PunchPitch;
        float settled = 0;
        for (int i = 0; i < 120; i++) { t += 1f / 60f; settled += r.Update(Ak, t, 1f / 60f).Pitch; }
        Assert.InRange(pitch + settled, -0.01f, 0.01f);
        Assert.Equal(0f, r.ShotIndex);
    }

    [Fact]
    public void MovingShotsAreThrownOff()
    {
        Assert.Equal(0f, SprayRecoil.Inaccuracy(Ak, 0f, 5f, true, false));
        Assert.Equal(0f, SprayRecoil.Inaccuracy(Ak, 1.5f, 5f, true, false));
        Assert.Equal(1f, SprayRecoil.Inaccuracy(Ak, 5f, 5f, true, false));
        Assert.True(SprayRecoil.Inaccuracy(Ak, 0f, 5f, false, false) > 1f);
        var awp = CsWeapons.All.First(w => w.Id == "awp");
        Assert.Equal(1f, SprayRecoil.Inaccuracy(awp, 0f, 5f, true, false));
        Assert.Equal(0f, SprayRecoil.Inaccuracy(awp, 0f, 5f, true, true));
    }
}

public class SoundMatcherTests
{
    static readonly string[] Files =
    {
        "sounds/weapons/ak47/ak47_01.vsnd_c", "sounds/weapons/ak47/ak47_02.vsnd_c", "sounds/weapons/ak47/ak47_distant_01.vsnd_c",
        "sounds/weapons/ak47/ak47_clipout.vsnd_c", "sounds/weapons/ak47/ak47_clipin.vsnd_c", "sounds/weapons/ak47/ak47_boltpull.vsnd_c",
        "sounds/weapons/m4a1/m4a1_01.vsnd_c", "sounds/weapons/m4a1/m4a1_silencer_01.vsnd_c", "sounds/weapons/m4a1/m4a1_clipout.vsnd_c",
        "sounds/weapons/m4a1/m4a1_silencer_screw_on.vsnd_c", "sounds/weapons/awp/awp_01.vsnd_c", "sounds/weapons/awp/awp_zoom.vsnd_c",
        "sounds/player/footsteps/concrete1.vsnd_c",
    };

    [Fact]
    public void FindsAkShotsAndReload()
    {
        var m = SoundMatcher.Find(CsWeapons.All.First(w => w.Id == "ak47"), Files);
        Assert.Equal(new[] { "sounds/weapons/ak47/ak47_01.vsnd_c", "sounds/weapons/ak47/ak47_02.vsnd_c" }, m.Shots);
        Assert.Equal("sounds/weapons/ak47/ak47_clipout.vsnd_c", m.ClipOut);
        Assert.Equal("sounds/weapons/ak47/ak47_clipin.vsnd_c", m.ClipIn);
    }

    [Fact]
    public void SeparatesM4A4FromM4A1S()
    {
        var m4a4 = SoundMatcher.Find(CsWeapons.All.First(w => w.Id == "m4a4"), Files);
        Assert.Equal(new[] { "sounds/weapons/m4a1/m4a1_01.vsnd_c" }, m4a4.Shots);
        var m4a1s = SoundMatcher.Find(CsWeapons.All.First(w => w.Id == "m4a1_silencer"), Files);
        Assert.Equal(new[] { "sounds/weapons/m4a1/m4a1_silencer_01.vsnd_c" }, m4a1s.Shots);
        Assert.Equal("sounds/weapons/m4a1/m4a1_clipout.vsnd_c", m4a1s.ClipOut);
    }

    [Fact]
    public void MissingGunGivesNothing()
    {
        var m = SoundMatcher.Find(CsWeapons.All.First(w => w.Id == "negev"), Files);
        Assert.Empty(m.Shots);
        Assert.Null(m.ClipOut);
    }
}
