using UnityEngine;

namespace BillGameCore
{
    /// <summary>
    /// Extension methods for common tween operations.
    /// Usage: transform.TweenMoveX(5f, 1f).SetEase(EaseType.OutBack);
    /// </summary>
    public static class TweenExtensions
    {
        // -------------------------------------------------------
        // Transform - Position
        // -------------------------------------------------------

        public static Tween TweenMoveX(this Transform t, float to, float dur) => BillTween.MoveX(t, to, dur);
        public static Tween TweenMoveY(this Transform t, float to, float dur) => BillTween.MoveY(t, to, dur);
        public static Tween TweenMoveZ(this Transform t, float to, float dur) => BillTween.MoveZ(t, to, dur);
        public static Tween TweenLocalMoveX(this Transform t, float to, float dur) => BillTween.LocalMoveX(t, to, dur);
        public static Tween TweenLocalMoveY(this Transform t, float to, float dur) => BillTween.LocalMoveY(t, to, dur);
        public static Tween TweenLocalMoveZ(this Transform t, float to, float dur) => BillTween.LocalMoveZ(t, to, dur);
        public static TweenSequence TweenMove(this Transform t, Vector3 to, float dur) => BillTween.Move(t, to, dur);
        public static TweenSequence TweenLocalMove(this Transform t, Vector3 to, float dur) => BillTween.LocalMove(t, to, dur);

        // -------------------------------------------------------
        // Transform - Scale
        // -------------------------------------------------------

        public static Tween TweenScaleX(this Transform t, float to, float dur) => BillTween.ScaleX(t, to, dur);
        public static Tween TweenScaleY(this Transform t, float to, float dur) => BillTween.ScaleY(t, to, dur);
        public static Tween TweenScaleZ(this Transform t, float to, float dur) => BillTween.ScaleZ(t, to, dur);
        public static Tween TweenScale(this Transform t, float to, float dur) => BillTween.Scale(t, to, dur);
        public static TweenSequence TweenScaleTo(this Transform t, Vector3 to, float dur) => BillTween.ScaleTo(t, to, dur);

        // -------------------------------------------------------
        // Transform - Rotation
        // -------------------------------------------------------

        public static Tween TweenRotateZ(this Transform t, float to, float dur) => BillTween.RotateZ(t, to, dur);

        // -------------------------------------------------------
        // CanvasGroup
        // -------------------------------------------------------

        public static Tween TweenFade(this CanvasGroup cg, float to, float dur) => BillTween.Fade(cg, to, dur);

        // -------------------------------------------------------
        // SpriteRenderer
        // -------------------------------------------------------

        public static Tween TweenFade(this SpriteRenderer sr, float to, float dur) => BillTween.Fade(sr, to, dur);
        public static Tween TweenColorR(this SpriteRenderer sr, float to, float dur) => BillTween.ColorR(sr, to, dur);
        public static Tween TweenColorG(this SpriteRenderer sr, float to, float dur) => BillTween.ColorG(sr, to, dur);
        public static Tween TweenColorB(this SpriteRenderer sr, float to, float dur) => BillTween.ColorB(sr, to, dur);

        // -------------------------------------------------------
        // UI Image
        // -------------------------------------------------------

        public static Tween TweenFade(this UnityEngine.UI.Image img, float to, float dur) => BillTween.Fade(img, to, dur);
        public static Tween TweenFillAmount(this UnityEngine.UI.Image img, float to, float dur) => BillTween.FillAmount(img, to, dur);

        // -------------------------------------------------------
        // UI Text
        // -------------------------------------------------------

        public static Tween TweenFade(this UnityEngine.UI.Text txt, float to, float dur) => BillTween.Fade(txt, to, dur);

        // -------------------------------------------------------
        // GameObject shortcuts
        // -------------------------------------------------------

        public static Tween TweenScale(this GameObject go, float to, float dur) => BillTween.Scale(go.transform, to, dur);
        public static Tween TweenMoveY(this GameObject go, float to, float dur) => BillTween.MoveY(go.transform, to, dur);
    }
}
