using System;
using System.Collections.Generic;
using UnityEngine;
using UnderstudyKingdom.Npc;

namespace UnderstudyKingdom.Core
{
    /// <summary>
    /// Thin orchestrator for the prep -> ruler-decision loop (FR-01, FR-02, FR-03).
    /// Holds no decision logic itself -- that lives in OverrideEvaluator (pure,
    /// testable) so this class stays a coordinator. See
    /// docs/superpowers/specs/2026-09-01-core-decision-cycle-design.md.
    /// </summary>
    public class DecisionCycleManager : MonoBehaviour
    {
        public RulerNpcController Ruler;

        public event Action<DecisionRecord> OnDecisionRecorded;

        private int currentCycleNumber;

        private void Awake()
        {
            LoadPersistedStateIfPresent();
        }

        /// <summary>
        /// Loads persisted RulerState from disk into Ruler.State if a save file exists.
        /// Called from Awake(); also exposed publicly because EditMode tests run outside
        /// Play Mode, where Unity does not invoke Awake() on GameObject.SetActive(true)
        /// for plain (non-[ExecuteAlways]) MonoBehaviours.
        /// </summary>
        public void LoadPersistedStateIfPresent()
        {
            if (Ruler != null && SaveService.HasSave())
            {
                Ruler.State = SaveService.Load();
            }
        }

        /// <summary>
        /// Seeds the local cycle counter from a server-known cycle number, but only
        /// if it is HIGHER than what we already have -- never moves the counter
        /// backward. This is what makes returning-player progress self-healing (see
        /// C-1 in milestone #10's final whole-branch review): currentCycleNumber is
        /// pure in-memory state that resets to 0 on every relaunch, but the player's
        /// kingdom/decisions persist server-side, so without this seed a returning
        /// player's first submission after relaunch collides with a cycle_number
        /// they already used, and the server silently drops it
        /// (server/src/routes/decisions.ts's onConflictDoNothing). If a local
        /// decision has already advanced the counter past this seed before it
        /// resolves (e.g. an async fetch racing a player's fast first submission),
        /// overwriting unconditionally would move the counter BACKWARD and cause a
        /// FUTURE local decision to collide with one that already landed -- so this
        /// is a "raise the floor," never a "set the value," operation.
        /// </summary>
        public void SeedCycleNumberIfHigher(int cycleNumber)
        {
            if (cycleNumber > currentCycleNumber)
            {
                currentCycleNumber = cycleNumber;
            }
        }

        /// <summary>
        /// Submits a resource-allocation recommendation and resolves the cycle
        /// immediately. `roll` is caller-supplied (not read from UnityEngine.Random
        /// internally) so this method is testable without Play Mode; the real UI
        /// call site passes UnityEngine.Random.value.
        /// </summary>
        public string SubmitRecommendation(ResourceAllocation recommendation, double roll)
        {
            currentCycleNumber++;

            OverrideResult result = OverrideEvaluator.Evaluate(Ruler.State, recommendation, roll);
            Ruler.State.ApplyDelta(result.MoodDelta, result.LoyaltyDelta);
            SaveService.Save(Ruler.State);

            OnDecisionRecorded?.Invoke(new DecisionRecord(
                currentCycleNumber, recommendation, result.Overridden, Ruler.State.Mood, Ruler.State.Loyalty));

            string templateTag = result.Overridden ? "ruler_override" : "ruler_accept";
            var variables = new Dictionary<string, string>
            {
                { "mood", Ruler.State.Mood.ToString() },
                { "loyalty", Ruler.State.Loyalty.ToString() }
            };

            return DialogueTemplateEngine.Resolve(templateTag, variables);
        }
    }
}
