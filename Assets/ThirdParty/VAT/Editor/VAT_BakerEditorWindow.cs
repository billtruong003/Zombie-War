using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public class VAT_BakerEditorWindow : EditorWindow
{
    private struct RendererInfo
    {
        public Renderer renderer;
        public Mesh sharedMesh;
        public Texture2D mainTexture;
        public bool isSkinned;
    }

    private List<GameObject> _sourceObjects = new List<GameObject>();
    private Vector2 _scrollPosition;
    private const int FRAMERATE_OVERRIDE = 30;

    [MenuItem("Tools/BillTheDev/VAT Baker Pro")]
    public static void ShowWindow() => GetWindow<VAT_BakerEditorWindow>("VAT Baker Pro");

    private void OnGUI()
    {
        GUILayout.Label("VAT Baker Professional", EditorStyles.boldLabel);
        DrawDragAndDropArea();
        DrawSourceObjectsList();

        using (new EditorGUI.DisabledScope(_sourceObjects.Count == 0))
        {
            if (GUILayout.Button("Bake All", GUILayout.Height(30))) BakeAllSelectedObjects();
        }
    }

    private void DrawDragAndDropArea()
    {
        var dropAreaRect = GUILayoutUtility.GetRect(0.0f, 50.0f, GUILayout.ExpandWidth(true));
        GUI.Box(dropAreaRect, "Drag & Drop GameObjects Here", EditorStyles.helpBox);
        ProcessDragAndDropEvents(dropAreaRect);
    }

    private void ProcessDragAndDropEvents(Rect dropArea)
    {
        var currentEvent = Event.current;
        if (!dropArea.Contains(currentEvent.mousePosition)) return;

        if (currentEvent.type == EventType.DragUpdated || currentEvent.type == EventType.DragPerform)
        {
            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            if (currentEvent.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();
                _sourceObjects.AddRange(DragAndDrop.objectReferences.OfType<GameObject>().Where(go => !_sourceObjects.Contains(go)));
            }
            currentEvent.Use();
        }
    }

    private void DrawSourceObjectsList()
    {
        using (var scrollView = new EditorGUILayout.ScrollViewScope(_scrollPosition, GUILayout.MinHeight(100), GUILayout.MaxHeight(300)))
        {
            _scrollPosition = scrollView.scrollPosition;
            for (int i = _sourceObjects.Count - 1; i >= 0; i--)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    _sourceObjects[i] = EditorGUILayout.ObjectField(_sourceObjects[i], typeof(GameObject), true) as GameObject;
                    if (GUILayout.Button("X", GUILayout.Width(25))) _sourceObjects.RemoveAt(i);
                }
            }
        }
        if (_sourceObjects.Count > 0 && GUILayout.Button("Clear List")) _sourceObjects.Clear();
    }

    private void BakeAllSelectedObjects()
    {
        string outputPath = EditorUtility.OpenFolderPanel("Select Output Folder", "Assets", "");
        if (string.IsNullOrEmpty(outputPath)) return;

        string projectRelativePath = "Assets" + outputPath.Substring(Application.dataPath.Length);
        int bakedCount = 0;

        try
        {
            for (int i = 0; i < _sourceObjects.Count; i++)
            {
                if (_sourceObjects[i] == null) continue;
                EditorUtility.DisplayProgressBar("Baking VAT", $"Processing {_sourceObjects[i].name}", (float)i / _sourceObjects.Count);
                if (BakeSingleObject(_sourceObjects[i], projectRelativePath)) bakedCount++;
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            AssetDatabase.Refresh();
        }
    }

    private bool BakeSingleObject(GameObject sourceObject, string directory)
    {
        if (!IsSourceValid(sourceObject, out var rendererInfo)) return false;

        var tempInstance = Instantiate(sourceObject);
        tempInstance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        tempInstance.transform.localScale = Vector3.one;

        try
        {
            var clips = tempInstance.GetComponentInChildren<Animator>().runtimeAnimatorController.animationClips
                .Where(c => !c.legacy && c.length > 0).Distinct().ToArray();

            if (clips.Length == 0) return false;

            var totalBounds = CalculateBounds(tempInstance, clips, rendererInfo);
            var (posTex, normTex, clipInfos) = BakeTextures(tempInstance, clips, rendererInfo, totalBounds);

            var bakedMesh = CreateMesh(rendererInfo.sharedMesh, totalBounds, sourceObject.name);
            var data = CreateDataAsset(bakedMesh, posTex, normTex, totalBounds, clipInfos, sourceObject.name);
            var material = CreateMaterial(sourceObject.name, data, rendererInfo.mainTexture);

            SaveAssets(directory, sourceObject.name, data, bakedMesh, posTex, normTex, material);
            CreatePrefab(directory, sourceObject.name, bakedMesh, material, data);
        }
        finally
        {
            if (tempInstance != null) DestroyImmediate(tempInstance);
        }
        return true;
    }

    private bool IsSourceValid(GameObject sourceObject, out RendererInfo info)
    {
        info = default;
        var anim = sourceObject.GetComponentInChildren<Animator>();
        if (anim == null || anim.runtimeAnimatorController == null) return false;

        Renderer renderer = null;
        Mesh mesh = null;
        bool isSkinned = false;

        var smr = sourceObject.GetComponentInChildren<SkinnedMeshRenderer>();
        if (smr != null && smr.sharedMesh != null)
        {
            renderer = smr;
            mesh = smr.sharedMesh;
            isSkinned = true;
        }
        else
        {
            var mr = sourceObject.GetComponentInChildren<MeshRenderer>();
            var mf = sourceObject.GetComponentInChildren<MeshFilter>();
            if (mr != null && mf != null && mf.sharedMesh != null)
            {
                renderer = mr;
                mesh = mf.sharedMesh;
                isSkinned = false;
            }
        }

        if (renderer != null && mesh != null)
        {
            Texture2D mainTex = null;
            if (renderer.sharedMaterial != null)
            {
                if (renderer.sharedMaterial.HasProperty("_BaseMap"))
                    mainTex = renderer.sharedMaterial.GetTexture("_BaseMap") as Texture2D;
                else if (renderer.sharedMaterial.HasProperty("_MainTex"))
                    mainTex = renderer.sharedMaterial.GetTexture("_MainTex") as Texture2D;
            }

            info = new RendererInfo
            {
                renderer = renderer,
                sharedMesh = mesh,
                isSkinned = isSkinned,
                mainTexture = mainTex
            };
            return true;
        }

        return false;
    }

    private Bounds CalculateBounds(GameObject instance, AnimationClip[] clips, RendererInfo info)
    {
        var bounds = new Bounds();
        var tempMesh = new Mesh();
        bool first = true;
        var renderer = instance.GetComponentInChildren<Renderer>();

        foreach (var clip in clips)
        {
            float step = 1f / FRAMERATE_OVERRIDE;
            for (float t = 0; t <= clip.length; t += step)
            {
                clip.SampleAnimation(instance, t);
                Vector3[] verts;

                if (info.isSkinned)
                {
                    ((SkinnedMeshRenderer)renderer).BakeMesh(tempMesh, true);
                    verts = tempMesh.vertices;
                }
                else
                {
                    var mtx = renderer.transform.localToWorldMatrix;
                    verts = info.sharedMesh.vertices.Select(v => mtx.MultiplyPoint3x4(v)).ToArray();
                }

                var b = GeometryUtility.CalculateBounds(verts, Matrix4x4.identity);
                if (first) { bounds = b; first = false; }
                else bounds.Encapsulate(b);
            }
        }
        bounds.Expand(0.01f);
        DestroyImmediate(tempMesh);
        return bounds;
    }

    private (Texture2D, Texture2D, List<VAT_AnimationData.ClipInfo>) BakeTextures(GameObject instance, AnimationClip[] clips, RendererInfo info, Bounds bounds)
    {
        var infos = new List<VAT_AnimationData.ClipInfo>();
        int vCount = info.sharedMesh.vertexCount;
        var tempMesh = new Mesh();
        var posColors = new List<Color[]>();
        var normColors = new List<Color[]>();
        int totalFrames = 0;
        var renderer = instance.GetComponentInChildren<Renderer>();

        foreach (var clip in clips)
        {
            int frames = Mathf.Max(2, Mathf.CeilToInt(clip.length * FRAMERATE_OVERRIDE));
            infos.Add(new VAT_AnimationData.ClipInfo { name = clip.name, startFrame = totalFrames, frameCount = frames, duration = clip.length, wrapMode = clip.wrapMode });

            for (int f = 0; f < frames; f++)
            {
                float t = (f / (float)(frames - 1)) * clip.length;
                clip.SampleAnimation(instance, t);

                Vector3[] verts;
                Vector3[] norms;

                if (info.isSkinned)
                {
                    ((SkinnedMeshRenderer)renderer).BakeMesh(tempMesh, true);
                    verts = tempMesh.vertices;
                    norms = tempMesh.normals;
                }
                else
                {
                    var mtx = renderer.transform.localToWorldMatrix;
                    verts = info.sharedMesh.vertices.Select(v => mtx.MultiplyPoint3x4(v)).ToArray();
                    norms = info.sharedMesh.normals.Select(n => mtx.MultiplyVector(n).normalized).ToArray();
                }

                var pCol = new Color[vCount];
                var nCol = new Color[vCount];

                for (int i = 0; i < vCount; i++)
                {
                    pCol[i] = EncodePosition(verts[i], bounds);
                    nCol[i] = EncodeNormal(norms[i]);
                }
                posColors.Add(pCol);
                normColors.Add(nCol);
            }
            totalFrames += frames;
        }
        DestroyImmediate(tempMesh);

        return (CreateTexture(vCount, totalFrames, posColors, $"{instance.name}_VAT_Pos"),
                CreateTexture(vCount, totalFrames, normColors, $"{instance.name}_VAT_Norm"),
                infos);
    }

    private Texture2D CreateTexture(int width, int height, List<Color[]> data, string name)
    {
        var tex = new Texture2D(width, height, TextureFormat.RGBAHalf, false, true)
        {
            name = name,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            anisoLevel = 0
        };
        for (int y = 0; y < height; y++) tex.SetPixels(0, y, width, 1, data[y]);
        tex.Apply(false, true);
        return tex;
    }

    private Color EncodePosition(Vector3 pos, Bounds b)
    {
        return new Color(
            Mathf.InverseLerp(b.min.x, b.max.x, pos.x),
            Mathf.InverseLerp(b.min.y, b.max.y, pos.y),
            Mathf.InverseLerp(b.min.z, b.max.z, pos.z), 1f);
    }

    private Color EncodeNormal(Vector3 n) => new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f);

    private Mesh CreateMesh(Mesh original, Bounds bounds, string name)
    {
        var mesh = Instantiate(original);
        mesh.name = $"{name}_VAT_Mesh";
        mesh.bounds = bounds;
        var uvs = new Vector2[mesh.vertexCount];
        float step = 1f / mesh.vertexCount;
        for (int i = 0; i < mesh.vertexCount; i++) uvs[i] = new Vector2(i * step + step * 0.5f, 0);
        mesh.SetUVs(1, uvs);
        mesh.UploadMeshData(true);
        return mesh;
    }

    private VAT_AnimationData CreateDataAsset(Mesh m, Texture2D pTex, Texture2D nTex, Bounds b, List<VAT_AnimationData.ClipInfo> clips, string name)
    {
        var data = CreateInstance<VAT_AnimationData>();
        data.name = $"{name}_Data";
        data.bakedMesh = m;
        data.positionTexture = pTex;
        data.normalTexture = nTex;
        data.positionMinBounds = b.min;
        data.positionMaxBounds = b.max;
        data.animationClips = clips;
        return data;
    }

    private Material CreateMaterial(string name, VAT_AnimationData data, Texture2D baseMap)
    {
        var shader = Shader.Find("BillTheDev/VAT/SimpleLit");
        if (shader == null)
        {
            Debug.LogError("Shader 'BillTheDev/VAT/SimpleLit' not found!");
            return null;
        }

        var mat = new Material(shader) { name = $"{name}_Mat", enableInstancing = true };

        mat.SetTexture("_PositionTexture", data.positionTexture);
        mat.SetTexture("_NormalTexture", data.normalTexture);
        mat.SetVector("_PositionMin", data.positionMinBounds);
        mat.SetVector("_PositionMax", data.positionMaxBounds);

        if (baseMap != null)
        {
            mat.SetTexture("_BaseMap", baseMap);
            mat.SetColor("_BaseColor", Color.white);
        }

        return mat;
    }

    private void SaveAssets(string dir, string name, VAT_AnimationData data, Mesh mesh, Texture2D pTex, Texture2D nTex, Material mat)
    {
        string path = Path.Combine(dir, $"{name}_VAT_Data.asset");
        AssetDatabase.CreateAsset(data, path);
        AssetDatabase.AddObjectToAsset(mesh, data);
        AssetDatabase.AddObjectToAsset(pTex, data);
        AssetDatabase.AddObjectToAsset(nTex, data);
        if (mat != null) AssetDatabase.AddObjectToAsset(mat, data);
        AssetDatabase.SaveAssets();
    }

    private void CreatePrefab(string dir, string name, Mesh mesh, Material mat, VAT_AnimationData data)
    {
        var go = new GameObject($"{name}_VAT");
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        var anim = go.AddComponent<VAT_Animator>();
        anim.animationData = data;

        string path = Path.Combine(dir, $"{name}_VAT.prefab");
        PrefabUtility.SaveAsPrefabAsset(go, path);
        DestroyImmediate(go);
    }
}