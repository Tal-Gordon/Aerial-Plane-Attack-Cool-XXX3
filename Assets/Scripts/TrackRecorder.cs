using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

/// <summary>
/// Track-authoring aid for the F35 prefab. While the Create Hoops switch is on,
/// a WaypointHoop is dropped at the jet's position every hoopSpacing units of
/// travel, oriented along the direction of flight — hoop local +Z points the way
/// the jet is going, matching the front(-Z)-to-back(+Z) crossing check in
/// FlightSchoolObjective.
///
/// Two ways to lay a track:
///  - Edit mode: drag the jet around the Scene view; hoops appear behind it as
///    real prefab instances (undoable).
///  - Play mode (editor): fly the jet (PlayerController) — or let an AI fly it —
///    with the switch on. Hoops spawn live as feedback, and when play mode exits
///    the recorded track is re-created in the scene as prefab instances so it
///    survives the play-mode teardown.
///
/// Point Track Parent at a FlightSchoolObjective object and its waypoints list
/// picks the hoops up automatically (children, in drop order = flight order).
/// Left empty, hoops are collected under a "Recorded Track" object instead.
/// </summary>
[ExecuteAlways]
public class TrackRecorder : MonoBehaviour
{
    [Tooltip("The switch: while on, hoops are dropped as the jet moves. Leave OFF in the prefab — during training every spawned jet carries this component.")]
    [SerializeField] private bool createHoops = false;

    [Tooltip("Distance the jet must travel before the next hoop is dropped.")]
    [Min(1f)]
    [SerializeField] private float hoopSpacing = 1250f;

    [Tooltip("The WaypointHoop prefab to instantiate.")]
    [SerializeField] private GameObject hoopPrefab;

    [Tooltip("Optional parent for the created hoops — point at a FlightSchoolObjective object so its waypoint list syncs. Empty = a 'Recorded Track' object.")]
    [SerializeField] private Transform trackParent;

    private bool wasCreating;
    private Vector3 lastHoopPosition;
    private Rigidbody rb;

    private void OnEnable()
    {
        TryGetComponent(out rb);
    }

    private void Update()
    {
#if UNITY_EDITOR
        // Never drop hoops while editing the prefab asset itself (prefab stage).
        if (!Application.isPlaying && EditorSceneManager.IsPreviewScene(gameObject.scene)) return;
#endif
        if (!createHoops)
        {
            wasCreating = false;
            return;
        }

        // Switch just flipped on: anchor here; the first hoop lands one spacing ahead.
        if (!wasCreating)
        {
            wasCreating = true;
            lastHoopPosition = transform.position;
            return;
        }

        Vector3 delta = transform.position - lastHoopPosition;
        if (delta.sqrMagnitude < hoopSpacing * hoopSpacing) return;

        DropHoop(transform.position, GetTravelRotation(delta));
        lastHoopPosition = transform.position;
    }

    // Hoop +Z must face along the travel direction so the jet crosses -Z -> +Z.
    // In flight the velocity tangent beats the chord from the last hoop; in
    // edit-mode drags there is no velocity, so the chord is the best signal.
    private Quaternion GetTravelRotation(Vector3 delta)
    {
        Vector3 dir = Application.isPlaying && rb != null && rb.linearVelocity.sqrMagnitude > 1f
            ? rb.linearVelocity.normalized
            : delta.normalized;
        if (dir.sqrMagnitude < 0.5f) dir = transform.forward;

        // Keep hoops upright; on a near-vertical path world-up degenerates, so
        // borrow the jet's own up to keep LookRotation well-defined.
        Vector3 up = Mathf.Abs(Vector3.Dot(dir, Vector3.up)) > 0.99f ? transform.up : Vector3.up;
        return Quaternion.LookRotation(dir, up);
    }

    private void DropHoop(Vector3 position, Quaternion rotation)
    {
        if (hoopPrefab == null)
        {
            Debug.LogWarning("[TrackRecorder] Create Hoops is on but no hoop prefab is assigned.", this);
            createHoops = false;
            return;
        }

        if (Application.isPlaying)
        {
            // Live feedback copy only — the persistent version is baked into the
            // scene when play mode exits (see RebakeRecordedTrack).
            Instantiate(hoopPrefab, position, rotation, trackParent);
#if UNITY_EDITOR
            RecordForRebake(position, rotation);
#endif
            return;
        }

#if UNITY_EDITOR
        GameObject hoop = (GameObject)PrefabUtility.InstantiatePrefab(hoopPrefab, gameObject.scene);
        hoop.transform.SetPositionAndRotation(position, rotation);
        hoop.transform.SetParent(GetOrCreateEditParent(), true);
        Undo.RegisterCreatedObjectUndo(hoop, "Drop Waypoint Hoop");
        EditorSceneManager.MarkSceneDirty(gameObject.scene);
#endif
    }

#if UNITY_EDITOR
    private Transform GetOrCreateEditParent()
    {
        if (trackParent != null) return trackParent;
        GameObject container = GameObject.Find(RebakeContainerName);
        if (container == null)
        {
            container = new GameObject(RebakeContainerName);
            Undo.RegisterCreatedObjectUndo(container, "Drop Waypoint Hoop");
        }
        return container.transform;
    }

    // --- Play-mode recording: poses live in SessionState (survives the
    // play->edit teardown and domain reloads) and are re-instantiated as prefab
    // instances once the edit-mode scene is back. ---

    private const string SessionKey = "TrackRecorder.RecordedTrack";
    private const string RebakeContainerName = "Recorded Track";

    [Serializable]
    private class RecordedTrack
    {
        public string prefabPath;
        public string parentName;
        public List<Vector3> positions = new List<Vector3>();
        public List<Quaternion> rotations = new List<Quaternion>();
    }

    private static RecordedTrack pendingTrack;

    private void RecordForRebake(Vector3 position, Quaternion rotation)
    {
        if (pendingTrack == null)
        {
            pendingTrack = new RecordedTrack
            {
                prefabPath = AssetDatabase.GetAssetPath(hoopPrefab),
                parentName = trackParent != null ? trackParent.name : null,
            };
        }
        pendingTrack.positions.Add(position);
        pendingTrack.rotations.Add(rotation);
        // Persist every drop so the track survives however play mode ends.
        SessionState.SetString(SessionKey, JsonUtility.ToJson(pendingTrack));
    }

    [InitializeOnLoadMethod]
    private static void HookPlayModeRebake()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.ExitingEditMode)
            {
                // Fresh recording per play session.
                pendingTrack = null;
                SessionState.EraseString(SessionKey);
            }
            else if (state == PlayModeStateChange.EnteredEditMode)
            {
                RebakeRecordedTrack();
            }
        };
    }

    private static void RebakeRecordedTrack()
    {
        string json = SessionState.GetString(SessionKey, "");
        pendingTrack = null;
        SessionState.EraseString(SessionKey);
        if (string.IsNullOrEmpty(json)) return;

        RecordedTrack track = JsonUtility.FromJson<RecordedTrack>(json);
        if (track == null || track.positions.Count == 0) return;

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(track.prefabPath);
        if (prefab == null)
        {
            Debug.LogWarning($"[TrackRecorder] Recorded {track.positions.Count} hoop(s) but the prefab at '{track.prefabPath}' could not be loaded.");
            return;
        }

        // Re-find the requested parent by name in the edit-mode scene; fall back
        // to a shared container so the bake never lands loose at the root.
        Transform parent = null;
        if (!string.IsNullOrEmpty(track.parentName))
        {
            GameObject parentGo = GameObject.Find(track.parentName);
            if (parentGo != null) parent = parentGo.transform;
        }
        if (parent == null)
        {
            GameObject container = GameObject.Find(RebakeContainerName);
            if (container == null)
            {
                container = new GameObject(RebakeContainerName);
                Undo.RegisterCreatedObjectUndo(container, "Bake Recorded Track");
            }
            parent = container.transform;
        }

        for (int i = 0; i < track.positions.Count; i++)
        {
            GameObject hoop = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent.gameObject.scene);
            hoop.transform.SetPositionAndRotation(track.positions[i], track.rotations[i]);
            hoop.transform.SetParent(parent, true);
            Undo.RegisterCreatedObjectUndo(hoop, "Bake Recorded Track");
        }
        EditorSceneManager.MarkSceneDirty(parent.gameObject.scene);
        Debug.Log($"[TrackRecorder] Baked {track.positions.Count} recorded hoop(s) into the scene under '{parent.name}'.");
    }
#endif
}
