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
        public int Mood;
        public int Loyalty;
        public int Agenda;
    }
}
