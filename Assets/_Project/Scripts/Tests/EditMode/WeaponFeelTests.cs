using NUnit.Framework;

namespace ZombieWar.Tests
{
    /// <summary>05/10: a fast gun kicks the same per second as a slow one, so Fire Rate never turns
    /// recoil and camera shake into an unbearable stack.</summary>
    public class WeaponFeelTests
    {
        [Test]
        public void SlowGunsKeepTheirFullKick()
        {
            Assert.AreEqual(1f, Weapon.FireFeelScale(0.25f), 1e-4f);
            Assert.AreEqual(1f, Weapon.FireFeelScale(0f), 1e-4f);
        }

        [Test]
        public void KickPerSecondIsCappedForFastGuns()
        {
            float perSecondAt20 = Weapon.FireFeelScale(0.05f) * 20f;
            float perSecondAt40 = Weapon.FireFeelScale(0.025f) * 40f;
            Assert.AreEqual(Weapon.FullFeelShotsPerSecond, perSecondAt20, 1e-3f);
            Assert.AreEqual(perSecondAt20, perSecondAt40, 1e-3f);
        }
    }
}
