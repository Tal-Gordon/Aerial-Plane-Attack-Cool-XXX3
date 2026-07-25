using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runtime chase camera used by the saved-run challenge. It temporarily suppresses
/// track/Cinemachine camera drivers and restores every previous state on exit.
/// </summary>
public sealed class ChallengeFollowCamera : MonoBehaviour
{
    private readonly List<Behaviour> suppressed = new();
    private readonly List<bool> suppressedStates = new();

    private Transform target;
    private Vector3 originalPosition;
    private Quaternion originalRotation;
    private Vector3 velocity;

    [SerializeField] private Vector3 localOffset = new(0f, 42f, -170f);
    [SerializeField] private float lookAhead = 115f;
    [SerializeField] private float smoothTime = 0.12f;

    public static ChallengeFollowCamera Attach(Transform target)
    {
        Camera camera = Camera.main != null
            ? Camera.main
            : FindFirstObjectByType<Camera>();
        if (camera == null) return null;

        ChallengeFollowCamera rig = camera.GetComponent<ChallengeFollowCamera>();
        if (rig == null) rig = camera.gameObject.AddComponent<ChallengeFollowCamera>();
        rig.Begin(target);
        return rig;
    }

    private void Begin(Transform followTarget)
    {
        enabled = true;
        target = followTarget;
        originalPosition = transform.position;
        originalRotation = transform.rotation;
        velocity = Vector3.zero;

        suppressed.Clear();
        suppressedStates.Clear();
        foreach (MonoBehaviour behaviour in FindObjectsByType<MonoBehaviour>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (behaviour == null || behaviour == this) continue;

            System.Type type = behaviour.GetType();
            string ns = type.Namespace ?? string.Empty;
            bool isTrackCamera = behaviour is CameraControllerFlight
                                 || behaviour is CameraControllerDogfight
                                 || behaviour is CameraControllerMaxAltitude;
            bool isCinemachine = ns.StartsWith("Unity.Cinemachine");
            if (!isTrackCamera && !isCinemachine) continue;

            suppressed.Add(behaviour);
            suppressedStates.Add(behaviour.enabled);
            if (isTrackCamera) behaviour.StopAllCoroutines();
            behaviour.enabled = false;
        }

        SnapToTarget();
    }

    private void LateUpdate()
    {
        if (target == null) return;

        Vector3 desired = target.TransformPoint(localOffset);
        transform.position = Vector3.SmoothDamp(
            transform.position, desired, ref velocity, smoothTime);

        Vector3 focus = target.position + target.forward * lookAhead;
        Vector3 direction = focus - transform.position;
        if (direction.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                Quaternion.LookRotation(direction.normalized, target.up),
                1f - Mathf.Exp(-10f * Time.unscaledDeltaTime));
    }

    private void SnapToTarget()
    {
        if (target == null) return;
        transform.position = target.TransformPoint(localOffset);
        Vector3 focus = target.position + target.forward * lookAhead;
        transform.rotation = Quaternion.LookRotation(
            (focus - transform.position).normalized, target.up);
    }

    public void Restore()
    {
        target = null;
        transform.SetPositionAndRotation(originalPosition, originalRotation);
        for (int i = 0; i < suppressed.Count; i++)
            if (suppressed[i] != null)
                suppressed[i].enabled = suppressedStates[i];

        suppressed.Clear();
        suppressedStates.Clear();
        // Keep the component disabled for reuse. Destroy() is deferred until the
        // end of the frame; a same-frame rematch used to bind this component to the
        // new player and then destroy it, breaking both rematch and later exit.
        enabled = false;
    }
}
