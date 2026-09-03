using System.Collections.Generic;
using NUnit.Framework;
using UnderstudyKingdom.Npc;

namespace UnderstudyKingdom.Tests
{
    public class DialogueTemplateEngineTests
    {
        [Test]
        public void Resolve_AcceptTemplate_SubstitutesVariables()
        {
            var variables = new Dictionary<string, string> { { "mood", "60" }, { "loyalty", "55" } };

            string result = DialogueTemplateEngine.Resolve("ruler_accept", variables);

            Assert.IsTrue(result.Contains("60"));
            Assert.IsTrue(result.Contains("55"));
        }

        [Test]
        public void Resolve_UnknownTag_ReturnsPlaceholderMarker()
        {
            string result = DialogueTemplateEngine.Resolve("not_a_real_tag", new Dictionary<string, string>());

            Assert.IsTrue(result.Contains("not_a_real_tag"));
        }

        [Test]
        public void Resolve_DuelWin_ReturnsExpectedNarration()
        {
            string result = DialogueTemplateEngine.Resolve("duel_win", new Dictionary<string, string>());

            Assert.AreEqual("Your strategy carried the day! A rival kingdom's ruler was won over.", result);
        }

        [Test]
        public void Resolve_DuelLose_ReturnsExpectedNarration()
        {
            string result = DialogueTemplateEngine.Resolve("duel_lose", new Dictionary<string, string>());

            Assert.AreEqual("A rival kingdom's ruler saw through your plan and refused it.", result);
        }
    }
}
