using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class SelectionInputManager : MonoBehaviour
{
    // The events other scripts will listen to 
    public static event Action<Transform> OnCubeSelected;
    public static event Action OnCubeDeselected;

    [Header("Settings")]
    [SerializeField] private LayerMask selectableLayer;
    [SerializeField] private Camera mainCamera;

    [Header("Input Bindings")]
    public InputAction selectAction;
    public InputAction deselectAction;

    private Transform currentSelection;

    private void Awake()
    {
        if (mainCamera == null) mainCamera = Camera.main;
    }

    private void OnEnable()
    {
        selectAction.Enable();
        deselectAction.Enable();

        selectAction.performed += OnSelectPerformed;
        deselectAction.performed += OnDeselectPerformed;
    }

    private void OnDisable()
    {
        selectAction.Disable();
        deselectAction.Disable();

        selectAction.performed -= OnSelectPerformed;
        deselectAction.performed -= OnDeselectPerformed;
    }

    private void OnSelectPerformed(InputAction.CallbackContext context)
    {
        if (Pointer.current == null) return;
        
        Vector2 pointerPos = Pointer.current.position.ReadValue();
        HandleSelectionRaycast(pointerPos);
    }

    private void OnDeselectPerformed(InputAction.CallbackContext context)
    {
        if (currentSelection != null)
        {
            currentSelection = null;
            OnCubeDeselected?.Invoke();
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
}