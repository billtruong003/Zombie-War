using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ZombieWar.Skills;
using ZombieWar.Stations;

namespace ZombieWar.Dev
{
    /// The twenty key-art reference shots (2026-09-30). Each one is a real moment of the game:
    /// the hero and outfits as they are, the enemy roster at its true scale, stations, chests,
    /// evolutions. Renders go to Review/KeyArt/concepts_v2.
    public sealed partial class SkillSandbox
    {
        const string Shark = "casual.pro.set.011";
        static readonly string[] Small = { "ZD_DogPup", "ZD_CatMeow", "ZD_Cacti", "ZD_Burrow", "ZD_CatBolt", "ZD_CatLightning" };
        static readonly string[] Medium = { "ZD_DogBark", "ZD_DogBowwow", "ZD_Cactus", "ZD_MoleRat", "ZD_Skeleton", "ZD_SkeletonMage" };
        static readonly string[] Mixed = { "ZD_Skeleton", "ZD_DogPup", "ZD_Cactus", "ZD_CatMeow", "ZD_MoleRat", "ZD_DogBark", "ZD_Cacti", "ZD_CatBolt", "ZD_Burrow", "ZD_DogBowwow" };

        /// Hides small props and grass near the player so nothing grows through a close shot.
        public void ClearFoliage(float radius)
        {
            var p = PlayerMovement.Instance; if (p == null) return;
            Vector3 c = p.transform.position;
            foreach (var r in FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                if (r.GetComponentInParent<PlayerMovement>() != null || r.GetComponentInParent<ZombieBase>() != null
                    || r.GetComponentInParent<Station>() != null || r.GetComponentInParent<Pickup>() != null) continue;
                var b = r.bounds;
                // Grass and trees are baked into one "Foliage" mesh per map chunk: hide the chunks near
                // the hero whole; farther chunks keep the horizon's trees.
                if (r.name == "Foliage") { var cc = c; cc.y = b.center.y; if (b.SqrDistance(cc) < radius * radius * 0.25f) r.enabled = false; continue; }
                if (b.size.y > 3f || b.size.x > 6f) continue;   // the ground and big set pieces stay
                Vector3 d = b.center - c; d.y = 0f;
                if (d.magnitude < radius) r.enabled = false;
            }
        }

        IEnumerator Prepare(string set, WeaponClass gun, float clear = 14f)
        {
            ResetSkills();
            ClearEnemies();
            ClearProps();
            DressForShot(set);
            EquipFamily(gun);
            CombatForShot(false);
            ClearFoliage(clear);
            Time.timeScale = 1f;
            yield return new WaitForSecondsRealtime(2.5f);   // splats and numbers of the last shot fade
            ClearFx();
        }

        /// Lets the hero turn and aim at the nearest enemy, holding fire.
        IEnumerator Aim(float seconds = 1.2f)
        {
            // The body turns to the nearest enemy on its own; the gun stays off so no one dies and no
            // flash, splat or number lands in the frame.
            CombatForShot(false);
            yield return new WaitForSeconds(seconds);
            ClearFx();
        }

        /// Stops every loose particle effect (spawn puffs, death clouds) so only the shot's own FX show.
        public void ClearFx()
        {
            foreach (var ps in FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None))
                if (ps.GetComponentInParent<PlayerMovement>() == null && ps.GetComponentInParent<Station>() == null)
                    ps.Clear(true);
        }

        void FirePower(string powerId, float radius, int targets)
        {
            const System.Reflection.BindingFlags F = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance;
            var apply = typeof(SkillCombatDriver).GetMethod("Apply", F);
            var proc = new SkillRuntime.PowerProc { skillId = powerId, targets = targets, radius = radius };
            apply?.Invoke(SkillCombatDriver.Instance, new object[] { SkillRuntime.Active, proc, PlayerMovement.Instance.transform.position });
        }

        Vector3 Face(Vector3 from) => new Vector3(from.x, 0f, from.z);

        public IEnumerator KeyArtBatch(int only = 0)
        {
            Shooting = true;
            _panelOpen = false;
            SetGod(true);
            SetMode(EnemyMode.Dummies);
            var st = StationDirector.Instance;
            var pk = PickupManager.Instance;

            // C01 turnaround
            if (only == 0 || only == 1)
            {
                yield return Prepare(Shark, WeaponClass.AssaultRifle);
                yield return Turnaround("c01_turn", new float[] { 180, 145, 90, 0 }, new Vector3(0, 1.4f, -4.6f), new Vector3(0, 0.8f, 0), 27f, 900, 1200);
                Shooting = true;
            }
            // C02 outfit lineup (shark in the middle when composited)
            if (only == 0 || only == 2)
            {
                yield return Prepare(Shark, WeaponClass.AssaultRifle);
                yield return Lineup("c02_outfit", new[] { "casual.pro.set.008", "casual.pro.set.009", Shark, "casual.pro.set.027", "casual.pro.set.018", "casual.pro.set.019" },
                                    null, 200f, new Vector3(0, 1.4f, -4.6f), new Vector3(0, 0.8f, 0), 27f, 800, 1200);
                Shooting = true;
            }
            // C03 gun classes, aiming
            if (only == 0 || only == 3)
            {
                yield return Prepare(Shark, WeaponClass.AssaultRifle);
                var guns = new[] { WeaponClass.Sidearm, WeaponClass.SMG, WeaponClass.AssaultRifle, WeaponClass.Shotgun, WeaponClass.LMG, WeaponClass.Marksman, WeaponClass.Rocket };
                for (int i = 0; i < guns.Length; i++)
                {
                    EquipFamily(guns[i]);
                    CombatForShot(false);
                    for (int k = 0; k < 8; k++) { FacePlayer(235f); yield return null; }
                    yield return Shot($"c03_gun_{i}", new Vector3(0.2f, 1.3f, -4.2f), new Vector3(0f, 0.85f, 0), 28f, 1000, 1000);
                }
            }
            // C04 enemy roster at true scale, hero at the left end
            if (only == 0 || only == 4)
            {
                yield return Prepare(Shark, WeaponClass.AssaultRifle, 30f);
                var all = new List<string>(Small); all.AddRange(Medium);
                float end = StageRow(all.ToArray(), 0.9f, 0f, 0.25f);
                for (int k = 0; k < 8; k++) { FacePlayer(200); yield return null; }
                yield return new WaitForSecondsRealtime(0.8f);
                float mid = (end - 0.8f) * 0.5f;
                yield return Shot("c04_roster", new Vector3(mid, 1.3f, -mid * 1.25f - 2f), new Vector3(mid, 0.7f, 0), 34f, 2400, 900);
            }
            // C05 the three bosses next to the hero
            if (only == 0 || only == 5)
            {
                yield return Prepare(Shark, WeaponClass.AssaultRifle, 30f);
                float end = StageRow(new[] { "ZD_CactusBoss", "ZD_MoleRatKing", "ZD_SkeletonGiant" }, 1.2f, 0.5f, 0.8f);
                for (int k = 0; k < 8; k++) { FacePlayer(200); yield return null; }
                yield return new WaitForSecondsRealtime(0.8f);
                float mid = (end - 0.8f) * 0.5f;
                yield return Shot("c05_bosses", new Vector3(mid, 2.2f, -mid * 1.6f - 3f), new Vector3(mid, 1.6f, 0), 38f, 2400, 1100);
            }
            // C06 poster: low angle, the hero aims past the viewer, the horde behind
            if (only == 0 || only == 6)
            {
                yield return Prepare(Shark, WeaponClass.AssaultRifle);
                StageArc(Mixed, 18, 6f, 12f, -45f, 45f, 6);
                yield return Aim();
                for (int k = 0; k < 10; k++) { FacePlayer(196f); yield return null; }   // turned to the viewer
                yield return Shot("c06_poster_low", new Vector3(0.9f, 0.7f, -3.1f), new Vector3(0, 1.4f, 2f), 48f, 1080, 1620);
            }
            // C07 back view: one hero facing the wave
            if (only == 0 || only == 7)
            {
                yield return Prepare(Shark, WeaponClass.AssaultRifle);
                StageArc(Mixed, 28, 4.5f, 13f, -38f, 38f, 7);
                yield return Aim();
                yield return Shot("c07_wave_back", new Vector3(0.9f, 1.4f, -3.4f), new Vector3(0, 1.0f, 6f), 50f, 1800, 1000);
                yield return Shot("c07_wave_back_portrait", new Vector3(0.5f, 1.6f, -3.6f), new Vector3(0, 1.2f, 6f), 55f, 1080, 1920);
            }
            // C08 the game's own camera: surrounded, Buzzsaw Halo spinning
            if (only == 0 || only == 8)
            {
                yield return Prepare(Shark, WeaponClass.SMG);
                Max(SkillCatalogDefs.EvoBuzzsaw);
                StageArc(Mixed, 30, 3.2f, 7.5f, 0f, 350f, 8);
                yield return Aim(1.5f);
                yield return Shot("c08_gameplay_buzzsaw", new Vector3(0, 12f, -8f), new Vector3(0, 0, 1f), 45f, 1080, 1920, true);
            }
            // C09 grenade launcher blast into a crowd
            if (only == 0 || only == 9)
            {
                yield return Prepare(Shark, WeaponClass.Rocket);
                StageArc(Mixed, 16, 5f, 9f, -30f, 30f, 9);
                CombatForShot(true);
                for (int i = 0; i < 4; i++)
                {
                    yield return new WaitForSeconds(0.45f);
                    yield return Shot($"c09_launcher_{i}", new Vector3(-2.2f, 1.6f, -3.2f), new Vector3(0, 0.8f, 5f), 48f, 1600, 1000);
                }
                CombatForShot(false);
            }
            // C10 capturing a relay on the hex pad as the horde closes in
            if (only == 0 || only == 10)
            {
                yield return Prepare(Shark, WeaponClass.AssaultRifle, 18f);
                st?.SpawnDebug(StationKind.SignalRelay, PlayerMovement.Instance.transform.position + new Vector3(1.8f, 0, 1.4f));
                StageArc(Mixed, 26, 7f, 11f, 0f, 350f, 10);
                yield return new WaitForSecondsRealtime(1.2f);
                FacePlayer(200);
                yield return Shot("c10_relay_pad", new Vector3(4.5f, 7.5f, -8.5f), new Vector3(0, 0, 0.5f), 42f, 1600, 1200);
            }
            // C12 the supply drop pod
            if (only == 0 || only == 12)
            {
                yield return Prepare(Shark, WeaponClass.AssaultRifle, 18f);
                st?.SpawnDebug(StationKind.SupplyDrop, PlayerMovement.Instance.transform.position + new Vector3(2.6f, 0, 3.2f));
                yield return new WaitForSecondsRealtime(1.5f);
                FacePlayer(40);
                yield return Shot("c12_drop_pod", new Vector3(-2.6f, 2.0f, -3.6f), new Vector3(1.6f, 1.0f, 2.6f), 45f, 1400, 1000);
            }
            // C13 a breather at the heal plinth, dogs coming from far off
            if (only == 0 || only == 13)
            {
                yield return Prepare(Shark, WeaponClass.AssaultRifle, 18f);
                st?.SpawnDebug(StationKind.HealZone, PlayerMovement.Instance.transform.position + new Vector3(1.6f, 0, 2.2f));
                StageArc(new[] { "ZD_DogPup", "ZD_DogBark", "ZD_DogBowwow" }, 6, 11f, 14f, -25f, 25f, 13);
                yield return new WaitForSecondsRealtime(1.5f);
                FacePlayer(35);
                yield return Shot("c13_heal_plinth", new Vector3(-2.4f, 1.8f, -3.2f), new Vector3(1.4f, 1.1f, 2.6f), 45f, 1400, 1000);
            }
            // C14 face to face with the Cactus Boss (its spit clouds cleared just before the frame)
            if (only == 0 || only == 14)
            {
                yield return Prepare(Shark, WeaponClass.Shotgun, 18f);
                Stage("ZD_CactusBoss", new Vector3(0.6f, 0, 5.5f), Vector3.zero);
                StageArc(new[] { "ZD_Cactus", "ZD_Cacti" }, 6, 5f, 8f, -50f, 50f, 14);
                yield return Aim(1.5f);
                yield return Shot("c14_boss_cactus", new Vector3(-1.4f, 1.0f, -2.4f), new Vector3(0.4f, 2.0f, 5.5f), 52f, 1600, 1100);
            }
            // C15 the Mole Rat King and his diggers
            if (only == 0 || only == 15)
            {
                yield return Prepare(Shark, WeaponClass.LMG, 18f);
                Stage("ZD_MoleRatKing", new Vector3(1f, 0, 6f), Vector3.zero);
                StageArc(new[] { "ZD_MoleRat", "ZD_Burrow" }, 10, 4.5f, 9f, -50f, 50f, 15);
                yield return Aim();
                yield return Shot("c15_molerat_king", new Vector3(-2.6f, 1.4f, -2.8f), new Vector3(1f, 1.2f, 5.5f), 50f, 1600, 1100);
            }
            // C16 the skeleton host: giant, mages, bones
            if (only == 0 || only == 16)
            {
                yield return Prepare(Shark, WeaponClass.Marksman, 18f);
                Stage("ZD_SkeletonGiant", new Vector3(0, 0, 9f), Vector3.zero);
                Stage("ZD_SkeletonMage", new Vector3(-3.2f, 0, 7f), Vector3.zero);
                Stage("ZD_SkeletonMage", new Vector3(3.2f, 0, 7.5f), Vector3.zero);
                StageArc(new[] { "ZD_Skeleton" }, 9, 4.5f, 7f, -45f, 45f, 16);
                yield return Aim();
                yield return Shot("c16_skeleton_host", new Vector3(0.8f, 1.1f, -3f), new Vector3(0, 2f, 8f), 50f, 1080, 1620);
            }
            // C17 the cute swarm: cats and dogs all round
            if (only == 0 || only == 17)
            {
                yield return Prepare(Shark, WeaponClass.Shotgun, 18f);
                StageArc(new[] { "ZD_CatMeow", "ZD_DogPup", "ZD_CatBolt", "ZD_DogBark", "ZD_CatLightning", "ZD_DogBowwow" }, 26, 3f, 7f, 0f, 350f, 17);
                yield return Aim();
                yield return Shot("c17_pets_swarm", new Vector3(0, 6.5f, -6.5f), new Vector3(0, 0, 0.8f), 45f, 1400, 1400);
            }
            // C18 Meteor Storm lands on the crowd
            if (only == 0 || only == 18)
            {
                yield return Prepare(Shark, WeaponClass.AssaultRifle, 18f);
                Max(SkillCatalogDefs.EvoMeteorStorm);
                StageArc(Mixed, 22, 6f, 11f, -35f, 35f, 18);
                yield return Aim(0.8f);
                FirePower(SkillCatalogDefs.AutoMeteor, 4f, 1);
                for (int i = 0; i < 4; i++)
                {
                    yield return new WaitForSeconds(0.35f);
                    yield return Shot($"c18_meteor_{i}", new Vector3(-3f, 5.5f, -6f), new Vector3(0, 0, 6f), 48f, 1600, 1000);
                }
            }
            // C19 Absolute Zero freezes the ring
            if (only == 0 || only == 19)
            {
                yield return Prepare(Shark, WeaponClass.AssaultRifle, 18f);
                Max(SkillCatalogDefs.EvoAbsoluteZero);
                StageArc(Mixed, 24, 3f, 7f, 0f, 350f, 19);
                yield return Aim(0.8f);
                FirePower(SkillCatalogDefs.AutoFrostNova, SkillRuntime.Active.FrostRadius, 0);
                for (int i = 0; i < 3; i++)
                {
                    yield return new WaitForSeconds(0.25f);
                    yield return Shot($"c19_absolute_zero_{i}", new Vector3(0, 7f, -7f), new Vector3(0, 0, 0.8f), 45f, 1400, 1400);
                }
            }
            // C20 store feature: wide, the hero between two waves
            if (only == 0 || only == 20)
            {
                yield return Prepare(Shark, WeaponClass.AssaultRifle, 20f);
                StageArc(Mixed, 14, 5f, 10f, 55f, 115f, 20);
                StageArc(Mixed, 14, 5f, 10f, -115f, -55f, 21);
                yield return Aim();
                yield return Shot("c20_feature_wide", new Vector3(0.4f, 1.3f, -4.6f), new Vector3(0, 1.1f, 1f), 44f, 2048, 1000);
            }

            ResetSkills();
            ClearEnemies();
            CombatForShot(true);
            Time.timeScale = 1f;
            Shooting = false;
            Debug.Log("[KeyArt] batch done");
        }
    }
}
