using UnityEngine;
using UnityEngine.UI;
using TMPro;
using NGPN.Gameplay;

namespace NGPN.Gameplay.UI
{
    public class PlayerVoiceChatControlPanel : MonoBehaviour
    {
        [Header("UI")] [SerializeField] private TextMeshProUGUI playerName;
        [SerializeField] private Slider volumeSlider;
        [SerializeField] private Button muteButton;
        [SerializeField] private Button unmuteButton;

        private string _playerId;
        private int _connId = -1;
        private VoiceChatController _voice;

        private bool _bound;
        private bool _isSelf;

        // 외부 바인딩 API
        public void InitByConnId(string playerId, int connId, string displayNameOverride = null)
        {
            _playerId = playerId;
            _connId = connId;
            Setup();

            if (!string.IsNullOrWhiteSpace(displayNameOverride))
            {
                if (playerName) playerName.text = displayNameOverride;
            }
            else
            {
                // PlayerRegistry 표시명 조회
                ResolveAndSetDisplayName();
            }
        }

        // 슬롯 초기화
        public void Unbind()
        {
            _playerId = null;
            _connId = -1;
            if (playerName) playerName.text = "—";
            if (volumeSlider)
            {
                volumeSlider.SetValueWithoutNotify(100);
                volumeSlider.interactable = false;
            }

            gameObject.SetActive(false);
        }

        // 참가자 정보 바인딩
        public void Bind(string playerId, int connId, string displayName, bool isSelf = false)
        {
            _playerId = playerId;
            _connId = connId;
            _isSelf = isSelf;

            Setup();
            if (playerName) playerName.text = string.IsNullOrWhiteSpace(displayName) ? "Player" : displayName;

            gameObject.SetActive(true);
        }

        public void Init(string playerId, string displayName)
        {
            _playerId = playerId;
            _connId = -1;
            Setup();
            if (playerName) playerName.text = string.IsNullOrWhiteSpace(displayName) ? "Player" : displayName;
        }

        private void Setup()
        {
            if (_voice == null)
                _voice = FindFirstObjectByType<VoiceChatController>();

            // 볼륨 슬라이더 범위 설정
            if (volumeSlider != null)
            {
                volumeSlider.minValue = 0f;
                volumeSlider.maxValue = 100f;
                volumeSlider.wholeNumbers = true;
            }

            RefreshFromModel();

            if (!_bound)
            {
                _bound = true;
                if (volumeSlider != null)
                    volumeSlider.onValueChanged.AddListener(OnSliderChanged);
                if (muteButton != null)
                    muteButton.onClick.AddListener(OnClickMute);
                if (unmuteButton != null)
                    unmuteButton.onClick.AddListener(OnClickUnmute);
            }

            UpdateInteractable();

            UpdateMuteButtons();
        }

        private void OnEnable()
        {
            RefreshFromModel();
            UpdateInteractable();
            UpdateMuteButtons();

            gameObject.SetActive(!string.IsNullOrWhiteSpace(_playerId));
        }

        private void OnDestroy()
        {
            if (_bound)
            {
                if (volumeSlider != null)
                    volumeSlider.onValueChanged.RemoveListener(OnSliderChanged);
                if (muteButton != null)
                    muteButton.onClick.RemoveListener(OnClickMute);
                if (unmuteButton != null)
                    unmuteButton.onClick.RemoveListener(OnClickUnmute);
            }
        }

        private void RefreshFromModel()
        {
            if (volumeSlider == null) return;

            int vol = 100;
            if (_voice != null)
            {
                if (_isSelf)
                    // 로컬 마이크 볼륨 조회
                    vol = _voice.GetLocalMicVolumeUi();
                else if (!string.IsNullOrEmpty(_playerId) && _voice.HasPlayer(_playerId))
                    // 원격 참가자 볼륨 조회
                    vol = _voice.GetUserVolume(_playerId);
            }

            volumeSlider.SetValueWithoutNotify(vol);
            UpdateMuteButtons();
        }

        private void OnSliderChanged(float f)
        {
            if (_voice == null || string.IsNullOrEmpty(_playerId)) return;

            int vol = Mathf.RoundToInt(f);

            if (_isSelf)
            {
                // 로컬 마이크 볼륨 적용
                _voice.SetLocalMicVolumeUi(vol);
                if (_voice.IsLocalMicMuted())
                    _voice.SetLocalMicMuted(false);
            }
            else
            {
                // 원격 참가자 재생 볼륨 적용
                _voice.SetUserVolume(_playerId, vol);
                if (_voice.IsMuted(_playerId))
                    _voice.SetMuted(_playerId, false);
            }

            UpdateMuteButtons();
            _voice.RefreshAllNow();
        }

        private void UpdateInteractable()
        {
            bool ok;
            if (_voice == null || string.IsNullOrEmpty(_playerId))
                ok = false;
            else if (_isSelf)
                ok = true;
            else
                ok = _voice.HasPlayer(_playerId);

            if (volumeSlider != null) volumeSlider.interactable = ok;
            if (muteButton != null) muteButton.interactable = ok;
            if (unmuteButton != null) unmuteButton.interactable = ok;
        }

        private void ResolveAndSetDisplayName()
        {
            if (playerName == null) return;
            string display = null;

            PlayerRegistry reg = PlayerRegistry.Instance;
            if (reg != null && _connId >= 0)
            {
                // FishNet 연결 ID 기반 표시명 조회
                if (!reg.TryGetName(_connId, out display))
                    display = $"Conn-{_connId}";
            }
            else
            {
                display = "Player";
            }

            playerName.text = display;
        }

        private void OnClickMute()
        {
            if (_voice == null || string.IsNullOrEmpty(_playerId)) return;
            if (_isSelf) _voice.SetLocalMicMuted(true);
            else _voice.SetMuted(_playerId, true);
            UpdateMuteButtons();
            _voice.RefreshAllNow();
        }

        private void OnClickUnmute()
        {
            if (_voice == null || string.IsNullOrEmpty(_playerId)) return;
            if (_isSelf) _voice.SetLocalMicMuted(false);
            else _voice.SetMuted(_playerId, false);
            UpdateMuteButtons();
            _voice.RefreshAllNow();
        }

        private void UpdateMuteButtons()
        {
            if (muteButton == null && unmuteButton == null) return;
            bool muted = false;
            if (_voice != null && !string.IsNullOrEmpty(_playerId))
            {
                if (_isSelf) muted = _voice.IsLocalMicMuted();
                else if (_voice.HasPlayer(_playerId)) muted = _voice.IsMuted(_playerId);
            }

            // 뮤트 상태별 버튼 전환
            if (muteButton != null) muteButton.gameObject.SetActive(!muted);
            if (unmuteButton != null) unmuteButton.gameObject.SetActive(muted);
        }
    }
}
