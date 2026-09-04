using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using FishNet.Object;
using NGPN.Gameplay;

namespace NGPN.Gameplay.UI
{
    public class PlayerVoiceChatList : MonoBehaviour
    {
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("UI Slots")] [SerializeField]
        private PlayerVoiceChatControlPanel slot1;

        [SerializeField] private PlayerVoiceChatControlPanel slot2;
        [SerializeField] private PlayerVoiceChatControlPanel slot3;
        [SerializeField, Min(1)] private int maxPlayers = 3;

        private float nextScan;
        private VoiceChatController voiceChatController;


        private void OnEnable()
        {
            voiceChatController = FindFirstObjectByType<VoiceChatController>();
            if (voiceChatController != null) voiceChatController.ParticipantsChanged += ForceRefreshNow;

            VivoxBootstrap vb = VivoxBootstrap.Instance;
            if (vb != null)
            {
                vb.OnVoiceChannelJoined += HandleVivoxJoined;
                vb.OnVoiceChannelLeft += HandleVivoxLeft;

                // 현재 채널 참가 상태 반영
                if (vb.IsAnyChannelJoined)
                    SetInteractable(true);
                else
                    SetInteractable(false);
            }

            ForceRefreshNow();
        }

        private void OnDisable()
        {
            if (voiceChatController != null) voiceChatController.ParticipantsChanged -= ForceRefreshNow;

            VivoxBootstrap vb = VivoxBootstrap.Instance;
            if (vb != null)
            {
                vb.OnVoiceChannelJoined -= HandleVivoxJoined;
                vb.OnVoiceChannelLeft -= HandleVivoxLeft;
            }
        }

        private void Update()
        {
            // 참가자 상태의 주기적 보정
            if (Time.unscaledTime < nextScan) return;
            nextScan = Time.unscaledTime + 0.5f;
            ScanAndSync();
        }

        public void ForceRefreshNow()
        {
            nextScan = 0f;
        }

        private List<PlayerVoiceChatControlPanel> GetAvailableSlots()
        {
            List<PlayerVoiceChatControlPanel> slots = new();
            AddSlotIfMissing(slots, slot1);
            AddSlotIfMissing(slots, slot2);
            AddSlotIfMissing(slots, slot3);

            PlayerVoiceChatControlPanel[] childSlots =
                GetComponentsInChildren<PlayerVoiceChatControlPanel>(true);
            foreach (PlayerVoiceChatControlPanel childSlot in childSlots)
                AddSlotIfMissing(slots, childSlot);

            return slots;
        }

        private static void AddSlotIfMissing(List<PlayerVoiceChatControlPanel> slots,
            PlayerVoiceChatControlPanel slot)
        {
            if (slot != null && !slots.Contains(slot))
                slots.Add(slot);
        }

        private void ScanAndSync()
        {
            List<PlayerVoiceChatControlPanel> slots = GetAvailableSlots();
            int capacity = Mathf.Clamp(maxPlayers, 0, slots.Count);

            for (int i = capacity; i < slots.Count; i++)
                slots[i].Unbind();

            if (capacity == 0) return;

            // 현재 씬의 PlayerVoiceLink 수집
            PlayerVoiceLink[] links = FindObjectsOfType<PlayerVoiceLink>(false);

            // 참가자 식별 정보 구성
            List<(string pid, int connId, string display, bool isSelf)> collected = new(links.Length);
            foreach (PlayerVoiceLink link in links)
            {
                if (link == null) continue;
                string pid = link.VivoxPlayerId;
                if (string.IsNullOrEmpty(pid)) continue;
                NetworkObject nob = link.GetComponent<NetworkObject>();
                int connId = nob != null && nob.Owner != null ? nob.Owner.ClientId : -1;
                bool isSelf = link.IsOwner;
                string display = TryResolveDisplayName(connId);
                // PlayerId 중복 제거
                if (!collected.Any(c => c.pid == pid))
                    collected.Add((pid, connId, display, isSelf));
            }

            // 로컬 플레이어 우선 정렬
            (string pid, int connId, string display, bool isSelf) self = collected.FirstOrDefault(c => c.isSelf);
            List<(string pid, int connId, string display, bool isSelf)> others = collected.Where(c => !c.isSelf)
                .OrderBy(c => c.connId)
                .ThenBy(c => c.pid)
                .ToList();

            // maxPlayers와 사용 가능한 패널 수를 기준으로 목록 구성
            bool hasSelf = !string.IsNullOrEmpty(self.pid);
            List<(string pid, int connId, string display)> ordered = new(capacity);
            if (hasSelf) ordered.Add((self.pid, self.connId, self.display));
            foreach ((string pid, int connId, string display, bool isSelf) o in others)
            {
                if (ordered.Count >= capacity) break;
                ordered.Add((o.pid, o.connId, o.display));
            }

            while (ordered.Count < capacity) ordered.Add((null, -1, null));

            for (int i = 0; i < capacity; i++)
            {
                bool isSelfSlot = hasSelf && i == 0;
                ApplyToSlot(slots[i], ordered[i], isSelfSlot, isSelfSlot);
            }
        }

        private static string Shorten(string id)
        {
            if (string.IsNullOrEmpty(id)) return "Player";
            return id.Length <= 6 ? id : id.Substring(0, 6);
        }

        private void ApplyToSlot(PlayerVoiceChatControlPanel slot, (string pid, int connId, string display) data,
            bool forceSelfLabel = false, bool isSelf = false)
        {
            if (!slot) return;
            if (string.IsNullOrEmpty(data.pid))
            {
                slot.Unbind();
                return;
            }

            string label = data.display;
            if (forceSelfLabel && !string.IsNullOrEmpty(label))
            {
                // 로컬 슬롯 표시명 재확인
                PlayerRegistry reg = PlayerRegistry.Instance;
                if (reg != null) label = reg.GetLocalDisplayName();
            }

            slot.Bind(data.pid, data.connId, label, isSelf);
        }

        private string TryResolveDisplayName(int connId)
        {
            PlayerRegistry reg = PlayerRegistry.Instance;
            if (reg != null && connId >= 0)
                return reg.GetDisplayName(connId);

            return $"Conn-{connId}";
        }

        public void ClearAllSlots()
        {
            foreach (PlayerVoiceChatControlPanel slot in GetAvailableSlots())
                slot.Unbind();
        }

        private void SetInteractable(bool enabled)
        {
            if (canvasGroup == null) return;

            canvasGroup.interactable = enabled;
            canvasGroup.blocksRaycasts = enabled;
            canvasGroup.alpha = enabled ? 1f : 0.4f;
        }

        private void HandleVivoxJoined(string channel)
        {
            SetInteractable(true);
        }

        private void HandleVivoxLeft()
        {
            SetInteractable(false);
        }
    }
}
