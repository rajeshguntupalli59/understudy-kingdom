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

        /// <summary>True if a save file currently exists on disk.</summary>
        public static bool HasSave()
        {
            return File.Exists(SavePath);
        }

        public static void Save(RulerState state)
        {
            var data = new RulerSaveData
            {
                Mood = state.Mood,
                Loyalty = state.Loyalty,
                Agenda = (int)state.Agenda,
                CouncilRewardApplied = state.CouncilRewardApplied,
                TutorialCompleted = state.TutorialCompleted,
                ClaimedEventWeekId = state.ClaimedEventWeekId
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
                string raw = File.ReadAllText(SavePath);
                string trimmed = raw.TrimStart();

                // JsonUtility's own behavior on malformed input is not something we can
                // verify without a real Unity runtime (see task-4-report.md), so this
                // pre-check makes corruption detection deterministic instead of relying
                // on it: anything that doesn't even start a JSON object is treated as
                // corrupt before JsonUtility is ever invoked.
                if (trimmed.Length == 0 || trimmed[0] != '{')
                {
                    return new RulerState();
                }

                var data = JsonUtility.FromJson<RulerSaveData>(raw);

                var agenda = Enum.IsDefined(typeof(RulerState.AgendaType), data.Agenda)
                    ? (RulerState.AgendaType)data.Agenda
                    : RulerState.AgendaType.Expansionist;

                var loaded = new RulerState
                {
                    Mood = data.Mood,
                    Loyalty = data.Loyalty,
                    Agenda = agenda,
                    CouncilRewardApplied = data.CouncilRewardApplied,
                    TutorialCompleted = data.TutorialCompleted,
                    ClaimedEventWeekId = data.ClaimedEventWeekId ?? string.Empty
                };

                // Clamp Mood/Loyalty into [0,100] in case the file was corrupted with an
                // out-of-range value; ApplyDelta(0, 0) applies no change but still clamps.
                loaded.ApplyDelta(0, 0);

                return loaded;
            }
            catch (Exception)
            {
                return new RulerState();
            }
        }
    }
}
