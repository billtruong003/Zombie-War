using NUnit.Framework;
using ZombieWar.Online;

namespace ZombieWar.Tests
{
    public class IapStoreTests
    {
        [Test]
        public void ACheckedPurchase_IsGrantedOnce()
        {
            Assert.AreEqual(IapStore.Verdict.Grant, IapStore.Decide(200, duplicate: false, grantedHere: false));
            Assert.AreEqual(IapStore.Verdict.AlreadyGranted, IapStore.Decide(200, duplicate: true, grantedHere: true));
        }

        [Test]
        public void AServerReplay_StillGrantsWhenThisDeviceNeverDid()
        {
            // The server recorded the token but the app died before granting: grant now.
            Assert.AreEqual(IapStore.Verdict.Grant, IapStore.Decide(200, duplicate: true, grantedHere: false));
        }

        [Test]
        public void NoPlayKeyOnTheServer_TrustsTheStore()
        {
            Assert.AreEqual(IapStore.Verdict.Grant, IapStore.Decide(501, false, false));
            Assert.AreEqual(IapStore.Verdict.AlreadyGranted, IapStore.Decide(501, false, true));
        }

        [Test]
        public void UnpaidOrStolenTokens_AreRefused()
        {
            Assert.AreEqual(IapStore.Verdict.Refuse, IapStore.Decide(402, false, false));
            Assert.AreEqual(IapStore.Verdict.Refuse, IapStore.Decide(409, false, false));
        }

        [Test]
        public void NoAnswer_KeepsThePurchasePending()
        {
            foreach (long code in new long[] { 0, 401, 500, 503 })
                Assert.AreEqual(IapStore.Verdict.TryLater, IapStore.Decide(code, false, false), "code " + code);
        }

        [Test]
        public void TheCatalog_MatchesTheShopAndThePass()
        {
            foreach (var (id, _) in IapStore.Catalog)
                Assert.IsTrue(id == PassRewards.PremiumProductId || ShopOffers.FindPack(id) != null, id + " is sold but has no contents");
            foreach (var p in ShopOffers.Packs)
                Assert.IsTrue(System.Array.Exists(IapStore.Catalog, c => c.id == p.id), p.id + " is in the shop but not in the store catalog");
        }
    }
}
