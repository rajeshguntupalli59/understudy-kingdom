using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using TMPro;
using UnderstudyKingdom.Core;
using UnderstudyKingdom.Npc;
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

        /// <summary>
        /// Regression guard for the ruler-portrait final-review finding: a
        /// fully-non-null-array-but-all-elements-null rulerPortraits state (e.g.
        /// every LoadPortraitSprite call silently failing) doesn't throw, so
        /// none of the C-1 NRE-based guard tests above can detect it. This test
        /// loads the real scene, clicks the real Submit button, and asserts the
        /// portrait Image actually ends up with a non-null sprite -- the one
        /// place a well-formed-but-empty portraits array would be caught.
        /// </summary>
        [UnityTest]
        public IEnumerator LoadedCoreLoopScene_Portrait_HasNonNullSpriteAfterSubmit()
        {
            yield return SceneManager.LoadSceneAsync("CoreLoop");
            yield return null;

            var canvas = Object.FindFirstObjectByType<Canvas>();
            Assert.IsNotNull(canvas, "Canvas not found in the loaded CoreLoop scene.");

            Button submitButton = FindButton(canvas, "SubmitButton");
            Assert.IsNotNull(submitButton, "SubmitButton not found in the loaded CoreLoop scene.");

            GameObject portraitObject = FindChildByName(canvas.transform, "RulerPortraitImage");
            Assert.IsNotNull(portraitObject, "RulerPortraitImage not found in the loaded CoreLoop scene.");
            Image portraitImage = portraitObject.GetComponent<Image>();
            Assert.IsNotNull(portraitImage, "RulerPortraitImage has no Image component.");

            submitButton.onClick.Invoke();

            Assert.IsNotNull(portraitImage.sprite,
                "Expected the ruler portrait to have a non-null sprite after Submit.");
        }

        /// <summary>
        /// Regression guard mirroring
        /// LoadedCoreLoopScene_Portrait_HasNonNullSpriteAfterSubmit above: a
        /// fully-non-null-array-but-all-elements-null backgroundSprites state
        /// wouldn't throw on its own, so this loads the real scene and
        /// asserts the scene background actually has a sprite.
        /// CosmeticsPanelController.ApplyTheme() runs from Bind() on scene
        /// load, so no click is needed to observe it.
        /// </summary>
        [UnityTest]
        public IEnumerator LoadedCoreLoopScene_SceneBackground_HasNonNullSpriteOnLoad()
        {
            yield return SceneManager.LoadSceneAsync("CoreLoop");
            yield return null;

            var canvas = Object.FindFirstObjectByType<Canvas>();
            Assert.IsNotNull(canvas, "Canvas not found in the loaded CoreLoop scene.");

            GameObject backgroundObject = FindChildByName(canvas.transform, "SceneBackground");
            Assert.IsNotNull(backgroundObject, "SceneBackground not found in the loaded CoreLoop scene.");
            Image backgroundImage = backgroundObject.GetComponent<Image>();
            Assert.IsNotNull(backgroundImage, "SceneBackground has no Image component.");

            Assert.IsNotNull(backgroundImage.sprite,
                "Expected the scene background to have a non-null sprite after scene load.");
        }

        /// <summary>
        /// Regression guard mirroring the scene-background/portrait non-null-sprite
        /// checks above, extended to the intentionally-missing event_event.png
        /// slot: this pre-selects the Event theme via a seeded save file (the same
        /// save-then-reload shape a real relaunch takes -- see
        /// DecisionCycleManager.LoadPersistedStateIfPresent, called from Awake())
        /// so the scene loads with eventPanelSprites[2] == null, exercising
        /// GetBackgroundSprite's fallback-to-Default path for the Events panel
        /// specifically. History and Council both have real Event-theme art and
        /// are asserted the same way as a sanity check, but the Events panel's
        /// non-null sprite here is proof the fallback works, not proof of a
        /// populated slot -- event_event.png is deliberately not committed (see
        /// docs/superpowers/specs/2026-09-06-panel-art-design.md).
        /// </summary>
        [UnityTest]
        public IEnumerator LoadedCoreLoopScene_PanelArt_HasNonNullSpritesUnderEventTheme()
        {
            var seedState = new RulerState { SelectedTheme = "Event" };
            SaveService.Save(seedState);

            yield return SceneManager.LoadSceneAsync("CoreLoop");
            yield return null;

            var canvas = Object.FindFirstObjectByType<Canvas>();
            Assert.IsNotNull(canvas, "Canvas not found in the loaded CoreLoop scene.");

            GameObject historyPanel = FindChildByName(canvas.transform, "HistoryPanel");
            GameObject councilPanel = FindChildByName(canvas.transform, "CouncilPanel");
            GameObject eventPanel = FindChildByName(canvas.transform, "EventPanel");
            Assert.IsNotNull(historyPanel, "HistoryPanel not found in the loaded CoreLoop scene.");
            Assert.IsNotNull(councilPanel, "CouncilPanel not found in the loaded CoreLoop scene.");
            Assert.IsNotNull(eventPanel, "EventPanel not found in the loaded CoreLoop scene.");

            GameObject historyArt = FindChildByName(historyPanel.transform, "PanelArt");
            GameObject councilArt = FindChildByName(councilPanel.transform, "PanelArt");
            GameObject eventArt = FindChildByName(eventPanel.transform, "PanelArt");

            Image historyArtImage = historyArt.GetComponent<Image>();
            Image councilArtImage = councilArt.GetComponent<Image>();
            Image eventArtImage = eventArt.GetComponent<Image>();

            Assert.AreEqual("history_event_0", historyArtImage.sprite.name,
                "Expected HistoryPanel's art layer to show the real Event-theme art.");
            Assert.AreEqual("council_event_0", councilArtImage.sprite.name,
                "Expected CouncilPanel's art layer to show the real Event-theme art.");
            Assert.AreEqual("event_default_0", eventArtImage.sprite.name,
                "Expected EventPanel's art layer to show event_default via the fallback -- event_event.png is deliberately missing.");
        }

        [UnityTest]
        public IEnumerator LoadedCoreLoopScene_EventsButton_OpensPanelWithoutThrowing()
        {
            yield return SceneManager.LoadSceneAsync("CoreLoop");
            yield return null;

            var canvas = Object.FindFirstObjectByType<Canvas>();
            Assert.IsNotNull(canvas, "Canvas not found in the loaded CoreLoop scene.");

            Button eventsButton = FindButton(canvas, "EventsButton");
            Assert.IsNotNull(eventsButton, "EventsButton not found in the loaded CoreLoop scene.");

            GameObject eventPanel = FindChildByName(canvas.transform, "EventPanel");
            Assert.IsNotNull(eventPanel, "EventPanel not found in the loaded CoreLoop scene.");
            Assert.IsFalse(eventPanel.activeSelf, "Expected EventPanel to start inactive.");

            eventsButton.onClick.Invoke();

            Assert.IsTrue(eventPanel.activeSelf,
                "Expected EventPanel to become active after EventsButton is clicked.");
        }

        [UnityTest]
        public IEnumerator LoadedCoreLoopScene_CustomizeButton_OpensPanelWithoutThrowing()
        {
            yield return SceneManager.LoadSceneAsync("CoreLoop");
            yield return null;

            var canvas = Object.FindFirstObjectByType<Canvas>();
            Assert.IsNotNull(canvas, "Canvas not found in the loaded CoreLoop scene.");

            Button customizeButton = FindButton(canvas, "CustomizeButton");
            Assert.IsNotNull(customizeButton, "CustomizeButton not found in the loaded CoreLoop scene.");

            GameObject cosmeticsPanel = FindChildByName(canvas.transform, "CosmeticsPanel");
            Assert.IsNotNull(cosmeticsPanel, "CosmeticsPanel not found in the loaded CoreLoop scene.");
            Assert.IsFalse(cosmeticsPanel.activeSelf, "Expected CosmeticsPanel to start inactive.");

            customizeButton.onClick.Invoke();

            Assert.IsTrue(cosmeticsPanel.activeSelf,
                "Expected CosmeticsPanel to become active after CustomizeButton is clicked.");
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

        /// <summary>
        /// Regression guard for the final-review C-1 finding: DuelModalGate was a
        /// plain C# class, not a MonoBehaviour, so Unity's serializer silently
        /// dropped [SerializeField] private DuelModalGate gate on
        /// DuelButtonController/HistoryPanelController/CouncilPanelController --
        /// it deserialized as null in the real committed scene even though
        /// CoreLoopSceneBuilder.Build() wired it correctly at editor time. Every
        /// other test in this project calls Initialize(...) directly, bypassing
        /// scene deserialization entirely, so none of them could ever catch this.
        /// This test is the one place that loads the real Assets/Scenes/CoreLoop.unity
        /// and clicks the real button, so if gate were still null this click would
        /// throw a NullReferenceException on gate.IsDuelInFlight = true and fail
        /// the test -- no explicit exception assertion needed, an uncaught
        /// exception during a UnityTest fails it.
        /// </summary>
        [UnityTest]
        public IEnumerator LoadedCoreLoopScene_ChallengeButton_DoesNotThrow()
        {
            yield return SceneManager.LoadSceneAsync("CoreLoop");
            yield return null;

            var canvas = Object.FindFirstObjectByType<Canvas>();
            Assert.IsNotNull(canvas, "Canvas not found in the loaded CoreLoop scene.");

            Button challengeButton = FindButton(canvas, "ChallengeButton");
            Assert.IsNotNull(challengeButton, "ChallengeButton not found in the loaded CoreLoop scene.");

            challengeButton.onClick.Invoke();
        }

        /// <summary>
        /// Same C-1 regression guard as LoadedCoreLoopScene_ChallengeButton_DoesNotThrow,
        /// for HistoryPanelController's shared gate reference. OnViewHistory sets
        /// gate.IsModalOpen = true before panelRoot.SetActive(true) runs, so a null
        /// gate would throw before the panel ever became visible -- successfully
        /// seeing HistoryPanel become active is itself proof the shared gate
        /// reference survived scene deserialization.
        /// </summary>
        [UnityTest]
        public IEnumerator LoadedCoreLoopScene_ViewHistoryButton_OpensPanelWithoutThrowing()
        {
            yield return SceneManager.LoadSceneAsync("CoreLoop");
            yield return null;

            var canvas = Object.FindFirstObjectByType<Canvas>();
            Assert.IsNotNull(canvas, "Canvas not found in the loaded CoreLoop scene.");

            Button viewHistoryButton = FindButton(canvas, "ViewHistoryButton");
            Assert.IsNotNull(viewHistoryButton, "ViewHistoryButton not found in the loaded CoreLoop scene.");

            GameObject historyPanel = FindChildByName(canvas.transform, "HistoryPanel");
            Assert.IsNotNull(historyPanel, "HistoryPanel not found in the loaded CoreLoop scene.");
            Assert.IsFalse(historyPanel.activeSelf, "Expected HistoryPanel to start inactive.");

            viewHistoryButton.onClick.Invoke();

            Assert.IsTrue(historyPanel.activeSelf,
                "Expected HistoryPanel to become active after ViewHistoryButton is clicked.");
        }

        /// <summary>
        /// Same C-1 regression guard as the History/Challenge tests above, for
        /// CouncilPanelController's shared gate reference -- the third and
        /// last controller carrying a [SerializeField] DuelModalGate gate.
        /// OnCouncilButtonClicked sets gate.IsModalOpen = true before
        /// panelRoot.SetActive(true) runs, so a null gate would throw before
        /// the panel ever became visible, the same shape as View History's.
        /// </summary>
        [UnityTest]
        public IEnumerator LoadedCoreLoopScene_CouncilButton_OpensPanelWithoutThrowing()
        {
            yield return SceneManager.LoadSceneAsync("CoreLoop");
            yield return null;

            var canvas = Object.FindFirstObjectByType<Canvas>();
            Assert.IsNotNull(canvas, "Canvas not found in the loaded CoreLoop scene.");

            Button councilButton = FindButton(canvas, "CouncilButton");
            Assert.IsNotNull(councilButton, "CouncilButton not found in the loaded CoreLoop scene.");

            GameObject councilPanel = FindChildByName(canvas.transform, "CouncilPanel");
            Assert.IsNotNull(councilPanel, "CouncilPanel not found in the loaded CoreLoop scene.");
            Assert.IsFalse(councilPanel.activeSelf, "Expected CouncilPanel to start inactive.");

            councilButton.onClick.Invoke();

            Assert.IsTrue(councilPanel.activeSelf,
                "Expected CouncilPanel to become active after CouncilButton is clicked.");
        }

        [UnityTest]
        public IEnumerator LoadedCoreLoopScene_ButtonIcons_HaveNonNullSpritesOnLoad()
        {
            yield return SceneManager.LoadSceneAsync("CoreLoop");
            yield return null;

            var canvas = Object.FindFirstObjectByType<Canvas>();
            Assert.IsNotNull(canvas, "Canvas not found in the loaded CoreLoop scene.");

            GameObject submitButton = FindChildByName(canvas.transform, "SubmitButton");
            GameObject challengeButton = FindChildByName(canvas.transform, "ChallengeButton");
            GameObject viewHistoryButton = FindChildByName(canvas.transform, "ViewHistoryButton");
            GameObject councilButton = FindChildByName(canvas.transform, "CouncilButton");
            GameObject eventsButton = FindChildByName(canvas.transform, "EventsButton");
            GameObject customizeButton = FindChildByName(canvas.transform, "CustomizeButton");
            GameObject claimButton = FindChildByName(canvas.transform, "ClaimButton");

            GameObject submitIcon = FindChildByName(submitButton.transform, "Icon");
            GameObject challengeIcon = FindChildByName(challengeButton.transform, "Icon");
            GameObject viewHistoryIcon = FindChildByName(viewHistoryButton.transform, "Icon");
            GameObject councilIcon = FindChildByName(councilButton.transform, "Icon");
            GameObject eventsIcon = FindChildByName(eventsButton.transform, "Icon");
            GameObject customizeIcon = FindChildByName(customizeButton.transform, "Icon");
            GameObject claimIcon = FindChildByName(claimButton.transform, "Icon");

            Assert.IsNotNull(submitIcon.GetComponent<Image>().sprite, "Expected SubmitButton's Icon to have a non-null sprite.");
            Assert.IsNotNull(challengeIcon.GetComponent<Image>().sprite, "Expected ChallengeButton's Icon to have a non-null sprite.");
            Assert.IsNotNull(viewHistoryIcon.GetComponent<Image>().sprite, "Expected ViewHistoryButton's Icon to have a non-null sprite.");
            Assert.IsNotNull(councilIcon.GetComponent<Image>().sprite, "Expected CouncilButton's Icon to have a non-null sprite.");
            Assert.IsNotNull(eventsIcon.GetComponent<Image>().sprite, "Expected EventsButton's Icon to have a non-null sprite.");
            Assert.IsNotNull(customizeIcon.GetComponent<Image>().sprite, "Expected CustomizeButton's Icon to have a non-null sprite.");
            Assert.IsNotNull(claimIcon.GetComponent<Image>().sprite, "Expected ClaimButton's Icon to have a non-null sprite.");

            // Geometry guard flagged by milestone #15's final review as
            // untested: the 8px-inset, 32x32, left-center-anchored shape every
            // icon is supposed to share (CoreLoopSceneBuilder.cs) had no
            // automated check -- only the non-null-sprite assertions above did.
            foreach (GameObject icon in new[] { submitIcon, challengeIcon, viewHistoryIcon, councilIcon, eventsIcon, customizeIcon, claimIcon })
            {
                var rect = icon.GetComponent<RectTransform>();
                Assert.AreEqual(new Vector2(0f, 0.5f), rect.anchorMin, $"{icon.transform.parent.name}'s Icon has an unexpected anchorMin.");
                Assert.AreEqual(new Vector2(0f, 0.5f), rect.anchorMax, $"{icon.transform.parent.name}'s Icon has an unexpected anchorMax.");
                Assert.AreEqual(new Vector2(0f, 0.5f), rect.pivot, $"{icon.transform.parent.name}'s Icon has an unexpected pivot.");
                Assert.AreEqual(new Vector2(8f, 0f), rect.anchoredPosition, $"{icon.transform.parent.name}'s Icon has an unexpected anchoredPosition.");
                Assert.AreEqual(new Vector2(32f, 32f), rect.sizeDelta, $"{icon.transform.parent.name}'s Icon has an unexpected sizeDelta.");
            }
        }

        private static Button FindButton(Canvas canvas, string name)
        {
            foreach (Button candidate in canvas.GetComponentsInChildren<Button>(true))
            {
                if (candidate.gameObject.name == name)
                {
                    return candidate;
                }
            }

            Assert.Fail($"No Button named '{name}' found under the Canvas.");
            return null;
        }

        private static GameObject FindChildByName(Transform parent, string name)
        {
            foreach (Transform child in parent.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == name)
                {
                    return child.gameObject;
                }
            }

            Assert.Fail($"No child named '{name}' found under {parent.name}.");
            return null;
        }
    }
}
