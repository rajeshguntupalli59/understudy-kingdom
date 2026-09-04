using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using TMPro;
using UnderstudyKingdom.Backend;
using UnderstudyKingdom.UI;

namespace UnderstudyKingdom.Tests
{
    /// <summary>
    /// Real end-to-end: real Supabase sign-in, real local server/, a real submitted
    /// decision, real history fetch, asserts on the actual rendered row text.
    /// </summary>
    public class HistoryPanelControllerRealDataTests
    {
        private GameObject coordinatorObject;
        private GameObject controllerObject;
        private GameObject canvasObject;
        private GameObject panelRootObject;
        private GameObject directApiClientObject;
        private Slider armySlider;
        private Slider tradeSlider;
        private Slider religionSlider;
        private Button submitButton;
        private Button challengeButton;
        private Button councilButton;
        private Button viewHistoryButton;
        private Button closeButton;
        private TextMeshProUGUI[] rowTexts;

        [UnitySetUp]
        public IEnumerator UnitySetUp()
        {
            coordinatorObject = new GameObject("Coordinator");
            var coordinator = coordinatorObject.AddComponent<BackendSyncCoordinator>();
            coordinator.SupabaseUrl = "https://kszwkvxtnzbbndclpbbe.supabase.co";
            coordinator.SupabaseAnonKey = "sb_publishable_R277yUhT4qK5yTdZwamiuQ_3MD-gdvw";
            coordinator.BackendBaseUrl = "http://localhost:3000";

            // Wait for the coordinator's own session bootstrap + EnsureKingdom.
            yield return new WaitForSeconds(2f);

            // The coordinator doesn't expose its session, so read back the file it
            // just persisted via SessionStore.Save, and use a separate BackendApiClient
            // to submit one real decision directly -- giving this panel real history
            // to render without needing BackendSyncCoordinator to expose internals it
            // has no other reason to expose.
            SessionData session = SessionStore.Load();
            Assert.IsNotNull(session, "Coordinator did not persist a session during bootstrap");

            directApiClientObject = new GameObject("DirectApiClient");
            var directApiClient = directApiClientObject.AddComponent<BackendApiClient>();
            directApiClient.BackendBaseUrl = "http://localhost:3000";

            var dto = new DecisionSyncRequest
            {
                cycle_number = 1,
                player_recommendation = new PlayerRecommendationDto { army = 40, trade = 30, religion = 30 },
                ruler_outcome = new RulerOutcomeDto { mood = 55, loyalty = 60 },
                overridden = false
            };
            bool posted = false;
            directApiClient.PostDecision(session.AccessToken, dto, _ => posted = true, err => Assert.Fail($"PostDecision failed: {err}"));
            yield return new WaitUntil(() => posted);

            canvasObject = new GameObject("Canvas", typeof(Canvas));

            armySlider = CreateSlider("ArmySlider", 40);
            tradeSlider = CreateSlider("TradeSlider", 30);
            religionSlider = CreateSlider("ReligionSlider", 30);

            var submitButtonObject = new GameObject("SubmitButton", typeof(Image), typeof(Button));
            submitButtonObject.transform.SetParent(canvasObject.transform, false);
            submitButton = submitButtonObject.GetComponent<Button>();

            var challengeButtonObject = new GameObject("ChallengeButton", typeof(Image), typeof(Button));
            challengeButtonObject.transform.SetParent(canvasObject.transform, false);
            challengeButton = challengeButtonObject.GetComponent<Button>();

            var viewHistoryButtonObject = new GameObject("ViewHistoryButton", typeof(Image), typeof(Button));
            viewHistoryButtonObject.transform.SetParent(canvasObject.transform, false);
            viewHistoryButton = viewHistoryButtonObject.GetComponent<Button>();

            var councilButtonObject = new GameObject("CouncilButton", typeof(Image), typeof(Button));
            councilButtonObject.transform.SetParent(canvasObject.transform, false);
            councilButton = councilButtonObject.GetComponent<Button>();

            var eventsButtonObject = new GameObject("EventsButton", typeof(Image), typeof(Button));
            eventsButtonObject.transform.SetParent(canvasObject.transform, false);
            var eventsButton = eventsButtonObject.GetComponent<Button>();

            panelRootObject = new GameObject("PanelRoot");
            panelRootObject.transform.SetParent(canvasObject.transform, false);

            var closeButtonObject = new GameObject("CloseButton", typeof(Image), typeof(Button));
            closeButtonObject.transform.SetParent(panelRootObject.transform, false);
            closeButton = closeButtonObject.GetComponent<Button>();

            rowTexts = new TextMeshProUGUI[10];
            for (int i = 0; i < rowTexts.Length; i++)
            {
                var rowObject = new GameObject($"Row{i}", typeof(TextMeshProUGUI));
                rowObject.transform.SetParent(panelRootObject.transform, false);
                rowTexts[i] = rowObject.GetComponent<TextMeshProUGUI>();
            }

            controllerObject = new GameObject("Controller");
            var controller = controllerObject.AddComponent<HistoryPanelController>();
            controller.Initialize(viewHistoryButton, panelRootObject, closeButton, rowTexts, coordinator,
                armySlider, tradeSlider, religionSlider, submitButton, challengeButton, councilButton, eventsButton);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(controllerObject);
            Object.DestroyImmediate(canvasObject);
            Object.DestroyImmediate(coordinatorObject);
            Object.DestroyImmediate(directApiClientObject);
            SessionStore.Clear();
        }

        private Slider CreateSlider(string name, float initialValue)
        {
            var sliderObject = new GameObject(name, typeof(Slider));
            sliderObject.transform.SetParent(canvasObject.transform, false);
            var slider = sliderObject.GetComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 100f;
            slider.wholeNumbers = true;
            slider.SetValueWithoutNotify(initialValue);
            return slider;
        }

        [UnityTest]
        public IEnumerator ViewHistory_WithRealSubmittedDecision_RendersRealRowText()
        {
            viewHistoryButton.onClick.Invoke();

            // OnViewHistory now sets rowTexts[0] to "Loading..." synchronously
            // (M-3 fix) before the real network round-trip resolves, so waiting on
            // "non-empty" alone would trip on that placeholder -- wait for it to be
            // replaced by the real result instead.
            yield return new WaitUntil(() => !string.IsNullOrEmpty(rowTexts[0].text) && rowTexts[0].text != "Loading...");

            Assert.AreEqual(
                "Cycle 1: Army 40 / Trade 30 / Religion 30 -> Accepted (Mood 55, Loyalty 60)",
                rowTexts[0].text);
            Assert.IsFalse(rowTexts[1].gameObject.activeSelf);
        }
    }
}
