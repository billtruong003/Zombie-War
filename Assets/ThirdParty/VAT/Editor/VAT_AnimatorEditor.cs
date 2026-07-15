using UnityEngine;
using UnityEditor;
using System.Reflection;
using System.Linq;

[CustomEditor(typeof(VAT_Animator))]
public class VAT_AnimatorEditor : Editor
{
    private VAT_Animator _animator;

    // Serialized Properties for public fields
    private SerializedProperty _animationDataProp;
    private SerializedProperty _playbackSpeedProp;
    private SerializedProperty _defaultClipIndexProp;
    private SerializedProperty _showAnimationBoundsProp;

    // Reflection Fields for private state
    private FieldInfo _currentClipField;
    private FieldInfo _previousClipField;
    private FieldInfo _currentTimeSecondsField;
    private FieldInfo _crossFadeTimerField;
    private FieldInfo _crossFadeDurationField;
    private FieldInfo _isBlendingField;

    private void OnEnable()
    {
        _animator = (VAT_Animator)target;

        // Find public properties for the Inspector
        _animationDataProp = serializedObject.FindProperty("animationData");
        _playbackSpeedProp = serializedObject.FindProperty("playbackSpeed");
        _defaultClipIndexProp = serializedObject.FindProperty("defaultClipIndex");
        _showAnimationBoundsProp = serializedObject.FindProperty("showAnimationBounds");

        // Find private fields for live preview via Reflection
        var animatorType = typeof(VAT_Animator);
        _currentClipField = animatorType.GetField("_currentClip", BindingFlags.NonPublic | BindingFlags.Instance);
        _previousClipField = animatorType.GetField("_previousClip", BindingFlags.NonPublic | BindingFlags.Instance);
        _currentTimeSecondsField = animatorType.GetField("_currentTimeSeconds", BindingFlags.NonPublic | BindingFlags.Instance);
        _crossFadeTimerField = animatorType.GetField("_crossFadeTimer", BindingFlags.NonPublic | BindingFlags.Instance);
        _crossFadeDurationField = animatorType.GetField("_crossFadeDuration", BindingFlags.NonPublic | BindingFlags.Instance);
        _isBlendingField = animatorType.GetField("_isBlending", BindingFlags.NonPublic | BindingFlags.Instance);
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // Draw public properties explicitly
        EditorGUILayout.PropertyField(_animationDataProp);
        EditorGUILayout.PropertyField(_playbackSpeedProp);
        EditorGUILayout.PropertyField(_defaultClipIndexProp);
        EditorGUILayout.PropertyField(_showAnimationBoundsProp);

        if (_animator.animationData == null || !_animator.animationData.IsValid())
        {
            EditorGUILayout.HelpBox("Assign a valid VAT_AnimationData asset.", MessageType.Warning);
        }
        else
        {
            DrawLivePreviewAndControls();
        }

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawLivePreviewAndControls()
    {
        EditorGUILayout.Space(10);
        var style = new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleCenter };
        EditorGUILayout.LabelField("--- Live Preview & Controls ---", style);

        var currentClip = _currentClipField?.GetValue(_animator) as VAT_AnimationData.ClipInfo;
        var previousClip = _previousClipField?.GetValue(_animator) as VAT_AnimationData.ClipInfo;
        var isBlending = (bool)(_isBlendingField?.GetValue(_animator) ?? false);

        if (currentClip != null)
        {
            var currentTime = (float)(_currentTimeSecondsField?.GetValue(_animator) ?? 0f);
            float progress = (currentClip.duration > 0) ? Mathf.Repeat(currentTime, currentClip.duration) / currentClip.duration : 0;
            EditorGUI.ProgressBar(EditorGUILayout.GetControlRect(), progress, $"Current: {currentClip.name} ({progress:P0})");
        }

        if (isBlending && previousClip != null)
        {
            var crossFadeTimer = (float)(_crossFadeTimerField?.GetValue(_animator) ?? 0f);
            var crossFadeDuration = (float)(_crossFadeDurationField?.GetValue(_animator) ?? 1f);
            float blendWeight = (crossFadeDuration > 0) ? Mathf.Clamp01(crossFadeTimer / crossFadeDuration) : 1f;
            EditorGUI.ProgressBar(EditorGUILayout.GetControlRect(), blendWeight, $"Blending from: {previousClip.name} ({blendWeight:P0})");
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Play Clips (Cross-fade 0.2s)", EditorStyles.centeredGreyMiniLabel);

        const int buttonsPerRow = 3;
        var clipNames = _animator.animationData.animationClips.Select(c => c.name).ToList();

        for (int i = 0; i < clipNames.Count; i += buttonsPerRow)
        {
            EditorGUILayout.BeginHorizontal();
            for (int j = 0; j < buttonsPerRow && (i + j) < clipNames.Count; j++)
            {
                var clipName = clipNames[i + j];
                if (GUILayout.Button(clipName))
                {
                    Undo.RecordObject(_animator, $"Play VAT Clip '{clipName}'");
                    _animator.CrossFade(clipName, 0.2f);
                    EditorUtility.SetDirty(_animator);
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        if (Application.isPlaying == false)
        {
            Repaint();
        }
    }
}