using System;
using System.IO;
using UnityEngine;
using UnderstudyKingdom.Npc;

namespace UnderstudyKingdom.Core
{
    /// <summary>
    /// Local JSON persistence for RulerState (FR-03), ahead of the not-yet-designed
    /// backend. The one Unity-dependent piece in this feature (Application.persistentDataPath,
    /// JsonUtility) -- still testable in EditMode since EditMode tests run inside the
    /// Editor process. See docs/superpowers/specs/2026-09-01-core-decision-cycle-design.md
    /// Error Handling section for the missing/corrupt-file behavior below.
    /// </summary>
    public static class SaveService
    {
        private const string FileName = "ruler_save.json";

        public static string SavePath => Path.Combine(Application.persistentDataPath, FileName);

        public static void Save(RulerState state)
        {
            var data = new RulerSaveData
            {
                Mood = state.Mood,
                Loyalty = state.Loyalty,
                Agenda = (int)state.Agenda
            };
            File.WriteAllText(SavePath, JsonUtility.ToJson(data));
        }

        public static RulerState Load()
        {
            if (!File.Exists(SavePath))
            {
                return new RulerState();
            }

            try
            {
                var data = JsonUtility.FromJson<RulerSaveData>(File.ReadAllText(SavePath));
                return new RulerState
                {
                    Mood = data.Mood,
                    Loyalty = data.Loyalty,
                    Agenda = (RulerState.AgendaType)data.Agenda
                };
            }
            catch (Exception)
            {
                return new RulerState();
            }
        }
    }
}
