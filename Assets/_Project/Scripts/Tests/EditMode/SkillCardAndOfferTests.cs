using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using ZombieWar.Skills;

namespace ZombieWar.Tests
{
    /// <summary>
    /// M7.2 B2/B3/B5 — the cards and the 1-of-3 offer. M8: 23 originals + 6 powers + 6 evolutions.
    ///
    /// The offer builder is the part most likely to look fine and be subtly wrong, so its rules are
    /// asserted directly rather than only observed through play.
    /// </summary>
    public class SkillCardAndOfferTests
    {
        SkillRuntime _run;

        [SetUp]
        public void SetUp()
        {
            StatusCarrier.ClearAll();
            AutonomousPower.ResetGlobalBudget();
            _run = new SkillRuntime();
        }

        // ══════════════════════════════════════════════════ catalog integrity

        [Test]
        public void TheOwnerApprovedCardList_NothingMoreNothingLess()
        {
            var all = SkillCatalogDefs.All;
            Assert.AreEqual(35, all.Count, "23 originals + 6 M8 powers + 6 evolutions (owner, 2026-09-26)");

            var expected = new[]
            {
                "Damage Up", "Fire Rate Up", "Move Speed Up", "Max Health Up", "Coin Gain Up",
                "Run & Gun", "Quickstep Round", "Static Build-up", "Bullet Hose",
                "Focus Fire", "Breach Round", "Point Blank", "Concussion",
                "Heavy Pressure", "Shockwave Belt", "Longshot", "Hunter's Mark",
                "Chain Lightning", "Ordnance Core", "Soul Burst", "Emergency Detonation",
                "Execution Round", "Kinetic Shield",
                "Orbit Blades", "Drone Buddy", "Frost Nova", "Fire Trail", "Boomerang", "Airstrike",
                "Thunderstorm", "Carpet Bomb", "Buzzsaw Halo", "Absolute Zero", "Drone Squadron", "Reaper",
            };
            CollectionAssert.AreEquivalent(expected, all.Select(d => d.displayName).ToArray(),
                "no card may be added, removed or renamed without the owner");
        }

        [Test]
        public void EverySignatureCardBindsToAFamily_NeverToAWeaponId()
        {
            foreach (var d in SkillCatalogDefs.All.Where(d => d.layer == SkillLayer.Signature))
                Assert.IsNotNull(d.family, $"{d.displayName} must name a family");

            // Two per family, six families.
            var byFamily = SkillCatalogDefs.All.Where(d => d.layer == SkillLayer.Signature)
                .GroupBy(d => d.family.Value).ToDictionary(g => g.Key, g => g.Count());
            Assert.AreEqual(6, byFamily.Count);
            foreach (var kv in byFamily) Assert.AreEqual(2, kv.Value, $"{kv.Key} should have exactly 2 signature cards");
        }

        [Test]
        public void RanksStrengthenTheSameFantasy_Rank1UnlocksIt()
        {
            foreach (var d in SkillCatalogDefs.All)
            {
                Assert.AreEqual(0f, d.ValueAt(0), 1e-4, $"{d.displayName} must be inert at rank 0");
                Assert.AreNotEqual(0f, d.ValueAt(1), $"{d.displayName} rank 1 must unlock the behaviour");

                // Monotonic in one direction: most grow, a few (metres to walk) shrink.
                bool grows = d.ValueAt(d.maxRank) >= d.ValueAt(1);
                for (int r = 2; r <= d.maxRank; r++)
                {
                    if (grows) Assert.GreaterOrEqual(d.ValueAt(r), d.ValueAt(r - 1), $"{d.displayName} must not weaken at rank {r}");
                    else Assert.LessOrEqual(d.ValueAt(r), d.ValueAt(r - 1), $"{d.displayName} must keep shrinking at rank {r}");
                }
            }
        }

        [Test]
        public void EveryCardHasFiveRanks_ExceptEvolutions()
        {
            foreach (var d in SkillCatalogDefs.All)
                Assert.AreEqual(d.IsEvolution ? 1 : 5, d.maxRank, $"{d.displayName}");
        }

        /// Owner 2026-09-29: five ranks, and no rank may be a dead pick. A rank improves the card when
        /// its main value, its damage scale, its cooldown, its trigger interval, its threshold or its
        /// slow moves in the card's favour.
        [Test]
        public void EveryRankImprovesSomething()
        {
            foreach (var d in SkillCatalogDefs.All)
            {
                if (d.IsEvolution) continue;
                bool grows = d.ValueAt(d.maxRank) >= d.ValueAt(1);
                for (int r = 2; r <= d.maxRank; r++)
                {
                    bool better =
                        (grows ? d.ValueAt(r) > d.ValueAt(r - 1) : d.ValueAt(r) < d.ValueAt(r - 1)) ||
                        d.At("dmg", r, 1f) > d.At("dmg", r - 1, 1f) ||
                        d.At("cd", r, 0f) < d.At("cd", r - 1, 0f) ||
                        d.At("every", r, 0f) < d.At("every", r - 1, 0f) ||
                        d.At("threshold", r, 0f) > d.At("threshold", r - 1, 0f) ||
                        d.At("slow", r, 0f) > d.At("slow", r - 1, 0f);
                    Assert.IsTrue(better, $"{d.displayName} rank {r} changes nothing");
                }
            }
        }

        // ══════════════════════════════════════════════════ stat cards + soft caps

        [Test]
        public void DamageUpScalesWithRank()
        {
            float before = _run.DamageMultiplier;
            _run.Take(SkillCatalogDefs.StatDamage);
            float r1 = _run.DamageMultiplier;
            _run.Take(SkillCatalogDefs.StatDamage);
            float r2 = _run.DamageMultiplier;

            Assert.AreEqual(1f, before, 1e-4);
            Assert.Greater(r1, before);
            Assert.Greater(r2, r1);
        }

        [Test]
        public void FireRateNeverExceedsTheHardCap_HoweverManyPicks()
        {
            for (int i = 0; i < 5; i++) _run.Take(SkillCatalogDefs.StatFireRate);
            _run.EquippedFamily = WeaponClass.SMG;
            for (int i = 0; i < 3; i++) _run.Take(SkillCatalogDefs.SmgBulletHose);
            for (int i = 0; i < 40; i++) _run.Tick(0.1f, Vector3.zero, false, true, 1f);

            Assert.LessOrEqual(_run.FireRateMultiplier, SoftCap.FireRate.hard,
                "stacking stat + signature must still respect the 2.5x hard cap");
        }

        [Test]
        public void MaxHealthAppliesAtPickTime_AndIsConsumedOnce()
        {
            _run.Take(SkillCatalogDefs.StatMaxHealth);
            Assert.Greater(_run.PendingMaxHealthBonus, 0f);
            float taken = _run.ConsumeMaxHealthBonus();
            Assert.Greater(taken, 0f);
            Assert.AreEqual(0f, _run.PendingMaxHealthBonus, 1e-4, "a health grant must not be applied twice");
        }

        // ══════════════════════════════════════════════════ signature cards, by family

        [Test]
        public void PointBlankOnlyAppliesToShotguns_AndFadesWithDistance()
        {
            _run.Take(SkillCatalogDefs.ShotgunPointBlank);

            _run.EquippedFamily = WeaponClass.Shotgun;
            float close = _run.ModifyHitDamage(100f, 1, 0.5f, 1f, 0f);
            float far = _run.ModifyHitDamage(100f, 2, 12f, 1f, 0f);
            Assert.Greater(close, far, "Point Blank must reward closing distance");

            _run.EquippedFamily = WeaponClass.Marksman;
            float wrongFamily = _run.ModifyHitDamage(100f, 3, 0.5f, 1f, 0f);
            Assert.AreEqual(100f, wrongFamily, 1e-3, "a shotgun card must do nothing on a marksman rifle");
        }

        [Test]
        public void LongshotRewardsDistance_OppositeOfPointBlank()
        {
            _run.Take(SkillCatalogDefs.MarksmanLongshot);
            _run.EquippedFamily = WeaponClass.Marksman;

            float near = _run.ModifyHitDamage(100f, 1, 1f, 1f, 0f);
            float far = _run.ModifyHitDamage(100f, 2, 35f, 1f, 0f);
            Assert.Greater(far, near);
        }

        [Test]
        public void FocusFireRewardsStayingOnOneTarget()
        {
            _run.Take(SkillCatalogDefs.ArFocusFire);
            _run.EquippedFamily = WeaponClass.AssaultRifle;

            float first = _run.ModifyHitDamage(100f, targetId: 1, 5f, 1f, 0f);
            _run.ModifyHitDamage(100f, 1, 5f, 1f, 0.1f);
            float fourth = _run.ModifyHitDamage(100f, 1, 5f, 1f, 0.2f);
            float switched = _run.ModifyHitDamage(100f, targetId: 999, 5f, 1f, 0.3f);

            Assert.Greater(fourth, first, "consecutive hits on one target must ramp");
            Assert.Less(switched, fourth, "switching target must reset the ramp");
        }

        [Test]
        public void ConcussionAppliesASlowStatusRatherThanDamage()
        {
            _run.Take(SkillCatalogDefs.ShotgunConcussion);
            _run.EquippedFamily = WeaponClass.Shotgun;

            _run.ApplyHitStatuses(targetId: 55, now: 0f);
            Assert.Greater(StatusCarrier.Get(55, StatusKind.Slow, 0.5f), 0f);
            Assert.AreEqual(0f, StatusCarrier.Get(55, StatusKind.Slow, 99f), "the slow must expire");
        }

        [Test]
        public void BreachRoundFiresOnACadenceAndMarksTargetsExposed()
        {
            _run.Take(SkillCatalogDefs.ArBreach);
            _run.EquippedFamily = WeaponClass.AssaultRifle;

            int pierced = 0;
            for (int i = 0; i < 20; i++) if (_run.OnShotFired().bonusPierce > 0) pierced++;
            Assert.Greater(pierced, 0, "Breach Round must actually trigger");
            Assert.Less(pierced, 20, "it is every Nth shot, not every shot");

            _run.ApplyHitStatuses(7, 0f);
            Assert.IsTrue(StatusCarrier.Has(7, StatusKind.Exposed, 1f));
        }

        [Test]
        public void ShockwaveBeltFiresEveryNthShotOnLmgOnly()
        {
            _run.Take(SkillCatalogDefs.LmgShockwave);

            _run.EquippedFamily = WeaponClass.SMG;
            int wrong = 0;
            for (int i = 0; i < 30; i++) if (_run.OnShotFired().shockwave) wrong++;
            Assert.AreEqual(0, wrong, "an LMG card must not fire on an SMG");

            _run.EquippedFamily = WeaponClass.LMG;
            int fired = 0;
            for (int i = 0; i < 30; i++) if (_run.OnShotFired().shockwave) fired++;
            Assert.Greater(fired, 0);
        }

        [Test]
        public void RunAndGunOnlyHelpsWhileActuallyMoving()
        {
            _run.Take(SkillCatalogDefs.SidearmRunGun);
            _run.EquippedFamily = WeaponClass.Sidearm;

            for (int i = 0; i < 20; i++) _run.Tick(0.1f, Vector3.zero, isMoving: false, isFiring: true, 1f);
            float still = _run.FireRateMultiplier;

            for (int i = 0; i < 20; i++) _run.Tick(0.1f, Vector3.zero, isMoving: true, isFiring: true, 1f);
            float moving = _run.FireRateMultiplier;

            Assert.Greater(moving, still, "Run & Gun must pay out only while moving");
        }

        [Test]
        public void QuickstepEmpowersTheNextShotAfterTravelling_NoFiringRequired()
        {
            // The corrected design: distance-only trigger. Requiring "run N metres WITHOUT firing"
            // was impossible under auto-fire.
            _run.Take(SkillCatalogDefs.SidearmQuickstep);
            _run.EquippedFamily = WeaponClass.Sidearm;

            var p = Vector3.zero;
            for (int i = 0; i < 40; i++) { p += Vector3.forward * 1f; _run.Tick(0.1f, p, true, true, 1f); }

            float empowered = _run.ModifyHitDamage(100f, 1, 5f, 1f, 0f);
            float next = _run.ModifyHitDamage(100f, 2, 5f, 1f, 0f);

            Assert.Greater(empowered, next, "the charge must land on exactly one shot");
        }

        [Test]
        public void HuntersMarkOverridesTargetingAndIsConsumedByTheFirstHit()
        {
            _run.Take(SkillCatalogDefs.MarksmanHunters);
            _run.EquippedFamily = WeaponClass.Marksman;

            _run.OnTargetChanged(321, 0f);
            Assert.IsTrue(StatusCarrier.Has(321, StatusKind.Marked, 1f));

            float marked = _run.ModifyHitDamage(100f, 321, 10f, 1f, 1f);
            float after = _run.ModifyHitDamage(100f, 321, 10f, 1f, 1f);
            Assert.Greater(marked, after, "the mark empowers the first hit only");
        }

        [Test]
        public void ExecutionRoundOnlyFiresBelowItsHealthThreshold()
        {
            _run.Take(SkillCatalogDefs.UniExecution);

            float healthy = _run.ModifyHitDamage(100f, 1, 5f, 0.9f, 0f);
            float wounded = _run.ModifyHitDamage(100f, 2, 5f, 0.1f, 0f);

            Assert.AreEqual(100f, healthy, 1e-3);
            Assert.Greater(wounded, 100f);
        }

        [Test]
        public void ExecutionRoundIsUniversal_ItWorksOnEveryFamily()
        {
            _run.Take(SkillCatalogDefs.UniExecution);
            foreach (WeaponClass fam in new[] { WeaponClass.Sidearm, WeaponClass.SMG, WeaponClass.Shotgun,
                                                WeaponClass.AssaultRifle, WeaponClass.Marksman, WeaponClass.LMG })
            {
                _run.EquippedFamily = fam;
                Assert.Greater(_run.ModifyHitDamage(100f, 1, 5f, 0.1f, 0f), 100f, $"must work on {fam}");
            }
        }

        [Test]
        public void KineticShieldBlocksExactlyOneHitPerCharge()
        {
            _run.Take(SkillCatalogDefs.UniKinetic);
            Assert.IsFalse(_run.TryAbsorbDamage(), "uncharged shield blocks nothing");

            var p = Vector3.zero;
            for (int i = 0; i < 80; i++) { p += Vector3.forward; _run.Tick(0.1f, p, true, false, 1f); }

            Assert.IsTrue(_run.TryAbsorbDamage(), "charged shield blocks a hit");
            Assert.IsFalse(_run.TryAbsorbDamage(), "and only one");
        }

        // ══════════════════════════════════════════════════ autonomous powers

        [Test]
        public void AutonomousPowersProcThroughTheSharedFramework()
        {
            _run.Take(SkillCatalogDefs.AutoChainLightning);
            var procs = _run.PollPowers(0f, 1f);
            Assert.AreEqual(1, procs.Count);
            Assert.AreEqual(SkillCatalogDefs.AutoChainLightning, procs[0].skillId);
            Assert.LessOrEqual(procs[0].targets, TargetQuery.MaxChain, "never more than 6 arcs");
        }

        [Test]
        public void SoulBurstNeedsKills_NotATimer()
        {
            _run.Take(SkillCatalogDefs.AutoSoulBurst);
            Assert.AreEqual(0, _run.PollPowers(0f, 1f).Count);
            for (int i = 0; i < 12; i++) _run.OnKill();
            Assert.AreEqual(1, _run.PollPowers(1f, 1f).Count);
        }

        [Test]
        public void EmergencyDetonationOnlyFiresWhenHurt()
        {
            _run.Take(SkillCatalogDefs.AutoEmergency);
            Assert.AreEqual(0, _run.PollPowers(0f, 1f).Count, "not while healthy");
            Assert.AreEqual(1, _run.PollPowers(1f, 0.1f).Count, "fires when low");
        }

        [Test]
        public void GlobalProcCeilingHoldsWithEveryPowerTakenAtOnce()
        {
            // The guardrail that matters under load: many powers active must not multiply procs.
            _run.Take(SkillCatalogDefs.AutoChainLightning);
            _run.Take(SkillCatalogDefs.AutoOrdnance);
            _run.Take(SkillCatalogDefs.AutoSoulBurst);
            _run.Take(SkillCatalogDefs.AutoEmergency);
            for (int i = 0; i < 30; i++) _run.OnKill();

            int total = 0;
            for (float t = 0f; t < 1f; t += 0.1f) total += _run.PollPowers(t, 0.1f).Count;

            Assert.LessOrEqual(total, (int)AutonomousPower.GlobalProcsPerSecond,
                "no combination of powers may exceed the global procs-per-second ceiling");
        }

        // ══════════════════════════════════════════════════ offer rules

        [Test]
        public void OfferIsDeterministicFromTheRunSeed()
        {
            var a = SkillOfferBuilder.Build(_run, WeaponClass.Sidearm, runSeed: 12345, level: 3);
            var b = SkillOfferBuilder.Build(new SkillRuntime(), WeaponClass.Sidearm, runSeed: 12345, level: 3);

            CollectionAssert.AreEqual(a.Select(d => d.id).ToArray(), b.Select(d => d.id).ToArray(),
                "the same seed and level must always produce the same offer");
        }

        [Test]
        public void DifferentSeedsGiveDifferentOffers()
        {
            var seen = new HashSet<string>();
            for (int seed = 0; seed < 12; seed++)
                seen.Add(string.Join(",", SkillOfferBuilder.Build(new SkillRuntime(), WeaponClass.SMG, seed, 2)
                    .Select(d => d.id)));
            Assert.Greater(seen.Count, 1, "offers must actually vary across runs");
        }

        [Test]
        public void AtMostOneStatCardPerOffer()
        {
            for (int seed = 0; seed < 200; seed++)
                for (int level = 1; level <= 8; level++)
                {
                    var offer = SkillOfferBuilder.Build(new SkillRuntime(), WeaponClass.AssaultRifle, seed, level);
                    int stats = offer.Count(d => d.layer == SkillLayer.Stat);
                    Assert.LessOrEqual(stats, 1, $"seed {seed} level {level} offered {stats} stat cards");
                }
        }

        [Test]
        public void NeverOffersAnIncompatibleCard()
        {
            foreach (WeaponClass fam in new[] { WeaponClass.Sidearm, WeaponClass.SMG, WeaponClass.Shotgun,
                                                WeaponClass.AssaultRifle, WeaponClass.Marksman, WeaponClass.LMG })
                for (int seed = 0; seed < 60; seed++)
                {
                    var offer = SkillOfferBuilder.Build(new SkillRuntime(), fam, seed, 3);
                    foreach (var d in offer)
                        Assert.IsTrue(d.IsCompatibleWith(fam),
                            $"{d.displayName} was offered to a {fam} build");
                }
        }

        [Test]
        public void NeverOffersAMaxRankCard()
        {
            var run = new SkillRuntime();
            // Max out everything a sidearm build can hold.
            foreach (var d in SkillCatalogDefs.All.Where(d => d.IsCompatibleWith(WeaponClass.Sidearm)))
                for (int i = 0; i < d.maxRank; i++) run.Take(d.id);

            // Owner 2026-09-29: the offer stays at three; what fills the gap is overflow, never a maxed card.
            var offer = SkillOfferBuilder.Build(run, WeaponClass.Sidearm, 99, 20);
            Assert.AreEqual(3, offer.Count);
            Assert.IsTrue(offer.All(d => d.IsOverflow || run.RankOf(d.id) < d.maxRank), "a maxed card was offered");
        }

        [Test]
        public void ExhaustedPool_OffersOverflow_AndAutoPickTakesOne()
        {
            var run = new SkillRuntime();
            foreach (var d in SkillCatalogDefs.All.Where(d => d.IsCompatibleWith(WeaponClass.LMG)))
                for (int i = 0; i < d.maxRank; i++) run.Take(d.id);
            foreach (var d in SkillCatalogDefs.All.Where(d => d.IsEvolution)) run.Take(d.id);

            var offer = SkillOfferBuilder.Build(run, WeaponClass.LMG, 1, 30);
            Assert.AreEqual(3, offer.Count);
            Assert.IsTrue(offer.All(d => d.IsOverflow), "only overflow is left once the build cannot grow");
            var picked = SkillOfferBuilder.AutoPick(offer, run);
            Assert.IsNotNull(picked);
            Assert.IsTrue(run.Take(picked.id), "an overflow card is always takeable");
            Assert.IsNull(SkillOfferBuilder.AutoPick(new List<SkillDef>(), run), "an empty offer still auto-picks nothing");
        }

        [Test]
        public void TimeoutAutoPickAlwaysReturnsAValidTakeableCard()
        {
            for (int seed = 0; seed < 100; seed++)
            {
                var run = new SkillRuntime();
                var offer = SkillOfferBuilder.Build(run, WeaponClass.Shotgun, seed, 2);
                if (offer.Count == 0) continue;

                var picked = SkillOfferBuilder.AutoPick(offer, run);
                Assert.IsNotNull(picked, $"seed {seed}: auto-pick must choose something from a non-empty offer");
                Assert.IsTrue(run.Take(picked.id), $"seed {seed}: the auto-picked card must be takeable");
            }
        }

        [Test]
        public void OfferGivesThreeChoicesWhileThePoolAllows()
        {
            var offer = SkillOfferBuilder.Build(_run, WeaponClass.AssaultRifle, 7, 1);
            Assert.AreEqual(3, offer.Count, "a fresh run must get a full 1-of-3");
            CollectionAssert.AllItemsAreUnique(offer.Select(d => d.id).ToArray(), "no duplicate card in one offer");
        }

        [Test]
        public void EarlyLevelsLeanTowardUnlockingNewMechanics()
        {
            // Bias check, not determinism: across many seeds, early offers should contain more
            // unowned cards than late offers do for the same build.
            var run = new SkillRuntime();
            run.Take(SkillCatalogDefs.StatDamage);
            run.Take(SkillCatalogDefs.AutoChainLightning);

            int earlyNew = 0, lateNew = 0;
            for (int seed = 0; seed < 80; seed++)
            {
                earlyNew += SkillOfferBuilder.Build(run, WeaponClass.SMG, seed, 1).Count(d => run.RankOf(d.id) == 0);
                lateNew += SkillOfferBuilder.Build(run, WeaponClass.SMG, seed, 12).Count(d => run.RankOf(d.id) == 0);
            }
            Assert.Greater(earlyNew, lateNew, "early offers should favour new mechanics, later ones rank-ups");
        }

        [Test]
        public void EligiblePoolShrinksAsCardsAreMaxed()
        {
            int before = SkillOfferBuilder.EligiblePool(_run, WeaponClass.Sidearm).Count;
            for (int i = 0; i < 5; i++) _run.Take(SkillCatalogDefs.StatDamage);
            int after = SkillOfferBuilder.EligiblePool(_run, WeaponClass.Sidearm).Count;
            Assert.AreEqual(before - 1, after, "a maxed card leaves the pool");
        }

        // ══════════════════════════════════════════════════ M8: evolutions

        [Test]
        public void EveryEvolutionNamesAPowerAndAPartnerThatExist()
        {
            foreach (var evo in SkillCatalogDefs.All.Where(d => d.IsEvolution))
            {
                var power = SkillCatalogDefs.ById(evo.evolvesFrom);
                Assert.IsNotNull(power, $"{evo.displayName} evolves a missing power");
                Assert.AreEqual(SkillLayer.Autonomous, power.layer, $"{evo.displayName} must evolve a power");
                Assert.IsNotNull(SkillCatalogDefs.ById(evo.partner), $"{evo.displayName} needs a real partner");
                Assert.AreSame(evo, SkillCatalogDefs.EvolutionOf(evo.evolvesFrom));
            }
        }

        [Test]
        public void AnEvolutionNeedsTheMaxedPowerAndItsPartner()
        {
            var evo = SkillCatalogDefs.ById(SkillCatalogDefs.EvoThunderstorm);
            Assert.IsFalse(_run.Take(evo.id), "not available from nothing");

            while (_run.Take(SkillCatalogDefs.AutoChainLightning)) { }
            Assert.IsFalse(_run.CanEvolve(evo), "a maxed power alone is not enough");
            Assert.IsFalse(_run.Take(evo.id));

            _run.Take(SkillCatalogDefs.StatFireRate);
            Assert.IsTrue(_run.CanEvolve(evo), "max power + partner owned");
            Assert.IsTrue(_run.Take(evo.id));
            Assert.IsTrue(_run.IsEvolved(SkillCatalogDefs.AutoChainLightning));
            Assert.IsFalse(_run.CanEvolve(evo), "an evolution is taken once");
        }

        [Test]
        public void AReadyEvolutionAlwaysTakesSlotA()
        {
            for (int seed = 0; seed < 50; seed++)
            {
                var run = new SkillRuntime();
                while (run.Take(SkillCatalogDefs.AutoOrdnance)) { }
                run.Take(SkillCatalogDefs.StatDamage);
                var offer = SkillOfferBuilder.Build(run, WeaponClass.Sidearm, seed, 9);
                Assert.AreEqual(SkillCatalogDefs.EvoCarpetBomb, offer[0].id, $"seed {seed}: the payoff must be offered");
            }
        }

        [Test]
        public void EvolutionsAreNeverOfferedBeforeTheyAreEarned()
        {
            for (int seed = 0; seed < 100; seed++)
                for (int level = 1; level <= 10; level++)
                    Assert.IsFalse(SkillOfferBuilder.Build(new SkillRuntime(), WeaponClass.SMG, seed, level)
                        .Any(d => d.IsEvolution), $"seed {seed} level {level}");
        }

        [Test]
        public void LevelsTwoAndThreeAlwaysOfferAnAutonomousPower()
        {
            for (int seed = 0; seed < 200; seed++)
                for (int level = 2; level <= 3; level++)
                    Assert.IsTrue(SkillOfferBuilder.Build(new SkillRuntime(), WeaponClass.Shotgun, seed, level)
                        .Any(d => d.layer == SkillLayer.Autonomous), $"seed {seed} level {level}");
        }

        // ══════════════════════════════════════════════════ M8: power magnitudes

        [Test]
        public void PowerDamageGrowsWithRankDamageUpAndEvolution()
        {
            for (int i = 0; i < 1; i++) _run.Take(SkillCatalogDefs.AutoOrbit);
            float r1 = _run.PowerDamage(10f, SkillCatalogDefs.AutoOrbit);
            _run.Take(SkillCatalogDefs.AutoOrbit);
            float r2 = _run.PowerDamage(10f, SkillCatalogDefs.AutoOrbit);
            _run.Take(SkillCatalogDefs.StatDamage);
            float withDamageUp = _run.PowerDamage(10f, SkillCatalogDefs.AutoOrbit);
            while (_run.Take(SkillCatalogDefs.AutoOrbit)) { }
            _run.Take(SkillCatalogDefs.StatMoveSpeed);
            _run.Take(SkillCatalogDefs.EvoBuzzsaw);
            float evolved = _run.PowerDamage(10f, SkillCatalogDefs.AutoOrbit);

            Assert.Greater(r2, r1, "rank must add damage, not only cooldown");
            Assert.Greater(withDamageUp, r2, "Damage Up must feed powers too");
            Assert.Greater(evolved, withDamageUp);
            Assert.AreEqual(6, _run.OrbitBladeCount, "Buzzsaw Halo is six blades");
        }

        [Test]
        public void OrbitAndDroneCountsFollowRank()
        {
            Assert.AreEqual(0, _run.OrbitBladeCount);
            int[] blades = { 2, 2, 3, 3, 4 };                 // rank 1..5
            for (int r = 1; r <= 5; r++)
            {
                _run.Take(SkillCatalogDefs.AutoOrbit);
                Assert.AreEqual(blades[r - 1], _run.OrbitBladeCount, $"rank {r}");
            }

            Assert.AreEqual(0, _run.DroneCount);
            _run.Take(SkillCatalogDefs.AutoDrone);
            Assert.AreEqual(1, _run.DroneCount);
        }

        [Test]
        public void FireTrailPaysOutByDistance_NotByStandingStill()
        {
            _run.Take(SkillCatalogDefs.AutoFireTrail);
            for (int i = 0; i < 30; i++) _run.Tick(0.1f, Vector3.zero, false, false, 1f);
            Assert.AreEqual(0, _run.ConsumeFireTrailDrops(), "standing still leaves no fire");

            var p = Vector3.zero;
            for (int i = 0; i < 10; i++) { p += Vector3.forward * 0.5f; _run.Tick(0.1f, p, true, false, 1f); }
            int expected = Mathf.FloorToInt(5f / SkillRuntime.FireTrailSpacing);
            Assert.AreEqual(expected, _run.ConsumeFireTrailDrops(), $"5 m walked at {SkillRuntime.FireTrailSpacing} m spacing = {expected} patches");
        }

        [Test]
        public void NewBurstPowersProcThroughTheSharedFramework()
        {
            _run.Take(SkillCatalogDefs.AutoFrostNova);
            _run.Take(SkillCatalogDefs.AutoBoomerang);
            _run.Take(SkillCatalogDefs.AutoAirstrike);
            var ids = _run.PollPowers(0f, 1f).Select(p => p.skillId).ToArray();
            CollectionAssert.AreEquivalent(new[] { SkillCatalogDefs.AutoFrostNova, SkillCatalogDefs.AutoBoomerang,
                                                   SkillCatalogDefs.AutoAirstrike }, ids);
            Assert.AreEqual(0, _run.PollPowers(0.5f, 1f).Count, "each waits for its own cooldown");
        }

        [Test]
        public void ReaperOnlyRollsOnceEvolved_AndAboutAQuarterOfKills()
        {
            Assert.IsFalse(_run.RollReaper());
            while (_run.Take(SkillCatalogDefs.AutoSoulBurst)) { }
            _run.Take(SkillCatalogDefs.UniExecution);
            _run.Take(SkillCatalogDefs.EvoReaper);
            int hits = 0;
            for (int i = 0; i < 1000; i++) if (_run.RollReaper()) hits++;
            Assert.That(hits, Is.InRange(180, 320));
        }

        [Test]
        public void EveryCardHasASpecificDescriptionAtEveryRank()
        {
            foreach (var d in SkillCatalogDefs.All)
                for (int r = 1; r <= d.maxRank; r++)
                {
                    string text = SkillDescriptions.Describe(d, r);
                    Assert.IsNotEmpty(text, d.displayName);
                    Assert.AreNotEqual(d.displayName, text, $"{d.displayName} fell through to its bare name");
                    StringAssert.DoesNotContain("Automatic power", text, "the generic line is gone");
                }
        }

        [Test]
        public void SidearmBuildCannotBeOfferedShotgunOrLmgCards()
        {
            var pool = SkillOfferBuilder.EligiblePool(_run, WeaponClass.Sidearm);
            Assert.IsFalse(pool.Any(d => d.id == SkillCatalogDefs.ShotgunPointBlank));
            Assert.IsFalse(pool.Any(d => d.id == SkillCatalogDefs.LmgShockwave));
            Assert.IsTrue(pool.Any(d => d.id == SkillCatalogDefs.SidearmRunGun));
            Assert.IsTrue(pool.Any(d => d.id == SkillCatalogDefs.UniExecution), "universals are always eligible");
        }
    }
}
