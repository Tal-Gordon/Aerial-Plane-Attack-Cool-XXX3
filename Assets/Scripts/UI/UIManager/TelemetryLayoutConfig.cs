using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Data-driven description of the telemetry window: which sections exist, which
/// widget prefabs they contain and in what order. One asset per scene (or one
/// shared default) is the single place to decide what gets built — reorder the
/// lists or untick <c>enabled</c> flags instead of editing scene hierarchies.
///
/// <para>Chrome (window frame, scroll view, section headers) is built from code
/// with the style values below. If you'd rather keep hand-authored visuals, assign
/// <see cref="windowChromePrefab"/> / <see cref="sectionPrefab"/> and the builder
/// instantiates those instead, only populating them.</para>
/// </summary>
[CreateAssetMenu(fileName = "TelemetryLayout", menuName = "UI/Telemetry Layout Config")]
public class TelemetryLayoutConfig : ScriptableObject
{
    [Serializable]
    public class WidgetEntry
    {
        public UIWidget prefab;
        [Tooltip("Untick to skip this widget without removing it from the list.")]
        public bool enabled = true;
    }

    [Serializable]
    public class SectionDefinition
    {
        public string title = "Section";
        [Tooltip("Untick to skip this whole section without removing it from the list.")]
        public bool enabled = true;
        public bool startFolded = false;
        public List<WidgetEntry> widgets = new List<WidgetEntry>();
    }

    [Header("Sections (built top to bottom)")]
    public List<SectionDefinition> sections = new List<SectionDefinition>();

    [Header("Window")]
    public string windowTitle = "Telemetry";
    public Vector2 windowSize = new Vector2(380f, 640f);
    [Tooltip("Anchored offset of the window from the canvas' top-right corner.")]
    public Vector2 windowOffset = new Vector2(-16f, -16f);

    [Header("Optional hand-authored chrome (leave empty for code-built)")]
    [Tooltip("Window shell prefab. Must contain a ScrollRect — sections are instantiated under its content.")]
    public TelemetryWindow windowChromePrefab;
    [Tooltip("Section shell prefab. Its FoldContent is used as the widget container.")]
    public UISection sectionPrefab;
    [Tooltip("Separator instantiated between widgets inside a section.")]
    public GameObject widgetSeparatorPrefab;

    [Header("Code-built chrome style")]
    public Color windowBackground = new Color(0.13f, 0.14f, 0.17f, 0.94f);
    public Color titleBarColor = new Color(0.08f, 0.11f, 0.16f, 1f);
    public Color sectionHeaderColor = new Color(0.16f, 0.22f, 0.32f, 1f);
    public Color textColor = new Color(0.85f, 0.87f, 0.92f, 1f);
    public float titleBarHeight = 28f;
    public float sectionHeaderHeight = 24f;
    public float scrollbarWidth = 10f;
}
