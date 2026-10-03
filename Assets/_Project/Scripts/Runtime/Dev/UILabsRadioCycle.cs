using System;
using UnityEngine;
using ZombieWar.UI;

namespace ZombieWar.Dev
{
    /// UI Labs only: plays a few radio lines on one card in turn, so typing and the speaking bars
    /// can be judged in Play. The first line is shown in the editor too.
    public sealed class UILabsRadioCycle : MonoBehaviour
    {
        [Serializable]
        public struct Line
        {
            public string channel, title, body;
            public Sprite face;
        }

        [SerializeField] private RadioCallView view;
        [SerializeField] private Line[] lines = new Line[0];
        [SerializeField] private float every = 6f;

        int _i;
        float _next;

        void OnEnable()
        {
            _i = 0;
            Show();
            _next = Time.unscaledTime + every;
        }

        void Update()
        {
            if (!Application.isPlaying || lines.Length < 2 || Time.unscaledTime < _next) return;
            _i = (_i + 1) % lines.Length;
            Show();
            _next = Time.unscaledTime + every;
        }

        void Show()
        {
            if (view == null || lines.Length == 0) return;
            var l = lines[_i];
            view.Say(l.channel, l.title, l.body, l.face);
        }
    }
}
