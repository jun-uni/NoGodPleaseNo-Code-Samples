// UI Tween 공통 설정과 수명주기

using DG.Tweening;
using UnityEngine;

namespace NGPN.Gameplay.UI_Animation.Runtime
{
    public abstract class UIAnimationBase : MonoBehaviour, IUIAnimation
    {
        [Header("Target")]
        [SerializeField] protected GameObject target;

        [Header("Tween")]
        [Min(0f)] public float duration = 0.3f;
        [Min(0f)] public float delay;
        public Ease ease = Ease.OutSine;

        [Header("Options")]
        public bool ignoreTimeScale = true;
        public bool isFrom;
        public bool autoKill;
        public bool autoPlay;
        public bool initializeOnAwake;
        public bool initializeOnEnable = true;

        public Tween CurrentTween { get; protected set; }
        public bool IsPlaying =>
            CurrentTween != null &&
            CurrentTween.IsActive() &&
            CurrentTween.IsPlaying();

        protected GameObject TargetOrSelf => target != null ? target : gameObject;

        protected virtual void Reset()
        {
            target = gameObject;
        }

        protected virtual void Awake()
        {
            if (initializeOnAwake)
                InitializeToStartState();

            if (autoPlay)
                Play();
        }

        protected virtual void OnEnable()
        {
            if (Application.isPlaying && initializeOnEnable)
                InitializeToStartState();
        }

        protected virtual void OnDisable()
        {
            if (Application.isPlaying)
                Kill(false);
        }

        public void BuildIfNeeded()
        {
            if (CurrentTween == null || !CurrentTween.IsActive())
                CurrentTween = BuildTween();
        }

        public void Play()
        {
            BuildIfNeeded();

            if (CurrentTween == null)
                return;

            // 완료된 재사용 Tween을 시작 상태로 복원
            if (CurrentTween.IsComplete())
                CurrentTween.Rewind(true);

            CurrentTween.Play();
        }

        public void Pause()
        {
            if (CurrentTween != null && CurrentTween.IsActive())
                CurrentTween.Pause();
        }

        public void Rewind(bool includeDelay = true)
        {
            if (CurrentTween != null && CurrentTween.IsActive())
                CurrentTween.Rewind(includeDelay);
        }

        public void Complete(bool withCallbacks = false)
        {
            if (CurrentTween != null && CurrentTween.IsActive())
                CurrentTween.Complete(withCallbacks);
        }

        public void Kill(bool complete = false)
        {
            if (CurrentTween == null)
                return;

            if (CurrentTween.IsActive())
                CurrentTween.Kill(complete);

            CurrentTween = null;
        }

        protected Tween ApplyCommonSettings(Tween tween)
        {
            tween.SetDelay(delay)
                .SetEase(ease)
                .SetUpdate(ignoreTimeScale)
                .SetLink(TargetOrSelf)
                .SetAutoKill(autoKill);

            return tween;
        }

        public virtual void InitializeToStartState()
        {
        }

        protected abstract Tween BuildTween();
    }
}
