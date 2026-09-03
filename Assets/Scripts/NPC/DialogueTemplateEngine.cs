using System.Collections.Generic;

namespace UnderstudyKingdom.Npc
{
    /// <summary>
    /// Generates ruler dialogue from templated strings with variable slots keyed to
    /// current mood/history. Explicitly NOT backed by an on-device LLM -- see
    /// docs/NPC_PERFORMANCE_NOTES.md. See docs/PROJECT_PLAN.md FR-05.
    /// </summary>
    public static class DialogueTemplateEngine
    {
        private static readonly Dictionary<string, string> Templates = new Dictionary<string, string>
        {
            { "ruler_accept", "The ruler nods. \"A wise allocation.\" (mood {mood}, loyalty {loyalty})" },
            { "ruler_override", "The ruler waves a hand. \"I have other plans.\" (mood {mood}, loyalty {loyalty})" },
            { "duel_win", "Your strategy carried the day! A rival kingdom's ruler was won over." },
            { "duel_lose", "A rival kingdom's ruler saw through your plan and refused it." }
        };

        public static string Resolve(string templateTag, IReadOnlyDictionary<string, string> variables)
        {
            if (!Templates.TryGetValue(templateTag, out string template))
            {
                return $"[missing template: {templateTag}]";
            }

            string result = template;
            foreach (var pair in variables)
            {
                result = result.Replace("{" + pair.Key + "}", pair.Value);
            }
            return result;
        }
    }
}
