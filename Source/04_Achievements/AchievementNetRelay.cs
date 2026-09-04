// 서버 조건 판정과 오너 클라이언트 해금 전달

using System.Collections.Generic;
using FishNet.Connection;
using FishNet.Object;

namespace NGPN.Gameplay
{
    public class AchievementNetRelay : NetworkBehaviour
    {
        private readonly HashSet<AchievementKey> _serverUnlockedThisSession = new();

        [Server]
        public void ReportAbilityMetric(AbilityId ability, AbilityMetric metric, float value)
        {
            if (!IsServerInitialized) return;

            AchievementManager achievementManager = GameManager.Instance
                ? GameManager.Instance.AchievementManager
                : null;
            if (achievementManager == null) return;

            // 서버에서 설정 데이터 기준 달성 여부 판정
            if (!achievementManager.TryResolveAbilityUnlock(ability, metric, value, out AchievementKey key))
                return;

            // 플레이어별 세션 중복 해금 차단
            if (_serverUnlockedThisSession.Contains(key)) return;
            _serverUnlockedThisSession.Add(key);

            TargetUnlock(Owner, key);
        }

        [TargetRpc]
        private void TargetUnlock(NetworkConnection owner, AchievementKey key)
        {
            if (!IsOwner) return;

            // 실제 플랫폼 API 호출은 해당 오너 클라이언트에서 수행
            AchievementManager achievementManager = GameManager.Instance
                ? GameManager.Instance.AchievementManager
                : null;
            achievementManager?.Unlock(key);
        }
    }
}
