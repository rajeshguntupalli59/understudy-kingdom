using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using TMPro;
using UnderstudyKingdom.Backend;
using UnderstudyKingdom.Core;
using UnderstudyKingdom.Npc;
using UnderstudyKingdom.UI;

namespace UnderstudyKingdom.Tests
{
    /// <summary>
    /// Real end-to-end: real Supabase sign-in, real local server/, 3 real
    /// decisions posted via BackendApiClient directly (mirroring
    /// CouncilPanelControllerRealDataTests' precedent of posting decisions
    /// directly rather than through slider/Submit UI, which this project
    /// has no existing automated-testing precedent for) -- only eventsButton
    /// and claimButton are actually clicked.
    /// </summary>
    public class EventPanelControllerRealDataTests
    {
        private GameObject rulerObject;
        private GameObject managerObject;
        private GameObject coordinatorObject;
        private GameObject screenControllerObject;
        private GameObject controllerObject;
        private GameObject canvasObject;
        private GameObject panelRootObject;
        private GameObject directApiClientObject;
        private RulerNpcController ruler;
        private Button eventsButton;
        private Button claimButton;
        private TextMeshProUGUI progressLabel;
        private TextMeshProUGUI statusMessageText;

        [UnitySetUp]
        public IEnumerator UnitySetUp()
        {
            rulerObject = new GameObject("Ruler");
            ruler = rulerObject.AddComponent<RulerNpcController>();

            managerObject = new GameObject("Manager");
            var manager = managerObject.AddComponent<DecisionCycleManager>();
            manager.Ruler = ruler;

            coordinatorObject = new GameObject("Coordinator");
            var coordinator = coordinatorObject.AddComponent<BackendSyncCoordinator>();
            coordinator.SupabaseUrl = "https://kszwkvxtnzbbndclpbbe.supabase.co";
            coordinator.SupabaseAnonKey = "sb_publishable_R277yUhT4qK5yTdZwamiuQ_3MD-gdvw";
            coordinator.BackendBaseUrl = "http://localhost:3000";
            coordinator.DecisionCycleManager = manager;

            yield return new WaitForSeconds(2f);

            SessionData session = SessionStore.Load();
            Assert.IsNotNull(session, "Coordinator did not persist a session during bootstrap");

            directApiClientObject = new GameObject("DirectApiClient");
            var directApiClient = directApiClientObject.AddComponent<BackendApiClient>();
            directApiClient.BackendBaseUrl = "http://localhost:3000";

            // Every hardcoded event this milestone defines has
            // objectiveDecisionCount = 3 -- see
            // server/src/game/liveOpsEvents.ts -- so 3 real decisions always
            // clears the objective regardless of which event is currently
            // active.
            for (int cycle = 1; cycle <= 3; cycle++)
            {
                var dto = new DecisionSyncRequest
                {
                    cycle_number = cycle,
                    player_recommendation = new PlayerRecommendationDto { army = 40, trade = 30, religion = 30 },
                    ruler_outcome = new RulerOutcomeDto { mood = 55, loyalty = 60 },
                    overridden = false
                };
                bool posted = false;
                directApiClient.PostDecision(session.AccessToken, dto, _ => posted = true, err => Assert.Fail($"PostDecision failed: {err}"));
                yield return new WaitUntil(() => posted);
            }

            canvasObject = new GameObject("Canvas", typeof(Canvas));

            var armySlider = CreateSlider("ArmySlider", 40);
            var tradeSlider = CreateSlider("TradeSlider", 30);
            var religionSlider = CreateSlider("ReligionSlider", 30);

            var moodLabel = CreateLabel("MoodLabel");
            var loyaltyLabel = CreateLabel("LoyaltyLabel");
            var agendaLabel = CreateLabel("AgendaLabel");
            var narrationText = CreateLabel("NarrationText");

            var submitButtonObject = new GameObject("SubmitButton", typeof(Image), typeof(Button));
            submitButtonObject.transform.SetParent(canvasObject.transform, false);
            var submitButton = submitButtonObject.GetComponent<Button>();

            screenControllerObject = new GameObject("ScreenController");
            var screenController = screenControllerObject.AddComponent<CoreLoopScreenController>();
            screenController.Initialize(manager, armySlider, tradeSlider, religionSlider,
                moodLabel, loyaltyLabel, agendaLabel, narrationText, submitButton);

            var challengeButtonObject = new GameObject("ChallengeButton", typeof(Image), typeof(Button));
            challengeButtonObject.transform.SetParent(canvasObject.transform, false);
            var challengeButton = challengeButtonObject.GetComponent<Button>();

            var viewHistoryButtonObject = new GameObject("ViewHistoryButton", typeof(Image), typeof(Button));
            viewHistoryButtonObject.transform.SetParent(canvasObject.transform, false);
            var viewHistoryButton = viewHistoryButtonObject.GetComponent<Button>();

            var councilButtonObject = new GameObject("CouncilButton", typeof(Image), typeof(Button));
            councilButtonObject.transform.SetParent(canvasObject.transform, false);
            var councilButton = councilButtonObject.GetComponent<Button>();

            var customizeButtonObject = new GameObject("CustomizeButton", typeof(Image), typeof(Button));
            customizeButtonObject.transform.SetParent(canvasObject.transform, false);
            var customizeButton = customizeButtonObject.GetComponent<Button>();

            var eventsButtonObject = new GameObject("EventsButton", typeof(Image), typeof(Button));
            eventsButtonObject.transform.SetParent(canvasObject.transform, false);
            eventsButton = eventsButtonObject.GetComponent<Button>();

            panelRootObject = new GameObject("EventPanel");
            panelRootObject.transform.SetParent(canvasObject.transform, false);

            var closeButtonObject = new GameObject("CloseButton", typeof(Image), typeof(Button));
            closeButtonObject.transform.SetParent(panelRootObject.transform, false);
            var closeButton = closeButtonObject.GetComponent<Button>();

            var nameLabel = CreateLabel("NameLabel", panelRootObject.transform);
            var narrationLabel = CreateLabel("NarrationLabel", panelRootObject.transform);
            progressLabel = CreateLabel("ProgressLabel", panelRootObject.transform);
            statusMessageText = CreateLabel("StatusMessageText", panelRootObject.transform);

            var claimButtonObject = new GameObject("ClaimButton", typeof(Image), typeof(Button));
            claimButtonObject.transform.SetParent(panelRootObject.transform, false);
            claimButton = claimButtonObject.GetComponent<Button>();

            controllerObject = new GameObject("Controller");
            var controller = controllerObject.AddComponent<EventPanelController>();
            controller.Initialize(eventsButton, panelRootObject, closeButton, nameLabel, narrationLabel,
                progressLabel, statusMessageText, claimButton, coordinator, manager, screenController,
                armySlider, tradeSlider, religionSlider, submitButton, challengeButton, viewHistoryButton, councilButton, customizeButton);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(controllerObject);
            Object.DestroyImmediate(screenControllerObject);
            Object.DestroyImmediate(canvasObject);
            Object.DestroyImmediate(coordinatorObject);
            Object.DestroyImmediate(managerObject);
            Object.DestroyImmediate(rulerObject);
            Object.DestroyImmediate(directApiClientObject);

            if (File.Exists(SaveService.SavePath))
            {
                File.Delete(SaveService.SavePath);
            }
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

        private TextMeshProUGUI CreateLabel(string name, Transform parent = null)
        {
            var labelObject = new GameObject(name, typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(parent != null ? parent : canvasObject.transform, false);
            return labelObject.GetComponent<TextMeshProUGUI>();
        }

        [UnityTest]
        public IEnumerator EventsButton_AfterThreeRealDecisions_ShowsObjectiveMetAndClaimApplies()
        {
            eventsButton.onClick.Invoke();

            yield return new WaitUntil(() => !string.IsNullOrEmpty(progressLabel.text));

            // Every hardcoded event has objectiveDecisionCount = 3, and 3
            // real decisions were posted in UnitySetUp above, so the
            // objective is exactly met.
            Assert.IsTrue(claimButton.interactable, $"Expected Claim to be interactable with progress '{progressLabel.text}'");

            int moodBefore = ruler.State.Mood;
            int loyaltyBefore = ruler.State.Loyalty;

            claimButton.onClick.Invoke();

            Assert.AreEqual(moodBefore + 15, ruler.State.Mood);
            Assert.AreEqual(loyaltyBefore + 15, ruler.State.Loyalty);
            Assert.IsFalse(string.IsNullOrEmpty(ruler.State.ClaimedEventWeekId));
            Assert.IsFalse(claimButton.interactable);

            RulerState persisted = SaveService.Load();
            Assert.AreEqual(ruler.State.ClaimedEventWeekId, persisted.ClaimedEventWeekId);
            Assert.AreEqual(moodBefore + 15, persisted.Mood);
            Assert.AreEqual(loyaltyBefore + 15, persisted.Loyalty);
        }
    }
}
