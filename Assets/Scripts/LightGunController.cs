using UnityEngine;
using System.Collections;

/// <summary>
/// Drives a fixed glowing sphere at the tower representing the light gun.
/// Just swaps the sphere's material color/emission and toggles visibility
/// to flash, based on the scenario's signal. Sphere does not move or track
/// the aircraft - it stays fixed at the tower.
/// </summary>
public class LightGunController : MonoBehaviour
{
    [Header("Beam Sphere")]
    [Tooltip("The glowing sphere GameObject (fixed at the tower)")]
    [SerializeField] private GameObject beamSphere;
    [SerializeField] private Renderer beamSphereRenderer;
    [SerializeField] private MaterialPropertyBlock propBlock;

    [Header("Colors")]
    [SerializeField] private Color redColor = Color.red;
    [SerializeField] private Color greenColor = Color.green;
    [SerializeField] private Color whiteColor = Color.white;
    [SerializeField] private float emissionIntensity = 3f;

    [Header("Flashing")]
    [SerializeField] private float flashOnTime = 0.4f;
    [SerializeField] private float flashOffTime = 0.3f;

    private Coroutine flashRoutine;
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    [Header("TEMP TEST ONLY - remove before final build")]
    [SerializeField] private bool testOnStart = false;
    [SerializeField] private LightColor testColor = LightColor.Red;
    [SerializeField] private LightPattern testPattern = LightPattern.Flashing;

    private void Awake()
    {
        if (propBlock == null) propBlock = new MaterialPropertyBlock();

        if (beamSphere != null)
            beamSphere.SetActive(false);
    }

    private void Start()
    {
        if (testOnStart)
        {
            SetSignal(testColor, testPattern);
        }
    }

    public void SetSignal(LightColor color, LightPattern pattern)
    {
        StopFlashing();
        Color c = GetColor(color);

        if (pattern == LightPattern.Steady)
        {
            SetSphereVisible(true, c);
        }
        else
        {
            flashRoutine = StartCoroutine(FlashRoutine(c));
        }
    }

    public void ClearSignal()
    {
        StopFlashing();
        SetSphereVisible(false, Color.black);
    }

    private IEnumerator FlashRoutine(Color c)
    {
        while (true)
        {
            SetSphereVisible(true, c);
            yield return new WaitForSeconds(flashOnTime);
            SetSphereVisible(false, c);
            yield return new WaitForSeconds(flashOffTime);
        }
    }

    private void StopFlashing()
    {
        if (flashRoutine != null)
        {
            StopCoroutine(flashRoutine);
            flashRoutine = null;
        }

        if (alternatingRoutine != null)
        {
            StopCoroutine(alternatingRoutine);
            alternatingRoutine = null;
        }
    }

    private void SetSphereVisible(bool visible, Color c)
    {
        if (beamSphere != null)
            beamSphere.SetActive(visible);

        if (visible && beamSphereRenderer != null)
        {
            beamSphereRenderer.GetPropertyBlock(propBlock);
            propBlock.SetColor(BaseColorId, c);
            propBlock.SetColor(EmissionColorId, c * emissionIntensity);
            beamSphereRenderer.SetPropertyBlock(propBlock);
        }
    }

    private Color GetColor(LightColor color)
    {
        switch (color)
        {
            case LightColor.Red: return redColor;
            case LightColor.Green: return greenColor;
            case LightColor.White: return whiteColor;
            default: return Color.magenta;
        }
    }

    private Coroutine alternatingRoutine;

    public void PlayAlternatingSignal()
    {
        StopFlashing();

        if (alternatingRoutine != null)
        {
            StopCoroutine(alternatingRoutine);
        }

        alternatingRoutine = StartCoroutine(AlternatingRoutine());
    }

    private IEnumerator AlternatingRoutine()
    {
        while (true)
        {
            SetSphereVisible(true, redColor);
            yield return new WaitForSeconds(flashOnTime);

            SetSphereVisible(true, greenColor);
            yield return new WaitForSeconds(flashOnTime);
        }
    }


}