using NUnit.Framework;

namespace GTFO.AbsurdWeapons.Tests;

[TestFixture]
public class FormulasTests
{
    [TestCase(1.0f, 1.0f, true)]
    [TestCase(0.96f, 1.0f, true)]   // within the 95% leeway
    [TestCase(0.94f, 1.0f, false)]
    [TestCase(5.0f, 1.8f, true)]    // held well past full (the Gravity Hammer's never-ending charge)
    public void IsFullCharge_UsesTheLeeway(float held, float full, bool expected)
    {
        Assert.That(Formulas.IsFullCharge(held, full, 0.95f), Is.EqualTo(expected));
    }

    [Test]
    public void AreaFalloff_FullAtCenterEdgeShareAtRadius()
    {
        Assert.That(Formulas.AreaFalloff(0f, 5f, 0.25f), Is.EqualTo(1f).Within(1e-6));
        Assert.That(Formulas.AreaFalloff(5f, 5f, 0.25f), Is.EqualTo(0.25f).Within(1e-6));
        Assert.That(Formulas.AreaFalloff(2.5f, 5f, 0.25f), Is.EqualTo(0.625f).Within(1e-6));
    }

    [Test]
    public void AreaFalloff_ClampsOutsideTheRadius()
    {
        Assert.That(Formulas.AreaFalloff(9f, 5f, 0.4f), Is.EqualTo(0.4f).Within(1e-6));
        Assert.That(Formulas.AreaFalloff(1f, 0f, 0.4f), Is.EqualTo(1f));
    }

    [Test]
    public void ChainStrikeDamage_FallsOffEachJump()
    {
        Assert.That(Formulas.ChainStrikeDamage(10f, 0.75f, 0), Is.EqualTo(10f).Within(1e-5));
        Assert.That(Formulas.ChainStrikeDamage(10f, 0.75f, 1), Is.EqualTo(7.5f).Within(1e-5));
        Assert.That(Formulas.ChainStrikeDamage(10f, 0.75f, 5), Is.EqualTo(2.373f).Within(1e-3));
    }

    [TestCase(10f, 5f, 1f, 45f)]  // a 12 m charged Warhammer fling stopped after 2 m
    [TestCase(1f, 5f, 1f, 0f)]    // the first metre is free
    [TestCase(0.5f, 5f, 1f, 0f)]
    [TestCase(10f, 0f, 1f, 0f)]   // knockback without slam damage (the Gravity Hammer's pull)
    public void SlamDamage_ChargesForTheDistanceNotTravelled(float unspent, float perMeter, float free, float expected)
    {
        Assert.That(Formulas.SlamDamage(unspent, perMeter, free), Is.EqualTo(expected).Within(1e-5));
    }

    [TestCase(0, 1, 4, 1)]
    [TestCase(3, 1, 4, 0)]   // wraps forward
    [TestCase(0, -1, 4, 3)]  // wraps backward
    [TestCase(-1, 1, 4, 0)]  // holding a vanilla weapon: next starts at the first
    [TestCase(-1, -1, 4, 3)] // ...previous at the last
    [TestCase(0, 1, 1, 0)]   // only one weapon for the slot
    public void CycleIndex_WrapsAtBothEnds(int current, int step, int count, int expected)
    {
        Assert.That(Formulas.CycleIndex(current, step, count), Is.EqualTo(expected));
    }

    [Test]
    public void CycleIndex_RejectsAnEmptyList()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Formulas.CycleIndex(0, 1, 0));
    }

    [Test]
    public void EnlargedPoolLimit_NeverCompoundsAcrossLevels()
    {
        // The game sets the pools up again every level; the result must stay put (it used to go
        // 20 -> 80 -> 320 -> ...).
        var original = 20;
        var first = Formulas.EnlargedPoolLimit(original, 4f);
        var second = Formulas.EnlargedPoolLimit(original, 4f);
        Assert.That(first, Is.EqualTo(80));
        Assert.That(second, Is.EqualTo(80));
    }

    [Test]
    public void EnlargedPoolLimit_NeverShrinks()
    {
        Assert.That(Formulas.EnlargedPoolLimit(9, 1f), Is.EqualTo(9));
        Assert.That(Formulas.EnlargedPoolLimit(9, 0.5f), Is.EqualTo(9));
        Assert.That(Formulas.EnlargedPoolLimit(9, 1.5f), Is.EqualTo(14));
    }

    [Test]
    public void GrenadeFlashInterval_SpeedsUpTowardTheEnd()
    {
        Assert.That(Formulas.GrenadeFlashInterval(0f, 3f), Is.EqualTo(0.5f).Within(1e-6));
        Assert.That(Formulas.GrenadeFlashInterval(3f, 3f), Is.EqualTo(0.06f).Within(1e-6));
        var previous = float.MaxValue;
        for (var age = 0f; age <= 3f; age += 0.25f)
        {
            var interval = Formulas.GrenadeFlashInterval(age, 3f);
            Assert.That(interval, Is.LessThanOrEqualTo(previous));
            previous = interval;
        }
        Assert.That(Formulas.GrenadeFlashInterval(10f, 3f), Is.EqualTo(0.06f).Within(1e-6));
    }

    [Test]
    public void StealthBonusAfterKill_StacksToTheCap()
    {
        var bonus = 0f;
        for (var kill = 0; kill < 6; kill++) bonus = Formulas.StealthBonusAfterKill(bonus, 0.25f, 1f);
        Assert.That(bonus, Is.EqualTo(1f));
        Assert.That(Formulas.StealthBonusAfterKill(0f, 0.25f, 1f), Is.EqualTo(0.25f));
    }

    [TestCase(1.0f, 1.6f, 0.625f)]  // Tanto
    [TestCase(1.0f, 2.5f, 0.4f)]    // Shadow Dagger
    [TestCase(0.85f, 0.85f, 1.0f)]  // Odachi: slower means longer
    [TestCase(0.5f, 0f, 0.5f)]      // a zero speed leaves timing alone
    public void ScaledTiming_DividesBySpeedOnce(float value, float speed, float expected)
    {
        Assert.That(Formulas.ScaledTiming(value, speed), Is.EqualTo(expected).Within(1e-5));
    }

    [Test]
    public void AfterburnStacks_CapsAtTheMax()
    {
        var stacks = 0;
        for (var hit = 0; hit < 8; hit++) stacks = Formulas.AfterburnStacks(stacks, 5);
        Assert.That(stacks, Is.EqualTo(5));
        Assert.That(Formulas.AfterburnStacks(0, 5), Is.EqualTo(1));
    }
}
