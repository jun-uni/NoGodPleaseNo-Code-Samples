using System.Threading;
using UnityEngine;
using Cysharp.Threading.Tasks;
using DG.Tweening;

namespace NGPN.Gameplay
{
    // 여신상 상승·회전 가속·효과음 재생 수명주기
    public sealed class StatueLift : MonoBehaviour
    {
        [Header("Target")][SerializeField] private Transform statueRoot;

        [Header("Lift")][SerializeField] private float liftHeight = 6f;
        [SerializeField] private float liftDuration = 1.2f;
        [SerializeField] private Ease liftEase = Ease.InCubic;

        [Header("Rotation")][SerializeField] private bool enableSpin = true;

        [SerializeField] private float targetAngularSpeed = 720f;
        [SerializeField] private float spinAccelDuration = 1.2f;
        [SerializeField] private Ease spinEase = Ease.InCubic;

        private Tween _spinTween;
        private float _currentAngularSpeed;
        private bool _spinning;

        [Header("SFX")][SerializeField] private AudioSource sfxSource;

        [SerializeField] private AudioClip whooshClip;
        [SerializeField][Range(0f, 1.5f)] private float whooshVolume = 1f;

        [Header("SFX Delay")]
        [SerializeField]
        [Range(0f, 5f)]
        private float whooshDelaySeconds = 0f;


        private Tween _t;
        private Vector3 _startPos;
        private bool _hasStartPos;

        private void Awake()
        {
            if (statueRoot == null)
                statueRoot = transform;

            _startPos = statueRoot.position;
            _hasStartPos = true;
        }

        private void OnDestroy()
        {
            ResetToStart();
        }

        private void Update()
        {
            if (!_spinning || statueRoot == null)
                return;

            statueRoot.Rotate(
                Vector3.up,
                _currentAngularSpeed * Time.deltaTime,
                Space.World
            );
        }


        public void Lift()
        {
            if (statueRoot == null) return;

            _t?.Kill();
            _spinTween?.Kill();

            PlayWhooshAfterDelayAsync(this.GetCancellationTokenOnDestroy()).Forget();

            Vector3 to = statueRoot.position + Vector3.up * liftHeight;
            _t = statueRoot.DOMove(to, liftDuration)
                .SetEase(liftEase)
                .SetLink(statueRoot.gameObject);

            if (enableSpin)
            {
                _currentAngularSpeed = 0f;
                _spinning = true;

                _spinTween = DOTween.To(
                        () => _currentAngularSpeed,
                        v => _currentAngularSpeed = v,
                        targetAngularSpeed,
                        spinAccelDuration
                    )
                    .SetEase(spinEase)
                    .SetLink(gameObject);
            }
        }


        private async UniTaskVoid PlayWhooshAfterDelayAsync(CancellationToken ct)
        {
            if (sfxSource == null || whooshClip == null) return;

            if (whooshDelaySeconds > 0f)
                await UniTask.Delay(System.TimeSpan.FromSeconds(whooshDelaySeconds), cancellationToken: ct);

            if (ct.IsCancellationRequested) return;

            sfxSource.PlayOneShot(whooshClip, whooshVolume);
        }

        public void ResetToStart()
        {
            _t?.Kill();
            _spinTween?.Kill();

            _spinning = false;
            _currentAngularSpeed = 0f;

            if (_hasStartPos && statueRoot != null)
                statueRoot.position = _startPos;
        }
    }
}
