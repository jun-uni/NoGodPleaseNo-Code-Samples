using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using NGPN.Core;
using UnityEngine;
using Unity.Services.Vivox;
using UnityEngine.SceneManagement;


namespace NGPN.Gameplay
{
    // 참가자별 Vivox 볼륨과 음소거 상태 관리
    public class VoiceChatController : MonoBehaviour
    {
        [Header("Channel")] [Tooltip("VivoxBootstrap이 Join한 포지셔널 채널 이름")] [SerializeField]
        private string channelName;

        [Header("Tick")] [Tooltip("참가자 볼륨 재계산 주기(초)")] [SerializeField]
        private float tickInterval = 0.25f;

        [Header("Barbarian Settings")] [SerializeField]
        private float barbarianBoost = 1.5f;

        private float _nextTick;

        // PlayerId별 아바타 연결
        private readonly Dictionary<string, Transform> _avatarsByPlayerId = new();

        // PlayerId별 로컬 음성 상태
        private readonly Dictionary<string, PlayerVoiceState> _stateByPlayerId = new();

        public event Action ParticipantsChanged;

        // 로컬 마이크 상태 캐시
        private bool _localMicMuted;
        private int _localMicUiVolume = 100;

        private class PlayerVoiceState
        {
            public bool IsBarbarian;
            public bool Muted;

            // UI 볼륨 범위 0..100
            public int UiVolume = 100;

            public int LastAppliedVivoxVol = int.MinValue;
        }

        public void SetChannelName(string name)
        {
            channelName = name;
        }

        public string CurrentChannel => channelName;

        // 플레이어 음성 상태와 아바타 등록
        public void RegisterAvatar(string playerId, Transform avatar, bool isBarbarian = false)
        {
            if (string.IsNullOrEmpty(playerId)) return;

            _avatarsByPlayerId[playerId] = avatar;

            if (!_stateByPlayerId.TryGetValue(playerId, out PlayerVoiceState s))
                _stateByPlayerId[playerId] = s = new PlayerVoiceState();

            s.IsBarbarian = isBarbarian;

            ParticipantsChanged?.Invoke();
        }

        public void UnregisterAvatar(string playerId)
        {
            if (string.IsNullOrEmpty(playerId)) return;
            _avatarsByPlayerId.Remove(playerId);

            _stateByPlayerId.Remove(playerId);
            ParticipantsChanged?.Invoke();
        }

        public bool HasPlayer(string playerId)
        {
            return !string.IsNullOrEmpty(playerId) && _avatarsByPlayerId.ContainsKey(playerId);
        }

        public void SetPlayerRoleBarbarian(string playerId, bool isBarbarian)
        {
            if (string.IsNullOrEmpty(playerId)) return;
            if (!_stateByPlayerId.TryGetValue(playerId, out PlayerVoiceState s))
                _stateByPlayerId[playerId] = s = new PlayerVoiceState();
            s.IsBarbarian = isBarbarian;
        }

        public bool IsBarbarian(string playerId)
        {
            return _stateByPlayerId.TryGetValue(playerId, out PlayerVoiceState s) && s.IsBarbarian;
        }

        // 플레이어 UI 볼륨 설정
        public void SetUserVolume(string playerId, int uiVolume)
        {
            if (string.IsNullOrEmpty(playerId)) return;
            if (!_stateByPlayerId.TryGetValue(playerId, out PlayerVoiceState s))
                _stateByPlayerId[playerId] = s = new PlayerVoiceState();

            s.UiVolume = Mathf.Clamp(uiVolume, 0, 100);

            ParticipantsChanged?.Invoke();
        }

        public int GetUserVolume(string playerId)
        {
            return _stateByPlayerId.TryGetValue(playerId, out PlayerVoiceState s) ? s.UiVolume : 100;
        }

        public void SetMuted(string playerId, bool muted)
        {
            if (string.IsNullOrEmpty(playerId)) return;
            if (!_stateByPlayerId.TryGetValue(playerId, out PlayerVoiceState s))
                _stateByPlayerId[playerId] = s = new PlayerVoiceState();
            s.Muted = muted;

            ParticipantsChanged?.Invoke();
        }

        public bool IsMuted(string playerId)
        {
            return _stateByPlayerId.TryGetValue(playerId, out PlayerVoiceState s) && s.Muted;
        }

        public void SetTickInterval(float seconds)
        {
            tickInterval = Mathf.Clamp(seconds, 0.05f, 1.0f);
        }

        public void RefreshAllNow()
        {
            _nextTick = 0f;
        }

        private void Update()
        {
            if (Time.time < _nextTick) return;
            _nextTick = Time.time + tickInterval;

            if (string.IsNullOrEmpty(channelName)) return;

            if (!VivoxService.Instance.ActiveChannels.TryGetValue(
                    channelName,
                    out ReadOnlyCollection<VivoxParticipant> participants))
                return;

            foreach (VivoxParticipant p in participants)
            {
                if (p.IsSelf) continue;

                if (!_stateByPlayerId.TryGetValue(p.PlayerId, out PlayerVoiceState s))
                    _stateByPlayerId[p.PlayerId] = s = new PlayerVoiceState();

                // 역할 보정을 포함한 UI 볼륨 계산
                int effectiveUi0to200 = s.IsBarbarian
                    ? s.UiVolume == 0 ? 0 : (int)(s.UiVolume * barbarianBoost)
                    : s.UiVolume;


                int halfScaled0to100 = Mathf.RoundToInt(effectiveUi0to200 * 0.5f);
                int baseVol = UiToVivoxVolume(halfScaled0to100);

                // 음소거 우선 적용
                int targetVol = s.Muted ? -50 : baseVol;

                // 변경된 값만 Vivox에 반영
                if (s.LastAppliedVivoxVol != targetVol)
                {
                    p.SetLocalVolume(targetVol);
                    s.LastAppliedVivoxVol = targetVol;
                }
            }
        }

        // UI 볼륨을 Vivox 출력 범위로 변환
        private static int UiToVivoxVolume(int ui0to100)
        {
            float t = Mathf.InverseLerp(0, 100, Mathf.Clamp(ui0to100, 0, 100));
            return Mathf.RoundToInt(Mathf.Lerp(-50, 50, t));
        }

        private static int UiToVivoxMicVolume(int ui0to100)
        {
            float t = Mathf.InverseLerp(0, 100, Mathf.Clamp(ui0to100, 0, 100));
            return Mathf.RoundToInt(Mathf.Lerp(-20, 5, t));
        }

        // 음성 상태를 유지한 아바타 연결 해제
        public void ClearAllAvatarMappings()
        {
            _avatarsByPlayerId.Clear();
        }

        // 특정 플레이어의 로컬 음성 상태 제거
        public void ClearPlayerState(string playerId)
        {
            if (string.IsNullOrEmpty(playerId)) return;
            _stateByPlayerId.Remove(playerId);
        }

        public IReadOnlyList<string> SnapshotPlayerIds()
        {
            HashSet<string> set = new(_stateByPlayerId.Keys);
            foreach (string k in _avatarsByPlayerId.Keys)
                set.Add(k);
            return set.ToList();
        }

        // 로컬 마이크 음소거 상태 반환
        public bool IsLocalMicMuted()
        {
            return _localMicMuted;
        }

        public void SetLocalMicMuted(bool muted)
        {
            try
            {
                IVivoxService svc = VivoxService.Instance;
                if (svc == null) return;
                if (muted) svc.MuteInputDevice();
                else svc.UnmuteInputDevice();
                _localMicMuted = muted;
            }
            catch
            {
                // 음성 장치 전환 중 SDK 예외 허용
            }

            ParticipantsChanged?.Invoke();
        }

        // 로컬 마이크 UI 볼륨 반환
        public int GetLocalMicVolumeUi()
        {
            return _localMicUiVolume;
        }

        // UI 볼륨을 Vivox 입력 볼륨으로 변환
        public void SetLocalMicVolumeUi(int ui0to100)
        {
            _localMicUiVolume = Mathf.Clamp(ui0to100, 0, 100);
            try
            {
                IVivoxService svc = VivoxService.Instance;
                if (svc == null) return;

                int vivoxVol = UiToVivoxMicVolume(_localMicUiVolume);
                svc.SetInputDeviceVolume(vivoxVol);
            }
            catch
            {
                // 음성 장치 전환 중 SDK 예외 허용
            }

            ParticipantsChanged?.Invoke();
        }

        public void ResetAllStates()
        {
            _avatarsByPlayerId.Clear();
            _stateByPlayerId.Clear();

            // 로컬 음성 상태 초기화
            _localMicMuted = false;
            _localMicUiVolume = 100;

            if (VivoxBootstrap.Instance == null ||
                VivoxService.Instance == null ||
                !VivoxService.Instance.IsLoggedIn)
            {
                ParticipantsChanged?.Invoke();
                return;
            }

            // Vivox 입력 장치 기본값 복원
            try
            {
                IVivoxService svc = VivoxService.Instance;
                if (svc != null)
                {
                    svc.UnmuteInputDevice();

                    int vivoxVol = UiToVivoxMicVolume(_localMicUiVolume);
                    svc.SetInputDeviceVolume(vivoxVol);
                }
            }
            catch
            {
                // 세션 종료 중 SDK 예외 허용
            }

            ParticipantsChanged?.Invoke();
        }
    }
}
