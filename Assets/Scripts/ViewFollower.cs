using Microsoft.MixedReality.Toolkit.UI;
using Microsoft.MixedReality.Toolkit.Utilities;
using Unity.Profiling;
using UnityEngine;

#if WINDOWS_UWP
using Windows.Media.Capture;
using Windows.System;
#else
#endif

/// <summary>
/// Provides a head/camera tracking implementation that can be built off of. 
/// The basis of this implementation is contructed from the MRTK visual profiler debugger.
/// </summary>
public class ViewFollower : MonoBehaviour
{
    private static readonly Vector2 defaultWindowRotation = new Vector2(10.0f, 20.0f);
    private static readonly Vector3 defaultWindowScale = new Vector3(1.0f, 1.0f, 1.0f);

    /// <summary>
    /// The threshold at which the <see cref="ViewFollower"/> will change it's tilt.
    /// </summary>
    public const float X_AXIS_TILT_THRESHOLD = 0.2f;
    /// <summary>
    /// The threshold at which the <see cref="ViewFollower"/> will change it's y-axis tilt.
    /// </summary>
    public const float Y_AXIS_TILT_THRESHOLD = 0.15f;

    [Header("Window Settings")]
    [SerializeField, Tooltip("What part of the view port to anchor the window to.")]
    private TextAnchor windowAnchor = TextAnchor.MiddleCenter;

    public TextAnchor WindowAnchor
    {
        get { return windowAnchor; }
        set { windowAnchor = value; }
    }

    [SerializeField, Tooltip("The offset from the view port center applied based on the window anchor selection.")]
    private Vector3 windowOffset = new Vector3(1.0f, 1.0f, 0.0f);

    public Vector3 WindowOffset
    {
        get { return windowOffset; }
        set { windowOffset = value; }
    }

    public void SetWindowXOffset(SliderEventData args)
    {
        // Offset by range mid point set by PinchSlider Range attribute to allow offseting from a central point
        windowOffset.x = args.NewValue - 0.5f;
        // UpdateWindowAnchor(WindowOffset.x);
        int anchor = (int)windowAnchor;

        if (anchor < 3)
        {
            if (windowOffset.x > X_AXIS_TILT_THRESHOLD) // Offset to the right            
                WindowAnchor = TextAnchor.UpperRight;
            else if (windowOffset.x < -X_AXIS_TILT_THRESHOLD) // Offset to the left         
                WindowAnchor = TextAnchor.UpperLeft;
            else // Center ish displayed            
                WindowAnchor = TextAnchor.UpperCenter;
        }
        else if (anchor < 6)
        {
            if (windowOffset.x > X_AXIS_TILT_THRESHOLD) // Offset to the right    
                WindowAnchor = TextAnchor.MiddleRight;
            else if (windowOffset.x < -X_AXIS_TILT_THRESHOLD) // Offset to the left
                WindowAnchor = TextAnchor.MiddleLeft;
            else // Center ish displayed
                WindowAnchor = TextAnchor.MiddleCenter;
        }
        else
        {
            if (windowOffset.x > X_AXIS_TILT_THRESHOLD) // Offset to the right
                WindowAnchor = TextAnchor.LowerRight;
            else if (windowOffset.x < -X_AXIS_TILT_THRESHOLD) // Offset to the left
                WindowAnchor = TextAnchor.LowerLeft;
            else // Center ish displayed
                WindowAnchor = TextAnchor.LowerCenter;
        }
    }

    public void SetWindowYOffset(SliderEventData args)
    {
        windowOffset.y = args.NewValue - 0.5f;
        if (WindowAnchor == TextAnchor.UpperLeft || WindowAnchor == TextAnchor.MiddleLeft || WindowAnchor == TextAnchor.LowerLeft)
        {
            if (windowOffset.y > Y_AXIS_TILT_THRESHOLD) // Offset to upper           
                WindowAnchor = TextAnchor.UpperLeft;
            else if (windowOffset.y < -Y_AXIS_TILT_THRESHOLD) // Offset to lower        
                WindowAnchor = TextAnchor.LowerLeft;
            else
                WindowAnchor = TextAnchor.MiddleLeft;
        }
        else if (WindowAnchor == TextAnchor.UpperCenter || WindowAnchor == TextAnchor.MiddleCenter || WindowAnchor == TextAnchor.LowerCenter)
        {
            if (windowOffset.y > Y_AXIS_TILT_THRESHOLD)
                WindowAnchor = TextAnchor.UpperCenter;
            else if (windowOffset.y < -Y_AXIS_TILT_THRESHOLD)
                WindowAnchor = TextAnchor.LowerCenter;
            else // Center ish displayed
                WindowAnchor = TextAnchor.MiddleCenter;
        }
        else
        {
            if (windowOffset.y > Y_AXIS_TILT_THRESHOLD)
                WindowAnchor = TextAnchor.UpperRight;
            else if (windowOffset.y < -Y_AXIS_TILT_THRESHOLD)
                WindowAnchor = TextAnchor.LowerRight;
            else
                WindowAnchor = TextAnchor.MiddleRight;
        }
    }

    public void SetWindowZOffset(SliderEventData args)
        => windowOffset.z = args.NewValue;

    [SerializeField, Range(0.1f, 10.0f), Tooltip("Use to scale the window size up or down, can simulate a zooming effect.")]
    public float windowScale = 0.7f;

    public float WindowScale
    {
        get { return windowScale; }
        set { windowScale = Mathf.Clamp(value, 0.1f, 10.0f); }
    }
    public void SetWindowScale(SliderEventData args) => WindowScale = args.NewValue;

    [SerializeField, Range(0.0f, 100.0f), Tooltip("How quickly to interpolate the window towards its target position and rotation.")]
    private float windowFollowSpeed = 5.0f;

    public float WindowFollowSpeed
    {
        get { return windowFollowSpeed; }
        set { windowFollowSpeed = Mathf.Abs(value); }
    }

    public Transform window;
    //private Transform background;
    private Quaternion windowHorizontalRotation;
    private Quaternion windowHorizontalRotationInverse;
    private Quaternion windowVerticalRotation;
    private Quaternion windowVerticalRotationInverse;

    // Rendering resources.
    public Material defaultMaterial;
    public Material defaultInstancedMaterial;
    public Material backgroundMaterial;

#if WINDOWS_UWP
    private bool appCaptureIsCapturingVideo = false;
    private AppCapture appCapture;
#endif // WINDOWS_UWP

    private void Start()
    {
        BuildWindow();

#if WINDOWS_UWP
        appCapture = AppCapture.GetForCurrentView();
        if (appCapture != null)
        {
            appCaptureIsCapturingVideo = appCapture.IsCapturingVideo;
            appCapture.CapturingChanged += AppCapture_CapturingChanged;
        }
#endif // WINDOWS_UWP
    }

    private void OnDestroy()
    {
#if WINDOWS_UWP
        if (appCapture != null)
        {
            appCapture.CapturingChanged -= AppCapture_CapturingChanged;
        }
#endif // WINDOWS_UWP

        if (window != null)
            Destroy(window.gameObject);
    }

    private void LateUpdate()
    {
        // Update window transformation.
        Transform cameraTransform = CameraCache.Main ? CameraCache.Main.transform : null;

        if (cameraTransform != null)
        {
            float t = Time.deltaTime * windowFollowSpeed;
            window.position = Vector3.Lerp(window.position, CalculateWindowPosition(cameraTransform), t);
            window.rotation = Quaternion.Slerp(window.rotation, CalculateWindowRotation(cameraTransform), t);
            window.localScale = defaultWindowScale * windowScale;
        }
    }

#if WINDOWS_UWP
    private void AppCapture_CapturingChanged(AppCapture sender, object args) => appCaptureIsCapturingVideo = sender.IsCapturingVideo;
    private float previousFieldOfView = -1.0f;
#endif // WINDOWS_UWP

    private Vector3 CalculateWindowPosition(Transform cameraTransform)
    {
        float windowDistance =
#if WINDOWS_UWP
                Mathf.Max(16.0f / (appCaptureIsCapturingVideo ? previousFieldOfView : previousFieldOfView = CameraCache.Main.fieldOfView), Mathf.Max(CameraCache.Main.nearClipPlane, 0.5f)) + WindowOffset.z;
#else   // TODO add windowoffset.z to UWP imple
                Mathf.Max(16.0f / CameraCache.Main.fieldOfView, Mathf.Max(CameraCache.Main.nearClipPlane, 0.5f)) + WindowOffset.z;
#endif // WINDOWS_UWP

        Vector3 position = cameraTransform.position + (cameraTransform.forward * windowDistance);
        Vector3 horizontalOffset = cameraTransform.right * windowOffset.x;
        Vector3 verticalOffset = cameraTransform.up * windowOffset.y;

        position += horizontalOffset + verticalOffset;
        return position;
    }

    private static readonly ProfilerMarker CalculateWindowRotationPerfMarker = new ProfilerMarker("[MRTK] MixedRealityToolkitVisualProfiler.CalculateWindowRotation");

    private Quaternion CalculateWindowRotation(Transform cameraTransform)
    {
        using (CalculateWindowRotationPerfMarker.Auto())
        {
            Quaternion rotation = cameraTransform.rotation;

            switch (windowAnchor)
            {
                case TextAnchor.UpperLeft: rotation *= windowHorizontalRotationInverse * windowVerticalRotationInverse; break;
                case TextAnchor.UpperCenter: rotation *= windowHorizontalRotationInverse; break;
                case TextAnchor.UpperRight: rotation *= windowHorizontalRotationInverse * windowVerticalRotation; break;
                case TextAnchor.MiddleLeft: rotation *= windowVerticalRotationInverse; break;
                case TextAnchor.MiddleRight: rotation *= windowVerticalRotation; break;
                case TextAnchor.LowerLeft: rotation *= windowHorizontalRotation * windowVerticalRotationInverse; break;
                case TextAnchor.LowerCenter: rotation *= windowHorizontalRotation; break;
                case TextAnchor.LowerRight: rotation *= windowHorizontalRotation * windowVerticalRotation; break;
            }

            return rotation;
        }
    }

    private void BuildWindow()
    {
        // Build the window root.       
        windowHorizontalRotation = Quaternion.AngleAxis(defaultWindowRotation.y, Vector3.right);
        windowHorizontalRotationInverse = Quaternion.Inverse(windowHorizontalRotation);
        windowVerticalRotation = Quaternion.AngleAxis(defaultWindowRotation.x, Vector3.up);
        windowVerticalRotationInverse = Quaternion.Inverse(windowVerticalRotation);
    }
}
