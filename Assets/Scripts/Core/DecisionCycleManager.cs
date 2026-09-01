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

        private int currentCycleNumber;

        private void Awake()
        {
            if (Ruler != null)
            {
                Ruler.State = SaveService.Load();
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
