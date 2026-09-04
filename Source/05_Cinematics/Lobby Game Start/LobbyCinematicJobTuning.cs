using System;
using UnityEngine;
using NGPN.Core;

namespace NGPN.Gameplay
{
    // 직업과 슬롯별 더미 위치·손 IK 오프셋 설정
    [CreateAssetMenu(menuName = "Game/Lobby Cinematic Job Tuning", fileName = "LobbyCinematicJobTuning")]
    public sealed class LobbyCinematicJobTuning : ScriptableObject
    {
        [Serializable]
        public struct HandOffset
        {
            public Vector3 pos;
            public Vector3 euler;
        }

        [Serializable]
        public struct SlotOffset
        {
            public Vector3 pos;
            public float yaw;
        }

        [Serializable]
        public struct HandOffsetsPerSlot
        {
            public HandOffset left;
            public HandOffset right;
        }

        [Serializable]
        public class JobEntry
        {
            public JobType jobType;

            [Header("Standing offsets per slot index")]
            public SlotOffset[] slotOffsets;

            [Header("Hand offsets per slot index")]
            public HandOffsetsPerSlot[] handOffsets;
        }

        [SerializeField] private JobEntry[] jobs;

        public bool TryGet(JobType job, out JobEntry entry)
        {
            if (jobs != null)
                for (int i = 0; i < jobs.Length; i++)
                    if (jobs[i] != null && jobs[i].jobType.Equals(job))
                    {
                        entry = jobs[i];
                        return true;
                    }

            entry = null;
            return false;
        }
    }
}
