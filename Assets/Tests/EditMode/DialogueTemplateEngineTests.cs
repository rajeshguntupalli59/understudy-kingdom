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
    }
}
