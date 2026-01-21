using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using System;
using UnityEngine.Rendering;
using System.Collections.Generic;
using System.Linq;

[CustomEditor(typeof(SamplesShowcase))]
public class SamplesShowcaseEditor : Editor
{
    private SerializedProperty currentIndexProp;
    private VisualTreeAsset inspectorTemplate;
    private VisualElement root;

    private const string InspectorUXMLPath = "Assets/Editor/SamplesShowcaseInspector.uxml";
    private const string JSONSamplePath = "Assets/SamplesDescriptions.json";

    private void OnEnable()
    {
        currentIndexProp = serializedObject.FindProperty("currentIndex");
        inspectorTemplate = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(InspectorUXMLPath);

        if (inspectorTemplate == null)
        {
            Debug.LogError($"Failed to load Inspector UXML at path: {InspectorUXMLPath}");
        }

        RenderPipelineManager.activeRenderPipelineTypeChanged += OnRenderPipelineChanged;
    }

    private void OnDisable()
    {
        RenderPipelineManager.activeRenderPipelineTypeChanged -= OnRenderPipelineChanged;
    }

    public override VisualElement CreateInspectorGUI()
    {
        root = new VisualElement();

        if (inspectorTemplate != null)
        {
            inspectorTemplate.CloneTree(root);

            var dropdown = root.Q<DropdownField>("SamplesDropdown");
            var switchBackButton = root.Q<Button>("SwitchBackButton");
            var switchForwardButton = root.Q<Button>("SwitchForwardButton");

            ConfigureDropdown(dropdown);
            ConfigureNavigationButtons(switchBackButton, switchForwardButton);

            UpdatePipelineWarning(root);
        }
        else
        {
            root.Add(new Label("Inspector template could not be loaded."));
        }

        return root;
    }

    private void ConfigureDropdown(DropdownField dropdown)
    {
        if (dropdown == null) return;

        dropdown.choices = SamplesDataLoader.LoadSampleNames(JSONSamplePath);
        dropdown.RegisterValueChangedCallback(evt =>
        {
            currentIndexProp.intValue = dropdown.choices.IndexOf(evt.newValue);
            serializedObject.ApplyModifiedProperties();
        });

        dropdown.index = currentIndexProp.intValue;
    }

    private void ConfigureNavigationButtons(Button backButton, Button forwardButton)
    {
        if (backButton != null)
        {
            backButton.clicked += () => NavigateSamples(-1);
        }

        if (forwardButton != null)
        {
            forwardButton.clicked += () => NavigateSamples(1);
        }
    }

    private void NavigateSamples(int direction)
    {
        serializedObject.Update();

        int totalSamples = SamplesDataLoader.LoadSampleNames(JSONSamplePath).Count;
        currentIndexProp.intValue = (currentIndexProp.intValue + direction + totalSamples) % totalSamples;
        serializedObject.ApplyModifiedProperties();

        var dropdown = root.Q<DropdownField>("SamplesDropdown");
        if (dropdown != null)
        {
            dropdown.index = currentIndexProp.intValue;
        }
    }

    private void UpdatePipelineWarning(VisualElement root)
    {
        var warningLabel = root.Q<Label>("PipelineWarningLabel");

        if (warningLabel == null) return;

        string activePipeline = GraphicsSettings.currentRenderPipeline != null
            ? GraphicsSettings.currentRenderPipeline.name
            : "Built-in Render Pipeline";

        warningLabel.text = $"Active Render Pipeline: {activePipeline}";
    }

    private void OnRenderPipelineChanged()
    {
        if (root != null)
        {
            UpdatePipelineWarning(root);
        }
    }
}

public static class SamplesDataLoader
{
    public static List<string> LoadSampleNames(string jsonFilePath)
    {
        if (!System.IO.File.Exists(jsonFilePath))
        {
            Debug.LogError($"JSON file not found at path: {jsonFilePath}");
            return new List<string>();
        }

        string jsonContent = System.IO.File.ReadAllText(jsonFilePath);

        try
        {
            SamplesDescriptionsJson samplesJson = JsonUtility.FromJson<SamplesDescriptionsJson>(jsonContent);
            return samplesJson.samples.Select(sample => sample.name).ToList();
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error parsing JSON file: {ex.Message}");
            return new List<string>();
        }
    }
}

[Serializable]
public class SamplesDescriptionsJson
{
    public SampleDescription[] samples;
}

[Serializable]
public class SampleDescription
{
    public string name;
    public string description;
}
