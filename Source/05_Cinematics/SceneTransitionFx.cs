// Fade와 Iris 기반 씬 전환

using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace NGPN.Gameplay
{
    public class SceneTransitionFx : MonoBehaviour
    {
        [Header("Fade")]
        [SerializeField] private CanvasGroup fadeCanvas;
        [SerializeField] private RawImage centerImage;
        [SerializeField] private float fadeOutDuration = 0.5f;
        [SerializeField] private float fadeInDuration = 0.5f;
        [SerializeField] private float minimumBlackDuration = 3f;

        [Header("Iris Transition")]
        [SerializeField] private RawImage irisImage;
        [SerializeField] private float irisCloseDuration = 0.35f;
        [SerializeField, Range(0f, 0.2f)] private float irisSoftness = 0.02f;
        [SerializeField] private float irisHoldRadius = 0.12f;
        [SerializeField] private float irisHoldDuration = 0.5f;

        public float IrisCloseDuration => irisCloseDuration;
        public float IrisHoldDuration => irisHoldDuration;

        private float _blackShownStartUnscaled = -1f;
        private Material _irisMat;
        private Transform _irisFocus;
        private bool _useIrisNextFadeOut;
        private bool _isPlaying;

        private void Awake()
        {
            if (irisImage == null) return;

            // 공유 머티리얼에 영향을 주지 않는 Iris 인스턴스 구성
            _irisMat = Instantiate(irisImage.material);
            irisImage.material = _irisMat;
            irisImage.enabled = false;

            _irisMat.SetFloat("_Softness", irisSoftness);
            _irisMat.SetFloat("_Radius", 1.2f);
            _irisMat.SetVector("_Center", new Vector4(0.5f, 0.5f, 0f, 0f));
        }

        public void SetIrisFocus(Transform focus)
        {
            _irisFocus = focus;
        }

        public void UseIrisOnNextFadeOut(bool use)
        {
            _useIrisNextFadeOut = use;
        }

        public async UniTask FadeOutAndHoldAsync(Sprite midSprite = null)
        {
            if (_isPlaying) return;

            _isPlaying = true;
            _blackShownStartUnscaled = Time.unscaledTime;

            InteractionLockHub myHub = FindLocalPlayerHub();
            if (myHub != null)
                myHub.LockAllFor(InteractionLockHub.LockSource.SceneTransition);

            if (fadeCanvas != null)
                fadeCanvas.blocksRaycasts = true;

            if (centerImage != null)
            {
                centerImage.texture = midSprite != null ? midSprite.texture : null;
                centerImage.enabled = midSprite != null;
            }

            if (_useIrisNextFadeOut && irisImage != null && _irisMat != null)
            {
                await PlayIrisCloseAsync();
                _useIrisNextFadeOut = false;

                if (fadeCanvas != null) fadeCanvas.alpha = 1f;
                return;
            }

            await FadeTo(1f, fadeOutDuration);
        }

        public async UniTask FadeInWithMinimumHoldAsync()
        {
            if (!_isPlaying || _blackShownStartUnscaled < 0f)
            {
                await FadeInAsync();
                return;
            }

            float elapsed = Time.unscaledTime - _blackShownStartUnscaled;
            float remain = minimumBlackDuration - elapsed;

            if (remain > 0f)
                await UniTask.Delay(TimeSpan.FromSeconds(remain));

            await FadeInAsync();
        }

        public async UniTask FadeInAsync()
        {
            await FadeTo(0f, fadeInDuration);

            if (fadeCanvas != null)
                fadeCanvas.blocksRaycasts = false;

            InteractionLockHub myHub = FindLocalPlayerHub();
            if (myHub != null)
                myHub.UnlockAllFor(InteractionLockHub.LockSource.SceneTransition);

            _isPlaying = false;
        }

        private async UniTask FadeTo(float target, float duration)
        {
            if (fadeCanvas == null || duration <= 0f)
            {
                if (fadeCanvas != null)
                    fadeCanvas.alpha = target;
                return;
            }

            float start = fadeCanvas.alpha;
            float time = 0f;

            while (time < duration)
            {
                time += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(time / duration);

                if (!fadeCanvas) return;
                float a = Mathf.Lerp(start, target, t);
                fadeCanvas.alpha = a;

                await UniTask.Yield(PlayerLoopTiming.Update);
            }

            if (!fadeCanvas) return;
            fadeCanvas.alpha = target;
        }

        private async UniTask PlayIrisCloseAsync()
        {
            irisImage.enabled = true;

            if (fadeCanvas != null) fadeCanvas.alpha = 0f;

            float aspect = (float)Screen.width / Screen.height;
            _irisMat.SetFloat("_Aspect", aspect);

            Vector2 centerUv = GetFocusViewportCenter();
            _irisMat.SetVector("_Center", new Vector4(centerUv.x, centerUv.y, 0f, 0f));
            _irisMat.SetFloat("_Softness", irisSoftness);

            float startR = 1.2f;
            float holdR = Mathf.Clamp(irisHoldRadius, 0.001f, startR);
            float endR = 0f;

            float dur1 = irisCloseDuration * 0.5f;
            float dur2 = irisCloseDuration * 0.5f;

            await IrisRadiusTweenAsync(startR, holdR, dur1);

            if (irisHoldDuration > 0f)
                await UniTask.Delay(TimeSpan.FromSeconds(irisHoldDuration));

            await IrisRadiusTweenAsync(holdR, endR, dur2);

            _irisMat.SetFloat("_Radius", 0f);
            irisImage.enabled = false;

            if (fadeCanvas != null) fadeCanvas.alpha = 1f;
        }

        private async UniTask IrisRadiusTweenAsync(float from, float to, float duration)
        {
            if (duration <= 0f)
            {
                _irisMat.SetFloat("_Radius", to);
                return;
            }

            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(t / duration);
                float eased = u * u * u;

                Vector2 centerUv = GetFocusViewportCenter();
                _irisMat.SetVector("_Center", new Vector4(centerUv.x, centerUv.y, 0f, 0f));

                float aspect = (float)Screen.width / Screen.height;
                _irisMat.SetFloat("_Aspect", aspect);

                _irisMat.SetFloat("_Radius", Mathf.Lerp(from, to, eased));
                await UniTask.Yield();
            }

            _irisMat.SetFloat("_Radius", to);
        }

        private Vector2 GetFocusViewportCenter()
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                Camera[] cams = FindObjectsOfType<Camera>(false);
                for (int i = 0; i < cams.Length; i++)
                {
                    if (cams[i] == null || !cams[i].enabled) continue;
                    cam = cams[i];
                    break;
                }
            }

            if (cam == null || _irisFocus == null)
                return new Vector2(0.5f, 0.5f);

            Vector3 vp = cam.WorldToViewportPoint(_irisFocus.position);
            if (vp.z < 0f)
                return new Vector2(0.5f, 0.5f);

            return new Vector2(Mathf.Clamp01(vp.x), Mathf.Clamp01(vp.y));
        }

        private InteractionLockHub FindLocalPlayerHub()
        {
            InteractionLockHub[] hubs = FindObjectsOfType<InteractionLockHub>(true);
            foreach (InteractionLockHub h in hubs)
            {
                if (h != null && h.IsOwner)
                    return h;
            }

            return null;
        }
    }
}
