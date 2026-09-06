using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ZombieWar.Tests
{
    /// <summary>
    /// M5+ — hợp đồng material nhân vật sau khi hợp nhất về Toon.
    ///
    /// Bộ test này tồn tại vì một con số mơ hồ: báo cáo trước ghi "453 part" nhưng tổng vai trò lại là
    /// 517. Cả hai đều đúng, chỉ là ĐẾM HAI THỨ KHÁC NHAU — và một báo cáo không phân biệt được hai thứ
    /// đó thì không kiểm chứng được. Ở đây chúng được đếm và đối chiếu tách bạch.
    ///
    /// Vai trò metal riêng đã bị CẮT khỏi MVP (quyết định của chủ dự án). ColorC dùng chung
    /// M_Character_Toon. Hình học, UV và màu atlas giữ nguyên; chỉ hợp đồng chiếu sáng đổi. Test
    /// <see cref="NoCatalogEntry_UsesADedicatedMetalMaterial"/> khoá quyết định đó lại, để một lần
    /// "khôi phục cho đủ bộ" sau này không lặng lẽ dựng lại vai trò thứ tư.
    /// </summary>
    public class CharacterMaterialContractTests
    {
        private const string CatalogPath = "Assets/_Project/Data/Character/CasualCostumeCatalog.asset";
        private const string MaterialDir = "Assets/_Project/Art/Materials/Character/";
        private const string CharacterFbx =
            "Assets/ThirdParty/Layer Lab/3D CharactersCasual/3D Characters Pro-Casual/FBX/Character/Characters.fbx";

        private const string ToonMaterial = "M_Character_Toon";
        private const string GlassMaterial = "M_Character_Glass";
        private const string ToonAltMaterial = "M_Character_ToonAlt";
        private const string RetiredMetalMaterial = "M_Character_Metal";
        private const string RetiredMetalShader = "ZombieWar/Character/Metal";

        /// <summary>Ba vai trò production còn lại. Không có vai trò thứ tư.</summary>
        private static readonly string[] ApprovedMaterials =
        {
            MaterialDir + ToonMaterial + ".mat",
            MaterialDir + GlassMaterial + ".mat",
            MaterialDir + ToonAltMaterial + ".mat",
        };

        /// <summary>Ánh xạ vai trò FBX đã duyệt. ColorA và ColorC cùng trỏ về Toon.</summary>
        private static readonly (string role, string material)[] ApprovedRoleMapping =
        {
            ("ColorA", ToonMaterial),
            ("ColorB", GlassMaterial),
            ("ColorC", ToonMaterial),
            ("ColorD", ToonAltMaterial),
        };

        private sealed class Accounting
        {
            public int CatalogPartCount;
            public int MaterialSlotCount;
            public int ToonSlots, GlassSlots, ToonAltSlots, MetalSlots;
            public int NullMesh, NullBones, NullRoot, NullMaterial;
            public int VendorReferences;
            public int RuntimeMaterialInstances;
            public readonly List<string> Offenders = new();
        }

        private static Accounting Measure()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<ScriptableObject>(CatalogPath);
            Assert.IsNotNull(catalog, "catalog missing at " + CatalogPath);

            var a = new Accounting();
            var slots = catalog.GetType().GetField("slots").GetValue(catalog) as System.Collections.IEnumerable;

            foreach (var slot in slots)
            {
                string slotName = slot.GetType().GetField("slot").GetValue(slot) as string;
                var parts = slot.GetType().GetField("parts").GetValue(slot) as System.Collections.IEnumerable;
                foreach (var part in parts)
                {
                    a.CatalogPartCount++;
                    var pt = part.GetType();
                    string name = pt.GetField("name").GetValue(part) as string;

                    // Mọi thông báo lỗi đều mang slot + part + CHỈ SỐ Ô, vì "có một material sai" mà
                    // không nói ô nào thì vẫn phải đi dò tay 517 ô.
                    string Where(int index) => $"slot '{slotName}' / part '{name}' / material slot {index}";

                    if (pt.GetField("skinnedMesh").GetValue(part) == null) { a.NullMesh++; a.Offenders.Add($"slot '{slotName}' / part '{name}': null mesh"); }

                    var bones = pt.GetField("boneNames").GetValue(part) as string[];
                    if (bones == null || bones.Length == 0) { a.NullBones++; a.Offenders.Add($"slot '{slotName}' / part '{name}': no bones"); }

                    var root = pt.GetField("rootBoneName").GetValue(part) as string;
                    if (string.IsNullOrEmpty(root)) { a.NullRoot++; a.Offenders.Add($"slot '{slotName}' / part '{name}': no root bone"); }

                    var mats = pt.GetField("materials").GetValue(part) as Material[];
                    if (mats == null || mats.Length == 0) { a.NullMaterial++; a.Offenders.Add($"slot '{slotName}' / part '{name}': no material slots"); continue; }

                    for (int i = 0; i < mats.Length; i++)
                    {
                        a.MaterialSlotCount++;
                        var m = mats[i];
                        if (m == null) { a.NullMaterial++; a.Offenders.Add(Where(i) + ": null material"); continue; }

                        string path = AssetDatabase.GetAssetPath(m);

                        // Một material không nằm trong AssetDatabase là bản sao runtime, không phải
                        // asset chia sẻ — nó phá batching và không bao giờ được nằm trong catalog.
                        if (string.IsNullOrEmpty(path) || m.name.Contains("(Instance)"))
                        {
                            a.RuntimeMaterialInstances++;
                            a.Offenders.Add(Where(i) + $": runtime material instance '{m.name}'");
                            continue;
                        }

                        if (path.Contains("ThirdParty")) { a.VendorReferences++; a.Offenders.Add(Where(i) + $": vendor material {path}"); }

                        switch (m.name)
                        {
                            case ToonMaterial: a.ToonSlots++; break;
                            case GlassMaterial: a.GlassSlots++; break;
                            case ToonAltMaterial: a.ToonAltSlots++; break;
                            case RetiredMetalMaterial:
                                a.MetalSlots++;
                                a.Offenders.Add(Where(i) + ": dedicated metal material was CUT for MVP but is still referenced");
                                break;
                            default: a.Offenders.Add(Where(i) + $": unapproved material '{m.name}' at {path}"); break;
                        }
                    }
                }
            }

            return a;
        }

        private static string Report(Accounting a) => string.Join("\n", a.Offenders.Take(25));

        [Test]
        public void CatalogParts_AreAllValid()
        {
            var a = Measure();
            Assert.AreEqual(453, a.CatalogPartCount, "catalog part count changed");
            Assert.AreEqual(0, a.NullMesh, "parts with no mesh:\n" + Report(a));
            Assert.AreEqual(0, a.NullBones, "parts with no bone data:\n" + Report(a));
            Assert.AreEqual(0, a.NullRoot, "parts with no root bone:\n" + Report(a));
            Assert.AreEqual(0, a.NullMaterial, "parts with a missing material slot:\n" + Report(a));
        }

        /// <summary>
        /// PHẦN ĐẾM PHẢI KHỚP SỐ HỌC.
        ///
        /// 453 là số ENTRY trong catalog; 517 là số Ô MATERIAL trên các entry đó, vì 61 mesh dùng nhiều
        /// hơn một material (ví dụ Headgear_26 = Glass + Metal + ToonAlt trước khi hợp nhất). Test này
        /// khoá cả hai con số và bắt chúng cộng khớp, để không ai còn phải đoán "453 hay 517 mới đúng".
        /// </summary>
        [Test]
        public void PartCountAndMaterialSlotCount_ReconcileArithmetically()
        {
            var a = Measure();

            Assert.AreEqual(453, a.CatalogPartCount, "catalog part count changed");
            Assert.AreEqual(517, a.MaterialSlotCount, "material slot count changed");

            Assert.AreEqual(506, a.ToonSlots, "Toon slot count changed (422 former ColorA + 84 former ColorC)");
            Assert.AreEqual(8, a.GlassSlots, "Glass slot count changed");
            Assert.AreEqual(3, a.ToonAltSlots, "ToonAlt slot count changed");
            Assert.AreEqual(0, a.MetalSlots, "dedicated metal is CUT for MVP:\n" + Report(a));

            Assert.AreEqual(a.MaterialSlotCount,
                a.ToonSlots + a.GlassSlots + a.ToonAltSlots + a.MetalSlots,
                "role slot totals do not sum to the material slot count");

            Assert.GreaterOrEqual(a.MaterialSlotCount, a.CatalogPartCount,
                "there cannot be fewer material slots than parts");
        }

        [Test]
        public void NoCatalogEntry_ReferencesAVendorMaterial()
        {
            var a = Measure();
            Assert.AreEqual(0, a.VendorReferences,
                "catalog still references vendor character materials:\n" + Report(a));
        }

        [Test]
        public void NoCatalogEntry_ReferencesARuntimeMaterialInstance()
        {
            var a = Measure();
            Assert.AreEqual(0, a.RuntimeMaterialInstances,
                "catalog references runtime material instances instead of shared assets:\n" + Report(a));
        }

        [Test]
        public void EveryMaterialSlot_UsesAnApprovedProjectRoleMaterial()
        {
            var a = Measure();
            var unapproved = a.Offenders.Where(o => o.Contains("unapproved")).ToList();
            Assert.IsEmpty(unapproved, "unapproved materials:\n" + string.Join("\n", unapproved));
        }

        /// <summary>
        /// Metal bị cắt là QUYẾT ĐỊNH, không phải thiếu sót — nên nó phải được khoá ở cả ba mặt: không
        /// entry nào tham chiếu, không material asset nào còn dùng shader metal, và Shader.Find phải
        /// trả về null.
        /// </summary>
        [Test]
        public void NoCatalogEntry_UsesADedicatedMetalMaterial()
        {
            var a = Measure();
            Assert.AreEqual(0, a.MetalSlots,
                "dedicated character metal was CUT for the MVP; these slots still reference it:\n" + Report(a));

            Assert.IsNull(AssetDatabase.LoadAssetAtPath<Material>(MaterialDir + RetiredMetalMaterial + ".mat"),
                RetiredMetalMaterial + " was retired but still exists on disk");
            Assert.IsNull(Shader.Find(RetiredMetalShader),
                RetiredMetalShader + " was retired but is still compiled into the project");
        }

        /// <summary>
        /// Không đường chạy runtime nào được dựng lại material metal.
        ///
        /// CharacterModularApplier gán <c>smr.sharedMaterials = entry.materials</c> — thuần asset chia
        /// sẻ, không hề chạm tới <c>.material</c>. Vì vậy "catalog sạch metal" kéo theo "runtime sạch
        /// metal". Test này khoá đúng cơ chế đó: nếu ai đổi sang <c>.material</c> hay <c>.materials</c>,
        /// mỗi renderer sẽ đẻ ra một bản sao và cả hợp đồng batching lẫn hợp đồng metal cùng vỡ.
        /// </summary>
        [Test]
        public void RuntimeCostumePath_AssignsSharedMaterialsOnly()
        {
            string[] guids = AssetDatabase.FindAssets("CharacterModularApplier t:MonoScript");
            Assert.IsNotEmpty(guids, "CharacterModularApplier script not found");

            string path = guids.Select(AssetDatabase.GUIDToAssetPath)
                .First(p => p.EndsWith("/CharacterModularApplier.cs"));
            string source = System.IO.File.ReadAllText(path);

            StringAssert.Contains("sharedMaterials = entry.materials", source,
                path + " no longer assigns shared materials from the catalog");
            Assert.IsFalse(System.Text.RegularExpressions.Regex.IsMatch(source, @"\.material\s*="),
                path + " assigns Renderer.material, which instantiates a per-renderer copy");
            StringAssert.DoesNotContain(RetiredMetalMaterial, source,
                path + " references the retired metal material");
        }

        [Test]
        public void ApprovedRoleMaterials_ExistAndUseTheProductionShaders()
        {
            foreach (string path in ApprovedMaterials)
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                Assert.IsNotNull(m, "missing approved role material: " + path);
                StringAssert.DoesNotContain("Prototype", m.shader.name, path + " still uses a prototype shader");
                Assert.AreNotEqual(RetiredMetalShader, m.shader.name, path + " uses the retired metal shader");
            }

            Assert.AreEqual("ZombieWar/Character/Toon",
                AssetDatabase.LoadAssetAtPath<Material>(MaterialDir + ToonMaterial + ".mat").shader.name);
            Assert.AreEqual("ZombieWar/Character/Toon",
                AssetDatabase.LoadAssetAtPath<Material>(MaterialDir + ToonAltMaterial + ".mat").shader.name);
        }

        /// <summary>
        /// Ánh xạ vai trò của FBX LÀ nguồn sự thật — catalog chỉ đọc lại <c>smr.sharedMaterials</c> từ
        /// đó. Nên nếu remap đã bằng đúng bảng đã duyệt thì chạy lại authoring là no-op: đó chính là
        /// tính idempotent, và kiểm ở đây không phải dựng lại 453 entry mới biết.
        /// </summary>
        [Test]
        public void FbxRoleRemap_IsTheApprovedMapping_SoRegenerationIsANoOp()
        {
            var importer = AssetImporter.GetAtPath(CharacterFbx) as ModelImporter;
            Assert.IsNotNull(importer, "ModelImporter not found: " + CharacterFbx);

            var externals = importer.GetExternalObjectMap();
            foreach (var (role, material) in ApprovedRoleMapping)
            {
                var id = new AssetImporter.SourceAssetIdentifier { type = typeof(Material), name = role };
                Assert.IsTrue(externals.TryGetValue(id, out var mapped),
                    $"FBX role '{role}' has no external material remap");
                Assert.IsNotNull(mapped, $"FBX role '{role}' remaps to a missing material");
                Assert.AreEqual(MaterialDir + material + ".mat", AssetDatabase.GetAssetPath(mapped),
                    $"FBX role '{role}' does not map to the approved material");
            }

            var colorC = new AssetImporter.SourceAssetIdentifier { type = typeof(Material), name = "ColorC" };
            externals.TryGetValue(colorC, out var colorCMaterial);
            Assert.AreEqual(ToonMaterial, colorCMaterial.name,
                "ColorC must resolve to the Toon material — dedicated metal is CUT for the MVP");
        }

        /// <summary>
        /// ShadowCaster VẮNG MẶT là có chủ đích, nên nó phải được khoá lại — nếu không, một lần "sửa cho
        /// đủ bộ" sau này sẽ lặng lẽ thêm lại một pass không có shadow bias.
        /// </summary>
        [Test]
        public void ProductionShaders_HaveTheIntendedPassStructure()
        {
            var shader = Shader.Find("ZombieWar/Character/Toon");
            Assert.IsNotNull(shader, "missing production shader: ZombieWar/Character/Toon");
            Assert.AreEqual(0, ShaderUtil.GetShaderMessageCount(shader), "ZombieWar/Character/Toon has shader messages");
            Assert.AreEqual(2, shader.passCount,
                "ZombieWar/Character/Toon must have exactly ForwardLit + DepthOnly. ShadowCaster is " +
                "deliberately absent: the URP asset disables shadow maps and every Player renderer has " +
                "cast/receive off.");
        }

        [Test]
        public void VendorCharacterMaterials_KeepTheirOriginalShaders()
        {
            const string dir = "Assets/ThirdParty/Layer Lab/3D Casual Character/3D Casual Character/Material/";
            foreach (string n in new[] { "ColorA", "ColorB", "ColorC", "ColorD" })
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>(dir + n + ".mat");
                Assert.IsNotNull(m, "vendor material vanished: " + n);
                StringAssert.StartsWith("StylizedToonWorldKit/", m.shader.name,
                    n + " was mutated by the migration — vendor assets must stay untouched");
            }
        }
    }
}
