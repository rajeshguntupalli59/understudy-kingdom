using NUnit.Framework;
using UnderstudyKingdom.Backend;
using UnderstudyKingdom.UI;

namespace UnderstudyKingdom.Tests
{
    public class HistoryRowFormatterTests
    {
        [Test]
        public void Format_AcceptedDecision_ProducesExpectedLine()
        {
            var entry = new DecisionHistoryEntry
            {
                cycleNumber = 2,
                playerRecommendation = new PlayerRecommendationDto { army = 40, trade = 30, religion = 30 },
                rulerOutcome = new RulerOutcomeDto { mood = 55, loyalty = 60 },
                overridden = false
            };

            string result = HistoryRowFormatter.Format(entry);

            Assert.AreEqual("Cycle 2: Army 40 / Trade 30 / Religion 30 -> Accepted (Mood 55, Loyalty 60)", result);
        }

        [Test]
        public void Format_OverriddenDecision_ProducesExpectedLine()
        {
            var entry = new DecisionHistoryEntry
            {
                cycleNumber = 1,
                playerRecommendation = new PlayerRecommendationDto { army = 70, trade = 15, religion = 15 },
                rulerOutcome = new RulerOutcomeDto { mood = 40, loyalty = 45 },
                overridden = true
            };

            string result = HistoryRowFormatter.Format(entry);

            Assert.AreEqual("Cycle 1: Army 70 / Trade 15 / Religion 15 -> Overridden (Mood 40, Loyalty 45)", result);
        }
    }
}
