using System;
using UnityEngine;

namespace ZombieWar.WorldStreaming
{
    /// <summary>
    /// Bốn trọng số bề mặt liên tục tại một vị trí logic, cộng các trường thô sinh ra chúng.
    ///
    /// Bốn trọng số luôn nằm trong `[0,1]` và cộng lại xấp xỉ `1`. Không có enum biome nào ở đây:
    /// một chunk không "là" một biome, nó chỉ chứa các mẫu nằm trên một trường liên tục.
    /// </summary>
    public readonly struct BiomeSample
    {
        /// <summary>R — đất khô.</summary>
        public readonly float Dry;

        /// <summary>G — cỏ / thảm thực vật.</summary>
        public readonly float Grass;

        /// <summary>B — cát / cây bụi thưa.</summary>
        public readonly float Sand;

        /// <summary>A — đá / sỏi.</summary>
        public readonly float Rock;

        public readonly float Moisture;
        public readonly float Fertility;
        public readonly float Rockiness;
        public readonly float Macro;

        public BiomeSample(float dry, float grass, float sand, float rock,
                           float moisture, float fertility, float rockiness, float macro)
        {
            Dry = dry;
            Grass = grass;
            Sand = sand;
            Rock = rock;
            Moisture = moisture;
            Fertility = fertility;
            Rockiness = rockiness;
            Macro = macro;
        }

        /// <summary>Bốn trọng số đóng gói vào RGBA để ghi thẳng vào vertex color.</summary>
        public Color ToColor() => new Color(Dry, Grass, Sand, Rock);

        public float WeightSum => Dry + Grass + Sand + Rock;

        /// <summary>Tên kênh trội nhất — chỉ dùng cho hiển thị chẩn đoán.</summary>
        public string DominantName
        {
            get
            {
                float peak = Mathf.Max(Mathf.Max(Dry, Grass), Mathf.Max(Sand, Rock));
                if (peak == Dry) return "dry dirt";
                if (peak == Grass) return "grass";
                if (peak == Sand) return "sand/scrub";
                return "rock/gravel";
            }
        }

        public override string ToString() =>
            $"R{Dry:F2} G{Grass:F2} B{Sand:F2} A{Rock:F2}";
    }

    /// <summary>
    /// Trường biome toàn cục, thuần tính toán, không trạng thái.
    ///
    /// Mọi thứ ở đây chỉ phụ thuộc `(worldSeed, worldX, worldZ)`. Không `UnityEngine.Random`, không
    /// `System.Random` dùng chung, không bộ nhớ đệm có thứ tự — nên slot pool nào đang phục vụ toạ độ,
    /// đi tới đó bằng đường nào, hay lấy mẫu theo thứ tự nào đều không thể ảnh hưởng kết quả.
    ///
    /// Noise là value noise nội suy smootherstep trên lưới số nguyên toàn cục. Nó liên tục, đạo hàm
    /// bậc một liên tục, và cố ý KHÔNG khởi động lại bên trong từng chunk — đó chính là điều làm biên
    /// chunk không có đường nối.
    /// </summary>
    public static class BiomeSampler
    {
        // Mỗi trường một muối riêng. Thêm trường mới phải cấp muối mới, không được đổi muối cũ —
        // đổi là thay đổi cả thế giới đã sinh ra trước đó.
        private const int SaltMacro = 0x4D41;
        private const int SaltMoisture = 0x4D4F;
        private const int SaltFertility = 0x4645;
        private const int SaltRockiness = 0x524F;

        // Bước sóng tính bằng mét trong không gian logic.
        private const float MacroScale = 256f;
        private const float MoistureScaleMajor = 160f;
        private const float MoistureScaleMinor = 44f;
        private const float FertilityScaleMajor = 96f;
        private const float FertilityScaleMinor = 27f;
        private const float RockinessScaleMajor = 112f;
        private const float RockinessScaleMinor = 31f;

        /// <summary>Lấy mẫu trường biome tại một vị trí logic toàn cục.</summary>
        public static BiomeSample Sample(int worldSeed, float worldX, float worldZ)
        {
            float macro = ValueNoise(worldSeed, worldX / MacroScale, worldZ / MacroScale, SaltMacro);
            float moisture = Fbm(worldSeed, worldX, worldZ, MoistureScaleMajor, MoistureScaleMinor, SaltMoisture);
            float fertility = Fbm(worldSeed, worldX, worldZ, FertilityScaleMajor, FertilityScaleMinor, SaltFertility);
            float rockiness = Fbm(worldSeed, worldX, worldZ, RockinessScaleMajor, RockinessScaleMinor, SaltRockiness);

            // Bốn "ái lực" không âm. Đất khô giữ một nền tối thiểu để tổng không bao giờ bằng 0,
            // nên phép chuẩn hoá bên dưới luôn hợp lệ mà không cần epsilon giả.
            float dry = 0.30f + 0.45f * (1f - fertility) * (1f - 0.5f * moisture);
            float grass = 1.75f * Shape(moisture * 0.62f + fertility * 0.38f - 0.34f);
            float sand = 1.45f * Shape((1f - moisture) * 0.60f + macro * 0.40f - 0.42f);
            float rock = 1.60f * Shape(rockiness - 0.38f);

            float total = dry + grass + sand + rock;
            float inverse = 1f / total;

            return new BiomeSample(
                dry * inverse, grass * inverse, sand * inverse, rock * inverse,
                moisture, fertility, rockiness, macro);
        }

        public static BiomeSample Sample(int worldSeed, Vector3 worldPosition) =>
            Sample(worldSeed, worldPosition.x, worldPosition.z);

        /// <summary>Làm mềm và kẹp một ái lực thô về `[0,1]` bằng smoothstep.</summary>
        private static float Shape(float t)
        {
            if (t <= 0f) return 0f;
            if (t >= 1f) return 1f;
            return t * t * (3f - 2f * t);
        }

        /// <summary>Hai tầng value noise. Kết quả nằm trong `[0,1]`.</summary>
        private static float Fbm(int seed, float x, float z, float majorScale, float minorScale, int salt)
        {
            float major = ValueNoise(seed, x / majorScale, z / majorScale, salt);
            float minor = ValueNoise(seed, x / minorScale, z / minorScale, salt ^ 0x5BD1);
            return major * 0.68f + minor * 0.32f;
        }

        /// <summary>
        /// Value noise 2D trên lưới số nguyên toàn cục, nội suy smootherstep.
        ///
        /// `Math.Floor` chứ không ép kiểu int: ép kiểu cắt về 0 nên ô lưới quanh gốc toạ độ sẽ rộng
        /// gấp đôi và trường sẽ gãy ở phía âm.
        /// </summary>
        public static float ValueNoise(int seed, float x, float z, int salt)
        {
            int x0 = (int)Math.Floor(x);
            int z0 = (int)Math.Floor(z);
            float fx = x - x0;
            float fz = z - z0;

            float ux = Smootherstep(fx);
            float uz = Smootherstep(fz);

            float c00 = UnitHash(seed, x0, z0, salt);
            float c10 = UnitHash(seed, x0 + 1, z0, salt);
            float c01 = UnitHash(seed, x0, z0 + 1, salt);
            float c11 = UnitHash(seed, x0 + 1, z0 + 1, salt);

            float bottom = c00 + (c10 - c00) * ux;
            float top = c01 + (c11 - c01) * ux;
            return bottom + (top - bottom) * uz;
        }

        private static float Smootherstep(float t) => t * t * t * (t * (t * 6f - 15f) + 10f);

        /// <summary>Băm số nguyên có muối → `[0,1]`. Không trạng thái, không phụ thuộc thứ tự gọi.</summary>
        public static float UnitHash(int seed, int x, int z, int salt) =>
            (Hash(seed, x, z, salt) & 0x00FFFFFFu) * (1f / 0x00FFFFFF);

        /// <summary>Băm nguyên 32-bit theo kiểu avalanche, tất cả đầu vào đều là số nguyên.</summary>
        public static uint Hash(int seed, int x, int z, int salt)
        {
            unchecked
            {
                uint h = (uint)seed * 0x9E3779B1u;
                h ^= (uint)x * 0x85EBCA6Bu;
                h = (h ^ (h >> 15)) * 0xC2B2AE35u;
                h ^= (uint)z * 0x27D4EB2Fu;
                h = (h ^ (h >> 13)) * 0x165667B1u;
                h ^= (uint)salt * 0x9E3779B1u;
                h ^= h >> 16;
                return h;
            }
        }
    }
}
