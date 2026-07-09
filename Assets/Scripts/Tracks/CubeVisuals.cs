using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Renderer), typeof(Collider))]
public class CubeVisuals : MonoBehaviour
{
    [Header("Fade Settings")]
    [SerializeField] private float fadeDuration = 0.4f;
    [SerializeField] private float backgroundAlpha = 0.1f;

    private Material instancedMaterial;
    private Collider cubeCollider;
    private Coroutine fadeCoroutine;

    private void Awake()
    {
        // Instantiates the material so we don't accidentally edit the shared asset
        instancedMaterial = GetComponent<Renderer>().material;
        cubeCollider = GetComponent<Collider>();
    }

    private void OnEnable()
    {
        SelectionInputManager.OnCubeSelected += HandleSelection;
        SelectionInputManager.OnCubeDeselected += HandleDeselection;
    }

    private void OnDisable()
    {
        SelectionInputManager.OnCubeSelected -= HandleSelection;
        SelectionInputManager.OnCubeDeselected -= HandleDeselection;
    }

    private void HandleSelection(Transform selectedCube)
    {
        if (selectedCube == transform)
        {
            // This is the selected cube. Stay opaque.
            FadeTo(1f);
        }
        else
        {
            // This is a background cube. Fade out and ignore raycasts.
            FadeTo(backgroundAlpha);
            cubeCollider.enabled = false;
        }
    }

    private void HandleDeselection()
    {
        // Bring everything back and enable raycasts
        FadeTo(1f);
        cubeCollider.enabled = true;
    }

    private void FadeTo(float targetAlpha)
    {
        if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
        fadeCoroutine = StartCoroutine(FadeRoutine(targetAlpha));
    }

    private IEnumerator FadeRoutine(float targetAlpha)
    {
        Color color = instancedMaterial.color;
        float startAlpha = color.a;
        float timePassed = 0f;

        while (timePassed < fadeDuration)
        {
            timePassed += Time.deltaTime;
            float t = Mathf.Clamp01(timePassed / fadeDuration);

            color.a = Mathf.Lerp(startAlpha, targetAlpha, t);
            instancedMaterial.color = color;

            yield return null;
        }

        color.a = targetAlpha;
        instancedMaterial.color = color;
    }
}