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
            canvasScaler.referenceResolution = new Vector2(800f, 1400f);
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
                armySlider, tradeSlider, religionSlider, button, duelButton);

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
    }
}
