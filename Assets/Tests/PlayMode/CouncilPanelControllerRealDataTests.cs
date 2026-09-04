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
    /// Real end-to-end: real Supabase sign-in, real local server/, a real
    /// council crossing its real milestone threshold. Council creation and
    /// decision submission happen directly through BackendSyncCoordinator/
    /// BackendApiClient (not through nameInputField/typed UI, which this
    /// project has no existing automated-testing precedent for -- see Task
    /// 8/9's scope note); only councilButton is actually clicked, exactly
    /// mirroring HistoryPanelControllerRealDataTests' own structure.
    /// </summary>
    public class CouncilPanelControllerRealDataTests
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
        private Button councilButton;
        private TextMeshProUGUI rewardStatusLabel;

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

            bool councilCreated = false;
            coordinator.RequestCreateCouncil("Grinders", _ => councilCreated = true, err => Assert.Fail($"RequestCreateCouncil failed: {err}"));
            yield return new WaitUntil(() => councilCreated);

            for (int cycle = 1; cycle <= 10; cycle++)
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

            var eventsButtonObject = new GameObject("EventsButton", typeof(Image), typeof(Button));
            eventsButtonObject.transform.SetParent(canvasObject.transform, false);
            var eventsButton = eventsButtonObject.GetComponent<Button>();

            var customizeButtonObject = new GameObject("CustomizeButton", typeof(Image), typeof(Button));
            customizeButtonObject.transform.SetParent(canvasObject.transform, false);
            var customizeButton = customizeButtonObject.GetComponent<Button>();

            var councilButtonObject = new GameObject("CouncilButton", typeof(Image), typeof(Button));
            councilButtonObject.transform.SetParent(canvasObject.transform, false);
            councilButton = councilButtonObject.GetComponent<Button>();

            panelRootObject = new GameObject("CouncilPanel");
            panelRootObject.transform.SetParent(canvasObject.transform, false);

            var closeButtonObject = new GameObject("CloseButton", typeof(Image), typeof(Button));
            closeButtonObject.transform.SetParent(panelRootObject.transform, false);
            var closeButton = closeButtonObject.GetComponent<Button>();

            var notInCouncilViewObject = new GameObject("NotInCouncilView");
            notInCouncilViewObject.transform.SetParent(panelRootObject.transform, false);

            var nameInputObject = new GameObject("NameInput", typeof(TMP_InputField));
            nameInputObject.transform.SetParent(notInCouncilViewObject.transform, false);
            var nameInputField = nameInputObject.GetComponent<TMP_InputField>();

            var createButtonObject = new GameObject("CreateButton", typeof(Image), typeof(Button));
            createButtonObject.transform.SetParent(notInCouncilViewObject.transform, false);
            var createButton = createButtonObject.GetComponent<Button>();

            var joinCodeInputObject = new GameObject("JoinCodeInput", typeof(TMP_InputField));
            joinCodeInputObject.transform.SetParent(notInCouncilViewObject.transform, false);
            var joinCodeInputField = joinCodeInputObject.GetComponent<TMP_InputField>();

            var joinButtonObject = new GameObject("JoinButton", typeof(Image), typeof(Button));
            joinButtonObject.transform.SetParent(notInCouncilViewObject.transform, false);
            var joinButton = joinButtonObject.GetComponent<Button>();

            var inCouncilViewObject = new GameObject("InCouncilView");
            inCouncilViewObject.transform.SetParent(panelRootObject.transform, false);

            var nameLabel = CreateLabel("NameLabel", inCouncilViewObject.transform);
            var joinCodeLabel = CreateLabel("JoinCodeLabel", inCouncilViewObject.transform);
            var memberCountLabel = CreateLabel("MemberCountLabel", inCouncilViewObject.transform);
            var progressLabel = CreateLabel("ProgressLabel", inCouncilViewObject.transform);
            rewardStatusLabel = CreateLabel("RewardStatusLabel", inCouncilViewObject.transform);
            var statusMessageText = CreateLabel("StatusMessageText", panelRootObject.transform);

            controllerObject = new GameObject("Controller");
            var controller = controllerObject.AddComponent<CouncilPanelController>();
            controller.Initialize(councilButton, panelRootObject, closeButton, notInCouncilViewObject, inCouncilViewObject,
                nameInputField, createButton, joinCodeInputField, joinButton, statusMessageText,
                nameLabel, joinCodeLabel, memberCountLabel, progressLabel, rewardStatusLabel,
                coordinator, manager, screenController,
                armySlider, tradeSlider, religionSlider, submitButton, challengeButton, viewHistoryButton, eventsButton, customizeButton);
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
        public IEnumerator CouncilButton_AfterRealThresholdCrossing_AppliesRewardAndPersists()
        {
            councilButton.onClick.Invoke();

            // rewardStatusLabel starts unset (null, not ""), and is only ever
            // written by HandleStatusResult once the real RequestCouncilStatus
            // round-trip completes -- waiting on "!= string.Empty" alone would
            // trip immediately on that initial null (null != "" is true),
            // asserting before the real network response ever arrives. Mirrors
            // HistoryPanelControllerRealDataTests' identical fix for
            // OnViewHistory's synchronous placeholder.
            yield return new WaitUntil(() => !string.IsNullOrEmpty(rewardStatusLabel.text));

            Assert.AreEqual(
                "Your council's shared effort has lifted your ruler's spirits! (+10 mood, +10 loyalty)",
                rewardStatusLabel.text);
            Assert.AreEqual(60, ruler.State.Mood);
            Assert.AreEqual(60, ruler.State.Loyalty);
            Assert.IsTrue(ruler.State.CouncilRewardApplied);

            RulerState persisted = SaveService.Load();
            Assert.IsTrue(persisted.CouncilRewardApplied);
            Assert.AreEqual(60, persisted.Mood);
            Assert.AreEqual(60, persisted.Loyalty);
        }
    }
}
