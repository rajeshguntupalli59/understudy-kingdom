using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using UnderstudyKingdom.Backend;
using UnderstudyKingdom.Core;
using UnderstudyKingdom.Npc;
using UnderstudyKingdom.UI;

namespace UnderstudyKingdom.EditorTools
{
    /// <summary>
    /// Builds Assets/Scenes/CoreLoop.unity programmatically. Unity scene YAML
    /// is error-prone to hand-author (GUIDs, component fileIDs); building it
    /// via script is reproducible, inspectable, and re-runnable. See
    /// docs/superpowers/specs/2026-09-01-core-loop-vertical-slice-design.md.
    /// </summary>
    public static class CoreLoopSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/CoreLoop.unity";

        [MenuItem("Understudy Kingdom/Build Core Loop Scene")]
        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cameraObject = new GameObject("Main Camera", typeof(Camera));
            cameraObject.tag = "MainCamera";

            var rulerObject = new GameObject("Ruler");
            var ruler = rulerObject.AddComponent<RulerNpcController>();

            var managerObject = new GameObject("Manager");
            var manager = managerObject.AddComponent<DecisionCycleManager>();
            manager.Ruler = ruler;

            var backendCoordinatorObject = new GameObject("BackendSyncCoordinator");
            var backendCoordinator = backendCoordinatorObject.AddComponent<BackendSyncCoordinator>();
            backendCoordinator.SupabaseUrl = "https://kszwkvxtnzbbndclpbbe.supabase.co";
            backendCoordinator.SupabaseAnonKey = "sb_publishable_R277yUhT4qK5yTdZwamiuQ_3MD-gdvw";
            backendCoordinator.BackendBaseUrl = "http://localhost:3000";
            backendCoordinator.DecisionCycleManager = manager;

            var canvasObject = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            // Scale the whole canvas to fit whatever window/screen exists, matching
            // height, so the vertically-stacked UI (content extends to y=-420 from
            // center) never gets clipped regardless of the actual viewport size --
            // it was previously "Constant Pixel Size", which clipped the lower
            // elements (labels, button) on any viewport shorter than ~900px tall.
            var canvasScaler = canvasObject.GetComponent<CanvasScaler>();
            canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasScaler.referenceResolution = new Vector2(800f, 1600f);
            canvasScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            canvasScaler.matchWidthOrHeight = 1f;

            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

            Slider armySlider = CreateSlider(canvasObject.transform, "ArmySlider", 40f, 40f);
            Slider tradeSlider = CreateSlider(canvasObject.transform, "TradeSlider", 90f, 30f);
            Slider religionSlider = CreateSlider(canvasObject.transform, "ReligionSlider", 140f, 30f);

            TextMeshProUGUI moodLabel = CreateLabel(canvasObject.transform, "MoodLabel", 200f, "Mood: 50");
            TextMeshProUGUI loyaltyLabel = CreateLabel(canvasObject.transform, "LoyaltyLabel", 240f, "Loyalty: 50");
            TextMeshProUGUI agendaLabel = CreateLabel(canvasObject.transform, "AgendaLabel", 280f, "Agenda: Expansionist");
            TextMeshProUGUI narrationText = CreateLabel(canvasObject.transform, "NarrationText", 340f, string.Empty);

            var buttonObject = new GameObject("SubmitButton", typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(canvasObject.transform, false);
            var buttonRect = buttonObject.GetComponent<RectTransform>();
            buttonRect.anchoredPosition = new Vector2(0f, -420f);
            buttonRect.sizeDelta = new Vector2(220f, 44f);
            // Default Image color is white; the button label text is also white
            // (see CreateLabel) -- give the button a distinct background so its
            // own label isn't invisible white-on-white.
            buttonObject.GetComponent<Image>().color = new Color(0.3f, 0.5f, 0.7f, 1f);
            var button = buttonObject.GetComponent<Button>();
            TextMeshProUGUI buttonLabel = CreateLabel(buttonObject.transform, "Text", 0f, "Submit Recommendation");
            var buttonLabelRect = buttonLabel.GetComponent<RectTransform>();
            buttonLabelRect.anchorMin = Vector2.zero;
            buttonLabelRect.anchorMax = Vector2.one;
            buttonLabelRect.sizeDelta = Vector2.zero;
            buttonLabelRect.anchoredPosition = Vector2.zero;

            var controllerObject = new GameObject("CoreLoopScreenController");
            var controller = controllerObject.AddComponent<CoreLoopScreenController>();
            controller.Initialize(manager, armySlider, tradeSlider, religionSlider,
                moodLabel, loyaltyLabel, agendaLabel, narrationText, button);

            var duelButtonObject = new GameObject("ChallengeButton", typeof(Image), typeof(Button));
            duelButtonObject.transform.SetParent(canvasObject.transform, false);
            var duelButtonRect = duelButtonObject.GetComponent<RectTransform>();
            duelButtonRect.anchoredPosition = new Vector2(0f, -480f);
            duelButtonRect.sizeDelta = new Vector2(260f, 44f);
            duelButtonObject.GetComponent<Image>().color = new Color(0.6f, 0.3f, 0.3f, 1f);
            var duelButton = duelButtonObject.GetComponent<Button>();
            TextMeshProUGUI duelButtonLabel = CreateLabel(duelButtonObject.transform, "Text", 0f, "Challenge a Rival Kingdom");
            var duelButtonLabelRect = duelButtonLabel.GetComponent<RectTransform>();
            duelButtonLabelRect.anchorMin = Vector2.zero;
            duelButtonLabelRect.anchorMax = Vector2.one;
            duelButtonLabelRect.sizeDelta = Vector2.zero;
            duelButtonLabelRect.anchoredPosition = Vector2.zero;

            TextMeshProUGUI duelResultText = CreateLabel(canvasObject.transform, "DuelResultText", 540f, string.Empty);

            var duelControllerObject = new GameObject("DuelButtonController");
            var duelController = duelControllerObject.AddComponent<DuelButtonController>();
            duelController.Initialize(armySlider, tradeSlider, religionSlider, duelButton, duelResultText, backendCoordinator);

            var viewHistoryButtonObject = new GameObject("ViewHistoryButton", typeof(Image), typeof(Button));
            viewHistoryButtonObject.transform.SetParent(canvasObject.transform, false);
            var viewHistoryButtonRect = viewHistoryButtonObject.GetComponent<RectTransform>();
            viewHistoryButtonRect.anchoredPosition = new Vector2(0f, -600f);
            viewHistoryButtonRect.sizeDelta = new Vector2(220f, 44f);
            viewHistoryButtonObject.GetComponent<Image>().color = new Color(0.3f, 0.6f, 0.4f, 1f);
            var viewHistoryButton = viewHistoryButtonObject.GetComponent<Button>();
            TextMeshProUGUI viewHistoryLabel = CreateLabel(viewHistoryButtonObject.transform, "Text", 0f, "View History");
            var viewHistoryLabelRect = viewHistoryLabel.GetComponent<RectTransform>();
            viewHistoryLabelRect.anchorMin = Vector2.zero;
            viewHistoryLabelRect.anchorMax = Vector2.one;
            viewHistoryLabelRect.sizeDelta = Vector2.zero;
            viewHistoryLabelRect.anchoredPosition = Vector2.zero;

            var councilButtonObject = new GameObject("CouncilButton", typeof(Image), typeof(Button));
            councilButtonObject.transform.SetParent(canvasObject.transform, false);
            var councilButtonRect = councilButtonObject.GetComponent<RectTransform>();
            councilButtonRect.anchoredPosition = new Vector2(0f, -660f);
            councilButtonRect.sizeDelta = new Vector2(220f, 44f);
            councilButtonObject.GetComponent<Image>().color = new Color(0.5f, 0.35f, 0.65f, 1f);
            var councilButton = councilButtonObject.GetComponent<Button>();
            TextMeshProUGUI councilButtonLabel = CreateLabel(councilButtonObject.transform, "Text", 0f, "Council");
            var councilButtonLabelRect = councilButtonLabel.GetComponent<RectTransform>();
            councilButtonLabelRect.anchorMin = Vector2.zero;
            councilButtonLabelRect.anchorMax = Vector2.one;
            councilButtonLabelRect.sizeDelta = Vector2.zero;
            councilButtonLabelRect.anchoredPosition = Vector2.zero;

            var councilPanelRootObject = new GameObject("CouncilPanel", typeof(Image));
            councilPanelRootObject.transform.SetParent(canvasObject.transform, false);
            var councilPanelRect = councilPanelRootObject.GetComponent<RectTransform>();
            councilPanelRect.anchoredPosition = Vector2.zero;
            councilPanelRect.sizeDelta = new Vector2(700f, 800f);
            councilPanelRootObject.GetComponent<Image>().color = new Color(0.1f, 0.1f, 0.15f, 0.95f);

            var councilCloseButtonObject = new GameObject("CloseButton", typeof(Image), typeof(Button));
            councilCloseButtonObject.transform.SetParent(councilPanelRootObject.transform, false);
            var councilCloseButtonRect = councilCloseButtonObject.GetComponent<RectTransform>();
            councilCloseButtonRect.anchoredPosition = new Vector2(310f, 360f);
            councilCloseButtonRect.sizeDelta = new Vector2(60f, 40f);
            councilCloseButtonObject.GetComponent<Image>().color = new Color(0.6f, 0.3f, 0.3f, 1f);
            var councilCloseButton = councilCloseButtonObject.GetComponent<Button>();
            TextMeshProUGUI councilCloseLabel = CreateLabel(councilCloseButtonObject.transform, "Text", 0f, "X");
            var councilCloseLabelRect = councilCloseLabel.GetComponent<RectTransform>();
            councilCloseLabelRect.anchorMin = Vector2.zero;
            councilCloseLabelRect.anchorMax = Vector2.one;
            councilCloseLabelRect.sizeDelta = Vector2.zero;
            councilCloseLabelRect.anchoredPosition = Vector2.zero;

            TextMeshProUGUI councilTitleLabel = CreateLabel(councilPanelRootObject.transform, "Title", 0f, "Your Council");
            councilTitleLabel.fontSize = 28f;
            councilTitleLabel.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, 340f);

            TextMeshProUGUI councilStatusMessageText = CreateLabel(councilPanelRootObject.transform, "StatusMessageText", 0f, string.Empty);
            councilStatusMessageText.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, 260f);

            var notInCouncilViewObject = new GameObject("NotInCouncilView", typeof(RectTransform));
            notInCouncilViewObject.transform.SetParent(councilPanelRootObject.transform, false);

            // Persistent field labels, not placeholder-only -- ui-ux-pro-max's
            // Quick Reference (Forms & Feedback, `input-labels`) flags
            // placeholder-only labels as an anti-pattern: the placeholder
            // text on the input fields below disappears the moment the
            // player starts typing.
            TextMeshProUGUI nameFieldLabel = CreateLabel(notInCouncilViewObject.transform, "NameFieldLabel", 0f, "Council Name");
            nameFieldLabel.fontSize = 24f;
            nameFieldLabel.alignment = TextAlignmentOptions.Left;
            var nameFieldLabelRect = nameFieldLabel.GetComponent<RectTransform>();
            nameFieldLabelRect.anchoredPosition = new Vector2(0f, 215f);
            nameFieldLabelRect.sizeDelta = new Vector2(400f, 24f);

            TMP_InputField nameInputField = CreateInputField(notInCouncilViewObject.transform, "NameInput", "Council name");
            nameInputField.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, 180f);

            var createButtonObject = new GameObject("CreateButton", typeof(Image), typeof(Button));
            createButtonObject.transform.SetParent(notInCouncilViewObject.transform, false);
            var createButtonRect = createButtonObject.GetComponent<RectTransform>();
            createButtonRect.anchoredPosition = new Vector2(0f, 110f);
            createButtonRect.sizeDelta = new Vector2(220f, 44f);
            createButtonObject.GetComponent<Image>().color = new Color(0.3f, 0.5f, 0.7f, 1f);
            var createButton = createButtonObject.GetComponent<Button>();
            TextMeshProUGUI createButtonLabel = CreateLabel(createButtonObject.transform, "Text", 0f, "Create Council");
            var createButtonLabelRect = createButtonLabel.GetComponent<RectTransform>();
            createButtonLabelRect.anchorMin = Vector2.zero;
            createButtonLabelRect.anchorMax = Vector2.one;
            createButtonLabelRect.sizeDelta = Vector2.zero;
            createButtonLabelRect.anchoredPosition = Vector2.zero;

            TextMeshProUGUI joinCodeFieldLabel = CreateLabel(notInCouncilViewObject.transform, "JoinCodeFieldLabel", 0f, "Join Code");
            joinCodeFieldLabel.fontSize = 24f;
            joinCodeFieldLabel.alignment = TextAlignmentOptions.Left;
            var joinCodeFieldLabelRect = joinCodeFieldLabel.GetComponent<RectTransform>();
            joinCodeFieldLabelRect.anchoredPosition = new Vector2(0f, 35f);
            joinCodeFieldLabelRect.sizeDelta = new Vector2(400f, 24f);

            TMP_InputField joinCodeInputField = CreateInputField(notInCouncilViewObject.transform, "JoinCodeInput", "Join code");
            joinCodeInputField.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, 0f);

            var joinButtonObject = new GameObject("JoinButton", typeof(Image), typeof(Button));
            joinButtonObject.transform.SetParent(notInCouncilViewObject.transform, false);
            var joinButtonRect = joinButtonObject.GetComponent<RectTransform>();
            joinButtonRect.anchoredPosition = new Vector2(0f, -70f);
            joinButtonRect.sizeDelta = new Vector2(220f, 44f);
            joinButtonObject.GetComponent<Image>().color = new Color(0.3f, 0.6f, 0.4f, 1f);
            var joinButton = joinButtonObject.GetComponent<Button>();
            TextMeshProUGUI joinButtonLabel = CreateLabel(joinButtonObject.transform, "Text", 0f, "Join Council");
            var joinButtonLabelRect = joinButtonLabel.GetComponent<RectTransform>();
            joinButtonLabelRect.anchorMin = Vector2.zero;
            joinButtonLabelRect.anchorMax = Vector2.one;
            joinButtonLabelRect.sizeDelta = Vector2.zero;
            joinButtonLabelRect.anchoredPosition = Vector2.zero;

            var inCouncilViewObject = new GameObject("InCouncilView", typeof(RectTransform));
            inCouncilViewObject.transform.SetParent(councilPanelRootObject.transform, false);

            TextMeshProUGUI councilNameLabel = CreateLabel(inCouncilViewObject.transform, "NameLabel", 0f, string.Empty);
            councilNameLabel.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, 180f);

            TextMeshProUGUI councilJoinCodeLabel = CreateLabel(inCouncilViewObject.transform, "JoinCodeLabel", 0f, string.Empty);
            councilJoinCodeLabel.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, 120f);

            TextMeshProUGUI councilMemberCountLabel = CreateLabel(inCouncilViewObject.transform, "MemberCountLabel", 0f, string.Empty);
            councilMemberCountLabel.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, 60f);

            TextMeshProUGUI councilProgressLabel = CreateLabel(inCouncilViewObject.transform, "ProgressLabel", 0f, string.Empty);
            councilProgressLabel.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, 0f);

            TextMeshProUGUI councilRewardStatusLabel = CreateLabel(inCouncilViewObject.transform, "RewardStatusLabel", 0f, string.Empty);
            var councilRewardStatusLabelRect = councilRewardStatusLabel.GetComponent<RectTransform>();
            councilRewardStatusLabelRect.anchoredPosition = new Vector2(0f, -80f);
            councilRewardStatusLabelRect.sizeDelta = new Vector2(640f, 80f);

            var councilControllerObject = new GameObject("CouncilPanelController");
            var councilController = councilControllerObject.AddComponent<CouncilPanelController>();
            councilController.Initialize(councilButton, councilPanelRootObject, councilCloseButton, notInCouncilViewObject, inCouncilViewObject,
                nameInputField, createButton, joinCodeInputField, joinButton, councilStatusMessageText,
                councilNameLabel, councilJoinCodeLabel, councilMemberCountLabel, councilProgressLabel, councilRewardStatusLabel,
                backendCoordinator, manager, controller,
                armySlider, tradeSlider, religionSlider, button, duelButton, viewHistoryButton);

            var panelRootObject = new GameObject("HistoryPanel", typeof(Image));
            panelRootObject.transform.SetParent(canvasObject.transform, false);
            var panelRect = panelRootObject.GetComponent<RectTransform>();
            panelRect.anchoredPosition = Vector2.zero;
            panelRect.sizeDelta = new Vector2(700f, 800f);
            panelRootObject.GetComponent<Image>().color = new Color(0.1f, 0.1f, 0.15f, 0.95f);

            var closeButtonObject = new GameObject("CloseButton", typeof(Image), typeof(Button));
            closeButtonObject.transform.SetParent(panelRootObject.transform, false);
            var closeButtonRect = closeButtonObject.GetComponent<RectTransform>();
            closeButtonRect.anchoredPosition = new Vector2(310f, 360f);
            closeButtonRect.sizeDelta = new Vector2(60f, 40f);
            closeButtonObject.GetComponent<Image>().color = new Color(0.6f, 0.3f, 0.3f, 1f);
            var closeButton = closeButtonObject.GetComponent<Button>();
            TextMeshProUGUI closeLabel = CreateLabel(closeButtonObject.transform, "Text", 0f, "X");
            var closeLabelRect = closeLabel.GetComponent<RectTransform>();
            closeLabelRect.anchorMin = Vector2.zero;
            closeLabelRect.anchorMax = Vector2.one;
            closeLabelRect.sizeDelta = Vector2.zero;
            closeLabelRect.anchoredPosition = Vector2.zero;

            TextMeshProUGUI titleLabel = CreateLabel(panelRootObject.transform, "Title", 0f, "Your Reign So Far");
            titleLabel.fontSize = 28f;
            titleLabel.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, 340f);

            var rowTexts = new TextMeshProUGUI[10];
            for (int i = 0; i < rowTexts.Length; i++)
            {
                TextMeshProUGUI row = CreateLabel(panelRootObject.transform, $"Row{i}", 0f, string.Empty);
                // Matches the rest of the scene's label convention (CreateLabel's own
                // default is 24) -- this row previously used 18, noticeably smaller
                // than every other label, and was flagged as hard to read during the
                // milestone's manual Play Mode checkpoint.
                row.fontSize = 24f;
                row.alignment = TextAlignmentOptions.Left;
                var rowRect = row.GetComponent<RectTransform>();
                rowRect.sizeDelta = new Vector2(640f, 50f);
                rowRect.anchoredPosition = new Vector2(0f, 280f - i * 55f);
                rowTexts[i] = row;
            }

            var historyControllerObject = new GameObject("HistoryPanelController");
            var historyController = historyControllerObject.AddComponent<HistoryPanelController>();
            historyController.Initialize(viewHistoryButton, panelRootObject, closeButton, rowTexts, backendCoordinator,
                armySlider, tradeSlider, religionSlider, button, duelButton, councilButton);

            var tutorialOverlayObject = new GameObject("TutorialOverlay", typeof(Image));
            tutorialOverlayObject.transform.SetParent(canvasObject.transform, false);
            var tutorialOverlayRect = tutorialOverlayObject.GetComponent<RectTransform>();
            tutorialOverlayRect.anchorMin = Vector2.zero;
            tutorialOverlayRect.anchorMax = Vector2.one;
            tutorialOverlayRect.offsetMin = Vector2.zero;
            tutorialOverlayRect.offsetMax = Vector2.zero;
            tutorialOverlayObject.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.75f);

            var tutorialBoxObject = new GameObject("CalloutBox", typeof(Image));
            tutorialBoxObject.transform.SetParent(tutorialOverlayObject.transform, false);
            var tutorialBoxRect = tutorialBoxObject.GetComponent<RectTransform>();
            tutorialBoxRect.anchoredPosition = Vector2.zero;
            tutorialBoxRect.sizeDelta = new Vector2(600f, 500f);
            tutorialBoxObject.GetComponent<Image>().color = new Color(0.15f, 0.15f, 0.2f, 1f);

            TextMeshProUGUI tutorialStepIndicatorLabel = CreateLabel(tutorialBoxObject.transform, "StepIndicatorLabel", 0f, "Step 1 of 4");
            tutorialStepIndicatorLabel.fontSize = 20f;
            tutorialStepIndicatorLabel.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, 210f);

            TextMeshProUGUI tutorialTitleLabel = CreateLabel(tutorialBoxObject.transform, "TitleLabel", 0f, string.Empty);
            tutorialTitleLabel.fontSize = 28f;
            tutorialTitleLabel.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, 160f);

            TextMeshProUGUI tutorialBodyLabel = CreateLabel(tutorialBoxObject.transform, "BodyLabel", 0f, string.Empty);
            var tutorialBodyLabelRect = tutorialBodyLabel.GetComponent<RectTransform>();
            tutorialBodyLabelRect.anchoredPosition = new Vector2(0f, 20f);
            tutorialBodyLabelRect.sizeDelta = new Vector2(520f, 200f);

            var tutorialSkipButtonObject = new GameObject("SkipButton", typeof(Image), typeof(Button));
            tutorialSkipButtonObject.transform.SetParent(tutorialBoxObject.transform, false);
            var tutorialSkipButtonRect = tutorialSkipButtonObject.GetComponent<RectTransform>();
            tutorialSkipButtonRect.anchoredPosition = new Vector2(-140f, -190f);
            tutorialSkipButtonRect.sizeDelta = new Vector2(220f, 44f);
            tutorialSkipButtonObject.GetComponent<Image>().color = new Color(0.4f, 0.4f, 0.4f, 1f);
            var tutorialSkipButton = tutorialSkipButtonObject.GetComponent<Button>();
            TextMeshProUGUI tutorialSkipButtonLabel = CreateLabel(tutorialSkipButtonObject.transform, "Text", 0f, "Skip");
            var tutorialSkipButtonLabelRect = tutorialSkipButtonLabel.GetComponent<RectTransform>();
            tutorialSkipButtonLabelRect.anchorMin = Vector2.zero;
            tutorialSkipButtonLabelRect.anchorMax = Vector2.one;
            tutorialSkipButtonLabelRect.sizeDelta = Vector2.zero;
            tutorialSkipButtonLabelRect.anchoredPosition = Vector2.zero;

            var tutorialNextButtonObject = new GameObject("NextButton", typeof(Image), typeof(Button));
            tutorialNextButtonObject.transform.SetParent(tutorialBoxObject.transform, false);
            var tutorialNextButtonRect = tutorialNextButtonObject.GetComponent<RectTransform>();
            tutorialNextButtonRect.anchoredPosition = new Vector2(140f, -190f);
            tutorialNextButtonRect.sizeDelta = new Vector2(220f, 44f);
            tutorialNextButtonObject.GetComponent<Image>().color = new Color(0.3f, 0.5f, 0.7f, 1f);
            var tutorialNextButton = tutorialNextButtonObject.GetComponent<Button>();
            TextMeshProUGUI tutorialNextButtonLabel = CreateLabel(tutorialNextButtonObject.transform, "Text", 0f, "Next");
            var tutorialNextButtonLabelRect = tutorialNextButtonLabel.GetComponent<RectTransform>();
            tutorialNextButtonLabelRect.anchorMin = Vector2.zero;
            tutorialNextButtonLabelRect.anchorMax = Vector2.one;
            tutorialNextButtonLabelRect.sizeDelta = Vector2.zero;
            tutorialNextButtonLabelRect.anchoredPosition = Vector2.zero;

            var tutorialControllerObject = new GameObject("TutorialOverlayController");
            var tutorialController = tutorialControllerObject.AddComponent<TutorialOverlayController>();
            tutorialController.Initialize(tutorialOverlayObject, tutorialStepIndicatorLabel, tutorialTitleLabel, tutorialBodyLabel,
                tutorialNextButton, tutorialNextButtonLabel, tutorialSkipButton, manager,
                armySlider, tradeSlider, religionSlider, button, duelButton, viewHistoryButton, councilButton);

            canvasObject.GetComponent<RectTransform>().localScale = Vector3.one;

            Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            NormalizeCanvasLocalScaleInSavedScene();
            AssetDatabase.Refresh();

            Debug.Log($"CoreLoopSceneBuilder: saved scene to {ScenePath}");
        }

        /// <summary>
        /// The Canvas root's RectTransform.localScale is recomputed by Unity's
        /// ScreenSpaceOverlay Canvas from screen dimensions that don't exist in
        /// -nographics batch mode, so it always serializes as {0, 0, 0} no
        /// matter what is set on the live object beforehand -- confirmed
        /// harmless at runtime (it resolves correctly the moment a real
        /// screen/window exists), but it makes future diffs of this
        /// generated scene file look alarming. Patch it back to {1, 1, 1} in
        /// the just-saved YAML directly, since setting it programmatically
        /// before SaveScene does not survive the save.
        /// </summary>
        private static void NormalizeCanvasLocalScaleInSavedScene()
        {
            const string zeroScale = "m_LocalScale: {x: 0, y: 0, z: 0}";
            const string oneScale = "m_LocalScale: {x: 1, y: 1, z: 1}";

            string sceneText = File.ReadAllText(ScenePath);
            string patched = sceneText.Replace(zeroScale, oneScale);
            if (patched != sceneText)
            {
                File.WriteAllText(ScenePath, patched);
            }
        }

        [MenuItem("Understudy Kingdom/Add Core Loop Scene To Build Settings")]
        public static void AddToBuildSettings()
        {
            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            bool alreadyPresent = scenes.Exists(s => s.path == ScenePath);
            if (!alreadyPresent)
            {
                scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
                EditorBuildSettings.scenes = scenes.ToArray();
                Debug.Log($"CoreLoopSceneBuilder: added {ScenePath} to Build Settings");
            }
            else
            {
                Debug.Log($"CoreLoopSceneBuilder: {ScenePath} already in Build Settings");
            }
        }

        /// <summary>
        /// Scene-integrity sanity check: opens the scene and confirms a
        /// CoreLoopScreenController is present. This does NOT drive Play Mode
        /// or verify persistence across a stop/restart — it is a batch-mode
        /// tool only, reachable via -executeMethod, not the Editor menu, since
        /// EditorApplication.Exit(1) on failure would otherwise kill the
        /// Editor and discard unsaved work if someone ran it from the GUI.
        /// </summary>
        public static void Verify()
        {
            EditorSceneManager.OpenScene(ScenePath);

            var controller = Object.FindFirstObjectByType<CoreLoopScreenController>();
            if (controller == null)
            {
                Debug.LogError("CoreLoopSceneBuilder.Verify: no CoreLoopScreenController found in the scene.");
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(1);
                }
                return;
            }

            var historyController = Object.FindFirstObjectByType<HistoryPanelController>();
            if (historyController == null)
            {
                Debug.LogError("CoreLoopSceneBuilder.Verify: no HistoryPanelController found in the scene.");
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(1);
                }
                return;
            }

            var councilController = Object.FindFirstObjectByType<CouncilPanelController>();
            if (councilController == null)
            {
                Debug.LogError("CoreLoopSceneBuilder.Verify: no CouncilPanelController found in the scene.");
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(1);
                }
                return;
            }

            var tutorialController = Object.FindFirstObjectByType<TutorialOverlayController>();
            if (tutorialController == null)
            {
                Debug.LogError("CoreLoopSceneBuilder.Verify: no TutorialOverlayController found in the scene.");
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(1);
                }
                return;
            }

            Debug.Log("CoreLoopSceneBuilder.Verify: scene opened and controller found successfully.");
        }

        private static Slider CreateSlider(Transform parent, string name, float yOffset, float initialValue)
        {
            var sliderObject = new GameObject(name, typeof(Slider));
            sliderObject.transform.SetParent(parent, false);
            var rect = sliderObject.GetComponent<RectTransform>();
            rect.anchoredPosition = new Vector2(0f, -yOffset);
            rect.sizeDelta = new Vector2(320f, 20f);

            var slider = sliderObject.GetComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 100f;
            slider.wholeNumbers = true;

            var backgroundObject = new GameObject("Background", typeof(Image));
            backgroundObject.transform.SetParent(sliderObject.transform, false);
            var bgRect = backgroundObject.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.sizeDelta = Vector2.zero;
            backgroundObject.GetComponent<Image>().color = new Color(0.2f, 0.2f, 0.2f, 1f);

            var fillAreaObject = new GameObject("Fill Area", typeof(RectTransform));
            fillAreaObject.transform.SetParent(sliderObject.transform, false);
            var fillAreaRect = fillAreaObject.GetComponent<RectTransform>();
            fillAreaRect.anchorMin = Vector2.zero;
            fillAreaRect.anchorMax = Vector2.one;
            fillAreaRect.sizeDelta = Vector2.zero;

            var fillObject = new GameObject("Fill", typeof(Image));
            fillObject.transform.SetParent(fillAreaObject.transform, false);
            var fillRect = fillObject.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.sizeDelta = Vector2.zero;
            fillObject.GetComponent<Image>().color = new Color(0.4f, 0.7f, 0.9f, 1f);

            slider.fillRect = fillRect;
            slider.targetGraphic = fillObject.GetComponent<Image>();
            slider.SetValueWithoutNotify(initialValue);

            return slider;
        }

        private static TextMeshProUGUI CreateLabel(Transform parent, string name, float yOffset, string text)
        {
            var labelObject = new GameObject(name, typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(parent, false);
            var rect = labelObject.GetComponent<RectTransform>();
            rect.anchoredPosition = new Vector2(0f, -yOffset);
            rect.sizeDelta = new Vector2(420f, 32f);

            var label = labelObject.GetComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = 24f;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;

            return label;
        }

        private static TMP_InputField CreateInputField(Transform parent, string name, string placeholderText)
        {
            var fieldObject = new GameObject(name, typeof(Image), typeof(TMP_InputField));
            fieldObject.transform.SetParent(parent, false);
            var rect = fieldObject.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(400f, 44f);
            fieldObject.GetComponent<Image>().color = new Color(0.9f, 0.9f, 0.9f, 1f);

            var textAreaObject = new GameObject("Text Area", typeof(RectMask2D));
            textAreaObject.transform.SetParent(fieldObject.transform, false);
            var textAreaRect = textAreaObject.GetComponent<RectTransform>();
            textAreaRect.anchorMin = Vector2.zero;
            textAreaRect.anchorMax = Vector2.one;
            textAreaRect.offsetMin = new Vector2(10f, 6f);
            textAreaRect.offsetMax = new Vector2(-10f, -6f);

            var placeholderObject = new GameObject("Placeholder", typeof(TextMeshProUGUI));
            placeholderObject.transform.SetParent(textAreaObject.transform, false);
            var placeholderRect = placeholderObject.GetComponent<RectTransform>();
            placeholderRect.anchorMin = Vector2.zero;
            placeholderRect.anchorMax = Vector2.one;
            placeholderRect.sizeDelta = Vector2.zero;
            var placeholder = placeholderObject.GetComponent<TextMeshProUGUI>();
            placeholder.text = placeholderText;
            placeholder.fontSize = 24f;
            placeholder.color = new Color(0.5f, 0.5f, 0.5f, 1f);
            placeholder.alignment = TextAlignmentOptions.MidlineLeft;

            var textObject = new GameObject("Text", typeof(TextMeshProUGUI));
            textObject.transform.SetParent(textAreaObject.transform, false);
            var textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;
            var text = textObject.GetComponent<TextMeshProUGUI>();
            text.fontSize = 24f;
            text.color = Color.black;
            text.alignment = TextAlignmentOptions.MidlineLeft;

            var inputField = fieldObject.GetComponent<TMP_InputField>();
            inputField.textViewport = textAreaRect;
            inputField.textComponent = text;
            inputField.placeholder = placeholder;
            inputField.text = string.Empty;

            return inputField;
        }
    }
}
