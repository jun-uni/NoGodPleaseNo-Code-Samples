// 로비 직업 선택과 서버 변경 요청

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Components;
using TMPro;
using FishNet;
using FishNet.Connection;
using FishNet.Managing;

namespace NGPN.Gameplay.UI
{
    public class LobbyJobSelectUIController : MonoBehaviour
    {
        [SerializeField] private List<LobbyJobSelectButton> jobSelectButtons;
        [SerializeField] private TextMeshProUGUI jobNameTitleText;
        [SerializeField] private LocalizeStringEvent jobNameTitleLocalizeEvent;
        [SerializeField] private string jobNameTable = "Gameplay.Classes";
        [SerializeField] private string jobNameEntrySuffix = ".name";

        private bool _hasPendingJob;
        public JobType _pendingJob;

        private void OnJobSelectButtonClicked(JobType jobType)
        {
            _pendingJob = jobType;
            _hasPendingJob = true;

            UpdateJobNameTitle(jobType);
            SetSelectedButtonVisual(jobType);
        }

        private void CloseContentPanel(bool noChange = false)
        {
            if (!noChange)
                if (_hasPendingJob)
                {
                    SendSelectionToServer(_pendingJob);
                    _hasPendingJob = false;
                }

        }

        private void SendSelectionToServer(JobType jobType)
        {
            NetworkManager nm = InstanceFinder.NetworkManager;
            if (nm == null)
            {
                Debug.LogError("[LobbyJobSelectUI] NetworkManager not found.");
                return;
            }

            NetworkConnection myConn = nm.ClientManager.Connection;
            PlayerAvatarState state = myConn?.FirstObject.GetComponentInParent<PlayerAvatarState>();
            if (state == null)
            {
                Debug.LogError("[LobbyJobSelectUI] PlayerAvatarState component not found on player object.");
                return;
            }

            // 플레이어 소유 오브젝트를 통해 서버에 직업 변경 요청
            state.ServerSetSelectedJob(jobType);
        }

        private string MakeJobNameEntryKey(JobType jobType)
        {
            string head = jobType.ToString().ToLowerInvariant();
            return head + jobNameEntrySuffix;
        }

        private void UpdateJobNameTitle(JobType jobType)
        {
            SetJobNameTitleVisible(true);

            string entryKey = MakeJobNameEntryKey(jobType);
            bool hasLocComp = jobNameTitleLocalizeEvent != null;

            if (hasLocComp && !string.IsNullOrWhiteSpace(jobNameTable) && !string.IsNullOrWhiteSpace(entryKey))
            {
                LocalizedString ls = jobNameTitleLocalizeEvent.StringReference;
                ls.TableReference = jobNameTable;
                ls.TableEntryReference = entryKey;
                jobNameTitleLocalizeEvent.StringReference = ls;
                jobNameTitleLocalizeEvent.RefreshString();
                return;
            }

            // 로컬라이징 참조가 없을 때 enum 이름 사용
            if (jobNameTitleText != null)
                jobNameTitleText.text = jobType.ToString();
        }

        private void SetJobNameTitleVisible(bool visible)
        {
            if (jobNameTitleText != null)
                jobNameTitleText.gameObject.SetActive(visible);
        }

        private void SetSelectedButtonVisual(JobType? selected)
        {
            if (jobSelectButtons == null) return;

            foreach (LobbyJobSelectButton jb in jobSelectButtons)
            {
                if (jb == null || jb.button == null) continue;

                if (selected.HasValue)
                    jb.button.interactable = jb.jobType != selected.Value;
                else
                    jb.button.interactable = true;
            }
        }

    }
}
