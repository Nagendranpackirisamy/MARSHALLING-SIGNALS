using UnityEngine;

public class DualCameraLayerVisibility : MonoBehaviour
{
    [Header("Cameras")]
    [SerializeField] private Camera cameraOne;
    [SerializeField] private Camera cameraTwo;

    [Header("Target Objects")]
    [Tooltip("Visible to Camera One, invisible to Camera Two.")]
    [SerializeField] private GameObject objectForCameraOne;

    [Tooltip("Visible to Camera Two, invisible to Camera One.")]
    [SerializeField] private GameObject objectForCameraTwo;

    [Header("Layer Names (Ensure these exist in Tags & Layers)")]
    [SerializeField] private string layerForCamOne = "Cam1Only";
    [SerializeField] private string layerForCamTwo = "Cam2Only";

    private void Awake()
    {
        SetupVisibility();
    }

    public void SetupVisibility()
    {
        int layer1 = LayerMask.NameToLayer(layerForCamOne);
        int layer2 = LayerMask.NameToLayer(layerForCamTwo);

        if (layer1 == -1 || layer2 == -1)
        {
            Debug.LogError("DualCameraLayerVisibility: One or both layers do not exist! Create them in Project Settings -> Tags and Layers.");
            return;
        }

        // Assign layers recursively to objects and any child meshes
        if (objectForCameraOne != null)
            SetLayerRecursively(objectForCameraOne, layer1);

        if (objectForCameraTwo != null)
            SetLayerRecursively(objectForCameraTwo, layer2);

        // Configure Camera One: Show Layer 1, Hide Layer 2
        if (cameraOne != null)
        {
            cameraOne.cullingMask |= (1 << layer1);   // Enable Cam1Only
            cameraOne.cullingMask &= ~(1 << layer2);  // Disable Cam2Only
        }

        // Configure Camera Two: Show Layer 2, Hide Layer 1
        if (cameraTwo != null)
        {
            cameraTwo.cullingMask |= (1 << layer2);   // Enable Cam2Only
            cameraTwo.cullingMask &= ~(1 << layer1);  // Disable Cam1Only
        }
    }

    private void SetLayerRecursively(GameObject target, int newLayer)
    {
        target.layer = newLayer;
        foreach (Transform child in target.transform)
        {
            SetLayerRecursively(child.gameObject, newLayer);
        }
    }
}