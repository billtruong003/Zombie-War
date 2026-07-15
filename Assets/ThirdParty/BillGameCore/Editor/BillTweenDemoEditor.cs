#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using BillGameCore.Samples;

namespace BillGameCore.Editor
{
    public static class BillTweenDemoEditor
    {
        [MenuItem("BillGameCore/Tools/Create Tween Demo Scene")]
        static void CreateDemoScene()
        {
            // Confirm if current scene has unsaved changes
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            // Create new scene
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            scene.name = "BillTweenDemo";

            // Setup camera
            var cam = Camera.main;
            if (cam != null)
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.12f, 0.12f, 0.15f);
            }

            // Add directional light if not present
            var lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
            if (lights.Length == 0)
            {
                var lightGo = new GameObject("Directional Light");
                var light = lightGo.AddComponent<Light>();
                light.type = LightType.Directional;
                light.color = Color.white;
                light.intensity = 1f;
                lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            }

            // Ground plane
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.position = new Vector3(6f, -0.5f, -4f);
            ground.transform.localScale = new Vector3(3f, 1f, 2f);
            var groundMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            groundMat.color = new Color(0.15f, 0.15f, 0.18f);
            ground.GetComponent<Renderer>().material = groundMat;

            // Demo controller
            var demoGo = new GameObject("[BillTweenDemo]");
            var demo = demoGo.AddComponent<BillTweenDemo>();

            // Mark scene dirty so user gets save prompt
            EditorSceneManager.MarkSceneDirty(scene);

            // Focus
            Selection.activeGameObject = demoGo;

            Debug.Log("[BillGameCore] Tween Demo scene created. Enter Play Mode to see all 33 easing curves.");
            Debug.Log("[BillGameCore] Controls: [Space] Replay | [R] Reverse");
        }
    }
}
#endif
