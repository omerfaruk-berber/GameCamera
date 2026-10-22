using System.Collections.Generic;
using UnityEngine;

public enum DollyTrackInterpolation
{
    Linear,
    Smooth
}

/// <summary>Dolly Camera için dünya koordinatlarında örneklenebilen sanal ray hattı.</summary>
[AddComponentMenu("")]
[DisallowMultipleComponent]
public class CameraDollyTrack : MonoBehaviour
{
    [Header("Track Settings")]
    [SerializeField, Tooltip("Control points that form the track in order. Empty references are ignored.")]
    private List<Transform> controlPoints = new List<Transform>();

    [SerializeField, Tooltip("Linear creates straight segments. Smooth creates a curve that passes through the control points.")]
    private DollyTrackInterpolation interpolation = DollyTrackInterpolation.Smooth;

    [SerializeField, Tooltip("Connects the last control point to the first to create a closed track.")]
    private bool closedLoop = false;

    [SerializeField, Range(4, 64), Tooltip("Samples used per smooth segment for length calculation. Higher values improve accuracy at a small cost.")]
    private int samplesPerSegment = 16;

    [Header("Scene Preview")]
    [SerializeField, Tooltip("Shows the track and its control points in the Scene view.")]
    private bool showScenePreview = true;

    private readonly List<Transform> cachedPoints = new List<Transform>();
    private readonly List<Vector3> cachedPointPositions = new List<Vector3>();
    private readonly List<float> cachedParameters = new List<float>();
    private readonly List<float> cachedDistances = new List<float>();
    private DollyTrackInterpolation cachedInterpolation;
    private bool cachedClosedLoop;
    private int cachedSamplesPerSegment;
    private float cachedLength;
    private bool cacheIsValid;

    /// <summary>Boş olmayan kontrol noktalarının sayısı.</summary>
    public int ControlPointCount
    {
        get
        {
            int count = 0;
            for (int i = 0; i < controlPoints.Count; i++)
            {
                if (controlPoints[i] != null)
                {
                    count++;
                }
            }

            return count;
        }
    }

    /// <summary>Rayın cart hareketinde kullanılan yaklaşık dünya uzunluğu.</summary>
    public float Length
    {
        get
        {
            EnsureCache();
            return cachedLength;
        }
    }

    /// <summary>Rayın hareket için yeterli, birbirinden farklı en az iki noktası olup olmadığını bildirir.</summary>
    public bool IsValid => ControlPointCount >= 2 && Length > 0.0001f;

    private void OnValidate()
    {
        samplesPerSegment = Mathf.Clamp(samplesPerSegment, 4, 64);
        InvalidateCache();
    }

    /// <summary>Kontrol noktalarını verilen sırayla değiştirir; null noktalar örnekleme sırasında atlanır.</summary>
    public void SetControlPoints(IEnumerable<Transform> newControlPoints)
    {
        controlPoints.Clear();
        if (newControlPoints != null)
        {
            controlPoints.AddRange(newControlPoints);
        }

        InvalidateCache();
    }

    /// <summary>Rayın uzunluğu boyunca 0–1 aralığındaki konumu dünya koordinatlarında döndürür.</summary>
    public Vector3 EvaluatePosition(float normalizedPosition)
    {
        EnsureCache();
        if (cachedPoints.Count == 0)
        {
            return transform.position;
        }

        if (cachedPoints.Count == 1 || cachedLength <= 0.0001f)
        {
            return cachedPoints[0].position;
        }

        float parameter = DistanceToParameter(Mathf.Clamp01(normalizedPosition) * cachedLength);
        return EvaluateParameter(parameter);
    }

    /// <summary>Rayın 0–1 konumundaki hareket yönünü dünya koordinatlarında döndürür.</summary>
    public Vector3 EvaluateTangent(float normalizedPosition)
    {
        EnsureCache();
        if (cachedPoints.Count < 2 || cachedLength <= 0.0001f)
        {
            return transform.forward;
        }

        float step = 1f / Mathf.Max(64f, cachedParameters.Count * 4f);
        float current = Mathf.Clamp01(normalizedPosition);
        float before = closedLoop ? Mathf.Repeat(current - step, 1f) : Mathf.Max(0f, current - step);
        float after = closedLoop ? Mathf.Repeat(current + step, 1f) : Mathf.Min(1f, current + step);
        Vector3 tangent = EvaluatePosition(after) - EvaluatePosition(before);

        if (tangent.sqrMagnitude <= 0.000001f)
        {
            tangent = cachedPoints[cachedPoints.Count - 1].position - cachedPoints[0].position;
        }

        return tangent.sqrMagnitude > 0.000001f ? tangent.normalized : transform.forward;
    }

    private void InvalidateCache()
    {
        cacheIsValid = false;
    }

    private void EnsureCache()
    {
        if (cacheIsValid && !HasTrackChanged())
        {
            return;
        }

        RebuildCache();
    }

    private bool HasTrackChanged()
    {
        if (cachedInterpolation != interpolation ||
            cachedClosedLoop != closedLoop ||
            cachedSamplesPerSegment != samplesPerSegment)
        {
            return true;
        }

        int validIndex = 0;
        for (int i = 0; i < controlPoints.Count; i++)
        {
            Transform point = controlPoints[i];
            if (point == null)
            {
                continue;
            }

            if (validIndex >= cachedPoints.Count ||
                cachedPoints[validIndex] != point ||
                cachedPointPositions[validIndex] != point.position)
            {
                return true;
            }

            validIndex++;
        }

        return validIndex != cachedPoints.Count;
    }

    private void RebuildCache()
    {
        cachedPoints.Clear();
        cachedPointPositions.Clear();
        cachedParameters.Clear();
        cachedDistances.Clear();

        for (int i = 0; i < controlPoints.Count; i++)
        {
            Transform point = controlPoints[i];
            if (point != null)
            {
                cachedPoints.Add(point);
                cachedPointPositions.Add(point.position);
            }
        }

        cachedInterpolation = interpolation;
        cachedClosedLoop = closedLoop;
        cachedSamplesPerSegment = samplesPerSegment;
        cachedLength = 0f;
        cacheIsValid = true;

        if (cachedPoints.Count < 2)
        {
            return;
        }

        int segmentCount = GetSegmentCount();
        int sampleCount = interpolation == DollyTrackInterpolation.Linear
            ? segmentCount
            : segmentCount * samplesPerSegment;
        Vector3 previousPosition = EvaluateParameter(0f);
        cachedParameters.Add(0f);
        cachedDistances.Add(0f);

        for (int i = 1; i <= sampleCount; i++)
        {
            float parameter = i / (float)sampleCount;
            Vector3 position = EvaluateParameter(parameter);
            cachedLength += Vector3.Distance(previousPosition, position);
            cachedParameters.Add(parameter);
            cachedDistances.Add(cachedLength);
            previousPosition = position;
        }
    }

    private float DistanceToParameter(float distance)
    {
        if (distance <= 0f)
        {
            return 0f;
        }

        if (distance >= cachedLength)
        {
            return 1f;
        }

        int low = 0;
        int high = cachedDistances.Count - 1;
        while (low + 1 < high)
        {
            int middle = (low + high) / 2;
            if (cachedDistances[middle] < distance)
            {
                low = middle;
            }
            else
            {
                high = middle;
            }
        }

        float distanceRange = cachedDistances[high] - cachedDistances[low];
        float blend = distanceRange > 0.0001f
            ? (distance - cachedDistances[low]) / distanceRange
            : 0f;
        return Mathf.Lerp(cachedParameters[low], cachedParameters[high], blend);
    }

    private Vector3 EvaluateParameter(float parameter)
    {
        int pointCount = cachedPoints.Count;
        if (pointCount == 0)
        {
            return transform.position;
        }

        if (pointCount == 1)
        {
            return cachedPoints[0].position;
        }

        int segmentCount = GetSegmentCount();
        float scaledParameter = Mathf.Clamp01(parameter) * segmentCount;
        int segmentIndex = Mathf.Min(Mathf.FloorToInt(scaledParameter), segmentCount - 1);
        float segmentTime = Mathf.Min(scaledParameter - segmentIndex, 1f);
        Vector3 first = GetPointPosition(segmentIndex);
        Vector3 second = GetPointPosition(segmentIndex + 1);

        if (interpolation == DollyTrackInterpolation.Linear)
        {
            return Vector3.LerpUnclamped(first, second, segmentTime);
        }

        Vector3 previous = GetPointPosition(segmentIndex - 1);
        Vector3 next = GetPointPosition(segmentIndex + 2);
        float timeSquared = segmentTime * segmentTime;
        float timeCubed = timeSquared * segmentTime;
        return 0.5f * ((2f * first) +
            (-previous + second) * segmentTime +
            (2f * previous - 5f * first + 4f * second - next) * timeSquared +
            (-previous + 3f * first - 3f * second + next) * timeCubed);
    }

    private int GetSegmentCount()
    {
        return closedLoop ? cachedPoints.Count : cachedPoints.Count - 1;
    }

    private Vector3 GetPointPosition(int index)
    {
        if (closedLoop)
        {
            int wrappedIndex = (index % cachedPoints.Count + cachedPoints.Count) % cachedPoints.Count;
            return cachedPoints[wrappedIndex].position;
        }

        return cachedPoints[Mathf.Clamp(index, 0, cachedPoints.Count - 1)].position;
    }

    private void OnDrawGizmos()
    {
        if (!showScenePreview)
        {
            return;
        }

        EnsureCache();
        Color previousColor = Gizmos.color;

        Gizmos.color = new Color(0.15f, 0.85f, 1f, 1f);
        if (cachedPoints.Count >= 2)
        {
            int previewSteps = Mathf.Max(16, GetSegmentCount() * (interpolation == DollyTrackInterpolation.Smooth ? 16 : 1));
            Vector3 previousPosition = EvaluatePosition(0f);
            for (int i = 1; i <= previewSteps; i++)
            {
                Vector3 position = EvaluatePosition(i / (float)previewSteps);
                Gizmos.DrawLine(previousPosition, position);
                previousPosition = position;
            }
        }

        Gizmos.color = new Color(1f, 0.65f, 0.1f, 1f);
        for (int i = 0; i < cachedPoints.Count; i++)
        {
            Vector3 position = cachedPoints[i].position;
            Gizmos.DrawWireSphere(position, 0.2f);
#if UNITY_EDITOR
            UnityEditor.Handles.Label(position, $"Dolly Point {i + 1}");
#endif
        }

        Gizmos.color = previousColor;
    }
}
