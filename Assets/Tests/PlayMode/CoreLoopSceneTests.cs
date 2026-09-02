using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using TMPro;
using UnderstudyKingdom.Core;
using UnderstudyKingdom.UI;

namespace UnderstudyKingdom.Tests
{
    /// <summary>
    /// Covers the seam between CoreLoopScreenController (Task 2) and the
    /// scene CoreLoopSceneBuilder actually generates (Task 3): loads the real
    /// Assets/Scenes/CoreLoop.unity by name and exercises the real,
    /// scene-wired Submit button, rather than hand-built objects like
    /// CoreLoopScreenControllerTests does. This is the test most likely to
    /// catch a scene-authoring regression (e.g. missing wiring, or -- as the
    /// final review found -- TMP Essential Resources never having been
    /// imported) that unit-style tests against hand-built objects cannot see.
    /// </summary>
    public class CoreLoopSceneTests
    {
        [TearDown]
        public void TearDown()
        {
            if (File.Exists(SaveService.SavePath))
            {
                File.Delete(SaveService.SavePath);
            }
        }

        [UnityTest]
        public IEnumerator LoadedCoreLoopScene_SubmitButton_UpdatesNarrationAndStatusLabels()
        {
            yield return SceneManager.LoadSceneAsync("CoreLoop");
            yield return null;

            var controller = Object.FindFirstObjectByType<CoreLoopScreenController>();
            Assert.IsNotNull(controller, "CoreLoopScreenController not found in the loaded CoreLoop scene.");

            var manager = Object.FindFirstObjectByType<DecisionCycleManager>();
            Assert.IsNotNull(manager, "DecisionCycleManager not found in the loaded CoreLoop scene.");

            var canvas = Object.FindFirstObjectByType<Canvas>();
            Assert.IsNotNull(canvas, "Canvas not found in the loaded CoreLoop scene.");

            Button submitButton = null;
            foreach (Button candidate in Object.FindObjectsByType<Button>(FindObjectsSortMode.None))
            {
                if (candidate.gameObject.name == "SubmitButton")
                {
                    submitButton = candidate;
                    break;
                }
            }
            Assert.IsNotNull(submitButton, "SubmitButton not found in the loaded CoreLoop scene.");

            TextMeshProUGUI narrationText = FindLabel(canvas, "NarrationText");
            TextMeshProUGUI moodLabel = FindLabel(canvas, "MoodLabel");
            TextMeshProUGUI loyaltyLabel = FindLabel(canvas, "LoyaltyLabel");
            TextMeshProUGUI agendaLabel = FindLabel(canvas, "AgendaLabel");

            // Scene-authored initial value (see CoreLoopSceneBuilder.Build): empty
            // until a recommendation is submitted.
            Assert.IsTrue(string.IsNullOrEmpty(narrationText.text),
                "Expected narration text to still be empty before Submit is clicked.");

            submitButton.onClick.Invoke();

            Assert.IsFalse(string.IsNullOrEmpty(narrationText.text),
                "Expected narration text to change from empty after Submit is clicked.");
            Assert.AreEqual($"Mood: {manager.Ruler.State.Mood}", moodLabel.text);
            Assert.AreEqual($"Loyalty: {manager.Ruler.State.Loyalty}", loyaltyLabel.text);
            Assert.AreEqual($"Agenda: {manager.Ruler.State.Agenda}", agendaLabel.text);

            // Beyond the .text string property: force TMP to lay out the glyphs
            // and confirm it actually produced renderable character geometry.
            // If TMP Essential Resources were still missing (TMP_Settings.instance
            // == null), TextMeshProUGUI.Awake() would have no-op'd and this would
            // be 0 even though narrationText.text is non-empty.
            narrationText.ForceMeshUpdate();
            Assert.Greater(narrationText.textInfo.characterCount, 0,
                "Expected the narration label to have laid out renderable characters after ForceMeshUpdate.");
        }

        private static TextMeshProUGUI FindLabel(Canvas canvas, string name)
        {
            foreach (TextMeshProUGUI label in canvas.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                if (label.gameObject.name == name)
                {
                    return label;
                }
            }

            Assert.Fail($"No TextMeshProUGUI named '{name}' found under the Canvas.");
            return null;
        }
    }
}
