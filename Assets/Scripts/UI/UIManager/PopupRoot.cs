using UnityEngine;

/// <summary>
/// Marker for full-screen popups that live inside a widget prefab (e.g. the
/// hyperparameter editor's confirmation dialog). TelemetryWindowBuilder reparents
/// anything carrying this component to the canvas root after instantiation, so the
/// popup renders above the window instead of being clipped inside the scroll view.
/// The widget's serialized reference to it survives the reparent.
/// </summary>
public class PopupRoot : MonoBehaviour { }
