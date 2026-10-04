using NUnit.Framework;

namespace ZombieWar.Tests
{
    /// <summary>05/10 genre rules 5 and 9: drops answer the player's need, and a player low on
    /// health is rescued in time, but not so often that it looks scripted.</summary>
    public class NeedDropsTests
    {
        static NeedDrops.Context At(float health, int crowd = 0, bool danger = false, int orbs = 0, bool elite = false) =>
            new NeedDrops.Context { healthFraction = health, crowdNear = crowd, dangerNear = danger, looseOrbs = orbs, elite = elite };

        [Test]
        public void AFullHealthPlayerNeverGetsAHealFromLuck()
        {
            var d = new NeedDrops();
            for (int i = 0; i < 200; i++) Assert.IsFalse(d.OnKill(At(1f), i, 0f, 1f).heal);
        }

        [Test]
        public void HalfHealthForcesAHealAfterThePityKills()
        {
            var d = new NeedDrops();
            bool healed = false;
            for (int i = 0; i < NeedDrops.HealPityKills; i++) healed |= d.OnKill(At(0.45f), i, 1f, 1f).heal;
            Assert.IsTrue(healed);
        }

        [Test]
        public void ALowPlayerIsRescuedWithinTheForceWindow_ButNotTwiceInTheCooldown()
        {
            var d = new NeedDrops();
            float t = 0f;
            bool rescued = false;
            for (; t <= NeedDrops.RescueForceAfter + 0.1f && !rescued; t += 0.1f) rescued = d.Tick(0.1f, t);
            Assert.IsTrue(rescued, "no kill came, so a heal is placed by the player");
            for (float u = t; u < t + NeedDrops.RescueCooldown - 1f; u += 0.1f) Assert.IsFalse(d.Tick(0.1f, u));
        }

        [Test]
        public void RescuesStopAtTheRunCap()
        {
            var d = new NeedDrops();
            int rescues = 0;
            for (float t = 0f; t < 600f; t += 0.1f) if (d.Tick(0.1f, t)) rescues++;
            Assert.AreEqual(NeedDrops.MaxRescues, rescues);
        }

        [Test]
        public void TheItemAnswersTheBiggestNeed()
        {
            Assert.AreEqual(PickupEffect.Bomb, NeedDrops.BestItem(At(1f, crowd: 16), out _));
            Assert.AreEqual(PickupEffect.Freeze, NeedDrops.BestItem(At(1f, danger: true), out _));
            Assert.AreEqual(PickupEffect.Magnet, NeedDrops.BestItem(At(1f, orbs: 90), out _));
        }

        [Test]
        public void AnEliteAlwaysDropsAnItem_ANormalKillRespectsTheCooldown()
        {
            var d = new NeedDrops();
            Assert.AreNotEqual(PickupEffect.Currency, d.OnKill(At(1f, elite: true), 0f, 1f, 1f).item);
            Assert.AreEqual(PickupEffect.Currency, d.OnKill(At(1f, crowd: 20), 1f, 1f, 0f).item, "inside the item cooldown");
            Assert.AreEqual(PickupEffect.Bomb, d.OnKill(At(1f, crowd: 20), NeedDrops.ItemCooldown + 1f, 1f, 0f).item);
        }
    }
}
