using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor-only helpers for authoring camera <see cref="Waypoint"/>s from the Scene view.
/// Frame the shot you want in the Scene view, then press the hotkey to stamp a waypoint
/// at the exact camera position and rotation. Consecutive presses auto-chain the waypoints
/// (Next/Previous) so a whole sequence can be laid down quickly.
/// </summary>
public static class WaypointEditorTools
{
    private const string MenuRoot = "Tools/Waypoints/";

    // %=Ctrl, #=Shift, &=Alt
    [MenuItem(MenuRoot + "Create At Scene Camera %#w", false, 0)]
    private static void CreateAtSceneCamera()
    {
        if (!TryGetSceneCameraPose(out Vector3 position, out Quaternion rotation))
        {
            return;
        }

        // Chain after the selected waypoint if one is selected, so repeated presses
        // build a sequence. Falls back to appending to the end of the selected chain.
        Waypoint previous = Selection.activeGameObject != null
            ? Selection.activeGameObject.GetComponent<Waypoint>()
            : null;

        var go = new GameObject("Waypoint");
        Undo.RegisterCreatedObjectUndo(go, "Create Waypoint");

        // Keep siblings tidy: parent under the previous waypoint's parent, if any.
        if (previous != null)
        {
            Undo.SetTransformParent(go.transform, previous.transform.parent, "Create Waypoint");
        }

        go.transform.SetPositionAndRotation(position, rotation);
        var waypoint = Undo.AddComponent<Waypoint>(go);

        if (previous != null)
        {
            LinkAfter(previous, waypoint);
        }

        // Select the new one so the next press continues the chain.
        Selection.activeGameObject = go;
        EditorGUIUtility.PingObject(go);
    }

    [MenuItem(MenuRoot + "Move Selected To Scene Camera %#e", false, 1)]
    private static void MoveSelectedToSceneCamera()
    {
        var waypoint = Selection.activeGameObject != null
            ? Selection.activeGameObject.GetComponent<Waypoint>()
            : null;

        if (waypoint == null)
        {
            Debug.LogWarning("[Waypoints] Select a Waypoint first to recapture it from the Scene camera.");
            return;
        }

        if (!TryGetSceneCameraPose(out Vector3 position, out Quaternion rotation))
        {
            return;
        }

        Undo.RecordObject(waypoint.transform, "Move Waypoint To Scene Camera");
        waypoint.transform.SetPositionAndRotation(position, rotation);
    }

    [MenuItem(MenuRoot + "Align Scene View To Selected %#q", false, 2)]
    private static void AlignSceneViewToSelected()
    {
        var waypoint = Selection.activeGameObject != null
            ? Selection.activeGameObject.GetComponent<Waypoint>()
            : null;

        if (waypoint == null)
        {
            Debug.LogWarning("[Waypoints] Select a Waypoint first to preview its view.");
            return;
        }

        // Point the Scene camera exactly where this waypoint looks, to preview the shot.
        SceneView view = SceneView.lastActiveSceneView;
        if (view == null)
        {
            return;
        }

        view.AlignViewToObject(waypoint.transform);
        view.Repaint();
    }

    private static bool TryGetSceneCameraPose(out Vector3 position, out Quaternion rotation)
    {
        SceneView view = SceneView.lastActiveSceneView;
        if (view == null || view.camera == null)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;
            Debug.LogWarning("[Waypoints] No active Scene view — hover the Scene view and try again.");
            return false;
        }

        Transform cam = view.camera.transform;
        position = cam.position;
        rotation = cam.rotation;
        return true;
    }

    // Splices 'inserted' immediately after 'before', preserving any existing Next link.
    private static void LinkAfter(Waypoint before, Waypoint inserted)
    {
        Waypoint oldNext = before.Next;

        Undo.RecordObject(before, "Link Waypoint");
        Undo.RecordObject(inserted, "Link Waypoint");

        before.Next = inserted;
        inserted.Previous = before;

        inserted.Next = oldNext;
        if (oldNext != null)
        {
            Undo.RecordObject(oldNext, "Link Waypoint");
            oldNext.Previous = inserted;
        }
    }
}
