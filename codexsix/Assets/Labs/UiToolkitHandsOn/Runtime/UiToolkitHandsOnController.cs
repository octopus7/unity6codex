using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UIElements;

namespace CodexSix.UiToolkit.HandsOn
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UIDocument))]
    public sealed class UiToolkitHandsOnController : MonoBehaviour
    {
        private const string RootTemplatePath = "UiToolkitHandsOn/UiToolkitHandsOnRoot";
        private const string RootStylePath = "UiToolkitHandsOn/UiToolkitHandsOnStyles";

        private static readonly LessonDefinition[] Lessons =
        {
            new LessonDefinition(
                "Step 1 - Query and Click Event",
                "Query UXML controls by name and update state in a click callback.",
                "- Change the button text in UXML.\n- Add one extra Label and query it from C#.\n- Start the counter from 10 instead of 0.",
                "UiToolkitHandsOn/Lessons/Lesson01_QueryAndClick"),
            new LessonDefinition(
                "Step 2 - Value Changed Callback",
                "React to TextField and SliderInt changes, then sync ProgressBar and preview text.",
                "- Add one more input control in UXML.\n- Extend preview text composition in C#.\n- Clamp slider value range and observe UI updates.",
                "UiToolkitHandsOn/Lessons/Lesson02_ValueChanged"),
            new LessonDefinition(
                "Step 3 - Runtime List and Filter",
                "Generate button rows at runtime and filter them by keyword.",
                "- Change item seed data in C#.\n- Style item buttons in USS.\n- Add a second filter rule (for example, minimum length).",
                "UiToolkitHandsOn/Lessons/Lesson03_RuntimeList")
        };

        [SerializeField] private UIDocument _uiDocument;

        private readonly List<string> _seedItems = new List<string>
        {
            "Potion",
            "Shield",
            "Arrow Bundle",
            "Energy Bar",
            "Boots",
            "Flash Bomb",
            "Repair Kit",
            "Med Pack"
        };

        private PanelSettings _runtimePanelSettings;
        private int _currentLessonIndex;
        private int _clickCount;

        private Label _lessonProgressLabel;
        private Label _lessonTitleLabel;
        private Label _lessonObjectiveLabel;
        private Label _lessonChecklistLabel;
        private VisualElement _lessonStageRoot;
        private Button _previousButton;
        private Button _resetButton;
        private Button _nextButton;

        private void Awake()
        {
            EnsureDocumentSetup();
        }

        private void OnEnable()
        {
            BuildLabUi();
        }

        private void OnDisable()
        {
            UnregisterNavigationEvents();
        }

        private void OnDestroy()
        {
            if (_runtimePanelSettings == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(_runtimePanelSettings);
            }
            else
            {
                DestroyImmediate(_runtimePanelSettings);
            }
        }

        private void EnsureDocumentSetup()
        {
            if (_uiDocument == null)
            {
                _uiDocument = GetComponent<UIDocument>();
            }

            if (_uiDocument.panelSettings == null)
            {
                _runtimePanelSettings = CreateRuntimePanelSettings();
                if (_runtimePanelSettings != null)
                {
                    _uiDocument.panelSettings = _runtimePanelSettings;
                }
            }

            if (_uiDocument.visualTreeAsset == null)
            {
                _uiDocument.visualTreeAsset = Resources.Load<VisualTreeAsset>(RootTemplatePath);
            }
        }

        private static PanelSettings CreateRuntimePanelSettings()
        {
            var panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            panelSettings.clearColor = true;

            var defaultTheme = TryResolveEditorDefaultTheme();
            if (defaultTheme == null)
            {
                Debug.LogWarning(
                    "UI Toolkit HandsOn: Could not resolve a default Theme Style Sheet for runtime-created PanelSettings. " +
                    "Create a PanelSettings asset with a Theme Style Sheet and assign it to the UIDocument for reliable text rendering.");
                return panelSettings;
            }

            panelSettings.themeStyleSheet = defaultTheme;
            return panelSettings;
        }

        private static ThemeStyleSheet TryResolveEditorDefaultTheme()
        {
#if UNITY_EDITOR
            const BindingFlags Flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

            var creatorType = Type.GetType("UnityEditor.UIElements.PanelSettingsCreator, UnityEditor");
            if (creatorType == null)
            {
                return null;
            }

            var getter = creatorType.GetMethod("GetFirstThemeOrCreateDefaultTheme", Flags)
                         ?? creatorType.GetMethod("CreateDefaultTheme", Flags);
            if (getter == null)
            {
                return null;
            }

            return getter.Invoke(null, null) as ThemeStyleSheet;
#else
            return null;
#endif
        }

        private void BuildLabUi()
        {
            if (_uiDocument == null)
            {
                return;
            }

            var root = _uiDocument.rootVisualElement;
            if (root == null)
            {
                return;
            }

            root.Clear();

            var rootTemplate = Resources.Load<VisualTreeAsset>(RootTemplatePath);
            if (rootTemplate != null)
            {
                rootTemplate.CloneTree(root);
            }
            else
            {
                BuildFallbackRoot(root);
            }

            var rootStyle = Resources.Load<StyleSheet>(RootStylePath);
            if (rootStyle != null)
            {
                root.styleSheets.Add(rootStyle);
            }

            BindRootControls(root);
            RegisterNavigationEvents();
            ShowLesson(_currentLessonIndex);
        }

        private void BindRootControls(VisualElement root)
        {
            _lessonProgressLabel = root.Q<Label>("lesson-progress");
            _lessonTitleLabel = root.Q<Label>("lesson-title");
            _lessonObjectiveLabel = root.Q<Label>("lesson-objective");
            _lessonChecklistLabel = root.Q<Label>("lesson-checklist");
            _lessonStageRoot = root.Q<VisualElement>("lesson-stage");
            _previousButton = root.Q<Button>("lesson-prev");
            _resetButton = root.Q<Button>("lesson-reset");
            _nextButton = root.Q<Button>("lesson-next");
        }

        private void RegisterNavigationEvents()
        {
            if (_previousButton != null)
            {
                _previousButton.clicked += ShowPreviousLesson;
            }

            if (_resetButton != null)
            {
                _resetButton.clicked += ResetCurrentLesson;
            }

            if (_nextButton != null)
            {
                _nextButton.clicked += ShowNextLesson;
            }
        }

        private void UnregisterNavigationEvents()
        {
            if (_previousButton != null)
            {
                _previousButton.clicked -= ShowPreviousLesson;
            }

            if (_resetButton != null)
            {
                _resetButton.clicked -= ResetCurrentLesson;
            }

            if (_nextButton != null)
            {
                _nextButton.clicked -= ShowNextLesson;
            }
        }

        private void ShowPreviousLesson()
        {
            ShowLesson(_currentLessonIndex - 1);
        }

        private void ShowNextLesson()
        {
            ShowLesson(_currentLessonIndex + 1);
        }

        private void ResetCurrentLesson()
        {
            ShowLesson(_currentLessonIndex);
        }

        private void ShowLesson(int requestedIndex)
        {
            if (Lessons.Length == 0 || _lessonStageRoot == null)
            {
                return;
            }

            _currentLessonIndex = Mathf.Clamp(requestedIndex, 0, Lessons.Length - 1);
            var lesson = Lessons[_currentLessonIndex];

            if (_lessonProgressLabel != null)
            {
                _lessonProgressLabel.text = $"Step {_currentLessonIndex + 1}/{Lessons.Length}";
            }

            if (_lessonTitleLabel != null)
            {
                _lessonTitleLabel.text = lesson.Title;
            }

            if (_lessonObjectiveLabel != null)
            {
                _lessonObjectiveLabel.text = lesson.Objective;
            }

            if (_lessonChecklistLabel != null)
            {
                _lessonChecklistLabel.text = lesson.Checklist;
            }

            if (_previousButton != null)
            {
                _previousButton.SetEnabled(_currentLessonIndex > 0);
            }

            if (_nextButton != null)
            {
                _nextButton.SetEnabled(_currentLessonIndex < Lessons.Length - 1);
            }

            _lessonStageRoot.Clear();

            var template = Resources.Load<VisualTreeAsset>(lesson.TemplateResourcePath);
            if (template != null)
            {
                template.CloneTree(_lessonStageRoot);
            }
            else
            {
                _lessonStageRoot.Add(new Label($"Missing lesson template: {lesson.TemplateResourcePath}"));
            }

            ConfigureLessonBehavior(_currentLessonIndex);
        }

        private void ConfigureLessonBehavior(int lessonIndex)
        {
            switch (lessonIndex)
            {
                case 0:
                    ConfigureStepOne();
                    break;
                case 1:
                    ConfigureStepTwo();
                    break;
                case 2:
                    ConfigureStepThree();
                    break;
            }
        }

        private void ConfigureStepOne()
        {
            _clickCount = 0;

            var counterLabel = _lessonStageRoot.Q<Label>("counter-value");
            var counterButton = _lessonStageRoot.Q<Button>("counter-button");

            if (counterLabel != null)
            {
                counterLabel.text = "Click count: 0";
            }

            if (counterButton == null)
            {
                return;
            }

            counterButton.clicked += () =>
            {
                _clickCount++;
                if (counterLabel != null)
                {
                    counterLabel.text = $"Click count: {_clickCount}";
                }
            };
        }

        private void ConfigureStepTwo()
        {
            var playerNameField = _lessonStageRoot.Q<TextField>("player-name-field");
            var powerSlider = _lessonStageRoot.Q<SliderInt>("power-slider");
            var powerBar = _lessonStageRoot.Q<ProgressBar>("power-bar");
            var previewLabel = _lessonStageRoot.Q<Label>("preview-label");

            if (powerBar != null && powerSlider != null)
            {
                powerBar.lowValue = powerSlider.lowValue;
                powerBar.highValue = powerSlider.highValue;
                powerBar.value = powerSlider.value;
                powerBar.title = $"Power {powerSlider.value}";
            }

            void RefreshPreview()
            {
                if (previewLabel == null)
                {
                    return;
                }

                var playerName = playerNameField != null && !string.IsNullOrWhiteSpace(playerNameField.value)
                    ? playerNameField.value
                    : "Unnamed";

                var power = powerSlider != null ? powerSlider.value : 0;
                previewLabel.text = $"Preview: {playerName} / Power {power}";
            }

            if (playerNameField != null)
            {
                playerNameField.RegisterValueChangedCallback(_ => RefreshPreview());
            }

            if (powerSlider != null)
            {
                powerSlider.RegisterValueChangedCallback(evt =>
                {
                    if (powerBar != null)
                    {
                        powerBar.value = evt.newValue;
                        powerBar.title = $"Power {evt.newValue}";
                    }

                    RefreshPreview();
                });
            }

            RefreshPreview();
        }

        private void ConfigureStepThree()
        {
            var filterField = _lessonStageRoot.Q<TextField>("item-filter-field");
            var scrollView = _lessonStageRoot.Q<ScrollView>("item-scroll-view");
            var selectedItemLabel = _lessonStageRoot.Q<Label>("item-selected-label");

            if (scrollView == null)
            {
                return;
            }

            if (selectedItemLabel != null)
            {
                selectedItemLabel.text = "Selected item: none";
            }

            void DrawItems(string keyword)
            {
                scrollView.Clear();

                var normalizedKeyword = string.IsNullOrWhiteSpace(keyword)
                    ? string.Empty
                    : keyword.Trim().ToLowerInvariant();

                var matchedCount = 0;
                for (var i = 0; i < _seedItems.Count; i++)
                {
                    var itemName = _seedItems[i];
                    if (!string.IsNullOrEmpty(normalizedKeyword) &&
                        itemName.IndexOf(normalizedKeyword, StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        continue;
                    }

                    matchedCount++;
                    var itemButton = new Button
                    {
                        text = itemName
                    };
                    itemButton.AddToClassList("inventory-item-button");
                    itemButton.clicked += () =>
                    {
                        if (selectedItemLabel != null)
                        {
                            selectedItemLabel.text = $"Selected item: {itemName}";
                        }
                    };

                    scrollView.Add(itemButton);
                }

                if (matchedCount == 0)
                {
                    scrollView.Add(new Label("No items matched the current filter."));
                }
            }

            if (filterField != null)
            {
                filterField.RegisterValueChangedCallback(evt => DrawItems(evt.newValue));
                DrawItems(filterField.value);
            }
            else
            {
                DrawItems(string.Empty);
            }
        }

        private static void BuildFallbackRoot(VisualElement root)
        {
            var container = new VisualElement { name = "lab-root" };
            root.Add(container);

            var progress = new Label("Step 1/3") { name = "lesson-progress" };
            var title = new Label("UI Toolkit Hands-On Lab") { name = "lesson-title" };
            var objective = new Label("Objective") { name = "lesson-objective" };
            var checklist = new Label("Checklist") { name = "lesson-checklist" };
            var stage = new VisualElement { name = "lesson-stage" };

            var footer = new VisualElement();
            var previous = new Button { name = "lesson-prev", text = "Previous" };
            var reset = new Button { name = "lesson-reset", text = "Reset Step" };
            var next = new Button { name = "lesson-next", text = "Next" };

            footer.Add(previous);
            footer.Add(reset);
            footer.Add(next);

            container.Add(progress);
            container.Add(title);
            container.Add(objective);
            container.Add(checklist);
            container.Add(stage);
            container.Add(footer);
        }

        private readonly struct LessonDefinition
        {
            public LessonDefinition(string title, string objective, string checklist, string templateResourcePath)
            {
                Title = title;
                Objective = objective;
                Checklist = checklist;
                TemplateResourcePath = templateResourcePath;
            }

            public string Title { get; }
            public string Objective { get; }
            public string Checklist { get; }
            public string TemplateResourcePath { get; }
        }
    }
}
