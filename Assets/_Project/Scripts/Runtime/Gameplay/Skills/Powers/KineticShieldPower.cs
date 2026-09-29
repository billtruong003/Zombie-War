using System;
using UnityEngine;

namespace ZombieWar.Skills.Powers
{
    /// <summary>
    /// Kinetic Shield — a shell around the player, charged by distance walked, that eats one hit.
    /// Clear in the middle and glowing at its rim, back faces culled so nothing is drawn over the
    /// player; while it recharges a thin ring at the feet fills with the distance walked.
    /// </summary>
    public sealed class KineticShieldPower : PowerModule
    {
        [Serializable]
        public sealed class Assets
        {
            [Tooltip("ZombieWar/FX/SkillShield: the shell (clear inside, glowing rim, back faces culled).")]
            public Material material;
            [Tooltip("Fallback when there is no shell material: the old particle aura.")]
            public ParticleSystem auraFx;
            [Tooltip("The shield ate a hit.")]
            public ParticleSystem breakFx;
        }

        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int RingId = Shader.PropertyToID("_Ring");
        static readonly int FillId = Shader.PropertyToID("_Fill");
        static readonly int FadeId = Shader.PropertyToID("_Fade");
        static readonly int FlashId = Shader.PropertyToID("_Flash");

        ParticleSystem _aura;
        bool _shown;
        Transform _shell;
        MeshRenderer _shellRenderer, _chargeRing;
        float _fade, _flash;

        Assets A => Lib != null ? Lib.shield : null;

        public override void Tick(SkillRuntime run, Vector3 p, float dt)
        {
            var a = A;
            if (a == null) return;
            bool has = run.Has(SkillCatalogDefs.UniKinetic);
            bool charged = has && run.KineticCharged;
            var player = Host.Player;

            if (a.material == null)
            {
                if (charged && _aura == null && a.auraFx != null)
                {
                    _aura = UnityEngine.Object.Instantiate(a.auraFx, player);
                    _aura.transform.localPosition = Vector3.up * 0.9f;
                    _aura.transform.localScale = Vector3.one * 0.7f;
                }
                if (_aura == null || charged == _shown) return;
                _shown = charged;
                _aura.gameObject.SetActive(charged);
                if (charged) Host.Sfx("sfx.skill.shield.ready", p, 0.5f, 0.5f);
                return;
            }

            if (has && _shell == null)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.name = "KineticShell";
                UnityEngine.Object.Destroy(go.GetComponent<Collider>());
                _shell = go.transform;
                _shell.SetParent(player, false);
                _shell.localPosition = Vector3.up * 1.05f;
                _shell.localScale = Vector3.one * 2.1f;   // radius 1.05 around y 1.05: rests on the ground
                _shellRenderer = go.GetComponent<MeshRenderer>();
                _shellRenderer.sharedMaterial = a.material;
                _shellRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _shellRenderer.receiveShadows = false;
            }
            if (has && _chargeRing == null)
            {
                _chargeRing = Host.MakeGroundRenderer("KineticChargeRing");
                if (_chargeRing != null)
                {
                    _chargeRing.transform.SetParent(player, false);
                    _chargeRing.transform.localPosition = Vector3.up * 0.04f;
                    _chargeRing.transform.localScale = new Vector3(2.3f, 1f, 2.3f);
                }
            }

            if (charged != _shown)
            {
                _shown = charged;
                if (charged) { _flash = 0.6f; Host.Sfx("sfx.skill.shield.ready", p, 0.5f, 0.5f); }
            }
            _fade = Mathf.MoveTowards(_fade, charged ? 1f : 0f, dt * 4f);
            _flash = Mathf.MoveTowards(_flash, 0f, dt * 2.5f);
            var mpb = Host.Block;

            if (_shell != null)
            {
                bool visible = has && _fade > 0.001f;
                if (_shell.gameObject.activeSelf != visible) _shell.gameObject.SetActive(visible);
                if (visible)
                {
                    mpb.Clear();
                    mpb.SetFloat(FadeId, _fade);
                    mpb.SetFloat(FlashId, _flash);
                    _shellRenderer.SetPropertyBlock(mpb);
                }
            }
            if (_chargeRing != null)
            {
                float fill = run.KineticChargeFraction;
                bool ring = has && !charged && fill > 0.01f;
                if (_chargeRing.gameObject.activeSelf != ring) _chargeRing.gameObject.SetActive(ring);
                if (ring)
                {
                    mpb.Clear();
                    mpb.SetColor(ColorId, new Color(0.7f, 0.55f, 1f, 0.75f));
                    mpb.SetFloat(RingId, 0.07f);
                    mpb.SetFloat(FillId, fill);
                    _chargeRing.SetPropertyBlock(mpb);
                }
            }
        }

        /// <summary>The shield ate a hit: the shell lights up as it breaks and shards burst off.</summary>
        public void Break()
        {
            var a = A;
            var player = Host.Player;
            if (a != null && a.breakFx != null)
                FxPool.Play(a.breakFx, player.position + Vector3.up * 0.9f, PowerKit.Flat(a.breakFx), 1.3f);
            _flash = 1f;
            _fade = Mathf.Max(_fade, 0.8f);
            Host.Sfx("sfx.skill.shield.break", player.position, 0.8f, 0.1f);
            Host.Shake(0.12f);
        }
    }
}
