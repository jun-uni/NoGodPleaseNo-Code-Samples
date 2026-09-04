using DG.Tweening;
using DG.Tweening.Core;
using DG.Tweening.Plugins.Options;
using UnityEngine;

namespace NGPN.Gameplay.UI_Animation.Runtime
{
    [DisallowMultipleComponent]
    public class UIFadeAnimation : UIAnimationBase
    {
        [Header("Fade")]
        [Range(0f, 1f)] public float startValue;
        [Range(0f, 1f)] public float endValue = 1f;
        public bool affectInteractable = true;
        public bool affectBlocksRaycasts = true;

        private CanvasGroup _canvasGroup;

        protected override void Awake()
        {
            EnsureCanvasGroup();
            base.Awake();
        }

        public override void InitializeToStartState()
        {
            EnsureCanvasGroup();
            _canvasGroup.alpha = isFrom ? endValue : startValue;
            ApplyInputState();
        }

        protected override Tween BuildTween()
        {
            EnsureCanvasGroup();
            _canvasGroup.DOKill();

            float from = isFrom ? endValue : startValue;
            float to = isFrom ? startValue : endValue;

            _canvasGroup.alpha = from;
            ApplyInputState();

            TweenerCore<float, float, FloatOptions> tween =
                _canvasGroup.DOFade(to, duration);
            tween.OnUpdate(ApplyInputState);

            return ApplyCommonSettings(tween);
        }

        private void EnsureCanvasGroup()
        {
            GameObject animationTarget = TargetOrSelf;
            if (!animationTarget.TryGetComponent(out _canvasGroup))
                _canvasGroup = animationTarget.AddComponent<CanvasGroup>();
        }

        private void ApplyInputState()
        {
            if (affectInteractable)
                _canvasGroup.interactable = _canvasGroup.alpha > 0.99f;

            if (affectBlocksRaycasts)
                _canvasGroup.blocksRaycasts = _canvasGroup.alpha > 0.01f;
        }
    }
}
