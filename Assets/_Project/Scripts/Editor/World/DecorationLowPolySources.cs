using System.Collections.Generic;
using UnityEngine;
using ZombieWar.WorldStreaming;

namespace ZombieWar.Editor
{
    /// <summary>
    /// Sinh hình học trang trí RẺ, thuộc sở hữu project, thay cho những nguồn vendor quá đắt.
    ///
    /// Vì sao phải tự sinh thay vì chọn nguồn vendor rẻ hơn: trong pack này không còn nguồn nào rẻ hơn.
    /// Cỏ 230 đỉnh đã là mesh nhẹ nhất, các mức LOD1/LOD2 của cây cỏ nhỏ đều RỖNG (chúng chỉ để cull),
    /// và ô atlas 0 không phải atlas sprite mà là một BẢNG MÀU phẳng — nghĩa là silhouette của cỏ nằm ở
    /// hình học chứ không ở alpha, nên không thể thay bằng một tấm card alpha.
    ///
    /// Hai bộ sinh dưới đây tất định tuyệt đối: chúng chỉ dùng <see cref="DecorationHash"/> với hạt
    /// giống cố định, không đụng `UnityEngine.Random` hay `System.Random`.
    /// </summary>
    public static class DecorationLowPolySources
    {
        /// <summary>
        /// Bụi cỏ: N lá cỏ, mỗi lá là một quad thuôn nhọn (4 đỉnh / 2 tam giác).
        ///
        /// UV lấy đúng dải màu mà cỏ vendor đang lấy, nên màu và chuyển sắc gốc-ngọn giữ nguyên;
        /// chỉ số lượng lá giảm xuống.
        /// </summary>
        public static void BuildGrassTuft(int hashSeed, int bladeCount, float radius, float height,
            float bladeWidth, Vector2 uvBase, Vector2 uvTip,
            out Vector3[] positions, out Vector3[] normals, out Vector2[] uv, out int[] indices)
        {
            positions = new Vector3[bladeCount * 4];
            normals = new Vector3[bladeCount * 4];
            uv = new Vector2[bladeCount * 4];
            indices = new int[bladeCount * 6];

            for (int i = 0; i < bladeCount; i++)
            {
                float angle = DecorationHash.Unit(hashSeed, i, 0, 11, 0, HashLane.Rotation) * Mathf.PI * 2f;
                float dist = Mathf.Sqrt(DecorationHash.Unit(hashSeed, i, 0, 11, 1, HashLane.JitterX)) * radius;
                float yaw = DecorationHash.Unit(hashSeed, i, 0, 11, 2, HashLane.JitterZ) * Mathf.PI * 2f;
                float h = height * Mathf.Lerp(0.62f, 1.25f, DecorationHash.Unit(hashSeed, i, 0, 11, 3, HashLane.Scale));
                float leanX = DecorationHash.Signed(hashSeed, i, 0, 11, 4, HashLane.Acceptance) * 0.28f * h;
                float leanZ = DecorationHash.Signed(hashSeed, i, 0, 11, 5, HashLane.Priority) * 0.28f * h;

                var root = new Vector3(Mathf.Cos(angle) * dist, 0f, Mathf.Sin(angle) * dist);
                var side = new Vector3(Mathf.Cos(yaw), 0f, Mathf.Sin(yaw)) * (bladeWidth * 0.5f);
                var tipSide = side * 0.14f;
                var tip = root + new Vector3(leanX, h, leanZ);

                int v = i * 4;
                positions[v + 0] = root - side;
                positions[v + 1] = root + side;
                positions[v + 2] = tip + tipSide;
                positions[v + 3] = tip - tipSide;

                // Normal hướng lên: foliage ở đây ăn đèn ba dải phẳng, normal dựng đứng theo mặt lá
                // sẽ làm mỗi lá cỏ loé lên một sắc khác nhau và phá vỡ cảm giác cartoon.
                for (int k = 0; k < 4; k++) normals[v + k] = Vector3.up;

                uv[v + 0] = uvBase;
                uv[v + 1] = uvBase;
                uv[v + 2] = uvTip;
                uv[v + 3] = uvTip;

                int t = i * 6;
                indices[t + 0] = v + 0;
                indices[t + 1] = v + 3;
                indices[t + 2] = v + 2;
                indices[t + 3] = v + 0;
                indices[t + 4] = v + 2;
                indices[t + 5] = v + 1;
            }
        }

        /// <summary>
        /// Tán cây thuần hình học: một khối đa diện lệch, phẳng mặt, KHÔNG dùng card alpha.
        ///
        /// Đây là hình học của phương án B trong phép so sánh nghệ thuật M4.5. Tán cây hiện tại của
        /// phương án A là 1.840 đỉnh card cắt alpha; ở cỡ hiển thị thật, thứ người chơi đọc được chỉ
        /// là một khối lá có bóng dáng rõ ràng, và một khối đa diện cho đúng bóng dáng đó bằng vài
        /// chục đỉnh, không tốn overdraw, và ăn đúng cùng một mô hình ánh sáng toon như nhân vật.
        ///
        /// Sinh bằng cách bóp méo một khối cầu thấp: mỗi đỉnh được đẩy ra theo một hệ số băm ổn định,
        /// nên tán cây trông tự nhiên mà vẫn tất định tuyệt đối.
        /// </summary>
        public static void BuildLowPolyCanopy(int hashSeed, int rings, int segments, float radius,
            float height, float irregularity, Vector2 uvBase, Vector2 uvTip,
            out Vector3[] positions, out Vector3[] normals, out Vector2[] uv, out int[] indices)
        {
            rings = Mathf.Max(2, rings);
            segments = Mathf.Max(3, segments);

            var pos = new List<Vector3>((rings + 1) * segments);
            var idx = new List<int>(rings * segments * 6);

            for (int r = 0; r <= rings; r++)
            {
                float v = r / (float)rings;
                float ringY = Mathf.Cos(v * Mathf.PI);            // +1 -> -1
                float ringR = Mathf.Sin(v * Mathf.PI);

                for (int s = 0; s < segments; s++)
                {
                    float u = s / (float)segments;
                    float angle = u * Mathf.PI * 2f;

                    // Nhiễu ổn định theo (vòng, đoạn): cùng hạt giống luôn ra cùng một tán cây.
                    float wobble = 1f + (DecorationHash.Signed(hashSeed, r, s, 47, 0, HashLane.Scale) * irregularity);

                    pos.Add(new Vector3(
                        Mathf.Cos(angle) * ringR * radius * wobble,
                        (ringY * 0.5f + 0.5f) * height * wobble,
                        Mathf.Sin(angle) * ringR * radius * wobble));
                }
            }

            for (int r = 0; r < rings; r++)
            {
                for (int s = 0; s < segments; s++)
                {
                    int a = r * segments + s;
                    int b = r * segments + (s + 1) % segments;
                    int c = (r + 1) * segments + s;
                    int d = (r + 1) * segments + (s + 1) % segments;

                    idx.Add(a); idx.Add(c); idx.Add(b);
                    idx.Add(b); idx.Add(c); idx.Add(d);
                }
            }

            // Tách đỉnh theo từng tam giác để mỗi mặt có normal riêng — mặt phẳng, không nội suy mượt,
            // đúng ngôn ngữ hình khối của nhân vật và zombie.
            positions = new Vector3[idx.Count];
            normals = new Vector3[idx.Count];
            uv = new Vector2[idx.Count];
            indices = new int[idx.Count];

            float maxY = Mathf.Max(0.0001f, height);
            for (int t = 0; t < idx.Count; t += 3)
            {
                Vector3 p0 = pos[idx[t]], p1 = pos[idx[t + 1]], p2 = pos[idx[t + 2]];
                Vector3 n = Vector3.Cross(p1 - p0, p2 - p0).normalized;

                for (int k = 0; k < 3; k++)
                {
                    Vector3 p = k == 0 ? p0 : k == 1 ? p1 : p2;
                    positions[t + k] = p;
                    normals[t + k] = n;
                    uv[t + k] = Vector2.Lerp(uvBase, uvTip, Mathf.Clamp01(p.y / maxY));
                    indices[t + k] = t + k;
                }
            }
        }

        /// <summary>
        /// Cụm dương xỉ: N tàu lá bản rộng toả tròn từ gốc, mỗi tàu là một quad thuôn nhọn nghiêng lên.
        ///
        /// Hình này chép lại đúng bố cục của mesh vendor sau khi soi nó bằng ảnh chụp: dương xỉ vendor
        /// là một hoa thị gồm khoảng chục lá bản rộng xoè ra rồi vươn lên, chứ không phải bụi rậm.
        /// Bố cục đó vẽ được bằng vài cái quad, nên 968 đỉnh kia gần như toàn bộ nằm ở phần bo tròn và
        /// gân lá mà ở cỡ hiển thị thật không ai nhìn ra.
        ///
        /// Lá lấy UV trên cùng dải bảng màu mà vendor đang lấy (ô atlas 0), KHÔNG phải card alpha. Bản
        /// thử dùng card alpha `Leaf_2` đã bị loại: ảnh đó là một nhành lá dài mảnh, kéo lên khổ 1,3 m
        /// của dương xỉ thì ra một búi dây xám nhợt, sai hẳn so với hoa thị xanh đặc của vendor.
        /// </summary>
        public static void BuildFrondFan(int hashSeed, int frondCount, float radius, float height,
            float frondWidth, Vector2 uvBase, Vector2 uvTip,
            out Vector3[] positions, out Vector3[] normals, out Vector2[] uv, out int[] indices)
        {
            positions = new Vector3[frondCount * 4];
            normals = new Vector3[frondCount * 4];
            uv = new Vector2[frondCount * 4];
            indices = new int[frondCount * 6];

            for (int i = 0; i < frondCount; i++)
            {
                float angle = (i / (float)frondCount) * Mathf.PI * 2f
                              + DecorationHash.Signed(hashSeed, i, 0, 23, 0, HashLane.Rotation) * 0.35f;
                float length = radius * Mathf.Lerp(0.80f, 1.15f, DecorationHash.Unit(hashSeed, i, 0, 23, 1, HashLane.Scale));
                // Vươn ra nhiều hơn vươn lên. Để dải `rise` rộng như lúc đầu thì vài lá dựng gần thẳng
                // đứng và cả cụm mất dáng hoa thị, nhìn thành chữ V.
                float rise = height * Mathf.Lerp(0.35f, 0.80f, DecorationHash.Unit(hashSeed, i, 0, 23, 2, HashLane.JitterX));

                var outward = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                var side = new Vector3(-outward.z, 0f, outward.x) * (frondWidth * 0.5f);
                Vector3 root = outward * (radius * 0.12f);
                Vector3 tipPoint = root + outward * length + Vector3.up * rise;

                int v = i * 4;
                // Gốc lá hẹp, giữa phình, ngọn nhọn — nhưng chỉ có 4 đỉnh nên phần phình được gộp vào
                // bề rộng gốc. Ngọn thu còn một phần tư để lá có mũi thay vì cụt.
                positions[v + 0] = root - side;
                positions[v + 1] = root + side;
                positions[v + 2] = tipPoint + side * 0.26f;
                positions[v + 3] = tipPoint - side * 0.26f;

                for (int k = 0; k < 4; k++) normals[v + k] = Vector3.up;

                uv[v + 0] = uvBase;
                uv[v + 1] = uvBase;
                uv[v + 2] = uvTip;
                uv[v + 3] = uvTip;

                int t = i * 6;
                indices[t + 0] = v + 0;
                indices[t + 1] = v + 3;
                indices[t + 2] = v + 2;
                indices[t + 3] = v + 0;
                indices[t + 4] = v + 2;
                indices[t + 5] = v + 1;
            }
        }
    }
}
