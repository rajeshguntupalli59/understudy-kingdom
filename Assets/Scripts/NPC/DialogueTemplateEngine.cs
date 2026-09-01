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
        /// <summary>
        /// TODO(FR-05): look up a template line for the given mood/context tag,
        /// substitute the provided variable slots, and return the resolved string.
        /// Templates should live as data (ScriptableObjects or JSON), not code.
        /// </summary>
        public static string Resolve(string templateTag, IReadOnlyDictionary<string, string> variables)
        {
            throw new System.NotImplementedException("FR-05: template resolution not yet implemented");
        }
    }
}
