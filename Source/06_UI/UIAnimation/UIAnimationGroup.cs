// 여러 UI Tween의 병렬·순차 조합

using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

namespace NGPN.Gameplay.UI_Animation.Runtime
{
    public class UIAnimationGroup : MonoBehaviour
    {
        public enum PlayMode
        {
            Parallel,
            Sequential
        }

        [SerializeField] private PlayMode playMode = PlayMode.Parallel;
        [SerializeField] private List<UIAnimationBase> members = new();
        [SerializeField] private bool autoPlayOnEnable;
        [SerializeField] private bool killOnDisable = true;

        public Sequence CurrentSequence { get; private set; }
        public bool IsPlaying =>
            CurrentSequence != null &&
            CurrentSequence.IsActive() &&
            CurrentSequence.IsPlaying();

        private void OnEnable()
        {
            if (autoPlayOnEnable)
                Play();
        }

        private void OnDisable()
        {
            if (killOnDisable)
                Kill(false);
        }

        private void OnDestroy()
        {
            Kill(false);
        }

        public void Play()
        {
            BuildSequence();
            CurrentSequence.Play();
        }

        public void Pause()
        {
            if (CurrentSequence != null && CurrentSequence.IsActive())
                CurrentSequence.Pause();
        }

        public void Rewind(bool includeDelay = true)
        {
            if (CurrentSequence != null && CurrentSequence.IsActive())
                CurrentSequence.Rewind(includeDelay);
        }

        public void Complete(bool withCallbacks = false)
        {
            if (CurrentSequence != null && CurrentSequence.IsActive())
                CurrentSequence.Complete(withCallbacks);
        }

        public void Kill(bool complete = false)
        {
            if (CurrentSequence == null)
                return;

            if (CurrentSequence.IsActive())
                CurrentSequence.Kill(complete);

            CurrentSequence = null;
        }

        public void BuildSequence()
        {
            Kill(false);

            CurrentSequence = DOTween.Sequence()
                .SetAutoKill(false)
                .SetLink(gameObject);

            if (members == null)
                members = new List<UIAnimationBase>();

            // 설정에 따른 병렬 또는 순차 Sequence 구성
            foreach (UIAnimationBase member in members)
            {
                if (member == null)
                    continue;

                member.BuildIfNeeded();

                if (playMode == PlayMode.Parallel)
                    CurrentSequence.Join(member.CurrentTween);
                else
                    CurrentSequence.Append(member.CurrentTween);
            }

            CurrentSequence.Pause();
            CurrentSequence.Rewind(true);
        }

        public List<UIAnimationBase> Members => members;
    }
}
