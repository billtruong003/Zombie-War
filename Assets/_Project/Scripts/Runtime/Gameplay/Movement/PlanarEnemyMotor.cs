using UnityEngine;

namespace ZombieWar
{
    /// <summary>
    /// Bộ máy di chuyển phẳng thay cho <c>NavMeshAgent</c>.
    ///
    /// Thế giới sau M3B là một mặt phẳng mở, gần như không có vật cản dựng sẵn: trang trí thủ tục
    /// không có collider, và chỉ vài blocker gameplay là thật. Trong hoàn cảnh đó NavMesh trả tiền cho
    /// thứ nó không dùng đến — bake dữ liệu, giải đường đi, ràng buộc enemy vào một mặt lưới phải tồn
    /// tại trước. Cái thật sự cần là đuổi theo mục tiêu, không giẫm lên nhau, và lách qua vài vật cản.
    ///
    /// API cố tình giữ hình dạng gần với <c>NavMeshAgent</c> (<see cref="Speed"/>, <see cref="IsStopped"/>,
    /// <see cref="SetDestination"/>, <see cref="Move"/>, <see cref="Warp"/>) để việc chuyển đổi ở các
    /// zombie subclass là thay tên chứ không phải viết lại logic hành vi của chúng.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlanarEnemyMotor : MonoBehaviour
    {
        [Header("Locomotion")]
        [SerializeField] private float speed = 3f;
        [SerializeField] private float acceleration = 18f;
        [SerializeField] private float deceleration = 24f;
        [Tooltip("Độ/giây. Quay thân về hướng đang đi.")]
        [SerializeField] private float turnSpeed = 720f;

        [Header("Separation")]
        [SerializeField] private float separationRadius = 1.2f;
        [SerializeField] private float separationWeight = 1.6f;
        [Tooltip("Raw push below this is noise between neighbours who are already comfortable - " +
                 "treat it as zero so a settled crowd stops correcting itself.")]
        [SerializeField] private float separationDeadZone = 0.18f;
        [Tooltip("Ceiling on the processed push. Real penetration still pushes hard; nothing pushes " +
                 "harder than this no matter how many neighbours pile on.")]
        [SerializeField] private float separationMaxMagnitude = 1f;
        [Tooltip("Gain applied before the ceiling, so the push SATURATES once a neighbour is inside " +
                 "roughly half the separation radius. This is what fixes the settle distance: " +
                 "without it the raw falloff balances pursuit at ~0.45 m and the pair oscillates " +
                 "across the authored 0.4 m minimum.")]
        [SerializeField] private float separationGain = 2f;
        [Tooltip("Higher settles faster and jitters more. Frame-rate independent (exponential).")]
        [SerializeField] private float separationSmoothing = 8f;
        [Tooltip("Most the smoothed push may move toward its target in ONE sample. At 60 FPS the " +
                 "exponential term is already below this; at 15 FPS it is not, and trusting a coarse " +
                 "sample that much made the crowd oscillate far harder than at 60 FPS (measured " +
                 "39.3 vs 2.2 lateral reversals/s on the same crowd).")]
        [Range(0.02f, 1f)] [SerializeField] private float separationMaxBlendPerSample = 0.2f;
        [Tooltip("While stopped to attack, separation may move the enemy at most this fraction of " +
                 "its move speed - enough to unstack, not enough to dance during a swing.")]
        [Range(0f, 1f)] [SerializeField] private float stoppedSeparationMax = 0.2f;

        [Header("Obstacle avoidance")]
        [Tooltip("Tầm dò phía trước, mét. 0 = tắt hẳn việc dò vật cản.")]
        [SerializeField] private float obstacleProbeDistance = 1.5f;
        [SerializeField] private float obstacleAvoidWeight = 2.2f;
        [Tooltip("Layer của vật cản gameplay THẬT. Trang trí thủ tục không có collider nên không nằm ở đây.")]
        [SerializeField] private LayerMask obstacleMask = 0;

        [Header("Stuck recovery")]
        [Tooltip("Tốc độ thực tế dưới ngưỡng này trong khi vẫn muốn đi = coi như đang kẹt.")]
        [SerializeField] private float stuckSpeedThreshold = 0.35f;
        [SerializeField] private float stuckDuration = 0.8f;
        [Tooltip("Thời gian đi vòng sang bên sau khi phát hiện kẹt.")]
        [SerializeField] private float bypassDuration = 1.1f;
        [SerializeField] private float bypassWeight = 2.4f;

        [Header("Knockback")]
        [Tooltip("Hệ số tắt dần mỗi giây của xung lực ngoài.")]
        [SerializeField] private float knockbackDamping = 9f;

        /// <summary>Chỉ số trong registry của <see cref="PlanarSteeringWorld"/>. -1 = chưa đăng ký.</summary>
        internal int SteeringIndex { get; set; } = -1;

        private Vector3 _destination;
        private bool _hasDestination;
        private Vector3 _velocity;
        private Vector3 _smoothedSeparation;
        private Vector3 _externalImpulse;
        private float _stuckTimer;
        private float _bypassUntil;
        private float _bypassSign;
        private bool _externalControl;

        private static readonly RaycastHit[] ProbeHits = new RaycastHit[4];

        public float Speed
        {
            get => speed;
            set => speed = Mathf.Max(0f, value);
        }

        /// <summary>Authored turn rate, degrees/second. Exposed so a behaviour that owns facing while
        /// the motor is stopped (attacking) turns at the same rate the motor itself would.</summary>
        public float TurnSpeed => turnSpeed;

        /// <summary>Dừng bước đuổi. Xung lực ngoài (knockback) VẪN được áp — nó không phải là "đi".</summary>
        public bool IsStopped { get; set; }

        /// <summary>Vận tốc phẳng thực tế của frame gần nhất.</summary>
        public Vector3 Velocity => _velocity;

        /// <summary>Tốc độ thực tế, mét/giây.</summary>
        public float CurrentSpeed => _velocity.magnitude;

        public bool HasDestination => _hasDestination;
        public Vector3 Destination => _destination;

        /// <summary>Một hành vi đặc biệt (lao, nhảy vồ, chui đất) đang tự lái. Steering thường đứng ngoài.</summary>
        public bool IsExternallyControlled => _externalControl;

        /// <summary>Đang trong pha đi vòng để thoát kẹt.</summary>
        public bool IsBypassing => Time.time < _bypassUntil;

        /// <summary>Hiệu chỉnh giãn cách sau khi đã qua vùng chết, cắt trần và làm mượt. Test và
        /// công cụ đo đọc giá trị này để chứng minh nó không còn nhảy toàn lực.</summary>
        public Vector3 SmoothedSeparation => _smoothedSeparation;

        private void OnEnable()
        {
            PlanarSteeringWorld.Register(this);
            ResetMotion();
        }

        private void OnDisable() => PlanarSteeringWorld.Unregister(this);

        /// <summary>
        /// Xoá sạch mọi trạng thái động.
        ///
        /// Bắt buộc cho pooling: một enemy tái sử dụng không được mang theo vận tốc, xung lực hay pha
        /// đi vòng của kiếp trước, nếu không nó sẽ "trôi" ngay khi vừa hồi sinh.
        /// </summary>
        public void ResetMotion()
        {
            _velocity = Vector3.zero;
            _smoothedSeparation = Vector3.zero;   // a reused instance must not inherit a push
            _externalImpulse = Vector3.zero;
            _stuckTimer = 0f;
            _bypassUntil = 0f;
            _hasDestination = false;
            _externalControl = false;
            IsStopped = false;

            // Hướng lách trái/phải suy từ InstanceID nên nó CỐ ĐỊNH cho mỗi enemy: hai con gặp nhau
            // trực diện sẽ lách về hai phía khác nhau và tự gỡ thế kẹt. Random mỗi frame thì cả hai
            // sẽ đổi hướng liên tục và dính nhau mãi.
            _bypassSign = (GetInstanceID() & 1) == 0 ? 1f : -1f;
        }

        public void SetDestination(Vector3 worldPosition)
        {
            _destination = new Vector3(worldPosition.x, 0f, worldPosition.z);
            _hasDestination = true;
        }

        public void ClearDestination() => _hasDestination = false;

        /// <summary>Dịch chuyển tức thời, giữ nguyên mặt phẳng gameplay.</summary>
        public void Warp(Vector3 worldPosition)
        {
            transform.position = new Vector3(worldPosition.x, 0f, worldPosition.z);
            _velocity = Vector3.zero;
        }

        /// <summary>
        /// Dịch chuyển trực tiếp một đoạn, bỏ qua steering.
        ///
        /// Thay cho <c>NavMeshAgent.Move</c>: các pha đặc biệt (lao húc, nhảy vồ, knockback thủ công)
        /// dùng nó để tự lái. Vẫn ép về mặt phẳng gameplay.
        /// </summary>
        public void Move(Vector3 delta)
        {
            delta.y = 0f;
            transform.position += delta;
        }

        /// <summary>Nhận quyền lái từ một hành vi đặc biệt. Luôn phải có <see cref="EndExternalControl"/> đi kèm.</summary>
        public void BeginExternalControl()
        {
            _externalControl = true;
            _velocity = Vector3.zero;
        }

        public void EndExternalControl()
        {
            _externalControl = false;
            _stuckTimer = 0f;
            _bypassUntil = 0f;
        }

        /// <summary>
        /// Thêm một xung lực ngoài (knockback). Tắt dần theo <see cref="knockbackDamping"/>.
        ///
        /// Đây là thứ thay cho việc trước kia phải đẩy qua <c>Agent.Move</c> vì Rigidbody không nhúc
        /// nhích nổi một NavMeshAgent. Giờ không còn agent nào ghi đè vị trí, nên xung lực chỉ cần
        /// cộng vào chuyển động là thấy được ngay.
        /// </summary>
        public void AddImpulse(Vector3 impulse)
        {
            impulse.y = 0f;
            _externalImpulse += impulse;
        }

        /// <summary>Xoá xung lực ngoài đang có. Dùng khi một nguồn đẩy khác giành quyền sở hữu.</summary>
        public void ClearImpulse() => _externalImpulse = Vector3.zero;

        /// <summary>Đẩy ra xa một điểm, theo khoảng cách và thời gian mong muốn.</summary>
        public void ApplyKnockback(Vector3 fromPosition, float distance, float duration)
        {
            if (distance <= 0f) return;

            Vector3 away = transform.position - fromPosition;
            away.y = 0f;
            if (away.sqrMagnitude < 0.0001f) away = -transform.forward;

            duration = Mathf.Max(0.05f, duration);
            AddImpulse(away.normalized * (distance / duration));
        }

        /// <summary>
        /// Chỉ lấy thành phần giãn cách, không đuổi theo ai.
        ///
        /// Dùng cho enemy đang dừng (đánh, chờ, bị khoá): nó vẫn phải nhường chỗ cho hàng xóm, nhưng
        /// không được tự ý tiến về mục tiêu khi hành vi đang bảo nó đứng yên.
        ///
        /// M5.1.3: kết quả bị chặn ở <see cref="stoppedSeparationMax"/> phần tốc độ. Trước đây vector
        /// này được CHUẨN HOÁ rồi nhân với toàn bộ tốc độ, nên một chênh lệch tí xíu giữa hàng xóm
        /// cũng đẩy con đang đứng đánh chạy hết tốc lực — đó chính là cái "nhún nhảy" người chơi thấy.
        /// </summary>
        private Vector3 ComputeSeparationOnly(float dt)
        {
            if (separationWeight <= 0f || separationRadius <= 0f) return Vector3.zero;

            Vector3 separation = PlanarSteeringWorld.ComputeSeparation(this, separationRadius, out _);
            Vector3 processed = ProcessSeparation(separation, dt);
            return Vector3.ClampMagnitude(processed, stoppedSeparationMax);
        }

        /// <summary>
        /// Biến vector đẩy thô thành một hiệu chỉnh lái GIỮ ĐỘ LỚN, có vùng chết và có quán tính.
        ///
        /// Chuẩn hoá là gốc rễ của hiện tượng giật: nó ném đi thông tin "đẩy mạnh hay yếu" và biến
        /// mọi mất cân bằng nhỏ nhất thành một mệnh lệnh đầy sức. Ở đây độ lớn được giữ lại, trừ đi
        /// vùng chết (đám đông đã yên thì thôi sửa), cắt trần (không ai đẩy mạnh vô hạn), rồi làm
        /// mượt theo thời gian bằng hệ số độc lập khung hình — nên 15 FPS không dao động mạnh hơn 60.
        /// </summary>
        private Vector3 ProcessSeparation(Vector3 raw, float dt)
        {
            Vector3 target = Vector3.zero;
            float magnitude = raw.magnitude;

            if (magnitude > separationDeadZone)
            {
                // The dead zone GATES, it does not subtract. Subtracting weakened the mid-range
                // push that actually keeps a crowd spaced - two enemies chasing one point settled
                // 0.25 m apart instead of the authored 0.4 m minimum. A smoothstep ramp across the
                // gate band silences neighbour noise without taxing a push that is doing real work,
                // and avoids the pop a hard gate would produce.
                float gate = Mathf.SmoothStep(0f, 1f,
                    Mathf.InverseLerp(separationDeadZone, separationDeadZone * 2f, magnitude));
                float scaled = Mathf.Min(magnitude * separationGain, separationMaxMagnitude) * gate;
                target = raw / magnitude * scaled;
            }

            // Exponential smoothing: dt-độc lập, không cấp phát. dt<=0 (frame đầu, pause) giữ nguyên.
            // Trần mỗi mẫu là thứ giữ cho 15 FPS không rung hơn 60 FPS: khi khung hình thưa, mỗi mẫu
            // vừa nhiễu hơn vừa cách xa nhau hơn, nên không được tin nó nhiều hơn một mức nhất định.
            float blend = dt > 0f
                ? Mathf.Min(1f - Mathf.Exp(-separationSmoothing * dt), separationMaxBlendPerSample)
                : 0f;
            _smoothedSeparation += (target - _smoothedSeparation) * blend;
            if (_smoothedSeparation.sqrMagnitude < 0.000001f) _smoothedSeparation = Vector3.zero;
            return _smoothedSeparation;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            // Xung lực ngoài luôn được áp, kể cả khi đang bị điều khiển ngoài hoặc đang dừng: bị bắn
            // lùi trong lúc vung tay vẫn phải thấy được.
            if (_externalImpulse.sqrMagnitude > 0.0001f)
            {
                Move(_externalImpulse * dt);
                _externalImpulse = Vector3.MoveTowards(
                    _externalImpulse, Vector3.zero, knockbackDamping * dt * Mathf.Max(1f, _externalImpulse.magnitude));
            }
            else
            {
                _externalImpulse = Vector3.zero;
            }

            if (_externalControl) return;

            // Dừng để tấn công vẫn phải giãn cách. Nếu bỏ hẳn steering khi dừng, cả đám vây quanh
            // người chơi sẽ dồn về đúng một điểm và chồng khít lên nhau (đo được 0,16 m khi kiểm
            // chứng Map_Level1). Giữ lại riêng thành phần giãn cách biến đống đó thành một vòng vây.
            Vector3 desired;
            if (IsStopped || !_hasDestination) desired = ComputeSeparationOnly(dt);
            else desired = ComputeDesiredDirection(dt);
            Vector3 targetVelocity = desired * speed;

            float rate = targetVelocity.sqrMagnitude > _velocity.sqrMagnitude ? acceleration : deceleration;
            _velocity = Vector3.MoveTowards(_velocity, targetVelocity, rate * dt);
            _velocity.y = 0f;

            if (_velocity.sqrMagnitude > 0.000001f)
            {
                Move(_velocity * dt);
                // Facing ownership: while stopped (attacking/held) the BEHAVIOUR owns facing - it
                // turns toward its target. Rotating here off separation micro-velocities at the same
                // time made two writers fight over the transform every frame (M5.1 audit, CP2).
                if (!IsStopped) FaceMovement(dt);
            }

            // Giữ enemy đúng trên mặt phẳng gameplay. Mặt đất thủ tục chỉ là hình ảnh; độ cao gameplay
            // là hằng số, nên không cần raycast xuống đất mỗi frame.
            Vector3 position = transform.position;
            if (!Mathf.Approximately(position.y, 0f))
            {
                position.y = 0f;
                transform.position = position;
            }
        }

        private Vector3 ComputeDesiredDirection(float dt)
        {
            Vector3 origin = transform.position;
            Vector3 toTarget = _destination - origin;
            toTarget.y = 0f;

            float distance = toTarget.magnitude;

            // Standing ON the destination is not a reason to stop separating. Returning a flat zero
            // here let every enemy that reached the same point stack there: with magnitude-preserving
            // separation the push ramps in smoothly, so two chasers can both arrive before it has
            // built, and then this early-out froze them 2 cm apart. Arrived enemies keep the
            // separation term - it is the only thing holding the ring open.
            if (distance < 0.05f)
                return separationWeight > 0f && separationRadius > 0f
                    ? ProcessSeparation(PlanarSteeringWorld.ComputeSeparation(this, separationRadius, out _), dt)
                    : Vector3.zero;

            Vector3 pursuit = toTarget / distance;
            Vector3 desired = pursuit;

            if (separationWeight > 0f && separationRadius > 0f)
            {
                // Magnitude-preserving: a light brush nudges the pursuit line, a real overlap
                // shoves. Normalising here used to make both identical, so a jostled crowd
                // rewrote every enemy's heading at full strength every frame.
                Vector3 separation = PlanarSteeringWorld.ComputeSeparation(this, separationRadius, out _);
                desired += ProcessSeparation(separation, dt) * separationWeight;
            }

            if (obstacleProbeDistance > 0f && obstacleMask.value != 0)
            {
                Vector3 avoid = ComputeObstacleAvoidance(origin, pursuit);
                if (avoid.sqrMagnitude > 0.000001f) desired += avoid * obstacleAvoidWeight;
            }

            UpdateStuckState(dt, distance);

            if (IsBypassing)
            {
                // Đi vòng = thành phần vuông góc với hướng đuổi, chọn bên theo dấu cố định của enemy.
                var tangent = new Vector3(-pursuit.z, 0f, pursuit.x) * _bypassSign;
                desired += tangent * bypassWeight;
            }

            desired.y = 0f;
            return desired.sqrMagnitude > 0.000001f ? desired.normalized : Vector3.zero;
        }

        /// <summary>
        /// Dò một tia về phía trước và trả về hướng né.
        ///
        /// Dùng <c>RaycastNonAlloc</c> với bộ đệm tĩnh: một truy vấn cấp phát mỗi enemy mỗi frame là
        /// đúng thứ sinh rác đều đặn nhất trong một trận đông người.
        /// </summary>
        private Vector3 ComputeObstacleAvoidance(Vector3 origin, Vector3 forward)
        {
            Vector3 probeOrigin = origin + Vector3.up * 0.5f;
            int count = Physics.RaycastNonAlloc(probeOrigin, forward, ProbeHits, obstacleProbeDistance,
                obstacleMask, QueryTriggerInteraction.Ignore);
            if (count <= 0) return Vector3.zero;

            RaycastHit nearest = ProbeHits[0];
            for (int i = 1; i < count; i++)
                if (ProbeHits[i].distance < nearest.distance) nearest = ProbeHits[i];

            Vector3 normal = nearest.normal;
            normal.y = 0f;
            if (normal.sqrMagnitude < 0.0001f) return Vector3.zero;

            // Trượt dọc mặt vật cản thay vì bật thẳng ra: bật ra sẽ tạo dao động vào-ra liên tục.
            Vector3 slide = Vector3.ProjectOnPlane(forward, normal.normalized);
            slide.y = 0f;
            if (slide.sqrMagnitude < 0.0001f)
                slide = new Vector3(-forward.z, 0f, forward.x) * _bypassSign;

            float closeness = 1f - Mathf.Clamp01(nearest.distance / Mathf.Max(0.01f, obstacleProbeDistance));
            return slide.normalized * closeness;
        }

        private void UpdateStuckState(float dt, float distanceToTarget)
        {
            if (IsBypassing) return;

            // Kẹt = MUỐN đi (còn xa mục tiêu) nhưng thực tế gần như đứng yên.
            bool wantsToMove = distanceToTarget > 0.5f && !IsStopped;
            if (wantsToMove && CurrentSpeed < stuckSpeedThreshold)
            {
                _stuckTimer += dt;
                if (_stuckTimer >= stuckDuration)
                {
                    _bypassUntil = Time.time + bypassDuration;
                    _stuckTimer = 0f;
                }
            }
            else
            {
                _stuckTimer = 0f;
            }
        }

        private void FaceMovement(float dt)
        {
            Vector3 flat = _velocity;
            flat.y = 0f;
            if (flat.sqrMagnitude < 0.0001f) return;

            transform.rotation = Quaternion.RotateTowards(
                transform.rotation, Quaternion.LookRotation(flat), turnSpeed * dt);
        }

        /// <summary>Đặt lại các tham số điều khiển từ dữ liệu enemy khi (tái) sinh.</summary>
        public void ConfigureFromData(float moveSpeed)
        {
            speed = Mathf.Max(0f, moveSpeed);
        }
    }
}
