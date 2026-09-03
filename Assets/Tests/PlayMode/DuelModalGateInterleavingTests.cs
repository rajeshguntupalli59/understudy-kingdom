using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnderstudyKingdom.Backend;
using UnderstudyKingdom.UI;

namespace UnderstudyKingdom.Tests
{
    /// <summary>
    /// Proves the interleaving DuelModalGate exists to fix -- neither
    /// DuelButtonController's nor HistoryPanelController's own isolated
    /// tests exercise both real controllers sharing one real gate instance
    /// at once. See docs/superpowers/specs/2026-09-03-duel-modal-gate-design.md.
    /// </summary>
    public class DuelModalGateInterleavingTests
    {
        private GameObject coordinatorObject;
        private GameObject duelControllerObject;
        private GameObject historyControllerObject;
        private GameObject canvasObject;
        private GameObject panelRootObject;
        private Slider armySlider;
        private Slider tradeSlider;
        private Slider religionSlider;
        private Button submitButton;
        private Button challengeButton;
        private Button councilButton;
        private Button viewHistoryButton;
        private Button closeButton;
        private TextMeshProUGUI resultText;
        private TextMeshProUGUI[] rowTexts;
        private DuelModalGate gate;

        [SetUp]
        public void SetUp()
        {
            // Built inactive so Start() never runs on the coordinator --
            // currentSession stays null, giving both RequestDuel's and
            // RequestHistory's synchronous no-session error paths with zero
            // network dependency. This test only cares about local
            // interactable-flag bookkeeping, not real duel/history results.
            coordinatorObject = new GameObject("Coordinator");
            coordinatorObject.SetActive(false);
            var coordinator = coordinatorObject.AddComponent<BackendSyncCoordinator>();

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

            var councilButtonObject = new GameObject("CouncilButton", typeof(Image), typeof(Button));
            councilButtonObject.transform.SetParent(canvasObject.transform, false);
            councilButton = councilButtonObject.GetComponent<Button>();

            var viewHistoryButtonObject = new GameObject("ViewHistoryButton", typeof(Image), typeof(Button));
            viewHistoryButtonObject.transform.SetParent(canvasObject.transform, false);
            viewHistoryButton = viewHistoryButtonObject.GetComponent<Button>();

            var resultObject = new GameObject("ResultText", typeof(TextMeshProUGUI));
            resultObject.transform.SetParent(canvasObject.transform, false);
            resultText = resultObject.GetComponent<TextMeshProUGUI>();

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

            gate = new DuelModalGate();

            duelControllerObject = new GameObject("DuelController");
            var duelController = duelControllerObject.AddComponent<DuelButtonController>();
            duelController.Initialize(armySlider, tradeSlider, religionSlider, challengeButton, resultText, coordinator, gate);

            historyControllerObject = new GameObject("HistoryController");
            var historyController = historyControllerObject.AddComponent<HistoryPanelController>();
            historyController.Initialize(viewHistoryButton, panelRootObject, closeButton, rowTexts, coordinator,
                armySlider, tradeSlider, religionSlider, submitButton, challengeButton, councilButton, gate);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(duelControllerObject);
            Object.DestroyImmediate(historyControllerObject);
            Object.DestroyImmediate(canvasObject);
            Object.DestroyImmediate(coordinatorObject);
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

        [Test]
        public void DuelInFlightThenModalOpensAndCloses_ChallengeOnlyReenablesWhenDuelActuallyResolves()
        {
            var duelController = duelControllerObject.GetComponent<DuelButtonController>();

            // A real click can't hold a duel "in flight" in this zero-network
            // harness: RequestDuel's no-session path (currentSession stays null,
            // exactly as in DuelButtonControllerTests) resolves synchronously, so
            // OnChallenge's disable-and-set-flag would already be undone by the
            // time onClick.Invoke() returns control here -- that exact collapse is
            // what DuelButtonControllerTests.Challenge_WithNoSessionYet_
            // ShowsErrorAndReEnablesButton already proves. What THIS test proves
            // is the composition across the shared gate while a duel genuinely is
            // in flight, so reproduce OnChallenge's own two synchronous side
            // effects directly, holding the in-flight window open the way a real
            // (async) network round trip would.
            challengeButton.interactable = false;
            gate.IsDuelInFlight = true;
            Assert.IsFalse(challengeButton.interactable);
            Assert.IsTrue(gate.IsDuelInFlight);

            // Real HistoryPanelController opens while the duel is still in flight.
            viewHistoryButton.onClick.Invoke();
            Assert.IsTrue(gate.IsModalOpen);
            Assert.IsFalse(challengeButton.interactable, "modal open must keep Challenge disabled");

            // Real HandleError fires (simulating the duel resolving) while
            // the modal is still open -- Challenge must stay disabled since
            // the modal owns it right now. HandleError is private; invoke it
            // via reflection, the same established technique used elsewhere
            // in this project's PlayMode tests for internal state.
            MethodInfo handleError = typeof(DuelButtonController).GetMethod("HandleError", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(handleError, "HandleError method not found -- DuelButtonController internals changed");
            handleError.Invoke(duelController, new object[] { "simulated failure" });

            Assert.IsFalse(gate.IsDuelInFlight);
            Assert.IsFalse(challengeButton.interactable,
                "the duel resolving while a modal is open must NOT re-enable Challenge underneath it");

            // Real modal closes -- now, and only now, Challenge re-enables.
            closeButton.onClick.Invoke();
            Assert.IsTrue(challengeButton.interactable,
                "closing the modal after the duel has already resolved must re-enable Challenge -- this is the exact bug DuelModalGate fixes");
        }
    }
}
