using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using BillGameCore;

namespace ZombieWar
{
    /// Weapon, part: the fire loop (auto-fire, target check, one shot, audio).
    public partial class Weapon
    {
        /// <summary>
        /// Drives continuous automatic fire.
        ///
        /// There is no fire button: a target inside range is the only thing that makes the weapon
        /// shoot, and from M4 it then shoots without interruption - no magazine, no reload gap.
        ///
        /// Cadence is spent from <see cref="_fireAccumulator"/> rather than gated by a countdown, so
        /// the authored rate survives long frames. The clamps below are the whole safety story:
        ///
        /// - With no valid target the accumulator is held at one interval, so re-acquiring a target
        ///   shoots immediately, but standing idle for a minute banks one shot, not sixty.
        /// - While firing it is capped at the per-frame catch-up budget, so a hitch or a resumed
        ///   pause cannot dump a burst.
        /// - Pause needs no special case: `Time.deltaTime` is zero at `timeScale` zero, so nothing
        ///   accumulates while paused.
        /// </summary>
        private void TickAutoFire(float deltaTime)
        {
            var data = Current;
            float interval = ShotInterval(data);

            if (!HasValidTarget(data, out Vector3 aimDirection) || interval <= 0f
                || float.IsInfinity(interval))
            {
                // Hold exactly one shot ready. Not zero, or the player would eat a full interval of
                // delay every time an enemy walks into range; not "whatever accumulated", or a quiet
                // minute would empty itself into the first target seen.
                _fireAccumulator = float.IsInfinity(interval) ? 0f : interval;
                return;
            }

            _fireAccumulator += deltaTime;

            float cap = interval * MaxCatchUpShotsPerFrame;
            if (_fireAccumulator > cap) _fireAccumulator = cap;

            int shots = 0;
            while (_fireAccumulator >= interval && shots < MaxCatchUpShotsPerFrame)
            {
                if (!FireOnce(aimDirection)) break;
                _fireAccumulator -= interval;
                shots++;
            }
        }

        /// <summary>Seconds between shots at the current effective rate. Infinity when it cannot fire.</summary>
        private float ShotInterval(WeaponData data)
        {
            if (data == null) return float.PositiveInfinity;

            float rate = EffectiveFireRate(data);
            // A zero or negative authored rate is a data defect. Failing safe means never firing,
            // rather than dividing by zero and spraying every frame.
            return rate > 0f ? 1f / rate : float.PositiveInfinity;
        }

        /// <summary>Degrees the visible aim may lag the selected target and still fire. Tighter
        /// wastes cadence during ordinary tracking; looser sprays misses during target switches.
        /// The aim sweeps at 720°/s, so even a 150° switch clears this gate in ~0.2 s.</summary>
        [SerializeField] private float fireAlignmentConeDegrees = 5f;

        /// <summary>Assumed half-width of an enemy body for the distance-scaled part of the
        /// alignment gate (production zombie colliders are ~0.7 wide).</summary>
        private const float CloseTargetBodyRadius = 0.35f;

        private bool HasValidTarget(WeaponData data, out Vector3 aimDirection)
        {
            aimDirection = Vector3.forward;

            // Unarmed is a valid state (empty loadout, every asset invalid), and this runs every
            // frame - an unguarded Current here would throw once per frame, forever.
            if (data == null || _currentInstance == null) return false;

            var player = PlayerMovement.Instance;
            if (player == null || !player.HasTarget) return false;
            if (player.AimTargetDistance > data.range) return false;

            // Alignment gate (M5.1.2 CP1): while the visible aim is still sweeping toward the
            // selected target, HOLD fire. The caller's no-target path keeps the accumulator pinned
            // at one interval, so alignment recovery fires promptly but can never dump a banked
            // burst. Scripted/test fire (no live ITargetable) skips the gate.
            //
            // The gate widens with the angle the target's body actually subtends: at 10 m a body is
            // ~2° wide and the authored cone governs, but at 0.6 m it is ~30° wide - a fixed cone
            // there starved fire completely in a surrounding pack (measured live: 1 kill in 6 s of
            // SMG fire), because every nearest-target reshuffle re-opened a 5° gate the sweeping
            // aim kept missing.
            var target = player.AimTarget;
            if (target != null && target.Transform != null)
            {
                Vector3 toTarget = target.Transform.position - player.transform.position;
                toTarget.y = 0f;
                float distance = toTarget.magnitude;
                if (distance > 0.01f)
                {
                    float subtended = Mathf.Atan2(CloseTargetBodyRadius, distance) * Mathf.Rad2Deg;
                    float gate = Mathf.Max(fireAlignmentConeDegrees, subtended);
                    if (Vector3.Angle(player.AimDirection, toTarget) > gate) return false;
                }
            }

            aimDirection = player.AimDirection;
            return true;
        }

        /// <summary>
        /// Fires one shot if the weapon is ready, spending the cadence budget.
        ///
        /// Kept public because external callers (tests, scripted sequences) fire a single shot
        /// through it. It respects the same accumulator the automatic loop drains, so calling it
        /// cannot outrun the weapon's authored rate.
        /// </summary>
        public void TryFire(Vector3 aimDirection)
        {
            float interval = ShotInterval(Current);
            if (float.IsInfinity(interval)) return;
            if (_fireAccumulator < interval) return;

            if (FireOnce(aimDirection)) _fireAccumulator -= interval;
        }

        /// <summary>Resolves exactly one shot. Returns false when the weapon cannot fire at all.</summary>
        private bool FireOnce(Vector3 aimDirection)
        {
            // M7.2b — ramp cards (Bullet Hose, Heavy Pressure, Run & Gun) only ramp if something
            // tells them the trigger is actually held. One notify per resolved shot.
            ZombieWar.Skills.SkillCombatDriver.Instance?.NotifyFiring();

            // M7.2c — the shot plan is now CONSUMED rather than discarded. Breach Round's bonus
            // pierce and Shockwave Belt's cone were computed here and thrown away, which is what
            // made both cards half-stubbed.
            _shotPlan = ZombieWar.Skills.SkillRuntime.Active?.OnShotFired()
                        ?? default(ZombieWar.Skills.SkillRuntime.ShotPlan);
            if (_shotPlan.shockwave) FireShockwave(aimDirection);

            var data = Current;
            // Nothing equipped, or Current fell back to a roster entry that was never instantiated -
            // the muzzle lookup below dereferences _currentInstance, so both must be real.
            if (data == null || _currentInstance == null) return false;

            // Ban theo TRUC NONG (MuzzlePoint.forward = +Z cua muzzle) => tracer luon thang ra tu
            // nong, khong "meo" so voi than sung. Auto-aim + Multi-Aim IK lo viec chia nong vao zombie.
            bool hasMuzzle = _currentGripPoints != null && _currentGripPoints.MuzzlePoint != null;
            Vector3 muzzlePosition = hasMuzzle
                ? _currentGripPoints.MuzzlePoint.position
                : _currentInstance.transform.position;
            Vector3 muzzleForward = hasMuzzle
                ? _currentGripPoints.MuzzlePoint.forward
                : aimDirection;

            // Hit-test from the player's own axis, NOT the muzzle. The muzzle sits ~0.9 units in
            // front of the chest, so an enemy that walks inside that offset is BEHIND the ray origin
            // and Unity raycasts never hit a collider they start inside - the nearest attacker
            // became permanently unkillable while chewing on the player (M5 audit S1). The muzzle
            // stays the visual origin for tracer/flash; only the physics ray moves back. Range is
            // extended by the pull-back so the effective reach out of the barrel is unchanged.
            Vector3 rayOrigin = new Vector3(transform.position.x, muzzlePosition.y, transform.position.z);
            float rayRangeBonus = Vector3.Distance(rayOrigin, muzzlePosition);

            // M5.1.2 CP1 — the authoritative base direction is the VISIBLE aim, never the target.
            //
            // M5.1.1 briefly snapped the ray straight at the selected target; that made every
            // bullet a homing shot and could hit an enemy 150° away from where the gun visibly
            // pointed. The contract is restored to: selected target → smoothed AimDirection →
            // visible body/muzzle → shot. The muzzle tracks AimDirection within ~1.5° (measured),
            // so firing along the aim keeps gun, flash, tracer and hit as one coherent line. What
            // guards the hit rate instead is the ALIGNMENT GATE in HasValidTarget - the weapon
            // holds fire until the visible aim has swept inside the cone. Locomotion never enters.
            //
            // Fire-time staleness re-check: the aim lock is updated in FixedUpdate, so the target
            // can die within the frame. A stale target refuses the shot BEFORE any side effect -
            // no flash, no audio, no recoil, no cadence spend (return false leaves the
            // accumulator's shot budget unspent; TickAutoFire stops the burst).
            var player = PlayerMovement.Instance;
            var target = player != null ? player.AimTarget : null;
            if (target != null)
            {
                if (!target.IsTargetable || target.Transform == null) return false;
                Vector3 toTarget = target.Transform.position - transform.position;
                toTarget.y = 0f;
                if (toTarget.magnitude > data.range) return false;
            }

            Vector3 shotDirection = aimDirection.sqrMagnitude > 0.0001f ? aimDirection.normalized : muzzleForward;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _probeShotId++;
            _probeMuzzlePos = muzzlePosition;
            _probeMuzzleFwd = muzzleForward;
            _probeAim = shotDirection;
            _probeTargetDir = Vector3.zero;
            _probeTargetValid = target != null && target.IsTargetable;
            _probeTarget = target != null && target.Transform != null ? target.Transform : null;
            if (_probeTarget != null)
            {
                Vector3 td = _probeTarget.position - rayOrigin;
                td.y = 0f;
                if (td.sqrMagnitude > 0.0001f) _probeTargetDir = td.normalized;
            }
#endif

            int pellets = Mathf.Max(1, data.pelletCount);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _probeRayCount = pellets;
#endif
            for (int i = 0; i < pellets; i++)
            {
                Vector3 dir = (pellets > 1 || data.spreadAngle > 0f)
                    ? ScatterDirection(shotDirection, data.spreadAngle)
                    : shotDirection;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                _probeRayIndex = i;
#endif
                FireRay(data, rayOrigin, muzzlePosition, rayRangeBonus, dir);
            }

            ToonPointLights.Flash(muzzlePosition, MuzzleLight, 4f, 1.4f, 0.07f);

            // A3 Split Shot: extra bullets fanned out either side of the aim, 10° apart.
            for (int k = 1; k <= _shotPlan.splitBullets; k++)
            {
                float angle = ((k + 1) / 2) * 10f * ((k & 1) == 1 ? 1f : -1f);
                FireRay(data, rayOrigin, muzzlePosition, rayRangeBonus, Quaternion.Euler(0f, angle, 0f) * shotDirection);
            }
            // A3 Double Tap: one free extra bullet on (nearly) the same line.
            if (_shotPlan.doubleTap)
                FireRay(data, rayOrigin, muzzlePosition, rayRangeBonus, ScatterDirection(shotDirection, 3f));

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Third argument is the ACTUAL ray direction of this shot (the authoritative snapshot),
            // not the muzzle's visual forward - evidence probes must see what the physics saw.
            ShotProbe?.Invoke(data, muzzlePosition, shotDirection, aimDirection, _lastPierceHits, _lastPierceBlocked);
#endif

            SpawnMuzzleFlash(data, muzzlePosition, muzzleForward);

            PlayFireAudio(data);
            // 05/10 owner: a fast gun (Vector at 20 shots/s, more with Fire Rate) stacked recoil and
            // shake every shot until the view was unbearable. Both now scale with the shot interval,
            // so a gun kicks the same amount per SECOND whatever its rate, and firing alone never
            // shakes the camera past a small ceiling (hits and blasts still can).
            float rateScale = FireFeelScale(ShotInterval(data));
            ApplyRecoil(data, rateScale);
            ShakeCamera(cameraShakeOnFire * rateScale, FireShakeCeiling);
            return true;
        }

        /// <summary>Fire intervals shorter than this route through the retrigger voice. At 0.15 s
        /// (≈6.7 shots/s) a typical 0.3-0.5 s fire clip already overlaps itself 2-3 deep, which is
        /// where the per-key voice policy starts dropping shots (audible gaps - M5.1 CP7).</summary>
        private const float HighRateAudioInterval = 0.15f;

        private AudioCueHandle _fireCueHandle;

        /// <summary>
        /// Same-key voice budget for a slow weapon's guaranteed transients.
        ///
        /// Three rotating voices cover a handgun's ~0.3-0.4 s of audible body even at its fastest
        /// authored 6.5 shots/s, so a recycled voice has already spent its report. Multi-pellet
        /// weapons get one more: their blast body runs ~0.7-0.9 s and AA12 reaches 3 shots/s. The
        /// choice reads authored DATA (pellet count), never a weapon name.
        /// </summary>
        private static int TransientVoiceBudget(WeaponData data) => data.pelletCount > 1 ? 4 : 3;

        /// <summary>
        /// True for the families whose shots must read as separate reports: sidearms, shotguns and
        /// marksman rifles. Read from the authored <see cref="WeaponData.weaponClass"/> - the design
        /// property that already states the family - rather than matched from a weapon name, and
        /// stable when a fire-rate perk moves the cadence.
        /// </summary>
        private static bool WantsDiscreteReports(WeaponData data) =>
            data.weaponClass == WeaponClass.Sidearm
            || data.weaponClass == WeaponClass.Shotgun
            || data.weaponClass == WeaponClass.Marksman;

        // Cadence grammar:
        //
        //   discrete families (sidearm/shotgun/marksman)
        //       -> guaranteed bounded transient. The ordinary cue path counts a voice busy for the
        //          whole imported clip (1.25 s handgun), so at 4 shots/s the global perKeyLimit=3
        //          silently swallowed every fourth visible shot - the missing pistol reports a human
        //          playtest heard. Bounded recycling guarantees each visible shot a transient.
        //   automatic families (SMG/AR/LMG), or anything driven past the merge threshold
        //       -> one managed retrigger voice: per-bullet transients above ~6.7 shots/s merge
        //          anyway, and this keeps voice growth at zero under sustained fire.
        private void PlayFireAudio(WeaponData data)
        {
            if (string.IsNullOrEmpty(data.fireSfxKey)) return;
            // Burst bookkeeping for the tail (TickFireTail): when the gun stops after a real burst,
            // the room answers with the family's tail (audio coverage 06/10).
            if (_burstStartAt < 0f) _burstStartAt = Time.time;
            _lastShotAt = Time.time;
            _tailData = data;

            bool discrete = WantsDiscreteReports(data) && ShotInterval(data) >= HighRateAudioInterval;
            if (discrete)
                Bill.Audio?.PlayGuaranteedTransient(data.fireSfxKey, SfxPriority.High,
                                                    TransientVoiceBudget(data));
            else
                _fireCueHandle = Bill.Audio?.RetriggerCue(data.fireSfxKey, _fireCueHandle, SfxPriority.High)
                                 ?? AudioCueHandle.None;
        }

        // A switch or teardown must not leave the previous weapon's retrigger voice ringing.
        private void StopFireAudio()
        {
            Bill.Audio?.StopCue(_fireCueHandle, 0.05f);
            _fireCueHandle = AudioCueHandle.None;
        }

        private void OnDisable() => StopFireAudio();

        float _burstStartAt = -1f, _lastShotAt;
        WeaponData _tailData;
        public const float TailAfterSilence = 0.22f, TailMinBurst = 0.35f;

        /// <summary>Plays the family's tail once a burst of at least TailMinBurst has stopped.</summary>
        private void TickFireTail()
        {
            if (_burstStartAt < 0f || Time.time - _lastShotAt < TailAfterSilence) return;
            bool longEnough = _lastShotAt - _burstStartAt >= TailMinBurst;
            _burstStartAt = -1f;
            if (!longEnough || _tailData == null) return;
            string tail = ZombieWar.Audio.AudioKeys.Sibling(_tailData.fireSfxKey, "tail");
            if (tail != null) Bill.Audio?.PlayCue(tail, transform.position, SfxPriority.Low, 0.7f);
        }

        // Screen shake is what actually reads as "recoil" in 3rd-person; the gun-mount spring is
        // largely cancelled by the hand IK chasing the grips. Camera lookup is cached.
        private void ShakeCamera(float amount, float ceiling = 1f)
        {
            if (amount <= 0f) return;
            if (_cameraFollow == null && Camera.main != null)
                Camera.main.TryGetComponent(out _cameraFollow);
            if (_cameraFollow != null) _cameraFollow.Shake(amount, ceiling);
        }

        /// <summary>Shots per second at which recoil and shake are felt at full strength per shot.</summary>
        public const float FullFeelShotsPerSecond = 5f;
        /// <summary>Most camera trauma that firing alone can build (offset = trauma^2 x max).</summary>
        public const float FireShakeCeiling = 0.35f;

        /// <summary>Per-shot recoil and shake multiplier: 1 up to 5 shots/s, then shrinking so the
        /// kick per second stays constant.</summary>
        public static float FireFeelScale(float shotInterval) =>
            shotInterval <= 0f || float.IsInfinity(shotInterval) ? 1f : Mathf.Min(1f, shotInterval * FullFeelShotsPerSecond);
    }
}
