using UnityEngine;

/// <summary>Snaps the final 2D camera position while preserving subpixel follow movement.</summary>
[AddComponentMenu("")]
[RequireComponent(typeof(Camera), typeof(MainCamera))]
[DisallowMultipleComponent]
public class CameraPixelMove2D : MonoBehaviour
{
    [Header("Pixel Move")]
    [SerializeField, Min(1), Tooltip("Match the Pixels Per Unit value used by your pixel-art sprites. One movement step is 1 / Pixels Per Unit world units.")]
    private int pixelsPerUnit = 16;

    private Vector3 continuousPosition;
    private Vector3 snappedPosition;
    private bool hasPosition;

    /// <summary>The number of sprite pixels per world unit. Values below 1 are clamped.</summary>
    public int PixelsPerUnit
    {
        get => pixelsPerUnit;
        set => pixelsPerUnit = Mathf.Max(1, value);
    }

    private void OnValidate()
    {
        PixelsPerUnit = pixelsPerUnit;
    }

    private void OnEnable()
    {
        hasPosition = false;
    }

    private void OnDisable()
    {
        hasPosition = false;
    }

    // MainCamera restores the precise follow position before calculating the next frame.
    // An external move or teleport replaces the stored position instead of being undone.
    internal Vector3 RestorePosition(Vector3 currentPosition)
    {
        bool restore = hasPosition && currentPosition.Equals(snappedPosition);
        hasPosition = false;
        return restore ? continuousPosition : currentPosition;
    }

    // MainCamera applies the result centrally, after follow and view offsets.
    internal Vector3 CalculatePosition(Vector3 position)
    {
        continuousPosition = position;
        snappedPosition = SnapPosition(position, pixelsPerUnit);
        hasPosition = true;
        return snappedPosition;
    }

    internal static Vector3 SnapPosition(Vector3 position, int pixelsPerUnit)
    {
        float scale = Mathf.Max(1, pixelsPerUnit);
        return new Vector3(
            Mathf.Round(position.x * scale) / scale,
            Mathf.Round(position.y * scale) / scale,
            position.z);
    }
}
