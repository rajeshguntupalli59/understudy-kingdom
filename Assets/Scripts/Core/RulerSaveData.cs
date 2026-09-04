using System;

namespace UnderstudyKingdom.Core
{
    /// <summary>
    /// JSON-serializable DTO mirroring RulerState, for JsonUtility (which cannot
    /// serialize nested enums directly as robustly across Unity versions, so the
    /// agenda is stored as its int ordinal).
    /// </summary>
    [Serializable]
    public class RulerSaveData
    {
        /// <summary>
        /// Save-format version, for future migrations. Not yet read by SaveService --
        /// this pass only needs the field to exist so old saves are distinguishable
        /// from a format change later, rather than retrofitting it after players
        /// already have version-less save files.
        /// </summary>
        public int Version = 1;

        public int Mood;
        public int Loyalty;
        public int Agenda;
        public bool CouncilRewardApplied;
        public bool TutorialCompleted;
        public string ClaimedEventWeekId;
    }
}
