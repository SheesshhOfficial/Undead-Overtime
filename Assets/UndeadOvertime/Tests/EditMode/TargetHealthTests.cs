using System;
using NUnit.Framework;

namespace UndeadOvertime.Prototype.Tests
{
    public sealed class TargetHealthTests
    {
        [Test]
        public void FourHitsKillExactlyOnce()
        {
            var health = new TargetHealth(100);
            Assert.That(health.ApplyDamage(25), Is.False);
            Assert.That(health.ApplyDamage(25), Is.False);
            Assert.That(health.ApplyDamage(25), Is.False);
            Assert.That(health.Current, Is.EqualTo(25));
            Assert.That(health.ApplyDamage(25), Is.True);
            Assert.That(health.Current, Is.Zero);
            Assert.That(health.IsAlive, Is.False);
            Assert.That(health.ApplyDamage(25), Is.False);
        }

        [Test]
        public void OverkillCannotOverflowOrProduceNegativeHealth()
        {
            var health = new TargetHealth(100);
            Assert.That(health.ApplyDamage(int.MaxValue), Is.True);
            Assert.That(health.Current, Is.Zero);
        }

        [Test]
        public void ResetRestoresHealthAndAnotherDeathTransition()
        {
            var health = new TargetHealth(100);
            health.ApplyDamage(100);
            health.Reset();
            Assert.That(health.Current, Is.EqualTo(100));
            Assert.That(health.IsAlive, Is.True);
            Assert.That(health.ApplyDamage(100), Is.True);
        }

        [Test]
        public void ZeroDamageDoesNothing()
        {
            var health = new TargetHealth(100);
            Assert.That(health.ApplyDamage(0), Is.False);
            Assert.That(health.Current, Is.EqualTo(100));
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void NonPositiveMaximumIsRejected(int maximum)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new TargetHealth(maximum));
        }

        [Test]
        public void NegativeDamageIsRejectedWithoutChangingHealth()
        {
            var health = new TargetHealth(100);
            Assert.Throws<ArgumentOutOfRangeException>(() => health.ApplyDamage(-1));
            Assert.That(health.Current, Is.EqualTo(100));
        }
    }
}
