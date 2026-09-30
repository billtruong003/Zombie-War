using System;
using BillGameCore;
using UnityEngine;

namespace ZombieWar
{
    /// What a bullet hit is made of (2026-09-30). Enemies answer from their ZombieData impact sound
    /// family; a prop answers with a <see cref="SurfaceMaterial"/>; anything else is the ground.
    public enum SurfaceKind { Ground, Flesh, Fur, Bone, Plant, Wood, Stone, Metal, Glass }

    public static class SurfaceImpact
    {
        const float SfxGap = 0.07f;
        static float _nextSfx;

        public static SurfaceKind KindOf(Collider c)
        {
            if (c == null) return SurfaceKind.Ground;
            var enemy = c.GetComponentInParent<ZombieBase>();
            if (enemy != null) return FromImpactKey(enemy.Data != null ? enemy.Data.impactSfxKey : null);
            var tag = c.GetComponentInParent<SurfaceMaterial>();
            return tag != null ? tag.kind : SurfaceKind.Ground;
        }

        /// sfx.impact.bone.* / fur / plant / flesh → the body the sound was chosen for.
        public static SurfaceKind FromImpactKey(string key)
        {
            if (string.IsNullOrEmpty(key)) return SurfaceKind.Flesh;
            if (key.Contains(".bone")) return SurfaceKind.Bone;
            if (key.Contains(".plant")) return SurfaceKind.Plant;
            if (key.Contains(".fur")) return SurfaceKind.Fur;
            return SurfaceKind.Flesh;
        }

        /// Plays the hit in the material of what was hit. False when no library entry covers it, so the
        /// caller can fall back to its own effect.
        public static bool Play(in RaycastHit hit)
        {
            var lib = SurfaceImpactLibrary.Instance;
            var kind = KindOf(hit.collider);
            var e = lib != null ? lib.For(kind) : null;
            if (e == null || e.fx == null) return false;
            FxPool.Play(e.fx, hit.point, Quaternion.LookRotation(hit.normal.sqrMagnitude > 0.01f ? hit.normal : Vector3.up), e.scale);
            if (!string.IsNullOrEmpty(e.sfxKey) && Time.time >= _nextSfx)
            {
                _nextSfx = Time.time + SfxGap;
                Bill.Audio?.PlayCue(e.sfxKey, hit.point, SfxPriority.Low, e.volume);
            }
            return true;
        }
    }
}
