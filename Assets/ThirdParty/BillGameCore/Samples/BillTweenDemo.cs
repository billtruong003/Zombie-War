using System;
using UnityEngine;
using TMPro;

namespace BillGameCore.Samples
{
    /// <summary>
    /// Visual demo of every BillTween easing function.
    /// Spawns a grid of cubes, each with a label. Press Space to replay.
    ///
    /// Setup: Add this component to an empty GameObject in a scene that has BillBootstrap.
    /// Or use the menu: BillGameCore > Create Tween Demo Scene
    /// </summary>
    public class BillTweenDemo : MonoBehaviour
    {
        [Header("Layout")]
        public float columnSpacing = 2.5f;
        public float rowSpacing = 2f;
        public int columnsPerRow = 6;
        public float tweenDistance = 3f;
        public float tweenDuration = 1.5f;
        public float staggerDelay = 0.05f;

        [Header("Visuals")]
        public float cubeSize = 0.5f;
        public float labelHeight = 1f;
        public int labelFontSize = 3;

        private Transform[] _cubes;
        private Vector3[] _startPositions;
        private EaseType[] _easeTypes;

        void Start()
        {
            if (!Bill.IsReady)
            {
                Bill.Events.Subscribe<GameReadyEvent>(OnReady);
                return;
            }
            Build();
        }

        void OnReady(GameReadyEvent _)
        {
            Bill.Events.Unsubscribe<GameReadyEvent>(OnReady);
            Build();
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Space)) Replay();
            if (Input.GetKeyDown(KeyCode.R)) ReplayReverse();
        }

        void Build()
        {
            _easeTypes = (EaseType[])Enum.GetValues(typeof(EaseType));
            int count = _easeTypes.Length;
            _cubes = new Transform[count];
            _startPositions = new Vector3[count];

            // Create container
            var container = new GameObject("[TweenDemo]").transform;
            container.SetParent(transform);

            // Create cubes
            for (int i = 0; i < count; i++)
            {
                int col = i % columnsPerRow;
                int row = i / columnsPerRow;

                Vector3 pos = new Vector3(
                    col * columnSpacing,
                    0f,
                    -row * rowSpacing
                );
                _startPositions[i] = pos;

                // Cube
                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.name = _easeTypes[i].ToString();
                cube.transform.SetParent(container);
                cube.transform.position = pos;
                cube.transform.localScale = Vector3.one * cubeSize;

                // Color by category
                var renderer = cube.GetComponent<Renderer>();
                renderer.material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                renderer.material.color = GetCategoryColor(_easeTypes[i]);

                // Remove collider (not needed)
                Destroy(cube.GetComponent<Collider>());

                _cubes[i] = cube.transform;

                // Label
                CreateLabel(cube.transform, _easeTypes[i].ToString(), pos);

                // Trail line (thin cube as path indicator)
                CreateTrail(container, pos);
            }

            // Camera position
            float midX = (columnsPerRow - 1) * columnSpacing * 0.5f;
            int totalRows = Mathf.CeilToInt((float)count / columnsPerRow);
            float midZ = -(totalRows - 1) * rowSpacing * 0.5f;
            float camDist = Mathf.Max(totalRows * rowSpacing, columnsPerRow * columnSpacing) * 0.8f;

            if (Camera.main != null)
            {
                Camera.main.transform.position = new Vector3(midX, camDist * 0.6f, midZ - camDist * 0.3f);
                Camera.main.transform.LookAt(new Vector3(midX, 0f, midZ));
            }

            // Info text
            CreateInfoLabel(container, new Vector3(midX, 2f, 1.5f));

            // Play
            PlayAll();
        }

        void CreateLabel(Transform parent, string text, Vector3 pos)
        {
            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(parent.parent); // parent to container, not cube
            labelGo.transform.position = pos + Vector3.up * labelHeight;

            var tmp = labelGo.AddComponent<TextMeshPro>();
            tmp.text = text;
            tmp.fontSize = labelFontSize;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            tmp.rectTransform.sizeDelta = new Vector2(columnSpacing, 0.5f);

            // Face camera
            labelGo.AddComponent<BillboardLabel>();
        }

        void CreateTrail(Transform container, Vector3 pos)
        {
            var trail = GameObject.CreatePrimitive(PrimitiveType.Cube);
            trail.name = "Trail";
            trail.transform.SetParent(container);
            trail.transform.position = pos + Vector3.up * (tweenDistance * 0.5f);
            trail.transform.localScale = new Vector3(0.02f, tweenDistance, 0.02f);

            var r = trail.GetComponent<Renderer>();
            r.material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            r.material.color = new Color(1f, 1f, 1f, 0.1f);

            Destroy(trail.GetComponent<Collider>());
        }

        void CreateInfoLabel(Transform container, Vector3 pos)
        {
            var go = new GameObject("Info");
            go.transform.SetParent(container);
            go.transform.position = pos;

            var tmp = go.AddComponent<TextMeshPro>();
            tmp.text = "<b>BillTween Demo</b>\n<size=70%>[Space] Replay  [R] Reverse</size>";
            tmp.fontSize = 5;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.yellow;
            tmp.rectTransform.sizeDelta = new Vector2(10f, 2f);
            go.AddComponent<BillboardLabel>();
        }

        // -------------------------------------------------------
        // Playback
        // -------------------------------------------------------

        void PlayAll()
        {
            for (int i = 0; i < _cubes.Length; i++)
            {
                var cube = _cubes[i];
                var ease = _easeTypes[i];
                var startPos = _startPositions[i];

                cube.position = startPos;

                BillTween.MoveY(cube, startPos.y + tweenDistance, tweenDuration)
                    ?.SetEase(ease)
                    .SetDelay(i * staggerDelay)
                    .SetTarget(cube);
            }
        }

        void Replay()
        {
            for (int i = 0; i < _cubes.Length; i++)
            {
                BillTween.KillTarget(_cubes[i]);
                _cubes[i].position = _startPositions[i];
            }
            PlayAll();
        }

        void ReplayReverse()
        {
            for (int i = 0; i < _cubes.Length; i++)
            {
                var cube = _cubes[i];
                var ease = _easeTypes[i];
                var startPos = _startPositions[i];
                float currentY = cube.position.y;

                BillTween.KillTarget(cube);
                BillTween.MoveY(cube, startPos.y, tweenDuration)
                    ?.SetEase(ease)
                    .SetDelay(i * staggerDelay)
                    .SetTarget(cube);
            }
        }

        // -------------------------------------------------------
        // Color coding by ease category
        // -------------------------------------------------------

        static Color GetCategoryColor(EaseType ease)
        {
            string name = ease.ToString();
            if (name == "Linear")             return new Color(0.9f, 0.9f, 0.9f);
            if (name.Contains("Sine"))        return new Color(0.3f, 0.7f, 1f);
            if (name.Contains("Quad"))        return new Color(0.3f, 0.9f, 0.5f);
            if (name.Contains("Cubic"))       return new Color(0.2f, 0.8f, 0.3f);
            if (name.Contains("Quart"))       return new Color(0.1f, 0.6f, 0.2f);
            if (name.Contains("Quint"))       return new Color(0.0f, 0.5f, 0.1f);
            if (name.Contains("Expo"))        return new Color(1f, 0.7f, 0.2f);
            if (name.Contains("Circ"))        return new Color(1f, 0.5f, 0.2f);
            if (name.Contains("Back"))        return new Color(1f, 0.3f, 0.5f);
            if (name.Contains("Elastic"))     return new Color(0.9f, 0.2f, 0.9f);
            if (name.Contains("Bounce"))      return new Color(1f, 0.2f, 0.2f);
            return Color.white;
        }
    }

    /// <summary>Simple billboard - always face camera.</summary>
    public class BillboardLabel : MonoBehaviour
    {
        private Transform _cam;
        void Start() => _cam = Camera.main?.transform;
        void LateUpdate()
        {
            if (_cam != null) transform.forward = _cam.forward;
        }
    }
}
