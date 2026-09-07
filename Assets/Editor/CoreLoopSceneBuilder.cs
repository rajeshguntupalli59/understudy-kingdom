using System.IO;
using System.Reflection;
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

            // Created before every other canvas child so it renders behind
            // all of them (Unity draws uGUI siblings in child order).
            var sceneBackgroundObject = new GameObject("SceneBackground", typeof(Image), typeof(AspectRatioFitter));
            sceneBackgroundObject.transform.SetParent(canvasObject.transform, false);
            var sceneBackgroundRect = sceneBackgroundObject.GetComponent<RectTransform>();
            // AspectRatioFitter in EnvelopeParent mode drives anchors,
            // anchoredPosition, and sizeDelta itself on its first layout pass
            // (resetting anchors to full-stretch), so only the pivot needs
            // setting here -- it resizes to fully cover the parent while
            // preserving the art's real aspect ratio (crops excess instead
            // of squeezing/stretching it).
            sceneBackgroundRect.pivot = new Vector2(0.5f, 0.5f);
            var sceneBackgroundFitter = sceneBackgroundObject.GetComponent<AspectRatioFitter>();
            sceneBackgroundFitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            // Matches the committed background art's actual aspect ratio
            // (1024x1792) -- update this if the art is ever re-exported at a
            // different ratio.
            sceneBackgroundFitter.aspectRatio = 1024f / 1792f;
            var sceneBackgroundImage = sceneBackgroundObject.GetComponent<Image>();
            sceneBackgroundImage.preserveAspect = false;
            // Full-screen decorative background sitting behind every other UI
            // element -- never the front-most hit target, but skipping it in
            // every raycast is free and correct for a purely visual element.
            sceneBackgroundImage.raycastTarget = false;
            Sprite[] backgroundSprites = LoadThemedSprites("Assets/Art/Backgrounds", "background");

            Slider armySlider = CreateSlider(canvasObject.transform, "ArmySlider", 40f, 40f);
            Slider tradeSlider = CreateSlider(canvasObject.transform, "TradeSlider", 90f, 30f);
            Slider religionSlider = CreateSlider(canvasObject.transform, "ReligionSlider", 140f, 30f);

            TextMeshProUGUI moodLabel = CreateLabel(canvasObject.transform, "MoodLabel", 200f, "Mood: 50");
            TextMeshProUGUI loyaltyLabel = CreateLabel(canvasObject.transform, "LoyaltyLabel", 240f, "Loyalty: 50");
            TextMeshProUGUI agendaLabel = CreateLabel(canvasObject.transform, "AgendaLabel", 280f, "Agenda: Expansionist");
            TextMeshProUGUI narrationText = CreateLabel(canvasObject.transform, "NarrationText", 340f, string.Empty);

            var rulerPortraitObject = new GameObject("RulerPortraitImage", typeof(Image));
            rulerPortraitObject.transform.SetParent(canvasObject.transform, false);
            var rulerPortraitRect = rulerPortraitObject.GetComponent<RectTransform>();
            rulerPortraitRect.anchoredPosition = new Vector2(-250f, -170f);
            rulerPortraitRect.sizeDelta = new Vector2(200f, 260f);
            var rulerPortraitImage = rulerPortraitObject.GetComponent<Image>();
            rulerPortraitImage.preserveAspect = true;
            Sprite[] rulerPortraits = LoadRulerPortraits();

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
            buttonLabelRect.offsetMin = new Vector2(40f, 0f);
            buttonLabelRect.offsetMax = Vector2.zero;

            var submitIconObject = new GameObject("Icon", typeof(Image));
            submitIconObject.transform.SetParent(buttonObject.transform, false);
            var submitIconRect = submitIconObject.GetComponent<RectTransform>();
            submitIconRect.anchorMin = new Vector2(0f, 0.5f);
            submitIconRect.anchorMax = new Vector2(0f, 0.5f);
            submitIconRect.pivot = new Vector2(0f, 0.5f);
            submitIconRect.anchoredPosition = new Vector2(8f, 0f);
            submitIconRect.sizeDelta = new Vector2(32f, 32f);
            var submitIconImage = submitIconObject.GetComponent<Image>();
            submitIconImage.sprite = LoadIconSprite("Assets/Art/ButtonIcons/submit.png");
            submitIconImage.raycastTarget = false;

            var controllerObject = new GameObject("CoreLoopScreenController");
            var controller = controllerObject.AddComponent<CoreLoopScreenController>();
            controller.Initialize(manager, armySlider, tradeSlider, religionSlider,
                moodLabel, loyaltyLabel, agendaLabel, narrationText, button,
                rulerPortraitImage, rulerPortraits, submitIconImage);

            var duelModalGateObject = new GameObject("DuelModalGate");
            var duelModalGate = duelModalGateObject.AddComponent<DuelModalGate>();

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
            duelButtonLabelRect.offsetMin = new Vector2(40f, 0f);
            duelButtonLabelRect.offsetMax = Vector2.zero;

            var challengeIconObject = new GameObject("Icon", typeof(Image));
            challengeIconObject.transform.SetParent(duelButtonObject.transform, false);
            var challengeIconRect = challengeIconObject.GetComponent<RectTransform>();
            challengeIconRect.anchorMin = new Vector2(0f, 0.5f);
            challengeIconRect.anchorMax = new Vector2(0f, 0.5f);
            challengeIconRect.pivot = new Vector2(0f, 0.5f);
            challengeIconRect.anchoredPosition = new Vector2(8f, 0f);
            challengeIconRect.sizeDelta = new Vector2(32f, 32f);
            var challengeIconImage = challengeIconObject.GetComponent<Image>();
            challengeIconImage.sprite = LoadIconSprite("Assets/Art/ButtonIcons/challenge.png");
            challengeIconImage.raycastTarget = false;

            TextMeshProUGUI duelResultText = CreateLabel(canvasObject.transform, "DuelResultText", 540f, string.Empty);

            var duelControllerObject = new GameObject("DuelButtonController");
            var duelController = duelControllerObject.AddComponent<DuelButtonController>();
            duelController.Initialize(armySlider, tradeSlider, religionSlider, duelButton, duelResultText, backendCoordinator, duelModalGate, challengeIconImage);

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
            viewHistoryLabelRect.offsetMin = new Vector2(40f, 0f);
            viewHistoryLabelRect.offsetMax = Vector2.zero;

            var viewHistoryIconObject = new GameObject("Icon", typeof(Image));
            viewHistoryIconObject.transform.SetParent(viewHistoryButtonObject.transform, false);
            var viewHistoryIconRect = viewHistoryIconObject.GetComponent<RectTransform>();
            viewHistoryIconRect.anchorMin = new Vector2(0f, 0.5f);
            viewHistoryIconRect.anchorMax = new Vector2(0f, 0.5f);
            viewHistoryIconRect.pivot = new Vector2(0f, 0.5f);
            viewHistoryIconRect.anchoredPosition = new Vector2(8f, 0f);
            viewHistoryIconRect.sizeDelta = new Vector2(32f, 32f);
            var viewHistoryIconImage = viewHistoryIconObject.GetComponent<Image>();
            viewHistoryIconImage.sprite = LoadIconSprite("Assets/Art/ButtonIcons/history.png");
            viewHistoryIconImage.raycastTarget = false;

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
            councilButtonLabelRect.offsetMin = new Vector2(40f, 0f);
            councilButtonLabelRect.offsetMax = Vector2.zero;

            var councilIconObject = new GameObject("Icon", typeof(Image));
            councilIconObject.transform.SetParent(councilButtonObject.transform, false);
            var councilIconRect = councilIconObject.GetComponent<RectTransform>();
            councilIconRect.anchorMin = new Vector2(0f, 0.5f);
            councilIconRect.anchorMax = new Vector2(0f, 0.5f);
            councilIconRect.pivot = new Vector2(0f, 0.5f);
            councilIconRect.anchoredPosition = new Vector2(8f, 0f);
            councilIconRect.sizeDelta = new Vector2(32f, 32f);
            var councilIconImage = councilIconObject.GetComponent<Image>();
            councilIconImage.sprite = LoadIconSprite("Assets/Art/ButtonIcons/council.png");
            councilIconImage.raycastTarget = false;

            var eventsButtonObject = new GameObject("EventsButton", typeof(Image), typeof(Button));
            eventsButtonObject.transform.SetParent(canvasObject.transform, false);
            var eventsButtonRect = eventsButtonObject.GetComponent<RectTransform>();
            eventsButtonRect.anchoredPosition = new Vector2(0f, -720f);
            eventsButtonRect.sizeDelta = new Vector2(220f, 44f);
            eventsButtonObject.GetComponent<Image>().color = new Color(0.65f, 0.55f, 0.25f, 1f);
            var eventsButton = eventsButtonObject.GetComponent<Button>();
            TextMeshProUGUI eventsButtonLabel = CreateLabel(eventsButtonObject.transform, "Text", 0f, "This Week's Event");
            var eventsButtonLabelRect = eventsButtonLabel.GetComponent<RectTransform>();
            eventsButtonLabelRect.anchorMin = Vector2.zero;
            eventsButtonLabelRect.anchorMax = Vector2.one;
            eventsButtonLabelRect.offsetMin = new Vector2(40f, 0f);
            eventsButtonLabelRect.offsetMax = Vector2.zero;

            var eventsIconObject = new GameObject("Icon", typeof(Image));
            eventsIconObject.transform.SetParent(eventsButtonObject.transform, false);
            var eventsIconRect = eventsIconObject.GetComponent<RectTransform>();
            eventsIconRect.anchorMin = new Vector2(0f, 0.5f);
            eventsIconRect.anchorMax = new Vector2(0f, 0.5f);
            eventsIconRect.pivot = new Vector2(0f, 0.5f);
            eventsIconRect.anchoredPosition = new Vector2(8f, 0f);
            eventsIconRect.sizeDelta = new Vector2(32f, 32f);
            var eventsIconImage = eventsIconObject.GetComponent<Image>();
            eventsIconImage.sprite = LoadIconSprite("Assets/Art/ButtonIcons/events.png");
            eventsIconImage.raycastTarget = false;

            var customizeButtonObject = new GameObject("CustomizeButton", typeof(Image), typeof(Button));
            customizeButtonObject.transform.SetParent(canvasObject.transform, false);
            var customizeButtonRect = customizeButtonObject.GetComponent<RectTransform>();
            customizeButtonRect.anchoredPosition = new Vector2(0f, -780f);
            customizeButtonRect.sizeDelta = new Vector2(220f, 44f);
            customizeButtonObject.GetComponent<Image>().color = new Color(0.45f, 0.45f, 0.5f, 1f);
            var customizeButton = customizeButtonObject.GetComponent<Button>();
            TextMeshProUGUI customizeButtonLabel = CreateLabel(customizeButtonObject.transform, "Text", 0f, "Customize");
            var customizeButtonLabelRect = customizeButtonLabel.GetComponent<RectTransform>();
            customizeButtonLabelRect.anchorMin = Vector2.zero;
            customizeButtonLabelRect.anchorMax = Vector2.one;
            customizeButtonLabelRect.offsetMin = new Vector2(40f, 0f);
            customizeButtonLabelRect.offsetMax = Vector2.zero;

            var customizeIconObject = new GameObject("Icon", typeof(Image));
            customizeIconObject.transform.SetParent(customizeButtonObject.transform, false);
            var customizeIconRect = customizeIconObject.GetComponent<RectTransform>();
            customizeIconRect.anchorMin = new Vector2(0f, 0.5f);
            customizeIconRect.anchorMax = new Vector2(0f, 0.5f);
            customizeIconRect.pivot = new Vector2(0f, 0.5f);
            customizeIconRect.anchoredPosition = new Vector2(8f, 0f);
            customizeIconRect.sizeDelta = new Vector2(32f, 32f);
            var customizeIconImage = customizeIconObject.GetComponent<Image>();
            customizeIconImage.sprite = LoadIconSprite("Assets/Art/ButtonIcons/customize.png");
            customizeIconImage.raycastTarget = false;

            // Task 6 replaces this placeholder with the real Estate button;
            // exists only so these controllers' estateButton field is never
            // null before Task 6 lands (SetCoreLoopControlsInteractable
            // dereferences it unconditionally, matching every other button
            // in that method).
            var estatePlaceholderButtonObject = new GameObject("EstatePlaceholderButton", typeof(Image), typeof(Button));
            estatePlaceholderButtonObject.transform.SetParent(canvasObject.transform, false);
            var estatePlaceholderButton = estatePlaceholderButtonObject.GetComponent<Button>();

            var eventPanelRootObject = new GameObject("EventPanel", typeof(Image));
            eventPanelRootObject.transform.SetParent(canvasObject.transform, false);
            var eventPanelRect = eventPanelRootObject.GetComponent<RectTransform>();
            eventPanelRect.anchoredPosition = Vector2.zero;
            eventPanelRect.sizeDelta = new Vector2(700f, 800f);
            eventPanelRootObject.GetComponent<Image>().color = new Color(0.1f, 0.1f, 0.15f, 0.95f);
            eventPanelRootObject.AddComponent<RectMask2D>();
            var eventArtObject = new GameObject("PanelArt", typeof(Image), typeof(AspectRatioFitter));
            eventArtObject.transform.SetParent(eventPanelRootObject.transform, false);
            var eventArtRect = eventArtObject.GetComponent<RectTransform>();
            eventArtRect.pivot = new Vector2(0.5f, 0.5f);
            var eventArtFitter = eventArtObject.GetComponent<AspectRatioFitter>();
            eventArtFitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            eventArtFitter.aspectRatio = 1024f / 1792f;
            var eventArtImage = eventArtObject.GetComponent<Image>();
            eventArtImage.preserveAspect = false;
            eventArtImage.raycastTarget = false;
            eventArtObject.transform.SetAsFirstSibling();

            var eventCloseButtonObject = new GameObject("CloseButton", typeof(Image), typeof(Button));
            eventCloseButtonObject.transform.SetParent(eventPanelRootObject.transform, false);
            var eventCloseButtonRect = eventCloseButtonObject.GetComponent<RectTransform>();
            eventCloseButtonRect.anchoredPosition = new Vector2(310f, 360f);
            // 44pt tall, not the 60x40 this scene's other close buttons use --
            // this project's touch-target minimum is 44pt; only this new
            // panel's close button is corrected here (see Global Constraints
            // in docs/superpowers/plans/2026-09-03-live-ops-events.md).
            eventCloseButtonRect.sizeDelta = new Vector2(60f, 44f);
            eventCloseButtonObject.GetComponent<Image>().color = new Color(0.6f, 0.3f, 0.3f, 1f);
            var eventCloseButton = eventCloseButtonObject.GetComponent<Button>();
            TextMeshProUGUI eventCloseLabel = CreateLabel(eventCloseButtonObject.transform, "Text", 0f, "X");
            var eventCloseLabelRect = eventCloseLabel.GetComponent<RectTransform>();
            eventCloseLabelRect.anchorMin = Vector2.zero;
            eventCloseLabelRect.anchorMax = Vector2.one;
            eventCloseLabelRect.sizeDelta = Vector2.zero;
            eventCloseLabelRect.anchoredPosition = Vector2.zero;

            TextMeshProUGUI eventTitleLabel = CreateLabel(eventPanelRootObject.transform, "Title", 0f, "This Week's Event");
            eventTitleLabel.fontSize = 28f;
            eventTitleLabel.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, 340f);

            TextMeshProUGUI eventNameLabel = CreateLabel(eventPanelRootObject.transform, "NameLabel", 0f, string.Empty);
            eventNameLabel.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, 280f);

            TextMeshProUGUI eventNarrationLabel = CreateLabel(eventPanelRootObject.transform, "NarrationLabel", 0f, string.Empty);
            eventNarrationLabel.alignment = TextAlignmentOptions.Left;
            var eventNarrationLabelRect = eventNarrationLabel.GetComponent<RectTransform>();
            eventNarrationLabelRect.anchoredPosition = new Vector2(0f, 180f);
            eventNarrationLabelRect.sizeDelta = new Vector2(620f, 140f);

            TextMeshProUGUI eventProgressLabel = CreateLabel(eventPanelRootObject.transform, "ProgressLabel", 0f, string.Empty);
            eventProgressLabel.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, 60f);

            TextMeshProUGUI eventStatusMessageText = CreateLabel(eventPanelRootObject.transform, "StatusMessageText", 0f, string.Empty);
            var eventStatusMessageRect = eventStatusMessageText.GetComponent<RectTransform>();
            eventStatusMessageRect.anchoredPosition = new Vector2(0f, 10f);
            eventStatusMessageRect.sizeDelta = new Vector2(620f, 60f);

            var claimButtonObject = new GameObject("ClaimButton", typeof(Image), typeof(Button));
            claimButtonObject.transform.SetParent(eventPanelRootObject.transform, false);
            var claimButtonRect = claimButtonObject.GetComponent<RectTransform>();
            claimButtonRect.anchoredPosition = new Vector2(0f, -60f);
            claimButtonRect.sizeDelta = new Vector2(220f, 44f);
            claimButtonObject.GetComponent<Image>().color = new Color(0.3f, 0.5f, 0.7f, 1f);
            var claimButton = claimButtonObject.GetComponent<Button>();
            TextMeshProUGUI claimButtonLabel = CreateLabel(claimButtonObject.transform, "Text", 0f, "Claim Reward");
            var claimButtonLabelRect = claimButtonLabel.GetComponent<RectTransform>();
            claimButtonLabelRect.anchorMin = Vector2.zero;
            claimButtonLabelRect.anchorMax = Vector2.one;
            claimButtonLabelRect.offsetMin = new Vector2(40f, 0f);
            claimButtonLabelRect.offsetMax = Vector2.zero;

            var claimIconObject = new GameObject("Icon", typeof(Image));
            claimIconObject.transform.SetParent(claimButtonObject.transform, false);
            var claimIconRect = claimIconObject.GetComponent<RectTransform>();
            claimIconRect.anchorMin = new Vector2(0f, 0.5f);
            claimIconRect.anchorMax = new Vector2(0f, 0.5f);
            claimIconRect.pivot = new Vector2(0f, 0.5f);
            claimIconRect.anchoredPosition = new Vector2(8f, 0f);
            claimIconRect.sizeDelta = new Vector2(32f, 32f);
            var claimIconImage = claimIconObject.GetComponent<Image>();
            claimIconImage.sprite = LoadIconSprite("Assets/Art/ButtonIcons/claim.png");
            claimIconImage.raycastTarget = false;

            var eventControllerObject = new GameObject("EventPanelController");
            var eventController = eventControllerObject.AddComponent<EventPanelController>();
            eventController.Initialize(eventsButton, eventPanelRootObject, eventCloseButton, eventNameLabel, eventNarrationLabel,
                eventProgressLabel, eventStatusMessageText, claimButton, backendCoordinator, manager, controller,
                armySlider, tradeSlider, religionSlider, button, duelButton, viewHistoryButton, councilButton, customizeButton, duelModalGate,
                eventsIconImage, claimIconImage, estatePlaceholderButton);

            var councilPanelRootObject = new GameObject("CouncilPanel", typeof(Image));
            councilPanelRootObject.transform.SetParent(canvasObject.transform, false);
            var councilPanelRect = councilPanelRootObject.GetComponent<RectTransform>();
            councilPanelRect.anchoredPosition = Vector2.zero;
            councilPanelRect.sizeDelta = new Vector2(700f, 800f);
            councilPanelRootObject.GetComponent<Image>().color = new Color(0.1f, 0.1f, 0.15f, 0.95f);
            councilPanelRootObject.AddComponent<RectMask2D>();
            var councilArtObject = new GameObject("PanelArt", typeof(Image), typeof(AspectRatioFitter));
            councilArtObject.transform.SetParent(councilPanelRootObject.transform, false);
            var councilArtRect = councilArtObject.GetComponent<RectTransform>();
            councilArtRect.pivot = new Vector2(0.5f, 0.5f);
            var councilArtFitter = councilArtObject.GetComponent<AspectRatioFitter>();
            councilArtFitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            councilArtFitter.aspectRatio = 1024f / 1792f;
            var councilArtImage = councilArtObject.GetComponent<Image>();
            councilArtImage.preserveAspect = false;
            councilArtImage.raycastTarget = false;
            councilArtObject.transform.SetAsFirstSibling();

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
            createButtonLabelRect.offsetMin = new Vector2(40f, 0f);
            createButtonLabelRect.offsetMax = Vector2.zero;

            var createIconObject = new GameObject("Icon", typeof(Image));
            createIconObject.transform.SetParent(createButtonObject.transform, false);
            var createIconRect = createIconObject.GetComponent<RectTransform>();
            createIconRect.anchorMin = new Vector2(0f, 0.5f);
            createIconRect.anchorMax = new Vector2(0f, 0.5f);
            createIconRect.pivot = new Vector2(0f, 0.5f);
            createIconRect.anchoredPosition = new Vector2(8f, 0f);
            createIconRect.sizeDelta = new Vector2(32f, 32f);
            var createIconImage = createIconObject.GetComponent<Image>();
            createIconImage.sprite = LoadIconSprite("Assets/Art/ButtonIcons/create.png");
            createIconImage.raycastTarget = false;

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
            joinButtonLabelRect.offsetMin = new Vector2(40f, 0f);
            joinButtonLabelRect.offsetMax = Vector2.zero;

            var joinIconObject = new GameObject("Icon", typeof(Image));
            joinIconObject.transform.SetParent(joinButtonObject.transform, false);
            var joinIconRect = joinIconObject.GetComponent<RectTransform>();
            joinIconRect.anchorMin = new Vector2(0f, 0.5f);
            joinIconRect.anchorMax = new Vector2(0f, 0.5f);
            joinIconRect.pivot = new Vector2(0f, 0.5f);
            joinIconRect.anchoredPosition = new Vector2(8f, 0f);
            joinIconRect.sizeDelta = new Vector2(32f, 32f);
            var joinIconImage = joinIconObject.GetComponent<Image>();
            joinIconImage.sprite = LoadIconSprite("Assets/Art/ButtonIcons/join.png");
            joinIconImage.raycastTarget = false;

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
                armySlider, tradeSlider, religionSlider, button, duelButton, viewHistoryButton, eventsButton, customizeButton, duelModalGate, councilIconImage, createIconImage, joinIconImage, estatePlaceholderButton);

            var panelRootObject = new GameObject("HistoryPanel", typeof(Image));
            panelRootObject.transform.SetParent(canvasObject.transform, false);
            var panelRect = panelRootObject.GetComponent<RectTransform>();
            panelRect.anchoredPosition = Vector2.zero;
            panelRect.sizeDelta = new Vector2(700f, 800f);
            panelRootObject.GetComponent<Image>().color = new Color(0.1f, 0.1f, 0.15f, 0.95f);
            panelRootObject.AddComponent<RectMask2D>();
            var historyArtObject = new GameObject("PanelArt", typeof(Image), typeof(AspectRatioFitter));
            historyArtObject.transform.SetParent(panelRootObject.transform, false);
            var historyArtRect = historyArtObject.GetComponent<RectTransform>();
            historyArtRect.pivot = new Vector2(0.5f, 0.5f);
            var historyArtFitter = historyArtObject.GetComponent<AspectRatioFitter>();
            historyArtFitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            historyArtFitter.aspectRatio = 1024f / 1792f;
            var historyArtImage = historyArtObject.GetComponent<Image>();
            historyArtImage.preserveAspect = false;
            historyArtImage.raycastTarget = false;
            historyArtObject.transform.SetAsFirstSibling();

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
                armySlider, tradeSlider, religionSlider, button, duelButton, councilButton, eventsButton, customizeButton, duelModalGate, viewHistoryIconImage, estatePlaceholderButton);

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
            tutorialStepIndicatorLabel.fontSize = 24f;
            tutorialStepIndicatorLabel.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, 210f);

            TextMeshProUGUI tutorialTitleLabel = CreateLabel(tutorialBoxObject.transform, "TitleLabel", 0f, string.Empty);
            tutorialTitleLabel.fontSize = 28f;
            tutorialTitleLabel.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, 160f);

            TextMeshProUGUI tutorialBodyLabel = CreateLabel(tutorialBoxObject.transform, "BodyLabel", 0f, string.Empty);
            // Left-aligned, not CreateLabel's Center default -- multi-line
            // wrapped paragraph text reads noticeably worse center-aligned
            // (ragged, uneven line starts) than a short single-line label does.
            tutorialBodyLabel.alignment = TextAlignmentOptions.Left;
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
            tutorialSkipButtonLabelRect.offsetMin = new Vector2(40f, 0f);
            tutorialSkipButtonLabelRect.offsetMax = Vector2.zero;

            var tutorialSkipIconObject = new GameObject("Icon", typeof(Image));
            tutorialSkipIconObject.transform.SetParent(tutorialSkipButtonObject.transform, false);
            var tutorialSkipIconRect = tutorialSkipIconObject.GetComponent<RectTransform>();
            tutorialSkipIconRect.anchorMin = new Vector2(0f, 0.5f);
            tutorialSkipIconRect.anchorMax = new Vector2(0f, 0.5f);
            tutorialSkipIconRect.pivot = new Vector2(0f, 0.5f);
            tutorialSkipIconRect.anchoredPosition = new Vector2(8f, 0f);
            tutorialSkipIconRect.sizeDelta = new Vector2(32f, 32f);
            var tutorialSkipIconImage = tutorialSkipIconObject.GetComponent<Image>();
            tutorialSkipIconImage.sprite = LoadIconSprite("Assets/Art/ButtonIcons/skip.png");
            tutorialSkipIconImage.raycastTarget = false;

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
                armySlider, tradeSlider, religionSlider, button, duelButton, viewHistoryButton, councilButton, eventsButton, customizeButton, tutorialSkipIconImage, estatePlaceholderButton);

            var cosmeticsPanelRootObject = new GameObject("CosmeticsPanel", typeof(Image));
            cosmeticsPanelRootObject.transform.SetParent(canvasObject.transform, false);
            var cosmeticsPanelRect = cosmeticsPanelRootObject.GetComponent<RectTransform>();
            cosmeticsPanelRect.anchoredPosition = Vector2.zero;
            cosmeticsPanelRect.sizeDelta = new Vector2(700f, 800f);
            cosmeticsPanelRootObject.GetComponent<Image>().color = new Color(0.1f, 0.1f, 0.15f, 0.95f);

            var cosmeticsCloseButtonObject = new GameObject("CloseButton", typeof(Image), typeof(Button));
            cosmeticsCloseButtonObject.transform.SetParent(cosmeticsPanelRootObject.transform, false);
            var cosmeticsCloseButtonRect = cosmeticsCloseButtonObject.GetComponent<RectTransform>();
            cosmeticsCloseButtonRect.anchoredPosition = new Vector2(310f, 360f);
            cosmeticsCloseButtonRect.sizeDelta = new Vector2(60f, 44f);
            cosmeticsCloseButtonObject.GetComponent<Image>().color = new Color(0.6f, 0.3f, 0.3f, 1f);
            var cosmeticsCloseButton = cosmeticsCloseButtonObject.GetComponent<Button>();
            TextMeshProUGUI cosmeticsCloseLabel = CreateLabel(cosmeticsCloseButtonObject.transform, "Text", 0f, "X");
            var cosmeticsCloseLabelRect = cosmeticsCloseLabel.GetComponent<RectTransform>();
            cosmeticsCloseLabelRect.anchorMin = Vector2.zero;
            cosmeticsCloseLabelRect.anchorMax = Vector2.one;
            cosmeticsCloseLabelRect.sizeDelta = Vector2.zero;
            cosmeticsCloseLabelRect.anchoredPosition = Vector2.zero;

            TextMeshProUGUI cosmeticsTitleLabel = CreateLabel(cosmeticsPanelRootObject.transform, "Title", 0f, "Customize Your Court");
            cosmeticsTitleLabel.fontSize = 28f;
            cosmeticsTitleLabel.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, 340f);

            var themeStatusLabels = new TextMeshProUGUI[3];
            var themeApplyButtons = new Button[3];
            float[] themeRowY = { 240f, 140f, 40f };
            for (int i = 0; i < 3; i++)
            {
                TextMeshProUGUI statusLabel = CreateLabel(cosmeticsPanelRootObject.transform, $"ThemeStatusLabel{i}", 0f, string.Empty);
                statusLabel.alignment = TextAlignmentOptions.Left;
                var statusLabelRect = statusLabel.GetComponent<RectTransform>();
                statusLabelRect.sizeDelta = new Vector2(360f, 50f);
                statusLabelRect.anchoredPosition = new Vector2(-40f, themeRowY[i]);
                themeStatusLabels[i] = statusLabel;

                var applyButtonObject = new GameObject($"ApplyButton{i}", typeof(Image), typeof(Button));
                applyButtonObject.transform.SetParent(cosmeticsPanelRootObject.transform, false);
                var applyButtonRect = applyButtonObject.GetComponent<RectTransform>();
                applyButtonRect.anchoredPosition = new Vector2(270f, themeRowY[i]);
                applyButtonRect.sizeDelta = new Vector2(140f, 44f);
                applyButtonObject.GetComponent<Image>().color = new Color(0.3f, 0.5f, 0.7f, 1f);
                var applyButton = applyButtonObject.GetComponent<Button>();
                TextMeshProUGUI applyButtonLabel = CreateLabel(applyButtonObject.transform, "Text", 0f, "Apply");
                var applyButtonLabelRect = applyButtonLabel.GetComponent<RectTransform>();
                applyButtonLabelRect.anchorMin = Vector2.zero;
                applyButtonLabelRect.anchorMax = Vector2.one;
                applyButtonLabelRect.sizeDelta = Vector2.zero;
                applyButtonLabelRect.anchoredPosition = Vector2.zero;
                themeApplyButtons[i] = applyButton;
            }

            Sprite[] historyPanelSprites = LoadThemedSprites("Assets/Art/PanelArt", "history");
            Sprite[] councilPanelSprites = LoadThemedSprites("Assets/Art/PanelArt", "council");
            Sprite[] eventPanelSprites = LoadThemedSprites("Assets/Art/PanelArt", "event", 2);

            var cosmeticsControllerObject = new GameObject("CosmeticsPanelController");
            var cosmeticsController = cosmeticsControllerObject.AddComponent<CosmeticsPanelController>();
            cosmeticsController.Initialize(customizeButton, cosmeticsPanelRootObject, cosmeticsCloseButton,
                themeStatusLabels, themeApplyButtons,
                eventPanelRootObject.GetComponent<Image>(), councilPanelRootObject.GetComponent<Image>(), panelRootObject.GetComponent<Image>(),
                manager, armySlider, tradeSlider, religionSlider, button, duelButton, viewHistoryButton, councilButton, eventsButton, duelModalGate,
                sceneBackgroundImage, backgroundSprites,
                historyPanelSprites, councilPanelSprites, eventPanelSprites,
                historyArtImage, councilArtImage, eventArtImage, customizeIconImage, estatePlaceholderButton);

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

            FieldInfo portraitsField = typeof(CoreLoopScreenController).GetField("rulerPortraits", BindingFlags.NonPublic | BindingFlags.Instance);
            var rulerPortraits = (Sprite[])portraitsField.GetValue(controller);
            if (rulerPortraits == null || rulerPortraits.Length != 15)
            {
                Debug.LogError($"CoreLoopSceneBuilder.Verify: expected a 15-element rulerPortraits array, found {(rulerPortraits == null ? "null" : rulerPortraits.Length.ToString())}.");
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(1);
                }
                return;
            }
            for (int j = 0; j < rulerPortraits.Length; j++)
            {
                if (rulerPortraits[j] == null)
                {
                    Debug.LogError($"CoreLoopSceneBuilder.Verify: rulerPortraits[{j}] is null.");
                    if (Application.isBatchMode)
                    {
                        EditorApplication.Exit(1);
                    }
                    return;
                }
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

            var eventController = Object.FindFirstObjectByType<EventPanelController>();
            if (eventController == null)
            {
                Debug.LogError("CoreLoopSceneBuilder.Verify: no EventPanelController found in the scene.");
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(1);
                }
                return;
            }

            var cosmeticsController = Object.FindFirstObjectByType<CosmeticsPanelController>();
            if (cosmeticsController == null)
            {
                Debug.LogError("CoreLoopSceneBuilder.Verify: no CosmeticsPanelController found in the scene.");
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(1);
                }
                return;
            }

            if (!VerifyIconField(eventController, "eventsIcon"))
            {
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(1);
                }
                return;
            }
            if (!VerifyIconField(eventController, "claimIcon"))
            {
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(1);
                }
                return;
            }
            if (!VerifyIconField(cosmeticsController, "customizeIcon"))
            {
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(1);
                }
                return;
            }

            FieldInfo sceneBackgroundImageField = typeof(CosmeticsPanelController).GetField("sceneBackgroundImage", BindingFlags.NonPublic | BindingFlags.Instance);
            if (sceneBackgroundImageField == null)
            {
                Debug.LogError("CoreLoopSceneBuilder.Verify: CosmeticsPanelController has no private sceneBackgroundImage field (renamed?).");
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(1);
                }
                return;
            }
            var sceneBackgroundImage = (Image)sceneBackgroundImageField.GetValue(cosmeticsController);
            if (sceneBackgroundImage == null)
            {
                Debug.LogError("CoreLoopSceneBuilder.Verify: CosmeticsPanelController's sceneBackgroundImage is null.");
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(1);
                }
                return;
            }

            FieldInfo backgroundSpritesField = typeof(CosmeticsPanelController).GetField("backgroundSprites", BindingFlags.NonPublic | BindingFlags.Instance);
            if (backgroundSpritesField == null)
            {
                Debug.LogError("CoreLoopSceneBuilder.Verify: CosmeticsPanelController has no private backgroundSprites field (renamed?).");
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(1);
                }
                return;
            }
            var backgroundSprites = (Sprite[])backgroundSpritesField.GetValue(cosmeticsController);
            if (backgroundSprites == null || backgroundSprites.Length != 3)
            {
                Debug.LogError($"CoreLoopSceneBuilder.Verify: expected a 3-element backgroundSprites array, found {(backgroundSprites == null ? "null" : backgroundSprites.Length.ToString())}.");
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(1);
                }
                return;
            }
            for (int j = 0; j < backgroundSprites.Length; j++)
            {
                if (backgroundSprites[j] == null)
                {
                    Debug.LogError($"CoreLoopSceneBuilder.Verify: backgroundSprites[{j}] is null.");
                    if (Application.isBatchMode)
                    {
                        EditorApplication.Exit(1);
                    }
                    return;
                }
            }

            // Panel-art arrays deliberately get presence/length checks only, NOT
            // the per-element null loop backgroundSprites gets above --
            // eventPanelSprites[2] (event_event.png) is intentionally missing
            // this pass (see docs/superpowers/specs/2026-09-06-panel-art-design.md),
            // so a per-element check would fail Verify() on a correctly-shipped
            // state.
            if (!TryGetThemedSpriteArray(cosmeticsController, "historyPanelSprites", out var historyPanelSprites))
            {
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(1);
                }
                return;
            }
            if (historyPanelSprites[0] == null || historyPanelSprites[1] == null || historyPanelSprites[2] == null)
            {
                Debug.LogError("CoreLoopSceneBuilder.Verify: historyPanelSprites has an unexpected null element.");
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(1);
                }
                return;
            }

            if (!TryGetThemedSpriteArray(cosmeticsController, "councilPanelSprites", out var councilPanelSprites))
            {
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(1);
                }
                return;
            }
            if (councilPanelSprites[0] == null || councilPanelSprites[1] == null || councilPanelSprites[2] == null)
            {
                Debug.LogError("CoreLoopSceneBuilder.Verify: councilPanelSprites has an unexpected null element.");
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(1);
                }
                return;
            }

            if (!TryGetThemedSpriteArray(cosmeticsController, "eventPanelSprites", out var eventPanelSprites))
            {
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(1);
                }
                return;
            }
            if (eventPanelSprites[0] == null || eventPanelSprites[1] == null)
            {
                Debug.LogError("CoreLoopSceneBuilder.Verify: eventPanelSprites has an unexpected null element (checking only indices 0-1; index 2 -- event_event.png -- is deliberately missing this pass, see docs/superpowers/specs/2026-09-06-panel-art-design.md).");
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(1);
                }
                return;
            }

            var coreLoopController = Object.FindFirstObjectByType<CoreLoopScreenController>();
            if (coreLoopController == null)
            {
                Debug.LogError("CoreLoopSceneBuilder.Verify: no CoreLoopScreenController found in the scene.");
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(1);
                }
                return;
            }
            if (!VerifyIconField(coreLoopController, "submitIcon"))
            {
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(1);
                }
                return;
            }

            var duelButtonController = Object.FindFirstObjectByType<DuelButtonController>();
            if (duelButtonController == null)
            {
                Debug.LogError("CoreLoopSceneBuilder.Verify: no DuelButtonController found in the scene.");
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(1);
                }
                return;
            }
            if (!VerifyIconField(duelButtonController, "challengeIcon"))
            {
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(1);
                }
                return;
            }

            var historyPanelController = Object.FindFirstObjectByType<HistoryPanelController>();
            if (historyPanelController == null)
            {
                Debug.LogError("CoreLoopSceneBuilder.Verify: no HistoryPanelController found in the scene.");
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(1);
                }
                return;
            }
            if (!VerifyIconField(historyPanelController, "viewHistoryIcon"))
            {
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(1);
                }
                return;
            }

            var councilPanelController = Object.FindFirstObjectByType<CouncilPanelController>();
            if (councilPanelController == null)
            {
                Debug.LogError("CoreLoopSceneBuilder.Verify: no CouncilPanelController found in the scene.");
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(1);
                }
                return;
            }
            if (!VerifyIconField(councilPanelController, "councilIcon"))
            {
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(1);
                }
                return;
            }
            if (!VerifyIconField(councilPanelController, "createIcon"))
            {
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(1);
                }
                return;
            }
            if (!VerifyIconField(councilPanelController, "joinIcon"))
            {
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(1);
                }
                return;
            }

            var tutorialOverlayController = Object.FindFirstObjectByType<TutorialOverlayController>();
            if (tutorialOverlayController == null)
            {
                Debug.LogError("CoreLoopSceneBuilder.Verify: no TutorialOverlayController found in the scene.");
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(1);
                }
                return;
            }
            if (!VerifyIconField(tutorialOverlayController, "skipIcon"))
            {
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(1);
                }
                return;
            }

            Debug.Log("CoreLoopSceneBuilder.Verify: scene opened and controller found successfully.");
        }

        // Confirms an icon Image field is non-null AND its .sprite is non-null --
        // icons have no fallback/intentionally-missing slot (unlike the themed
        // panel art), so both checks apply here.
        private static bool VerifyIconField(Component controller, string fieldName)
        {
            FieldInfo field = controller.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null)
            {
                Debug.LogError($"CoreLoopSceneBuilder.Verify: {controller.GetType().Name} has no private {fieldName} field (renamed?).");
                return false;
            }
            var image = (Image)field.GetValue(controller);
            if (image == null)
            {
                Debug.LogError($"CoreLoopSceneBuilder.Verify: {controller.GetType().Name}.{fieldName} is null.");
                return false;
            }
            if (image.sprite == null)
            {
                Debug.LogError($"CoreLoopSceneBuilder.Verify: {controller.GetType().Name}.{fieldName}'s Image has a null sprite.");
                return false;
            }
            return true;
        }

        private static bool TryGetThemedSpriteArray(CosmeticsPanelController controller, string fieldName, out Sprite[] array)
        {
            FieldInfo field = typeof(CosmeticsPanelController).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null)
            {
                Debug.LogError($"CoreLoopSceneBuilder.Verify: CosmeticsPanelController has no private {fieldName} field (renamed?).");
                array = null;
                return false;
            }
            array = (Sprite[])field.GetValue(controller);
            if (array == null || array.Length != 3)
            {
                Debug.LogError($"CoreLoopSceneBuilder.Verify: expected a 3-element {fieldName} array, found {(array == null ? "null" : array.Length.ToString())}.");
                return false;
            }
            return true;
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

        // RULE, established after 3 separate readability bugs (milestones #6, #7,
        // #8) all caught only by manual Play Mode testing, never by automated
        // review: every label in this scene defaults to 24pt (this method's own
        // fontSize below). Never override it smaller "just for this one small
        // label" -- 18-22pt has repeatedly looked fine in isolation while
        // reading noticeably worse than everything around it once rendered for
        // real. Only 28pt (panel/section titles) is an established, deliberate
        // exception to the 24pt default. If a new label genuinely needs a
        // different size, say so explicitly in a comment at the override site,
        // the way this one does -- don't silently drop below 24.
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
            // A shared outline material (not per-instance outlineWidth/
            // outlineColor, which only set shader properties without
            // enabling TMP's OUTLINE_ON keyword -- the outline silently
            // never rendered, and created one unique material instance per
            // label, breaking UI batching) keeps white text readable
            // regardless of which scene background theme is active -- the
            // pre-fix baseline measured ~3.8:1 against the Harvest Hall
            // background without an outline, below WCAG AA's 4.5:1 minimum
            // (not re-measured against the current outline width; this is
            // TMP's own shipped outline preset for this font, already
            // carrying OUTLINE_ON, not a value this project chose/tuned).
            const string outlineMaterialPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Outline.mat";
            var outlineMaterial = AssetDatabase.LoadAssetAtPath<Material>(outlineMaterialPath);
            if (outlineMaterial != null)
            {
                label.fontSharedMaterial = outlineMaterial;
            }
            else
            {
                Debug.LogError($"CoreLoopSceneBuilder.CreateLabel: failed to load outline material at {outlineMaterialPath} -- '{name}' will render without the WCAG-contrast outline.");
            }

            return label;
        }

        // The 15 portrait PNGs were added to the project as raw files (not
        // through Unity's asset-creation menu), so their TextureImporter
        // defaults to Default (Texture2D), not Sprite -- AssetDatabase.
        // LoadAssetAtPath<Sprite> silently returns null for a Default-typed
        // texture. This fixes the import type once (idempotent -- skips the
        // reimport if it's already correct) before loading.
        private static Sprite LoadPortraitSprite(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null && importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        // Mood-major order matches CoreLoopScreenController.RefreshStatusLabels()'s
        // GetMoodTier(mood) * 3 + GetLoyaltyTier(loyalty) indexing exactly.
        private static Sprite[] LoadRulerPortraits()
        {
            string[] moods = { "furious", "displeased", "neutral", "pleased", "delighted" };
            string[] loyalties = { "low", "medium", "high" };
            var portraits = new Sprite[15];
            int i = 0;
            foreach (string mood in moods)
            {
                foreach (string loyalty in loyalties)
                {
                    string path = $"Assets/Art/RulerPortraits/ruler_{mood}_{loyalty}.png";
                    portraits[i] = LoadPortraitSprite(path);
                    i++;
                }
            }

            for (int j = 0; j < portraits.Length; j++)
            {
                if (portraits[j] == null)
                {
                    Debug.LogError($"CoreLoopSceneBuilder.LoadRulerPortraits: failed to load portrait sprite at index {j}");
                }
            }

            return portraits;
        }

        private static Sprite LoadThemedSprite(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null && importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        // Order matches the existing Themes array exactly (Default, Council,
        // Event) -- CosmeticsPanelController.GetBackgroundSprite indexes into
        // this array using the same Themes array as its lookup key. Generic
        // over folder/fileNamePrefix so it serves the scene background AND
        // all three panel-art sets (history/council/event) -- see
        // docs/superpowers/specs/2026-09-06-panel-art-design.md's Loader
        // consolidation section. A missing file (event_event.png is
        // deliberately not committed -- see that spec's Scope Decisions) logs
        // an error here and leaves that index null; CosmeticsPanelController's
        // existing GetBackgroundSprite falls back to index 0 for it.
        private static Sprite[] LoadThemedSprites(string folder, string fileNamePrefix, params int[] optionalIndices)
        {
            string[] themeIds = { "default", "council", "event" };
            var sprites = new Sprite[3];
            for (int i = 0; i < themeIds.Length; i++)
            {
                sprites[i] = LoadThemedSprite($"{folder}/{fileNamePrefix}_{themeIds[i]}.png");
            }
            for (int i = 0; i < sprites.Length; i++)
            {
                if (sprites[i] == null)
                {
                    if (System.Array.IndexOf(optionalIndices, i) >= 0)
                    {
                        Debug.LogWarning($"CoreLoopSceneBuilder.LoadThemedSprites: sprite at index {i} for {fileNamePrefix} in {folder} is intentionally missing.");
                    }
                    else
                    {
                        Debug.LogError($"CoreLoopSceneBuilder.LoadThemedSprites: failed to load sprite at index {i} for {fileNamePrefix} in {folder}");
                    }
                }
            }
            return sprites;
        }

        // Icons are static (no theme/state variation), so this is a single-file
        // loader, not the array-based LoadThemedSprites shape -- mirrors how
        // LoadPortraitSprite/LoadRulerPortraits coexist as their own shape for
        // their own differently-keyed asset set.
        private static Sprite LoadIconSprite(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null && importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.SaveAndReimport();
            }
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
            {
                Debug.LogError($"CoreLoopSceneBuilder.LoadIconSprite: failed to load icon sprite at {path}");
            }
            return sprite;
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
