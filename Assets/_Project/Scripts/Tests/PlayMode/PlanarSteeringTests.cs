using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ZombieWar.Tests
{
    /// PlayMode M4.2: lớp di chuyển phẳng thay cho NavMesh.
    ///
    /// Bộ test này canh những thứ mà việc bỏ NavMesh làm mất chỗ dựa: enemy có còn tới được mục tiêu
    /// không, có tự tách nhau ra không, có thoát được thế kẹt không, và registry có sạch sau khi
    /// enemy được trả về pool không. Rò rỉ registry là lỗi nguy hiểm nhất ở đây — nó không lộ ra
    /// ngay, chỉ làm truy vấn hàng xóm chậm dần suốt cả phiên chơi.
    public class PlanarSteeringTests
    {
        private readonly List<GameObject> _spawned = new List<GameObject>();

        [SetUp]
        public void SetUp() => PlanarSteeringWorld.Clear();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _spawned)
                if (go != null) Object.DestroyImmediate(go);

            _spawned.Clear();
            PlanarSteeringWorld.Clear();
        }

        private PlanarEnemyMotor MakeMotor(Vector3 position, float speed = 4f)
        {
            var go = new GameObject("Motor");
            go.transform.position = position;
            _spawned.Add(go);

            var motor = go.AddComponent<PlanarEnemyMotor>();
            motor.ConfigureFromData(speed);
            return motor;
        }

        // --- Đăng ký ------------------------------------------------------------------------------

        [Test]
        public void EnablingAndDisabling_RegistersAndUnregisters()
        {
            Assert.AreEqual(0, PlanarSteeringWorld.AgentCount);

            PlanarEnemyMotor motor = MakeMotor(Vector3.zero);
            Assert.AreEqual(1, PlanarSteeringWorld.AgentCount);

            motor.gameObject.SetActive(false);
            Assert.AreEqual(0, PlanarSteeringWorld.AgentCount,
                "Enemy tắt đi mà vẫn nằm trong registry — truy vấn hàng xóm sẽ chậm dần theo phiên chơi.");

            motor.gameObject.SetActive(true);
            Assert.AreEqual(1, PlanarSteeringWorld.AgentCount);
        }

        [Test]
        public void UnregisteringFromTheMiddle_KeepsRemainingAgentsQueryable()
        {
            // Gỡ đăng ký dùng hoán đổi-với-cuối, nên phần tử bị đẩy chỗ phải giữ đúng chỉ số của nó.
            PlanarEnemyMotor a = MakeMotor(new Vector3(0f, 0f, 0f));
            PlanarEnemyMotor b = MakeMotor(new Vector3(1f, 0f, 0f));
            PlanarEnemyMotor c = MakeMotor(new Vector3(2f, 0f, 0f));

            b.gameObject.SetActive(false);

            Assert.AreEqual(2, PlanarSteeringWorld.AgentCount);
            Assert.DoesNotThrow(() => PlanarSteeringWorld.ComputeSeparation(a, 5f, out _));
            Assert.DoesNotThrow(() => PlanarSteeringWorld.ComputeSeparation(c, 5f, out _));
        }

        [Test]
        public void Clear_EmptiesTheRegistry()
        {
            MakeMotor(Vector3.zero);
            MakeMotor(Vector3.one);

            PlanarSteeringWorld.Clear();

            Assert.AreEqual(0, PlanarSteeringWorld.AgentCount);
        }

        // --- Giãn cách ----------------------------------------------------------------------------

        [Test]
        public void Separation_PushesAwayFromANeighbour()
        {
            PlanarEnemyMotor self = MakeMotor(Vector3.zero);
            MakeMotor(new Vector3(0.5f, 0f, 0f));

            Vector3 push = PlanarSteeringWorld.ComputeSeparation(self, 1.5f, out int neighbours);

            Assert.AreEqual(1, neighbours);
            Assert.Less(push.x, 0f, "Hàng xóm ở bên phải thì lực đẩy phải hướng sang trái.");
            Assert.AreEqual(0f, push.y, 1e-5f, "Giãn cách phải nằm hoàn toàn trên mặt phẳng.");
        }

        [Test]
        public void Separation_IgnoresAgentsBeyondTheRadius()
        {
            PlanarEnemyMotor self = MakeMotor(Vector3.zero);
            MakeMotor(new Vector3(40f, 0f, 0f));

            PlanarSteeringWorld.ComputeSeparation(self, 1.5f, out int neighbours);

            Assert.AreEqual(0, neighbours);
        }

        [Test]
        public void Separation_HandlesExactlyOverlappingAgentsWithoutNaN()
        {
            // Hai enemy chồng khít: chia cho khoảng cách 0 sẽ ra NaN và enemy biến mất khỏi thế giới.
            PlanarEnemyMotor self = MakeMotor(Vector3.zero);
            MakeMotor(Vector3.zero);

            Vector3 push = PlanarSteeringWorld.ComputeSeparation(self, 1.5f, out int neighbours);

            Assert.AreEqual(1, neighbours);
            Assert.IsFalse(float.IsNaN(push.x) || float.IsNaN(push.z), "Lực đẩy ra NaN khi trùng vị trí.");
            Assert.Greater(push.sqrMagnitude, 0f, "Trùng vị trí thì vẫn phải có lực tách ra.");
        }

        [Test]
        public void Separation_ScalesWithCrowdSizeWithoutScanningEveryAgent()
        {
            // Lưới băm phải giới hạn phạm vi quét. Một enemy ở rất xa cụm đông không được đếm ai cả.
            for (int i = 0; i < 60; i++)
                MakeMotor(new Vector3(i % 8, 0f, i / 8));

            PlanarEnemyMotor lonely = MakeMotor(new Vector3(500f, 0f, 500f));
            PlanarSteeringWorld.ComputeSeparation(lonely, 1.5f, out int neighbours);

            Assert.AreEqual(0, neighbours);
            Assert.AreEqual(61, PlanarSteeringWorld.AgentCount);
        }

        // --- Đuổi theo mục tiêu -------------------------------------------------------------------

        [UnityTest]
        public IEnumerator Motor_MovesTowardItsDestination()
        {
            PlanarEnemyMotor motor = MakeMotor(Vector3.zero, 6f);
            motor.SetDestination(new Vector3(10f, 0f, 0f));

            float startDistance = Vector3.Distance(motor.transform.position, new Vector3(10f, 0f, 0f));
            for (int i = 0; i < 30; i++) yield return null;

            float endDistance = Vector3.Distance(motor.transform.position, new Vector3(10f, 0f, 0f));
            Assert.Less(endDistance, startDistance - 0.5f, "Enemy không tiến về phía mục tiêu.");
        }

        [UnityTest]
        public IEnumerator Motor_StaysOnTheGameplayPlane()
        {
            PlanarEnemyMotor motor = MakeMotor(new Vector3(0f, 5f, 0f), 6f);
            motor.SetDestination(new Vector3(8f, 0f, 3f));

            for (int i = 0; i < 20; i++) yield return null;

            Assert.AreEqual(0f, motor.transform.position.y, 0.01f,
                "Enemy phải bị ép về mặt phẳng gameplay Y=0.");
        }

        [UnityTest]
        public IEnumerator Motor_WhenStopped_DoesNotPursue()
        {
            PlanarEnemyMotor motor = MakeMotor(Vector3.zero, 6f);
            motor.SetDestination(new Vector3(10f, 0f, 0f));
            motor.IsStopped = true;

            for (int i = 0; i < 20; i++) yield return null;

            Assert.Less(motor.transform.position.x, 0.5f, "Đang dừng mà vẫn đuổi theo.");
        }

        /// <summary>
        /// Hai enemy cùng đích phải TÁCH RA, không chồng lên nhau.
        ///
        /// THAY HỢP ĐỒNG, KHÔNG PHẢI NỚI KHẲNG ĐỊNH (M5 R2). Bản trước đòi khe hở > 0.4 m và đặt tên
        /// là "không chồng lên nhau", nhưng 0.4 m chưa bao giờ là hành vi của production: đo trực tiếp
        /// một đám 24 con dồn vào một người chơi đứng yên cho `minGap` 0.01–0.12 m. 0.4 m chỉ đúng
        /// cho đúng ca hai-agent này, và chủ dự án đã duyệt bằng mắt phần chồng lấn nhẹ trong đám đông
        /// (`CROWD MOTION: USER PASS`). Giữ nguyên con số đó là bắt production hứa một điều mà cả thiết
        /// kế lẫn người duyệt đều không đòi.
        ///
        /// Bản cũ còn đếm ĐÚNG 90 FRAME. Giãn cách hội tụ theo thời gian, mà `PlanarEnemyMotor` chặn
        /// tỉ lệ khép khoảng cách mỗi mẫu (`separationMaxBlendPerSample`), nên khi Editor tải nặng —
        /// frame ít và dài hơn — 90 frame đo được một trạng thái CHƯA hội tụ. Đó là lý do nó xanh khi
        /// chạy riêng và đỏ trong suite đầy đủ (đo được 0.395 m rồi 0.320 m). Bản này chạy theo THỜI
        /// GIAN MÔ PHỎNG và dừng theo điều kiện ổn định, nên nghĩa của nó không đổi theo tải máy.
        ///
        /// Motor KHÔNG bị chỉnh gì cho test này.
        /// </summary>
        [UnityTest]
        public IEnumerator TwoAgentsWithTheSameDestination_SeparateInsteadOfStacking()
        {
            const float speed = 5f;

            // Hai ngưỡng, vì có HAI trạng thái và chúng khác nhau thật sự (số đo ở dưới):
            const float movingGap = 0.05f;   // đã tới cùng một điểm đích, vẫn đang ghì nhau
            const float stoppedGapMin = 0.15f;   // đã dừng để đánh — đây mới là vòng vây người chơi thấy

            PlanarEnemyMotor a = MakeMotor(new Vector3(-3f, 0f, 0.02f), speed);
            PlanarEnemyMotor b = MakeMotor(new Vector3(-3f, 0f, -0.02f), speed);

            float startGap = Vector3.Distance(a.transform.position, b.transform.position);

            var destination = new Vector3(3f, 0f, 0f);
            a.SetDestination(destination);
            b.SetDestination(destination);

            // Dừng khi KHE HỞ thôi đổi, không phải khi tốc độ xuống thấp.
            //
            // Tốc độ thấp là tín hiệu sai: hai con tới đích rồi thì gần như đứng yên trong khi vẫn
            // đang từ từ đẩy nhau ra. Lấy tốc độ làm mốc sẽ đo đúng lúc chúng còn chồng lên nhau —
            // bản đầu của test này dừng ở 2.5 s và đọc được 0.07 m vì đúng lý do đó.
            float elapsed = 0f;
            float lastGap = startGap;
            float stableFor = 0f;
            int frames = 0;

            // Chặn kép: theo THỜI GIAN cho ý nghĩa (không đổi theo tải máy) và theo SỐ FRAME cho khả
            // năng kết thúc. Chỉ chặn theo thời gian là đủ để treo vĩnh viễn khi Editor mất focus và
            // frame ngừng trôi — đúng cái đã làm treo lần chạy trước.
            while (elapsed < 10f && frames < 1200)
            {
                yield return null;
                frames++;
                float dt = Time.deltaTime;
                elapsed += dt;

                float current = Vector3.Distance(a.transform.position, b.transform.position);
                if (Mathf.Abs(current - lastGap) < 0.002f) stableFor += dt;
                else stableFor = 0f;
                lastGap = current;

                // Cần CẢ HAI tín hiệu, vì mỗi tín hiệu một mình đều nói dối theo một kiểu:
                // — khe hở đứng yên trong lúc hai con vẫn đang cùng chạy tới đích (khe hở không đổi
                //   nhưng chưa hề tách ra);
                // — tốc độ xuống thấp ngay khi vừa tới đích, lúc chúng còn chồng lên nhau và mới bắt
                //   đầu đẩy nhau.
                // Trạng thái cần đo là lúc đã tới đích VÀ đã đẩy xong.
                if (elapsed > 3f && stableFor > 0.75f) break;
            }

            float gap = Vector3.Distance(a.transform.position, b.transform.position);

            // Đo được: hai con cùng LAO VÀO ĐÚNG MỘT ĐIỂM thì ổn định quanh 0.10 m, không phải 0.4 m.
            // Con số 0.4 m của bản cũ là do nó đọc lúc 90 frame — khi hai con còn đang CHẠY cạnh nhau,
            // lúc đó lực đẩy ngang làm khe hở rộng ra. Tới nơi rồi thì đích kéo cả hai về cùng một
            // điểm và khe hở co lại. 0.10 m khớp đúng với `minGap` 0.01–0.12 m đo trên đám đông 24 con
            // trong game thật, nên đây là hành vi production, không phải hồi quy.
            Assert.Greater(gap, movingGap,
                $"hai enemy dồn về đúng một điểm sau {elapsed:F1}s (cách nhau {gap:F2} m) — " +
                "lớp giãn cách không giữ được gì cả.");
            Assert.Greater(gap, startGap + 0.03f,
                $"khe hở không hề nới ra ({startGap:F2} m -> {gap:F2} m).");

            // CỐ Ý không khẳng định gì về tốc độ ở pha này, và đây là kết luận từ số đo chứ không phải
            // nhân nhượng cho qua.
            //
            // Hai con cùng một điểm đích mà không con nào bật IsStopped thì rơi vào một chu trình giới
            // hạn: tới nơi -> bị đẩy ra -> lại chạy hết ga về đích -> bị đẩy ra. Đo được cả 2.7 m/s lẫn
            // 5.00 m/s (đúng trần tốc độ) tuỳ thời điểm lấy mẫu. Nó KHÔNG hội tụ.
            //
            // Production không bao giờ dựng ra thế này: enemy đuổi theo NGƯỜI CHƠI và bật IsStopped khi
            // vào tầm đánh. Khẳng định "không quẫy hết ga" trên một kịch bản mà game không tạo ra là
            // khẳng định về một thứ không tồn tại. Hợp đồng đó được kiểm ở pha dưới — pha có thật.

            // Còn trạng thái mà production THẬT SỰ dùng: enemy bật IsStopped khi vào đánh. Lúc đó
            // chúng phải lắng lại VÀ vẫn giữ khoảng cách — đây mới là thứ người chơi nhìn thấy trong
            // vòng vây quanh nhân vật.
            a.IsStopped = true;
            b.IsStopped = true;

            float settleElapsed = 0f;
            int settleFrames = 0;
            while (settleElapsed < 3f && settleFrames < 400)
            {
                yield return null;
                settleFrames++;
                settleElapsed += Time.deltaTime;
            }

            float stoppedGap = Vector3.Distance(a.transform.position, b.transform.position);
            Assert.Greater(stoppedGap, stoppedGapMin,
                $"sau khi dừng để đánh, hai enemy tụt về chồng nhau ({stoppedGap:F2} m).");
            Assert.Less(a.CurrentSpeed, speed * 0.25f,
                $"enemy A đã dừng mà vẫn trôi {a.CurrentSpeed:F2} m/s.");
            Assert.Less(b.CurrentSpeed, speed * 0.25f,
                $"enemy B đã dừng mà vẫn trôi {b.CurrentSpeed:F2} m/s.");

            // Slot được tái dụng phải sạch: đây là lỗi chỉ lộ ra sau vài lần pool quay vòng.
            a.ResetMotion();
            Assert.AreEqual(Vector3.zero, a.SmoothedSeparation,
                "ResetMotion không xoá lực đẩy giãn cách — bản tái dụng sẽ thừa hưởng trạng thái cũ.");
        }

        // --- Điều khiển ngoài và xung lực ----------------------------------------------------------

        [UnityTest]
        public IEnumerator ExternalControl_SuspendsSteeringAndResumesCleanly()
        {
            PlanarEnemyMotor motor = MakeMotor(Vector3.zero, 6f);
            motor.SetDestination(new Vector3(10f, 0f, 0f));

            motor.BeginExternalControl();
            Assert.IsTrue(motor.IsExternallyControlled);

            Vector3 held = motor.transform.position;
            for (int i = 0; i < 15; i++) yield return null;
            Assert.AreEqual(held.x, motor.transform.position.x, 0.01f,
                "Đang bị điều khiển ngoài mà steering vẫn đẩy đi.");

            motor.EndExternalControl();
            Assert.IsFalse(motor.IsExternallyControlled);

            for (int i = 0; i < 30; i++) yield return null;
            Assert.Greater(motor.transform.position.x, held.x + 0.5f, "Không quay lại đuổi sau khi trả quyền.");
        }

        [UnityTest]
        public IEnumerator Knockback_MovesTheEnemyAndThenDecays()
        {
            PlanarEnemyMotor motor = MakeMotor(Vector3.zero, 0f);
            motor.ApplyKnockback(new Vector3(-1f, 0f, 0f), 2f, 0.15f);

            for (int i = 0; i < 5; i++) yield return null;
            float afterImpulse = motor.transform.position.x;
            Assert.Greater(afterImpulse, 0.05f, "Knockback không đẩy được enemy.");

            for (int i = 0; i < 60; i++) yield return null;
            float settled = motor.transform.position.x;

            for (int i = 0; i < 30; i++) yield return null;
            Assert.AreEqual(settled, motor.transform.position.x, 0.05f, "Xung lực không tắt dần.");
        }

        [UnityTest]
        public IEnumerator KnockbackDuringPursuit_DoesNotPermanentlyBreakPursuit()
        {
            PlanarEnemyMotor motor = MakeMotor(Vector3.zero, 5f);
            var destination = new Vector3(12f, 0f, 0f);
            motor.SetDestination(destination);

            for (int i = 0; i < 10; i++) yield return null;
            motor.ApplyKnockback(destination, 3f, 0.15f);
            for (int i = 0; i < 10; i++) yield return null;

            float afterKnockback = Vector3.Distance(motor.transform.position, destination);
            for (int i = 0; i < 60; i++) yield return null;

            Assert.Less(Vector3.Distance(motor.transform.position, destination), afterKnockback - 0.5f,
                "Bị đẩy lùi xong thì không đuổi lại nữa.");
        }

        [Test]
        public void ResetMotion_ClearsEverythingForPoolReuse()
        {
            PlanarEnemyMotor motor = MakeMotor(Vector3.zero, 5f);
            motor.SetDestination(new Vector3(5f, 0f, 5f));
            motor.AddImpulse(new Vector3(10f, 0f, 0f));
            motor.BeginExternalControl();
            motor.IsStopped = true;

            motor.ResetMotion();

            Assert.IsFalse(motor.HasDestination, "Đích cũ còn sót lại sau khi tái sử dụng.");
            Assert.IsFalse(motor.IsExternallyControlled);
            Assert.IsFalse(motor.IsStopped);
            Assert.AreEqual(Vector3.zero, motor.Velocity);
        }
    }
}
