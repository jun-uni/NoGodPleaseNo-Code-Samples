// 팀 구성 업적의 시작·종료 조건 검증

using System.Collections.Generic;
using NGPN.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NGPN.Gameplay
{
    public class AchievementTracker : MonoBehaviour
    {
        [SerializeField] private SceneDatabase sceneDatabase;

        private string _gameSceneName;
        private string _victorySceneName;

        private bool _startSnapshotTaken;
        private int _startedPlayerCount;
        private HashSet<int> _startedOwnerIds = new();
        private bool _startedAllSameJob;
        private JobType _startedTeamJob = JobType.None;

        private AchievementManager AchievementManager =>
            GameManager.Instance != null ? GameManager.Instance.AchievementManager : null;

        private void OnEnable()
        {
            _gameSceneName = sceneDatabase.GetSceneName(SceneId.Ingame);
            _victorySceneName = sceneDatabase.GetSceneName(SceneId.Victory);

            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void Update()
        {
            // 네트워크 직업 정보가 준비될 때까지 시작 스냅샷 재시도
            if (!_startSnapshotTaken && SceneManager.GetActiveScene().name == _gameSceneName)
                TryCaptureStartTeamSnapshot();
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == _gameSceneName)
            {
                ResetStartTeamSnapshot();
                TryCaptureStartTeamSnapshot();
                return;
            }

            if (scene.name != _victorySceneName || !_startedAllSameJob) return;

            // 시작 멤버와 직업 구성이 승리 시점까지 유지됐는지 검증
            if (!IsSameTeamAsStarted(out JobType currentJob)) return;
            if (currentJob != _startedTeamJob) return;

            AchievementKey key = GetTeamJobAchievementKey(currentJob);
            if (key != AchievementKey.Null)
                AchievementManager?.Unlock(key);
        }

        private void ResetStartTeamSnapshot()
        {
            _startSnapshotTaken = false;
            _startedOwnerIds.Clear();
            _startedPlayerCount = 0;
            _startedAllSameJob = false;
            _startedTeamJob = JobType.None;
        }

        private bool TryCaptureStartTeamSnapshot()
        {
            if (_startSnapshotTaken) return true;

            PlayerRegistry registry = PlayerRegistry.Instance;
            if (registry == null || registry.PlayerCount <= 0) return false;
            if (!registry.TryGetConnectedOwnerIds(out List<int> ownerIds)) return false;

            JobType firstJob = JobType.None;
            bool allSameJob = true;

            foreach (int ownerId in ownerIds)
            {
                // 스폰 직후 직업 동기화가 끝나지 않았다면 다음 프레임 재시도
                if (!registry.TryGetJob(ownerId, out JobType job) || job == JobType.None)
                    return false;

                if (firstJob == JobType.None)
                    firstJob = job;
                else if (job != firstJob)
                    allSameJob = false;
            }

            _startedOwnerIds = new HashSet<int>(ownerIds);
            _startedPlayerCount = _startedOwnerIds.Count;
            _startedAllSameJob = allSameJob && firstJob != JobType.None;
            _startedTeamJob = _startedAllSameJob ? firstJob : JobType.None;
            _startSnapshotTaken = true;

            return true;
        }

        private bool IsSameTeamAsStarted(out JobType currentTeamJob)
        {
            currentTeamJob = JobType.None;
            if (!_startSnapshotTaken || _startedPlayerCount <= 0) return false;

            PlayerRegistry registry = PlayerRegistry.Instance;
            if (registry == null) return false;
            if (!registry.TryGetConnectedOwnerIds(out List<int> currentOwnerIds)) return false;

            HashSet<int> currentOwnerIdSet = new(currentOwnerIds);

            // 인원수와 소유자 집합을 함께 비교
            if (currentOwnerIdSet.Count != _startedPlayerCount) return false;
            if (!_startedOwnerIds.SetEquals(currentOwnerIdSet)) return false;

            JobType firstJob = JobType.None;
            foreach (int ownerId in currentOwnerIdSet)
            {
                if (!registry.TryGetJob(ownerId, out JobType job) || job == JobType.None)
                    return false;

                if (firstJob == JobType.None)
                    firstJob = job;
                else if (job != firstJob)
                    return false;
            }

            currentTeamJob = firstJob;
            return currentTeamJob != JobType.None;
        }

        private static AchievementKey GetTeamJobAchievementKey(JobType job)
        {
            return job switch
            {
                JobType.Barbarian => AchievementKey.TeamBarbarian,
                JobType.Tanker => AchievementKey.TeamTanker,
                JobType.Lancer => AchievementKey.TeamLancer,
                JobType.Ranger => AchievementKey.TeamRanger,
                JobType.Pirate => AchievementKey.TeamPirate,
                JobType.Voodoo => AchievementKey.TeamVoodoo,
                _ => AchievementKey.Null
            };
        }
    }
}
