using System;
using UnityEngine;
using ZombieWar.Skills;
using ZombieWar.Skills.Powers;

namespace ZombieWar
{
    /// <summary>
    /// Phase A8 (owner-approved 2026-09-29): the three mechanic items an elite (or a Supply Drop) can
    /// leave behind — Magnet, Bomb, Freeze Clock. At most one is on the map at a time, so each one is
    /// an event the player walks to, never ground clutter. What each does when collected lives here;
    /// the drop table and the one-at-a-time rule live in <see cref="PickupManager"/>.
    /// </summary>
    public static class MechanicItems
    {
        [Serializable]
        public sealed class Assets
        {
            [Tooltip("A burst on the player the moment a magnet is taken.")]
            public ParticleSystem magnetFx;
            [Tooltip("The bomb's blast at the player.")]
            public ParticleSystem bombFx;
            public float bombNativeRadius = 3f;
            [Tooltip("A small blast on each enemy the bomb reaches (budgeted).")]
            public ParticleSystem bombHitFx;
            [Tooltip("The freeze clock's burst at the player.")]
            public ParticleSystem freezeFx;
            public float freezeNativeRadius = 3f;
            [Tooltip("Ice bursting on each frozen enemy (budgeted).")]
            public ParticleSystem freezeHitFx;
        }

        public const string BombSource = "item.bomb";
        public const float Reach = 16f;                 // on screen in every direction from the top-down camera
        public const float EliteBombFraction = 0.3f;    // of an elite's max health; a normal enemy just dies
        public const float FreezeSeconds = 4f;
        public const float EliteFreezeSeconds = 2f;
        const int FxBudget = 10;

        static readonly Color Blast = new(1f, 0.55f, 0.15f, 1f);
        static readonly Color Ice = new(0.6f, 0.9f, 1f, 1f);

        /// <summary>A mechanic item (as opposed to loot or a chest).</summary>
        public static bool IsMechanic(PickupEffect e) =>
            e == PickupEffect.Magnet || e == PickupEffect.Bomb || e == PickupEffect.Freeze;

        /// <summary>
        /// The drop table: which item a roll in [0,1) gives. Magnet is the common one (it saves a
        /// walk), the bomb clears a crowd, the freeze clock is the rare panic button.
        /// </summary>
        public static PickupEffect Pick(float roll) =>
            roll < 0.45f ? PickupEffect.Magnet : roll < 0.8f ? PickupEffect.Bomb : PickupEffect.Freeze;

        /// <summary>How much of an enemy's health the bomb takes.</summary>
        public static float BombDamage(bool elite, float maxHealth) =>
            elite ? maxHealth * EliteBombFraction : maxHealth + 1f;

        static IPowerHost Host => SkillArsenal.Instance;
        static Assets A => SkillArsenal.Instance != null && SkillArsenal.Instance.Library != null ? SkillArsenal.Instance.Library.items : null;

        public static void Magnet(Vector3 at)
        {
            PickupManager.BeginMagnetSweep();
            var a = A; var host = Host;
            if (host == null) return;
            if (a?.magnetFx != null) FxPool.Play(a.magnetFx, at + Vector3.up * 0.8f, PowerKit.Flat(a.magnetFx), 1f);
            host.Shockwave(at, 0.5f, 7f, new Color(0.95f, 0.3f, 0.35f, 1f), 0.5f);
        }

        /// <summary>
        /// Clears the screen: a blast ring races out from the player in three bands, and each band
        /// looks its enemies up when it arrives (a kill fires synchronously, so a pooled enemy looked
        /// up earlier could already be someone else).
        /// </summary>
        public static void Bomb(Vector3 at)
        {
            var host = Host; var a = A;
            if (host == null) return;
            at.y = 0f;
            BillGameCore.Bill.Audio?.PlayCue("sfx.pickup.bomb", at, BillGameCore.SfxPriority.High, 0.85f);   // the grab, under the blast (audio 06/10)
            if (a?.bombFx != null) PowerKit.PlaySized(a.bombFx, at + Vector3.up * 0.2f, 3.2f, a.bombNativeRadius);
            host.Shockwave(at, 0.5f, Reach, Blast, 0.55f);
            host.Sfx("sfx.player.bomb.explode", at, 1f, 0.1f);
            host.Shake(0.55f);
            _fxLeft = FxBudget;
            host.Delay(0f, p => BombBand(p, 0f, Reach / 3f), at);
            host.Delay(0.12f, p => BombBand(p, Reach / 3f, Reach * 2f / 3f), at);
            host.Delay(0.24f, p => BombBand(p, Reach * 2f / 3f, Reach), at);
        }

        static int _fxLeft;

        static void BombBand(Vector3 at, float inner, float outer)
        {
            var host = Host; var a = A;
            if (host == null) return;
            // "Every enemy on screen" walks the live list itself: the shared query buffer is capped
            // (its clustering maths is O(n^2)) and a crowd can be bigger than the cap.
            var alive = ZombieManager.Alive;
            for (int i = alive.Count - 1; i >= 0; i--)
            {
                var e = alive[i];
                if (e == null || e.IsDead) continue;
                Vector3 d = e.transform.position - at; d.y = 0f;
                float d2 = d.sqrMagnitude;
                if (d2 < inner * inner || d2 > outer * outer) continue;
                if (!host.OnScreen(e.transform.position)) continue;
                var hp = e.Life;
                bool elite = e.Data != null && e.Data.isElite;
                PowerKit.Hit(e, BombDamage(elite, hp != null ? hp.Max : 100f), 1.2f, BombSource);
                if (_fxLeft-- > 0 && a?.bombHitFx != null) FxPool.Play(a.bombHitFx, PowerKit.Chest(e), PowerKit.Flat(a.bombHitFx), 0.8f);
            }
        }

        /// <summary>
        /// Owner rule 2026-10-05: getting back up kills every enemy on the map, so a revive buys the
        /// player real time. Same blast as the bomb, but no screen or reach limit and elites die too.
        /// </summary>
        public static void ReviveClear(Vector3 at)
        {
            var host = Host; var a = A;
            at.y = 0f;
            if (host != null)
            {
                if (a?.bombFx != null) PowerKit.PlaySized(a.bombFx, at + Vector3.up * 0.2f, 4f, a.bombNativeRadius);
                host.Shockwave(at, 0.5f, Reach, Blast, 0.6f);
                host.Sfx("sfx.player.bomb.explode", at, 1f, 0.1f);
                host.Shake(0.6f);
            }
            int budget = FxBudget;
            var alive = ZombieManager.Alive;   // a kill leaves the list at once, so walk it backwards
            for (int i = alive.Count - 1; i >= 0; i--)
            {
                if (i >= alive.Count) continue;
                var e = alive[i];
                if (e == null || e.IsDead) continue;
                // Bosses stay: dying next to a beacon boss and taking the free revive paid its full
                // reward without a fight (07/10).
                if (e.IsBeaconOwned || e is ZombieBoss) continue;
                var hp = e.Life;
                PowerKit.Hit(e, (hp != null ? hp.Max : 100f) * 10f + 1f, 1.2f, ReviveSource);
                if (budget-- > 0 && a?.bombHitFx != null && host != null && host.OnScreen(e.transform.position))
                    FxPool.Play(a.bombHitFx, PowerKit.Chest(e), PowerKit.Flat(a.bombHitFx), 0.8f);
            }
        }

        public const string ReviveSource = "revive.clear";

        /// <summary>Freezes every enemy on screen solid (elites for less).</summary>
        public static void Freeze(Vector3 at)
        {
            var host = Host; var a = A;
            if (host == null) return;
            at.y = 0f;
            if (a?.freezeFx != null) PowerKit.PlaySized(a.freezeFx, at + Vector3.up * 0.2f, 3f, a.freezeNativeRadius);
            host.Shockwave(at, 0.5f, Reach, Ice, 0.6f);
            host.Sfx("sfx.skill.frost", at, 0.9f, 0.1f);
            host.Shake(0.2f);
            var tint = host.Library != null ? host.Library.frost.frozenTint : Ice;
            float now = Time.time;
            int budget = FxBudget;
            var alive = ZombieManager.Alive;   // every enemy on screen, not the capped query buffer
            for (int i = alive.Count - 1; i >= 0; i--)
            {
                var e = alive[i];
                if (e == null || e.IsDead || !host.OnScreen(e.transform.position)) continue;
                Vector3 d = e.transform.position - at; d.y = 0f;
                if (d.sqrMagnitude > Reach * Reach) continue;
                float seconds = e.Data != null && e.Data.isElite ? EliteFreezeSeconds : FreezeSeconds;
                StatusCarrier.Apply(e.transform.GetInstanceID(), StatusKind.Frozen, 1f, seconds, now);
                SkillFxDirector.Instance?.TintEnemy(e, tint, seconds);
                if (budget-- > 0 && a?.freezeHitFx != null) FxPool.Play(a.freezeHitFx, PowerKit.Chest(e), PowerKit.Flat(a.freezeHitFx), 0.7f);
            }
        }
    }
}
