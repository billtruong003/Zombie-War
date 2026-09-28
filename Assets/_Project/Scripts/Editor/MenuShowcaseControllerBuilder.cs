using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace ZombieWar.EditorTools
{
    /// <summary>
    /// Builds Resources/Menu/MenuShowcase.controller for <see cref="ZombieWar.UI.MenuGunShowcase"/>:
    /// the lobby idle plus the two aim poses the player uses in a run (rifle, pistol).
    /// </summary>
    public static class MenuShowcaseControllerBuilder
    {
        const string OutPath = "Assets/_Project/Resources/Menu/MenuShowcase.controller";
        const string MenuIdle = "Assets/_Project/Animation/MenuIdle.controller";
        const string Player = "Assets/_Project/Animations/PlayerAnimator.controller";

        [MenuItem("HordeCall/Menu/Build Showcase Controller")]
        public static string Build()
        {
            var idle = Clip(MenuIdle, "Stand_Idle1");
            var rifle = Clip(Player, "S_Rifle_Aim_N");
            var pistol = Clip(Player, "H_Gun Aim N");
            if (idle == null || rifle == null || pistol == null) return "missing clip";

            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(OutPath));
            AssetDatabase.DeleteAsset(OutPath);
            var c = AnimatorController.CreateAnimatorControllerAtPath(OutPath);
            var sm = c.layers[0].stateMachine;
            sm.defaultState = State(sm, "Idle", idle, new Vector3(300, 0));
            State(sm, "Rifle", rifle, new Vector3(300, 80));
            State(sm, "Pistol", pistol, new Vector3(300, 160));
            var layers = c.layers; layers[0].iKPass = true; c.layers = layers;   // hands onto the gun (MenuGunShowcase)
            AssetDatabase.SaveAssets();
            return OutPath;
        }

        static AnimatorState State(AnimatorStateMachine sm, string name, Motion clip, Vector3 pos)
        {
            var s = sm.AddState(name, pos);
            s.motion = clip;
            return s;
        }

        static AnimationClip Clip(string controllerPath, string name) =>
            AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(controllerPath)?.animationClips.FirstOrDefault(x => x.name == name);
    }
}
