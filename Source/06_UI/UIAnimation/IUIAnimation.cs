using DG.Tweening;

namespace NGPN.Gameplay.UI_Animation.Runtime
{
    public interface IUIAnimation
    {
        bool IsPlaying { get; }
        Tween CurrentTween { get; }

        void BuildIfNeeded();
        void Play();
        void Pause();
        void Rewind(bool includeDelay = true);
        void Complete(bool withCallbacks = false);
        void Kill(bool complete = false);
    }
}
