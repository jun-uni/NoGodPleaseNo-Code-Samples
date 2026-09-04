using System;
using UnityEngine;
using UnityEngine.Localization.Components;
using TMPro;

namespace NGPN.Gameplay.UI
{
    public class DeathSpectateUIController : MonoBehaviour
    {
        [Header("UI References")] [SerializeField]
        private GameObject content;

        [SerializeField] private TextMeshProUGUI remainingTimeText;
        [SerializeField] private TextMeshProUGUI hintText;
        [SerializeField] private TextMeshProUGUI targetNameText;

        private LocalizeStringEvent targetNameEvent;

        private void Awake()
        {
            if (targetNameText != null)
                targetNameEvent = targetNameText.GetComponent<LocalizeStringEvent>();

            remainingTimeText.gameObject.SetActive(false);
            hintText.gameObject.SetActive(false);
            targetNameText.gameObject.SetActive(false);
        }

        public void OnDestroy()
        {
            CloseContent();
        }

        public void OpenContent()
        {
            content.SetActive(true);
        }

        public void CloseContent()
        {
            if (targetNameText)
                targetNameText.gameObject.SetActive(false);
            if (remainingTimeText)
                remainingTimeText.gameObject.SetActive(false);
            if (hintText)
                hintText.gameObject.SetActive(false);
            if (content)
                content.SetActive(false);
        }

        public void UpdateDeathSpectatorTarget(CharacterActor targetActor)
        {
            if (targetNameText == null)
                return;

            if (targetActor == null || targetActor.IsOwner)
            {
                targetNameText.gameObject.SetActive(false);
                return;
            }

            string displayName = targetActor.DisplayName;
            PlayerRegistry reg = PlayerRegistry.Instance;
            if (reg != null && targetActor.Owner != null) displayName = reg.GetDisplayName(targetActor.Owner.ClientId);

            if (targetNameEvent != null)
            {
                bool wasEnabled = targetNameEvent.enabled;
                targetNameEvent.enabled = false;

                // 관전 대상 이름을 로컬라이징 인자로 설정
                targetNameEvent.StringReference.Arguments =
                    displayName != null ? new object[] { displayName } : Array.Empty<object>();

                targetNameEvent.StringReference.SetReference("UI.Common", "ingame.death.currentwatching");

                targetNameEvent.enabled = wasEnabled;
                targetNameEvent.RefreshString();
            }

            targetNameText.gameObject.SetActive(true);
        }

        // 부활까지 남은 시간 갱신
        public void UpdateRemainingTime(bool isAlive, float respawnTimeLeft)
        {
            if (remainingTimeText == null)
                return;

            if (!isAlive && respawnTimeLeft > 0f)
            {
                remainingTimeText.gameObject.SetActive(true);
                remainingTimeText.text = Mathf.Ceil(respawnTimeLeft).ToString("0");
            }
            else
            {
                remainingTimeText.gameObject.SetActive(false);
            }
        }

        public void ShowHint()
        {
            hintText.gameObject.SetActive(true);
        }
    }
}
