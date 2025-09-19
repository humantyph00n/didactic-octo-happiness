using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.Collections.Generic;

namespace HD2D.Camera
{
    /// <summary>
    /// Tilt-shift camera controller for HD-2D diorama effect
    /// Provides isometric-style camera with depth of field and miniature effect
    /// </summary>
    [RequireComponent(typeof(UnityEngine.Camera))]
    public class HD2DTiltShiftCamera : MonoBehaviour
    {
        [Header("Camera Configuration")]
        [SerializeField] private CameraMode cameraMode = CameraMode.Isometric;
        [SerializeField] private float cameraAngle = 45f;
        [SerializeField] private float cameraDistance = 20f;
        [SerializeField] private float fieldOfView = 30f;
        [SerializeField] private bool orthographic = false;
        [SerializeField] private float orthographicSize = 10f;
        
        [Header("Movement")]
        [SerializeField] private Transform followTarget;
        [SerializeField] private Vector3 targetOffset = Vector3.zero;
        [SerializeField] private float followSmoothness = 5f;
        [SerializeField] private bool lockYAxis = false;
        [SerializeField] private float fixedHeight = 10f;
        
        [Header("Boundaries")]
        [SerializeField] private bool useBoundaries = true;
        [SerializeField] private Bounds cameraBounds = new Bounds(Vector3.zero, Vector3.one * 100f);
        [SerializeField] private bool softBoundaries = true;
        [SerializeField] private float boundaryPadding = 5f;
        
        [Header("Tilt-Shift Effect")]
        [SerializeField] private bool enableTiltShift = true;
        [SerializeField] private float focusDistance = 15f;
        [SerializeField] private float focusRange = 5f;
        [SerializeField] private float blurAmount = 2f;
        [SerializeField] private AnimationCurve blurCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        [SerializeField] private bool autoFocus = false;
        [SerializeField] private LayerMask focusLayers = -1;
        
        [Header("Depth of Field")]
        [SerializeField] private bool enableDepthOfField = true;
        [SerializeField] private float aperture = 5.6f;
        [SerializeField] private float focalLength = 50f;
        [SerializeField] private int bladeCount = 5;
        [SerializeField] private float maxBlurSize = 3f;
        
        [Header("Camera Shake")]
        [SerializeField] private bool enableShake = true;
        [SerializeField] private float shakeDecay = 0.5f;
        [SerializeField] private float maxShakeIntensity = 1f;
        [SerializeField] private AnimationCurve shakeFalloff = AnimationCurve.EaseInOut(0, 1, 1, 0);
        
        [Header("Cinematic")]
        [SerializeField] private bool enableCinematicBars = false;
        [SerializeField] private float cinematicBarHeight = 0.1f;
        [SerializeField] private Color cinematicBarColor = Color.black;
        [SerializeField] private float cinematicTransitionSpeed = 2f;
        
        [Header("Debug")]
        [SerializeField] private bool showFocusPlane = false;
        [SerializeField] private Color focusPlaneColor = new Color(1, 1, 0, 0.3f);
        [SerializeField] private bool showBoundaries = false;
        
        // Components
        private UnityEngine.Camera mainCamera;
        private Volume postProcessVolume;
        private DepthOfField depthOfFieldEffect;
        private Transform cameraRig;
        
        // Runtime state
        private Vector3 currentVelocity;
        private Vector3 targetPosition;
        private Quaternion targetRotation;
        private float currentShakeIntensity;
        private Vector3 shakeOffset;
        private float currentCinematicBar;
        private List<CameraWaypoint> waypoints = new List<CameraWaypoint>();
        private int currentWaypointIndex;
        private bool isTransitioning;
        
        // Camera modes
        public enum CameraMode
        {
            Isometric,
            TopDown,
            ThirdPerson,
            Fixed,
            Cinematic,
            Free
        }
        
        [System.Serializable]
        public class CameraWaypoint
        {
            public Vector3 position;
            public Quaternion rotation;
            public float fieldOfView = 30f;
            public float focusDistance = 15f;
            public float waitTime = 0f;
            public AnimationCurve transitionCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        }
        
        #region Unity Lifecycle
        
        void Awake()
        {
            InitializeCamera();
            SetupPostProcessing();
        }
        
        void Start()
        {
            ApplyCameraMode();
        }
        
        void LateUpdate()
        {
            UpdateCameraPosition();
            UpdateCameraShake();
            UpdateTiltShift();
            UpdateCinematicBars();
            
            if (isTransitioning)
            {
                UpdateWaypointTransition();
            }
        }
        
        void OnValidate()
        {
            cameraAngle = Mathf.Clamp(cameraAngle, 0f, 90f);
            cameraDistance = Mathf.Max(1f, cameraDistance);
            fieldOfView = Mathf.Clamp(fieldOfView, 10f, 120f);
            orthographicSize = Mathf.Max(0.1f, orthographicSize);
            focusDistance = Mathf.Max(0.1f, focusDistance);
            focusRange = Mathf.Max(0.1f, focusRange);
            blurAmount = Mathf.Max(0f, blurAmount);
            aperture = Mathf.Clamp(aperture, 1f, 32f);
            focalLength = Mathf.Clamp(focalLength, 1f, 300f);
            bladeCount = Mathf.Clamp(bladeCount, 3, 9);
            maxBlurSize = Mathf.Clamp(maxBlurSize, 0f, 10f);
        }
        
        #endregion
        
        #region Initialization
        
        private void InitializeCamera()
        {
            mainCamera = GetComponent<UnityEngine.Camera>();
            
            // Create camera rig for smooth movement
            GameObject rigObject = new GameObject("CameraRig");
            cameraRig = rigObject.transform;
            cameraRig.position = transform.position;
            cameraRig.rotation = transform.rotation;
            transform.SetParent(cameraRig);
            
            // Set initial camera properties
            mainCamera.fieldOfView = fieldOfView;
            mainCamera.orthographic = orthographic;
            if (orthographic)
            {
                mainCamera.orthographicSize = orthographicSize;
            }
        }
        
        private void SetupPostProcessing()
        {
            // Find or create post-process volume
            postProcessVolume = GetComponent<Volume>();
            if (postProcessVolume == null)
            {
                postProcessVolume = gameObject.AddComponent<Volume>();
                postProcessVolume.isGlobal = true;
            }
            
            // Setup depth of field if profile exists
            if (postProcessVolume.profile != null)
            {
                if (!postProcessVolume.profile.TryGet(out depthOfFieldEffect))
                {
                    // Note: In actual implementation, you'd need to add the effect to the profile
                    // This requires the profile to be an instance, not a shared asset
                    Debug.LogWarning("Depth of Field effect not found in post-process profile");
                }
            }
        }
        
        #endregion
        
        #region Camera Modes
        
        private void ApplyCameraMode()
        {
            switch (cameraMode)
            {
                case CameraMode.Isometric:
                    SetupIsometricCamera();
                    break;
                case CameraMode.TopDown:
                    SetupTopDownCamera();
                    break;
                case CameraMode.ThirdPerson:
                    SetupThirdPersonCamera();
                    break;
                case CameraMode.Fixed:
                    SetupFixedCamera();
                    break;
                case CameraMode.Cinematic:
                    SetupCinematicCamera();
                    break;
                case CameraMode.Free:
                    // Free mode - no automatic setup
                    break;
            }
        }
        
        private void SetupIsometricCamera()
        {
            Vector3 cameraPos = new Vector3(-1, 1, -1).normalized * cameraDistance;
            cameraRig.position = followTarget != null ? followTarget.position + targetOffset : targetOffset;
            transform.localPosition = cameraPos;
            transform.LookAt(cameraRig.position);
            
            mainCamera.orthographic = true;
            mainCamera.orthographicSize = orthographicSize;
        }
        
        private void SetupTopDownCamera()
        {
            cameraRig.position = followTarget != null ? followTarget.position + targetOffset : targetOffset;
            transform.localPosition = Vector3.up * cameraDistance;
            transform.localRotation = Quaternion.Euler(90, 0, 0);
            
            mainCamera.orthographic = orthographic;
            if (orthographic)
            {
                mainCamera.orthographicSize = orthographicSize;
            }
        }
        
        private void SetupThirdPersonCamera()
        {
            if (followTarget != null)
            {
                Vector3 offset = Quaternion.Euler(cameraAngle, 0, 0) * Vector3.back * cameraDistance;
                cameraRig.position = followTarget.position + targetOffset;
                transform.localPosition = offset;
                transform.LookAt(followTarget.position + targetOffset);
            }
            
            mainCamera.orthographic = false;
            mainCamera.fieldOfView = fieldOfView;
        }
        
        private void SetupFixedCamera()
        {
            // Fixed camera doesn't move
            mainCamera.orthographic = orthographic;
            if (orthographic)
            {
                mainCamera.orthographicSize = orthographicSize;
            }
            else
            {
                mainCamera.fieldOfView = fieldOfView;
            }
        }
        
        private void SetupCinematicCamera()
        {
            if (waypoints.Count > 0)
            {
                StartWaypointSequence();
            }
            
            mainCamera.orthographic = false;
            mainCamera.fieldOfView = fieldOfView;
            enableCinematicBars = true;
        }
        
        #endregion
        
        #region Camera Movement
        
        private void UpdateCameraPosition()
        {
            if (cameraMode == CameraMode.Fixed || cameraMode == CameraMode.Free)
                return;
            
            // Calculate target position
            if (followTarget != null)
            {
                targetPosition = followTarget.position + targetOffset;
                
                if (lockYAxis)
                {
                    targetPosition.y = fixedHeight;
                }
            }
            
            // Apply boundaries
            if (useBoundaries)
            {
                targetPosition = ApplyBoundaries(targetPosition);
            }
            
            // Smooth movement
            if (followSmoothness > 0)
            {
                cameraRig.position = Vector3.SmoothDamp(
                    cameraRig.position,
                    targetPosition,
                    ref currentVelocity,
                    1f / followSmoothness
                );
            }
            else
            {
                cameraRig.position = targetPosition;
            }
            
            // Update camera look direction for third person
            if (cameraMode == CameraMode.ThirdPerson && followTarget != null)
            {
                Vector3 lookTarget = followTarget.position + targetOffset;
                transform.LookAt(lookTarget);
            }
        }
        
        private Vector3 ApplyBoundaries(Vector3 position)
        {
            if (softBoundaries)
            {
                // Soft boundaries with smooth clamping
                Vector3 min = cameraBounds.min + Vector3.one * boundaryPadding;
                Vector3 max = cameraBounds.max - Vector3.one * boundaryPadding;
                
                position.x = SmoothClamp(position.x, min.x, max.x, boundaryPadding);
                position.y = SmoothClamp(position.y, min.y, max.y, boundaryPadding);
                position.z = SmoothClamp(position.z, min.z, max.z, boundaryPadding);
            }
            else
            {
                // Hard boundaries
                position = cameraBounds.ClosestPoint(position);
            }
            
            return position;
        }
        
        private float SmoothClamp(float value, float min, float max, float smoothness)
        {
            if (value < min)
            {
                float t = (min - value) / smoothness;
                return Mathf.Lerp(value, min, Mathf.Clamp01(t));
            }
            else if (value > max)
            {
                float t = (value - max) / smoothness;
                return Mathf.Lerp(value, max, Mathf.Clamp01(t));
            }
            return value;
        }
        
        #endregion
        
        #region Camera Shake
        
        public void ShakeCamera(float intensity, float duration = 0.5f)
        {
            if (!enableShake)
                return;
            
            currentShakeIntensity = Mathf.Min(intensity, maxShakeIntensity);
            CancelInvoke(nameof(DecayShake));
            InvokeRepeating(nameof(DecayShake), 0, Time.deltaTime);
            Invoke(nameof(StopShake), duration);
        }
        
        private void UpdateCameraShake()
        {
            if (currentShakeIntensity > 0)
            {
                shakeOffset = Random.insideUnitSphere * currentShakeIntensity;
                transform.localPosition += shakeOffset;
            }
        }
        
        private void DecayShake()
        {
            currentShakeIntensity *= (1f - shakeDecay * Time.deltaTime);
            if (currentShakeIntensity < 0.01f)
            {
                StopShake();
            }
        }
        
        private void StopShake()
        {
            currentShakeIntensity = 0;
            shakeOffset = Vector3.zero;
            CancelInvoke(nameof(DecayShake));
        }
        
        #endregion
        
        #region Tilt-Shift Effect
        
        private void UpdateTiltShift()
        {
            if (!enableTiltShift)
                return;
            
            // Auto-focus on target
            if (autoFocus && followTarget != null)
            {
                focusDistance = Vector3.Distance(transform.position, followTarget.position);
            }
            
            // Update depth of field settings
            if (depthOfFieldEffect != null && enableDepthOfField)
            {
                depthOfFieldEffect.focusDistance.value = focusDistance;
                depthOfFieldEffect.aperture.value = aperture;
                depthOfFieldEffect.focalLength.value = focalLength;
                depthOfFieldEffect.bladeCount.value = bladeCount;
                
                // Calculate blur based on tilt-shift settings
                float blurMultiplier = blurCurve.Evaluate(Mathf.Clamp01(blurAmount / maxBlurSize));
                depthOfFieldEffect.maxBlurSize.value = maxBlurSize * blurMultiplier;
            }
        }
        
        public void SetFocusDistance(float distance)
        {
            focusDistance = Mathf.Max(0.1f, distance);
        }
        
        public void FocusOnPoint(Vector3 worldPoint)
        {
            focusDistance = Vector3.Distance(transform.position, worldPoint);
        }
        
        public void FocusOnObject(GameObject target)
        {
            if (target != null)
            {
                FocusOnPoint(target.transform.position);
            }
        }
        
        #endregion
        
        #region Cinematic
        
        private void UpdateCinematicBars()
        {
            float targetBar = enableCinematicBars ? cinematicBarHeight : 0f;
            currentCinematicBar = Mathf.Lerp(currentCinematicBar, targetBar, Time.deltaTime * cinematicTransitionSpeed);
            
            // This would typically be handled by UI or render texture
            // For now, we'll just track the value
        }
        
        public void StartWaypointSequence()
        {
            if (waypoints.Count == 0)
                return;
            
            currentWaypointIndex = 0;
            isTransitioning = true;
            MoveToWaypoint(0);
        }
        
        private void MoveToWaypoint(int index)
        {
            if (index >= waypoints.Count)
            {
                isTransitioning = false;
                return;
            }
            
            CameraWaypoint waypoint = waypoints[index];
            targetPosition = waypoint.position;
            targetRotation = waypoint.rotation;
            mainCamera.fieldOfView = waypoint.fieldOfView;
            focusDistance = waypoint.focusDistance;
        }
        
        private void UpdateWaypointTransition()
        {
            if (currentWaypointIndex >= waypoints.Count)
            {
                isTransitioning = false;
                return;
            }
            
            CameraWaypoint currentWaypoint = waypoints[currentWaypointIndex];
            
            // Check if we've reached the waypoint
            float distance = Vector3.Distance(cameraRig.position, currentWaypoint.position);
            if (distance < 0.1f)
            {
                // Wait at waypoint if specified
                if (currentWaypoint.waitTime > 0)
                {
                    Invoke(nameof(NextWaypoint), currentWaypoint.waitTime);
                    isTransitioning = false;
                }
                else
                {
                    NextWaypoint();
                }
            }
        }
        
        private void NextWaypoint()
        {
            currentWaypointIndex++;
            if (currentWaypointIndex < waypoints.Count)
            {
                isTransitioning = true;
                MoveToWaypoint(currentWaypointIndex);
            }
            else
            {
                isTransitioning = false;
            }
        }
        
        public void SetCinematicBars(bool enabled)
        {
            enableCinematicBars = enabled;
        }
        
        #endregion
        
        #region Public API
        
        public void SetCameraMode(CameraMode mode)
        {
            cameraMode = mode;
            ApplyCameraMode();
        }
        
        public void SetFollowTarget(Transform target)
        {
            followTarget = target;
        }
        
        public void SetCameraDistance(float distance)
        {
            cameraDistance = Mathf.Max(1f, distance);
            ApplyCameraMode();
        }
        
        public void SetCameraAngle(float angle)
        {
            cameraAngle = Mathf.Clamp(angle, 0f, 90f);
            ApplyCameraMode();
        }
        
        public void SetFieldOfView(float fov)
        {
            fieldOfView = Mathf.Clamp(fov, 10f, 120f);
            mainCamera.fieldOfView = fieldOfView;
        }
        
        public void SetOrthographicSize(float size)
        {
            orthographicSize = Mathf.Max(0.1f, size);
            if (mainCamera.orthographic)
            {
                mainCamera.orthographicSize = orthographicSize;
            }
        }
        
        public void ToggleOrthographic()
        {
            orthographic = !orthographic;
            mainCamera.orthographic = orthographic;
            
            if (orthographic)
            {
                mainCamera.orthographicSize = orthographicSize;
            }
            else
            {
                mainCamera.fieldOfView = fieldOfView;
            }
        }
        
        public void AddWaypoint(Vector3 position, Quaternion rotation, float fov = 30f, float focus = 15f, float wait = 0f)
        {
            waypoints.Add(new CameraWaypoint
            {
                position = position,
                rotation = rotation,
                fieldOfView = fov,
                focusDistance = focus,
                waitTime = wait
            });
        }
        
        public void ClearWaypoints()
        {
            waypoints.Clear();
            currentWaypointIndex = 0;
            isTransitioning = false;
        }
        
        public Vector3 GetCameraForward()
        {
            return transform.forward;
        }
        
        public Vector3 GetCameraRight()
        {
            return transform.right;
        }
        
        public Ray ScreenPointToRay(Vector3 screenPoint)
        {
            return mainCamera.ScreenPointToRay(screenPoint);
        }
        
        public Vector3 WorldToScreenPoint(Vector3 worldPoint)
        {
            return mainCamera.WorldToScreenPoint(worldPoint);
        }
        
        public Vector3 ScreenToWorldPoint(Vector3 screenPoint)
        {
            return mainCamera.ScreenToWorldPoint(screenPoint);
        }
        
        #endregion
        
        #region Gizmos
        
        void OnDrawGizmos()
        {
            if (showFocusPlane && enableTiltShift)
            {
                // Draw focus plane
                Gizmos.color = focusPlaneColor;
                Vector3 planeCenter = transform.position + transform.forward * focusDistance;
                Vector3 planeNormal = transform.forward;
                
                // Draw a disc to represent the focus plane
                DrawGizmoDisc(planeCenter, planeNormal, focusRange);
            }
            
            if (showBoundaries && useBoundaries)
            {
                // Draw camera boundaries
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireCube(cameraBounds.center, cameraBounds.size);
                
                if (softBoundaries)
                {
                    Gizmos.color = new Color(1, 1, 0, 0.3f);
                    Gizmos.DrawWireCube(cameraBounds.center, cameraBounds.size - Vector3.one * boundaryPadding * 2);
                }
            }
            
            // Draw waypoints
            if (cameraMode == CameraMode.Cinematic && waypoints.Count > 0)
            {
                Gizmos.color = Color.cyan;
                for (int i = 0; i < waypoints.Count; i++)
                {
                    Gizmos.DrawWireSphere(waypoints[i].position, 0.5f);
                    
                    if (i > 0)
                    {
                        Gizmos.DrawLine(waypoints[i - 1].position, waypoints[i].position);
                    }
                }
            }
        }
        
        private void DrawGizmoDisc(Vector3 center, Vector3 normal, float radius)
        {
            Vector3 tangent = Vector3.Cross(normal, Vector3.up);
            if (tangent.magnitude < 0.01f)
            {
                tangent = Vector3.Cross(normal, Vector3.right);
            }
            tangent.Normalize();
            
            Vector3 binormal = Vector3.Cross(normal, tangent);
            
            int segments = 32;
            for (int i = 0; i < segments; i++)
            {
                float angle1 = (i / (float)segments) * Mathf.PI * 2;
                float angle2 = ((i + 1) / (float)segments) * Mathf.PI * 2;
                
                Vector3 p1 = center + (tangent * Mathf.Cos(angle1) + binormal * Mathf.Sin(angle1)) * radius;
                Vector3 p2 = center + (tangent * Mathf.Cos(angle2) + binormal * Mathf.Sin(angle2)) * radius;
                
                Gizmos.DrawLine(p1, p2);
            }
        }
        
        #endregion
    }
}