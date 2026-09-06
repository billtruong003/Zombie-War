using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using ZombieWar;

namespace ZombieWar.Tests
{
    /// <summary>
    /// The continuous-fire contract (M4.3), replacing the per-slot ammo/reload fixture.
    ///
    /// That fixture asserted a contract that no longer exists: magazines survived a swap, reloads
    /// resumed rather than restarted, and cycling weapons could not be used as a free reload. Weapons
    /// now have no magazine at all, so the thing worth guarding changed shape. What can still go
    /// wrong is cadence: a weapon that quietly fires slower on a weak device, or one that banks a
    /// burst across a lull, a pause or a weapon switch.
    ///
    /// Cadence is driven through <c>TickAutoFire</c> by reflection because that is the real
    /// production entry point - testing a parallel copy of the maths would prove nothing about the
    /// code that actually ships.
    /// </summary>
    public class WeaponContinuousFireTests
    {
        private GameObject _host;
        private Weapon _weapon;
        private GameObject _dummyPrefab;
        private GameObject _playerGo;
        private PlayerMovement _player;
        private WeaponData _fast, _slow;
        private int _shots;

        private static FieldInfo Field(string n) =>
            typeof(Weapon).GetField(n, BindingFlags.NonPublic | BindingFlags.Instance);

        private static MethodInfo Method(string n) =>
            typeof(Weapon).GetMethod(n, BindingFlags.NonPublic | BindingFlags.Instance);

        private float Accumulator
        {
            get => (float)Field("_fireAccumulator").GetValue(_weapon);
            set => Field("_fireAccumulator").SetValue(_weapon, value);
        }

        private void Tick(float dt) => Method("TickAutoFire").Invoke(_weapon, new object[] { dt });

        /// <summary>Runs <paramref name="seconds"/> of game time in fixed steps and counts shots.</summary>
        private int FireFor(float seconds, float step)
        {
            _shots = 0;
            int steps = Mathf.RoundToInt(seconds / step);
            for (int i = 0; i < steps; i++) Tick(step);
            return _shots;
        }

        private WeaponData MakeWeapon(string id, float fireRate, bool twoHanded = false)
        {
            var d = ScriptableObject.CreateInstance<WeaponData>();
            d.name = id;
            d.weaponName = id;
            d.twoHanded = twoHanded;
            d.damage = 10f;
            d.fireRate = fireRate;
            d.range = 50f;
            d.weaponPrefab = _dummyPrefab;
            return d;
        }

        private void SetTarget(bool hasTarget, float distance = 5f)
        {
            typeof(PlayerMovement).GetProperty("HasTarget", BindingFlags.Public | BindingFlags.Instance)
                .SetValue(_player, hasTarget);
            typeof(PlayerMovement).GetProperty("AimTargetDistance", BindingFlags.Public | BindingFlags.Instance)
                .SetValue(_player, distance);
        }

        [SetUp]
        public void SetUp()
        {
            // The editor runs with DisableDomainReload, so a run left InProgress by a live play
            // session survives as RunState.Current INTO the test - its fire-rate perks multiply the
            // weapon cadence and shots overshoot (measured: 156 for an authored 100). The perk
            // multiplier is correct production behaviour; the ambient run is not. Isolate.
            RunState.Abandon();

            LogAssert.ignoreFailingMessages = true;
            _dummyPrefab = new GameObject("DummyWeaponModel");

            _playerGo = new GameObject("Player");
            _player = _playerGo.AddComponent<PlayerMovement>();
            typeof(PlayerMovement).GetProperty("Instance",
                BindingFlags.Public | BindingFlags.Static).SetValue(null, _player);
            SetTarget(true);

            _host = new GameObject("WeaponHost");
            _weapon = _host.AddComponent<Weapon>();

            _fast = MakeWeapon("Fast", 10f);
            _slow = MakeWeapon("Slow", 1f, twoHanded: true);   // slot 1-2 chi nhan sung hai tay

            _weapon.EquipToSlot(0, _fast);
            _weapon.EquipSlot(0);

            Weapon.ShotProbe += OnShot;
        }

        [TearDown]
        public void TearDown()
        {
            Weapon.ShotProbe -= OnShot;
            if (_host != null) Object.DestroyImmediate(_host);
            if (_playerGo != null) Object.DestroyImmediate(_playerGo);
            if (_dummyPrefab != null) Object.DestroyImmediate(_dummyPrefab);
            foreach (var d in new[] { _fast, _slow })
                if (d != null) Object.DestroyImmediate(d);
            LogAssert.ignoreFailingMessages = false;
        }

        private void OnShot(WeaponData d, Vector3 a, Vector3 b, Vector3 c, int e, bool f) => _shots++;

        // --- Không còn nạp đạn ---------------------------------------------------------------------

        [Test]
        public void NoReloadApiSurvivesOnTheWeapon()
        {
            // Không chỉ là không chạy: các khái niệm đó phải BIẾN MẤT khỏi API, nếu không sẽ có
            // người viết code dựa vào chúng rồi tưởng chúng còn ý nghĩa.
            System.Type t = typeof(Weapon);
            foreach (string name in new[] { "AmmoInMag", "MagazineSize", "IsReloading", "ReloadProgress" })
                Assert.IsNull(t.GetProperty(name), $"Weapon.{name} vẫn còn tồn tại.");

            foreach (string name in new[] { "_ammoInMag", "_reloading", "_reloadTimer" })
                Assert.IsNull(Field(name), $"Weapon.{name} vẫn còn tồn tại.");

            foreach (string name in new[] { "TickReload", "StartReload" })
                Assert.IsNull(Method(name), $"Weapon.{name}() vẫn còn tồn tại.");
        }

        [Test]
        public void NoMagazineFieldsSurviveOnWeaponData()
        {
            System.Type t = typeof(WeaponData);
            Assert.IsNull(t.GetField("magazineSize"), "WeaponData.magazineSize vẫn còn tồn tại.");
            Assert.IsNull(t.GetField("reloadDuration"), "WeaponData.reloadDuration vẫn còn tồn tại.");
        }

        [Test]
        public void FiresFarBeyondTheOldMagazineSize()
        {
            // Băng đạn cũ lớn nhất trong dự án là 999 (sniper trong test đạn đạo); mọi băng thật đều
            // nhỏ hơn nhiều. Bắn liên tục vượt hẳn con số đó là bằng chứng không còn giới hạn nào.
            int shots = FireFor(seconds: 120f, step: 1f / 60f);

            Assert.Greater(shots, 1000, $"Chỉ bắn được {shots} phát rồi dừng — vẫn còn giới hạn đạn.");
        }

        // --- Nhịp bắn độc lập khung hình ------------------------------------------------------------

        [Test]
        public void Cadence_IsStableAcrossFrameRates()
        {
            const float seconds = 10f;
            int at60 = FireFor(seconds, 1f / 60f);

            Accumulator = 0f;
            int at30 = FireFor(seconds, 1f / 30f);

            Accumulator = 0f;
            int at15 = FireFor(seconds, 1f / 15f);

            // 10 phát/giây trong 10 giây ≈ 100 phát, bất kể khung hình dài ngắn.
            Assert.AreEqual(100, at60, 2, $"60 FPS: {at60}");
            Assert.AreEqual(100, at30, 2, $"30 FPS: {at30}");
            Assert.AreEqual(100, at15, 2,
                $"15 FPS chỉ bắn {at15} — nhịp bắn tụt theo khung hình, đúng lỗi mà bộ tích luỹ phải chặn.");
        }

        [Test]
        public void SlowWeapon_KeepsItsOwnCadence()
        {
            _weapon.EquipToSlot(1, _slow);
            _weapon.EquipSlot(1);
            Accumulator = 0f;

            int shots = FireFor(seconds: 10f, step: 1f / 60f);

            Assert.AreEqual(10, shots, 1, $"Súng 1 phát/giây bắn {shots} phát trong 10 giây.");
        }

        [Test]
        public void FastAndSlowWeapons_RemainPerceptiblyDifferent()
        {
            int fast = FireFor(5f, 1f / 60f);

            _weapon.EquipToSlot(1, _slow);
            _weapon.EquipSlot(1);
            Accumulator = 0f;
            int slow = FireFor(5f, 1f / 60f);

            Assert.Greater(fast, slow * 5, $"nhanh={fast} chậm={slow}: bản sắc theo nhịp bắn đã mất.");
        }

        // --- Không được dồn loạt -------------------------------------------------------------------

        [Test]
        public void LosingTheTarget_DoesNotBankABurst()
        {
            SetTarget(false);
            for (int i = 0; i < 600; i++) Tick(1f / 60f);   // 10 giây không có mục tiêu

            SetTarget(true);
            _shots = 0;
            Tick(1f / 60f);

            Assert.LessOrEqual(_shots, 1,
                $"Vừa thấy mục tiêu đã nhả {_shots} phát — thời gian trống đã bị dồn thành loạt.");
        }

        [Test]
        public void ReacquiringATarget_AllowsAnImmediateShot()
        {
            // Mặt kia của cùng một đồng xu: chặn dồn loạt không được biến thành trễ khi gặp địch.
            SetTarget(false);
            for (int i = 0; i < 120; i++) Tick(1f / 60f);

            SetTarget(true);
            _shots = 0;
            Tick(1f / 60f);

            Assert.AreEqual(1, _shots, "Gặp lại mục tiêu phải bắn được ngay.");
        }

        [Test]
        public void Pause_DoesNotBankABurst()
        {
            // Khi timeScale = 0 thì deltaTime = 0: mô phỏng đúng bằng cách tick 0 giây.
            for (int i = 0; i < 600; i++) Tick(0f);

            _shots = 0;
            Tick(1f / 60f);

            Assert.LessOrEqual(_shots, 1, $"Bỏ pause là nhả {_shots} phát.");
        }

        [Test]
        public void SwitchingWeapons_DoesNotBankABurst()
        {
            _weapon.EquipToSlot(1, _slow);

            // Tích đầy bộ đếm rồi đổi súng: lần đổi phải xoá sạch thời gian còn nợ.
            Accumulator = 10f;
            _weapon.EquipSlot(1);

            Assert.AreEqual(0f, Accumulator, 1e-4f, "Đổi súng mà vẫn giữ thời gian bắn còn nợ.");

            _shots = 0;
            Tick(1f / 60f);
            Assert.AreEqual(0, _shots, "Đổi súng xong nhả loạt ngay.");
        }

        [Test]
        public void CatchUpIsBoundedPerFrame()
        {
            // Một frame dài bất thường (hitch, breakpoint) không được giải quyết vô hạn phát.
            _shots = 0;
            Tick(5f);

            Assert.LessOrEqual(_shots, 4, $"Một frame giải quyết {_shots} phát — trần bù nhịp không có tác dụng.");
        }

        // --- Trường hợp dữ liệu hỏng ----------------------------------------------------------------

        [Test]
        public void ZeroFireRate_FailsSafeInsteadOfSprayingEveryFrame()
        {
            var broken = MakeWeapon("Broken", 0f, twoHanded: true);
            try
            {
                _weapon.EquipToSlot(1, broken);
                _weapon.EquipSlot(1);

                _shots = 0;
                for (int i = 0; i < 120; i++) Tick(1f / 60f);

                Assert.AreEqual(0, _shots, "Nhịp bắn 0 phải là không bắn, không phải chia cho 0.");
            }
            finally { Object.DestroyImmediate(broken); }
        }

        [Test]
        public void NoTargetInRange_DoesNotFire()
        {
            SetTarget(true, distance: 999f);   // xa hơn tầm súng

            _shots = 0;
            for (int i = 0; i < 120; i++) Tick(1f / 60f);

            Assert.AreEqual(0, _shots, "Bắn vào mục tiêu ngoài tầm.");
        }

        [Test]
        public void UnarmedWeapon_TicksSafely()
        {
            var bare = new GameObject("Unarmed");
            try
            {
                var unarmed = bare.AddComponent<Weapon>();
                Assert.DoesNotThrow(() =>
                    Method("TickAutoFire").Invoke(unarmed, new object[] { 1f / 60f }));
                Assert.AreEqual(0f, unarmed.CurrentFireRate);
            }
            finally { Object.DestroyImmediate(bare); }
        }

        // --- Sức mạnh chiến đấu ---------------------------------------------------------------------

        [Test]
        public void CombatPower_UsesContinuousDpsWithNoReloadTerm()
        {
            var w = MakeWeapon("PowerProbe", 4f);
            try
            {
                w.damage = 25f;
                w.pelletCount = 1;

                float dps = CombatPower.EffectiveDps(w, 0);
                float expected = WeaponUpgradeMath.EffectiveDamage(w, 0) * WeaponUpgradeMath.EffectiveFireRate(w, 0);

                Assert.AreEqual(expected, dps, 0.001f,
                    "DPS phải đúng bằng sát thương × nhịp bắn, không còn số hạng nạp đạn nào.");
            }
            finally { Object.DestroyImmediate(w); }
        }

        [Test]
        public void CombatPower_ScalesWithPelletCount()
        {
            var single = MakeWeapon("Single", 2f);
            var shotgun = MakeWeapon("Shotgun", 2f);
            try
            {
                single.damage = 10f; single.pelletCount = 1;
                shotgun.damage = 10f; shotgun.pelletCount = 6;

                Assert.AreEqual(CombatPower.EffectiveDps(single, 0) * 6f,
                    CombatPower.EffectiveDps(shotgun, 0), 0.01f,
                    "Súng nhiều viên phải được tính đủ số viên.");
            }
            finally
            {
                Object.DestroyImmediate(single);
                Object.DestroyImmediate(shotgun);
            }
        }
    }
}
