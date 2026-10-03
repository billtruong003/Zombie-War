using NUnit.Framework;
using ZombieWar.Stations;

namespace ZombieWar.Tests
{
    /// <summary>FTUE v2 rules that do not need a scene (owner-approved mockup 03/10).</summary>
    public sealed class FtueV2Tests
    {
        [Test]
        public void StepKeysAreDistinctPerKind()
        {
            Assert.AreEqual("station.SignalRelay", Ftue.Station(StationKind.SignalRelay));
            Assert.AreNotEqual(Ftue.Station(StationKind.SupplyCache), Ftue.Station(StationKind.SupplyDrop));
            Assert.AreEqual("item.Magnet", Ftue.Item(PickupEffect.Magnet));
            Assert.AreNotEqual(Ftue.Item(PickupEffect.Bomb), Ftue.Item(PickupEffect.Freeze));
            Assert.AreEqual("unlock.3", Ftue.Unlock(3));
        }

        [Test]
        public void FreeReviveCountsButKeepsTheAdRevive()
        {
            ReviveRules.ResetForTests();
            ReviveRules.UseFree();
            Assert.AreEqual(1, ReviveRules.Used);
            Assert.IsTrue(ReviveRules.AdAvailable, "the first free revive must not spend the run's ad revive");
            ReviveRules.UseAd();
            Assert.AreEqual(2, ReviveRules.Used);
            Assert.IsFalse(ReviveRules.AdAvailable);
            ReviveRules.ResetForTests();
        }
    }
}
