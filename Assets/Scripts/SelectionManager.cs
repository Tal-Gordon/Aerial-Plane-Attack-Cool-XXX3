using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class SelectionManager : MonoBehaviour
{
    // The events other scripts will listen to 
    public static event Action<Transform> OnCubeSelected;
    public static event Action OnCubeDeselected;

    [Header("Settings")]
    [SerializeField] private LayerMask selectableLayer;
    [SerializeField] private Camera mainCamera;

    private Transform currentSelection;

    private void Awake()
    {
        if (mainCamera == null) mainCamera = Camera.main;
    }

    private void Update()
    {
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            HandleSelectionRaycast(Mouse.current.position.ReadValue());
        }
        else if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
        {
            HandleSelectionRaycast(Touchscreen.current.primaryTouch.position.ReadValue());
        }

        if (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame)
        {
            if (currentSelection != null)
            {
                Deselect();
            }
        }
    }

    private void HandleSelectionRaycast(Vector2 screenPos)
    {
        Ray ray = mainCamera.ScreenPointToRay(screenPos);

        if (Physics.Raycast(ray, out RaycastHit hit, 100f, selectableLayer))
        {
            if (hit.transform != currentSelection)
            {
                currentSelection = hit.transform;
                OnCubeSelected?.Invoke(currentSelection);
            }
        }
    }

    private void Deselect()
    {
        currentSelection = null;
        OnCubeDeselected?.Invoke();
    }
}