using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;
using System.Collections;
using HD2D.Rendering;
using HD2D.Rendering.Sprites;
using HD2D.Environment;
using HD2D.Camera;
using HD2D.PostProcessing;
using HD2D.Lighting;

namespace HD2D.Core
{
    /// <summary>
    /// Main scene composition manager for HD-2D rendering system
    /// Orchestrates all subsystems and manages scene setup
    /// </summary>
    public class HD2DSceneComposer : MonoBehaviour
    {
        private static HD2DSceneComposer instance;
        public static HD2DSceneComposer Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindObjectOfType<HD2DSceneComposer>();
                    if (instance == null)
                    {
                        GameObject go = new GameObject("HD2D Scene Composer");
                        instance = go.AddComponent<HD2DSceneComposer>();
                    }
                }
                return instance;
            }
        }
        
        [Header("Scene Configuration")]
        [SerializeField] private ScenePreset activePreset;
        [SerializeField] private List<ScenePreset> scenePresets = new List<ScenePreset>();
        [SerializeField] private bool autoInitialize = true;
        [SerializeField] private float initializationDelay = 0.1f;
        
        [Header("System Components")]
        [SerializeField] private HD2DEnvironmentBuilder environmentBuilder;
        [SerializeField] private HD2DTiltShiftCamera tiltShiftCamera;
        [SerializeField] private HD2DPostProcessingManager postProcessingManager;
        [SerializeField] private HD2DLightingManager lightingManager;
        [SerializeField] private HD2DMaterialManager materialManager;
        [SerializeField] private HD2DSpriteBatcher spriteBatcher;
        [SerializeField] private HD2DDepthSorting depthSorting;
        
        [Header("Scene Layers")]
        [SerializeField] private List<SceneLayer> sceneLayers = new List<SceneLayer>();
        [SerializeField] private int activeLayerMask = -1;
        [SerializeField] private bool autoSortLayers = true;
        
        [Header("Character Management")]
        [SerializeField] private Transform characterContainer;
        [SerializeField] private List<CharacterSetup> characters = new List<CharacterSetup>();
        [SerializeField] private float characterScale = 1f;
        [SerializeField] private bool autoSetupCharacters = true;
        
        [Header("Performance")]
        [SerializeField] private PerformanceProfile performanceProfile = PerformanceProfile.Balanced;
        [SerializeField] private int targetFrameRate = 60;
        [SerializeField] private bool adaptiveQuality = true;
        [SerializeField] private float qualityCheckInterval = 5f;
        
        [Header("Scene Transitions")]
        [SerializeField] private bool enableSceneTransitions = true;
        [SerializeField] private float defaultTransitionDuration = 1f;
        [SerializeField] private AnimationCurve transitionCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        [SerializeField] private TransitionEffect transitionEffect = TransitionEffect.Fade;
        
        [Header("Events")]
        public UnityEvent onSceneInitialized;
        public UnityEvent onSceneTransitionStart;
        public UnityEvent onSceneTransitionComplete;
        public UnityEvent<float> onPerformanceUpdate;
        
        // Runtime state
        private bool isInitialized = false;
        private bool isTransitioning = false;
        private Coroutine transitionCoroutine;
        private float lastQualityCheck;
        private float averageFrameTime;
        private Queue<float> frameTimeHistory = new Queue<float>();
        private Dictionary<string, GameObject> sceneObjects = new Dictionary<string, GameObject>();
        
        #region Data Structures
        
        [System.Serializable]
        public class ScenePreset
        {
            public string name = "New Scene";
            public SceneType sceneType = SceneType.Town;
            
            // Environment settings
            public EnvironmentSettings environment;
            
            // Camera settings
            public CameraSettings camera;
            
            // Lighting settings
            public LightingSettings lighting;
            
            // Post-processing settings
            public PostProcessSettings postProcess;
            
            // Audio settings
            public AudioSettings audio;
            
            [System.Serializable]
            public class EnvironmentSettings
            {
                public GameObject environmentPrefab;
                public float gridSize = 1f;
                public int gridWidth = 50;
                public int gridHeight = 50;
                public Material environmentMaterial;
                public bool enableProps = true;
                public float propDensity = 0.3f;
            }
            
            [System.Serializable]
            public class CameraSettings
            {
                public HD2DTiltShiftCamera.CameraMode cameraMode = HD2DTiltShiftCamera.CameraMode.Isometric;
                public float cameraDistance = 20f;
                public float cameraAngle = 45f;
                public float fieldOfView = 30f;
                public bool enableTiltShift = true;
                public float focusDistance = 15f;
            }
            
            [System.Serializable]
            public class LightingSettings
            {
                public float mainLightIntensity = 1f;
                public Color mainLightColor = Color.white;
                public Vector3 mainLightRotation = new Vector3(45f, -30f, 0f);
                public Color ambientColor = new Color(0.5f, 0.7f, 0.9f);
                public float ambientIntensity = 1f;
                public bool enableFog = true;
                public Color fogColor = new Color(0.5f, 0.6f, 0.7f);
                public float fogDensity = 0.01f;
            }
            
            [System.Serializable]
            public class PostProcessSettings
            {
                public bool enableBloom = true;
                public float bloomIntensity = 0.5f;
                public bool enableVignette = true;
                public float vignetteIntensity = 0.3f;
                public float saturation = 0f;
                public float contrast = 0f;
                public Color colorFilter = Color.white;
            }
            
            [System.Serializable]
            public class AudioSettings
            {
                public AudioClip ambientMusic;
                public float musicVolume = 0.7f;
                public AudioClip[] ambientSounds;
                public float ambientVolume = 0.5f;
            }
        }
        
        [System.Serializable]
        public class SceneLayer
        {
            public string name = "Layer";
            public LayerType type = LayerType.Environment;
            public GameObject layerRoot;
            public int sortingOrder = 0;
            public bool visible = true;
            public bool interactable = true;
            public float parallaxFactor = 0f;
            
            public enum LayerType
            {
                Background,
                Environment,
                Characters,
                Foreground,
                UI,
                Effects
            }
        }
        
        [System.Serializable]
        public class CharacterSetup
        {
            public string characterId;
            public GameObject characterPrefab;
            public Vector3 spawnPosition;
            public bool isPlayer = false;
            public bool autoSetupComponents = true;
            public HD2DSpriteRenderer.BillboardMode billboardMode = HD2DSpriteRenderer.BillboardMode.YAxis;
        }
        
        public enum SceneType
        {
            Town,
            Dungeon,
            Overworld,
            Battle,
            Interior,
            Cutscene
        }
        
        public enum PerformanceProfile
        {
            Low,
            Balanced,
            High,
            Ultra,
            Custom
        }
        
        public enum TransitionEffect
        {
            None,
            Fade,
            Iris,
            Wipe,
            Dissolve,
            Pixelate
        }
        
        #endregion
        
        #region Unity Lifecycle
        
        void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            
            instance = this;
            DontDestroyOnLoad(gameObject);
            
            if (autoInitialize)
            {
                StartCoroutine(InitializeWithDelay());
            }
        }
        
        void Start()
        {
            SetupPerformanceSettings();
        }
        
        void Update()
        {
            UpdatePerformanceMonitoring();
            
            if (adaptiveQuality && Time.time - lastQualityCheck > qualityCheckInterval)
            {
                CheckAndAdjustQuality();
                lastQualityCheck = Time.time;
            }
        }
        
        void OnDestroy()
        {
            CleanupScene();
        }
        
        #endregion
        
        #region Initialization
        
        private IEnumerator InitializeWithDelay()
        {
            yield return new WaitForSeconds(initializationDelay);
            InitializeScene();
        }
        
        public void InitializeScene()
        {
            if (isInitialized)
                return;
            
            Debug.Log("Initializing HD2D Scene...");
            
            // Find or create system components
            FindOrCreateComponents();
            
            // Setup scene layers
            SetupSceneLayers();
            
            // Apply active preset if available
            if (activePreset != null)
            {
                ApplyScenePreset(activePreset);
            }
            
            // Setup characters
            if (autoSetupCharacters)
            {
                SetupCharacters();
            }
            
            // Initialize subsystems
            InitializeSubsystems();
            
            isInitialized = true;
            onSceneInitialized?.Invoke();
            
            Debug.Log("HD2D Scene initialized successfully");
        }
        
        private void FindOrCreateComponents()
        {
            // Environment Builder
            if (environmentBuilder == null)
            {
                environmentBuilder = FindObjectOfType<HD2DEnvironmentBuilder>();
                if (environmentBuilder == null)
                {
                    GameObject envGo = new GameObject("HD2D Environment Builder");
                    environmentBuilder = envGo.AddComponent<HD2DEnvironmentBuilder>();
                }
            }
            
            // Camera
            if (tiltShiftCamera == null)
            {
                tiltShiftCamera = FindObjectOfType<HD2DTiltShiftCamera>();
                if (tiltShiftCamera == null && Camera.main != null)
                {
                    tiltShiftCamera = Camera.main.gameObject.AddComponent<HD2DTiltShiftCamera>();
                }
            }
            
            // Post Processing
            if (postProcessingManager == null)
            {
                postProcessingManager = FindObjectOfType<HD2DPostProcessingManager>();
                if (postProcessingManager == null)
                {
                    GameObject ppGo = new GameObject("HD2D Post Processing");
                    postProcessingManager = ppGo.AddComponent<HD2DPostProcessingManager>();
                }
            }
            
            // Lighting
            if (lightingManager == null)
            {
                lightingManager = FindObjectOfType<HD2DLightingManager>();
                if (lightingManager == null)
                {
                    GameObject lightGo = new GameObject("HD2D Lighting");
                    lightingManager = lightGo.AddComponent<HD2DLightingManager>();
                }
            }
            
            // Material Manager
            if (materialManager == null)
            {
                materialManager = HD2DMaterialManager.Instance;
            }
            
            // Sprite Batcher
            if (spriteBatcher == null)
            {
                spriteBatcher = FindObjectOfType<HD2DSpriteBatcher>();
                if (spriteBatcher == null)
                {
                    GameObject batcherGo = new GameObject("HD2D Sprite Batcher");
                    spriteBatcher = batcherGo.AddComponent<HD2DSpriteBatcher>();
                }
            }
            
            // Depth Sorting
            if (depthSorting == null)
            {
                depthSorting = FindObjectOfType<HD2DDepthSorting>();
                if (depthSorting == null)
                {
                    GameObject sortingGo = new GameObject("HD2D Depth Sorting");
                    depthSorting = sortingGo.AddComponent<HD2DDepthSorting>();
                }
            }
        }
        
        private void InitializeSubsystems()
        {
            // Initialize sprite batching
            if (spriteBatcher != null)
            {
                spriteBatcher.Initialize();
            }
            
            // Initialize depth sorting
            if (depthSorting != null)
            {
                depthSorting.Initialize();
            }
            
            // Setup material manager
            if (materialManager != null)
            {
                materialManager.WarmupShaders();
            }
        }
        
        #endregion
        
        #region Scene Layers
        
        private void SetupSceneLayers()
        {
            // Create default layers if none exist
            if (sceneLayers.Count == 0)
            {
                CreateDefaultLayers();
            }
            
            // Create layer game objects
            foreach (var layer in sceneLayers)
            {
                if (layer.layerRoot == null)
                {
                    layer.layerRoot = new GameObject($"Layer_{layer.name}");
                    layer.layerRoot.transform.SetParent(transform);
                }
                
                // Set layer properties
                SetLayerProperties(layer);
            }
            
            // Sort layers if enabled
            if (autoSortLayers)
            {
                SortLayers();
            }
        }
        
        private void CreateDefaultLayers()
        {
            sceneLayers.Add(new SceneLayer 
            { 
                name = "Background", 
                type = SceneLayer.LayerType.Background,
                sortingOrder = -100,
                parallaxFactor = 0.5f
            });
            
            sceneLayers.Add(new SceneLayer 
            { 
                name = "Environment", 
                type = SceneLayer.LayerType.Environment,
                sortingOrder = 0
            });
            
            sceneLayers.Add(new SceneLayer 
            { 
                name = "Characters", 
                type = SceneLayer.LayerType.Characters,
                sortingOrder = 100
            });
            
            sceneLayers.Add(new SceneLayer 
            { 
                name = "Foreground", 
                type = SceneLayer.LayerType.Foreground,
                sortingOrder = 200,
                parallaxFactor = -0.2f
            });
            
            sceneLayers.Add(new SceneLayer 
            { 
                name = "Effects", 
                type = SceneLayer.LayerType.Effects,
                sortingOrder = 300
            });
            
            sceneLayers.Add(new SceneLayer 
            { 
                name = "UI", 
                type = SceneLayer.LayerType.UI,
                sortingOrder = 1000,
                interactable = true
            });
        }
        
        private void SetLayerProperties(SceneLayer layer)
        {
            if (layer.layerRoot == null)
                return;
            
            // Set active state
            layer.layerRoot.SetActive(layer.visible);
            
            // Apply parallax if camera exists
            if (tiltShiftCamera != null && Mathf.Abs(layer.parallaxFactor) > 0.01f)
            {
                // Add parallax component if needed
                ParallaxLayer parallax = layer.layerRoot.GetComponent<ParallaxLayer>();
                if (parallax == null)
                {
                    parallax = layer.layerRoot.AddComponent<ParallaxLayer>();
                }
                parallax.parallaxFactor = layer.parallaxFactor;
            }
        }
        
        private void SortLayers()
        {
            sceneLayers.Sort((a, b) => a.sortingOrder.CompareTo(b.sortingOrder));
            
            for (int i = 0; i < sceneLayers.Count; i++)
            {
                if (sceneLayers[i].layerRoot != null)
                {
                    sceneLayers[i].layerRoot.transform.SetSiblingIndex(i);
                }
            }
        }
        
        #endregion
        
        #region Character Management
        
        private void SetupCharacters()
        {
            if (characterContainer == null)
            {
                SceneLayer characterLayer = sceneLayers.Find(l => l.type == SceneLayer.LayerType.Characters);
                if (characterLayer != null)
                {
                    characterContainer = characterLayer.layerRoot.transform;
                }
                else
                {
                    characterContainer = new GameObject("Characters").transform;
                    characterContainer.SetParent(transform);
                }
            }
            
            foreach (var setup in characters)
            {
                SpawnCharacter(setup);
            }
        }
        
        public GameObject SpawnCharacter(CharacterSetup setup)
        {
            if (setup.characterPrefab == null)
                return null;
            
            GameObject character = Instantiate(setup.characterPrefab, characterContainer);
            character.transform.position = setup.spawnPosition;
            character.transform.localScale = Vector3.one * characterScale;
            
            if (setup.autoSetupComponents)
            {
                SetupCharacterComponents(character, setup);
            }
            
            // Register character
            if (!string.IsNullOrEmpty(setup.characterId))
            {
                sceneObjects[setup.characterId] = character;
            }
            
            return character;
        }
        
        private void SetupCharacterComponents(GameObject character, CharacterSetup setup)
        {
            // Add or configure HD2D sprite renderer
            HD2DSpriteRenderer spriteRenderer = character.GetComponent<HD2DSpriteRenderer>();
            if (spriteRenderer == null)
            {
                spriteRenderer = character.AddComponent<HD2DSpriteRenderer>();
            }
            spriteRenderer.billboardMode = setup.billboardMode;
            spriteRenderer.receiveShadows = true;
            spriteRenderer.castShadows = true;
            
            // Add animation controller
            HD2DSpriteAnimationController animController = character.GetComponent<HD2DSpriteAnimationController>();
            if (animController == null)
            {
                animController = character.AddComponent<HD2DSpriteAnimationController>();
            }
            
            // Add shadow caster
            HD2DSpriteShadowCaster shadowCaster = character.GetComponent<HD2DSpriteShadowCaster>();
            if (shadowCaster == null)
            {
                shadowCaster = character.AddComponent<HD2DSpriteShadowCaster>();
            }
            
            // Setup player-specific components
            if (setup.isPlayer && tiltShiftCamera != null)
            {
                tiltShiftCamera.SetFollowTarget(character.transform);
            }
        }
        
        #endregion
        
        #region Scene Presets
        
        public void ApplyScenePreset(ScenePreset preset)
        {
            if (preset == null)
                return;
            
            activePreset = preset;
            
            // Apply environment settings
            if (preset.environment != null && environmentBuilder != null)
            {
                ApplyEnvironmentSettings(preset.environment);
            }
            
            // Apply camera settings
            if (preset.camera != null && tiltShiftCamera != null)
            {
                ApplyCameraSettings(preset.camera);
            }
            
            // Apply lighting settings
            if (preset.lighting != null && lightingManager != null)
            {
                ApplyLightingSettings(preset.lighting);
            }
            
            // Apply post-processing settings
            if (preset.postProcess != null && postProcessingManager != null)
            {
                ApplyPostProcessSettings(preset.postProcess);
            }
            
            // Apply audio settings
            if (preset.audio != null)
            {
                ApplyAudioSettings(preset.audio);
            }
        }
        
        private void ApplyEnvironmentSettings(ScenePreset.EnvironmentSettings settings)
        {
            // Environment builder settings would be applied here
            // This is a simplified version
            if (settings.environmentPrefab != null)
            {
                GameObject env = Instantiate(settings.environmentPrefab);
                env.transform.SetParent(GetLayer(SceneLayer.LayerType.Environment).transform);
            }
        }
        
        private void ApplyCameraSettings(ScenePreset.CameraSettings settings)
        {
            tiltShiftCamera.SetCameraMode(settings.cameraMode);
            tiltShiftCamera.SetCameraDistance(settings.cameraDistance);
            tiltShiftCamera.SetCameraAngle(settings.cameraAngle);
            tiltShiftCamera.SetFieldOfView(settings.fieldOfView);
            
            if (settings.enableTiltShift)
            {
                tiltShiftCamera.SetFocusDistance(settings.focusDistance);
            }
        }
        
        private void ApplyLightingSettings(ScenePreset.LightingSettings settings)
        {
            lightingManager.SetMainLightIntensity(settings.mainLightIntensity);
            lightingManager.SetMainLightColor(settings.mainLightColor);
            lightingManager.SetMainLightRotation(settings.mainLightRotation);
            lightingManager.SetAmbientIntensity(settings.ambientIntensity);
            
            if (settings.enableFog)
            {
                lightingManager.SetFogDensity(settings.fogDensity);
            }
        }
        
        private void ApplyPostProcessSettings(ScenePreset.PostProcessSettings settings)
        {
            postProcessingManager.SetBloomIntensity(settings.bloomIntensity);
            postProcessingManager.SetVignetteIntensity(settings.vignetteIntensity);
            postProcessingManager.SetSaturation(settings.saturation);
            postProcessingManager.SetContrast(settings.contrast);
            postProcessingManager.SetColorFilter(settings.colorFilter);
        }
        
        private void ApplyAudioSettings(ScenePreset.AudioSettings settings)
        {
            // Audio implementation would go here
            // This is a placeholder for audio system integration
        }
        
        #endregion
        
        #region Scene Transitions
        
        public void TransitionToScene(ScenePreset newPreset, float duration = -1)
        {
            if (isTransitioning)
                return;
            
            if (duration < 0)
                duration = defaultTransitionDuration;
            
            if (transitionCoroutine != null)
            {
                StopCoroutine(transitionCoroutine);
            }
            
            transitionCoroutine = StartCoroutine(PerformSceneTransition(newPreset, duration));
        }
        
        private IEnumerator PerformSceneTransition(ScenePreset newPreset, float duration)
        {
            isTransitioning = true;
            onSceneTransitionStart?.Invoke();
            
            // Perform transition effect
            yield return StartCoroutine(PerformTransitionEffect(transitionEffect, duration / 2, true));
            
            // Apply new preset
            ApplyScenePreset(newPreset);
            
            // Wait a frame for changes to apply
            yield return null;
            
            // Reverse transition effect
            yield return StartCoroutine(PerformTransitionEffect(transitionEffect, duration / 2, false));
            
            isTransitioning = false;
            onSceneTransitionComplete?.Invoke();
        }
        
        private IEnumerator PerformTransitionEffect(TransitionEffect effect, float duration, bool fadeOut)
        {
            float elapsed = 0f;
            
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = transitionCurve.Evaluate(elapsed / duration);
                
                if (!fadeOut)
                    t = 1f - t;
                
                switch (effect)
                {
                    case TransitionEffect.Fade:
                        // Apply fade effect
                        if (postProcessingManager != null)
                        {
                            postProcessingManager.SetBrightness(-t);
                        }
                        break;
                        
                    case TransitionEffect.Dissolve:
                        // Apply dissolve effect
                        Shader.SetGlobalFloat("_DissolveAmount", t);
                        break;
                        
                    case TransitionEffect.Pixelate:
                        // Apply pixelate effect
                        if (postProcessingManager != null)
                        {
                            postProcessingManager.EnablePixelation(Mathf.RoundToInt(Mathf.Lerp(1, 32, t)));
                        }
                        break;
                }
                
                yield return null;
            }
            
            // Reset effects
            if (postProcessingManager != null)
            {
                postProcessingManager.SetBrightness(0);
                postProcessingManager.DisablePixelation();
            }
            Shader.SetGlobalFloat("_DissolveAmount", 0);
        }
        
        #endregion
        
        #region Performance Management
        
        private void SetupPerformanceSettings()
        {
            Application.targetFrameRate = targetFrameRate;
            
            switch (performanceProfile)
            {
                case PerformanceProfile.Low:
                    QualitySettings.SetQualityLevel(0);
                    SetLowPerformanceSettings();
                    break;
                    
                case PerformanceProfile.Balanced:
                    QualitySettings.SetQualityLevel(2);
                    SetBalancedPerformanceSettings();
                    break;
                    
                case PerformanceProfile.High:
                    QualitySettings.SetQualityLevel(4);
                    SetHighPerformanceSettings();
                    break;
                    
                case PerformanceProfile.Ultra:
                    QualitySettings.SetQualityLevel(5);
                    SetUltraPerformanceSettings();
                    break;
            }
        }
        
        private void SetLowPerformanceSettings()
        {
            if (lightingManager != null)
            {
                lightingManager.SetQualityLevel(HD2DLightingManager.QualityLevel.Low);
            }
            
            if (materialManager != null)
            {
                materialManager.SetQualityLevel(0);
            }
            
            if (spriteBatcher != null)
            {
                spriteBatcher.SetMaxBatchSize(100);
            }
        }
        
        private void SetBalancedPerformanceSettings()
        {
            if (lightingManager != null)
            {
                lightingManager.SetQualityLevel(HD2DLightingManager.QualityLevel.Medium);
            }
            
            if (materialManager != null)
            {
                materialManager.SetQualityLevel(1);
            }
            
            if (spriteBatcher != null)
            {
                spriteBatcher.SetMaxBatchSize(500);
            }
        }
        
        private void SetHighPerformanceSettings()
        {
            if (lightingManager != null)
            {
                lightingManager.SetQualityLevel(HD2DLightingManager.QualityLevel.High);
            }
            
            if (materialManager != null)
            {
                materialManager.SetQualityLevel(2);
            }
            
            if (spriteBatcher != null)
            {
                spriteBatcher.SetMaxBatchSize(1000);
            }
        }
        
        private void SetUltraPerformanceSettings()
        {
            if (lightingManager != null)
            {
                lightingManager.SetQualityLevel(HD2DLightingManager.QualityLevel.Ultra);
            }
            
            if (materialManager != null)
            {
                materialManager.SetQualityLevel(3);
            }
            
            if (spriteBatcher != null)
            {
                spriteBatcher.SetMaxBatchSize(2000);
            }
        }
        
        private void UpdatePerformanceMonitoring()
        {
            float frameTime = Time.deltaTime;
            frameTimeHistory.Enqueue(frameTime);
            
            if (frameTimeHistory.Count > 60)
            {
                frameTimeHistory.Dequeue();
            }
            
            // Calculate average frame time
            float total = 0f;
            foreach (float time in frameTimeHistory)
            {
                total += time;
            }
            averageFrameTime = total / frameTimeHistory.Count;
        }
        
        private void CheckAndAdjustQuality()
        {
            float targetFrameTime = 1f / targetFrameRate;
            float performanceRatio = targetFrameTime / averageFrameTime;
            
            onPerformanceUpdate?.Invoke(performanceRatio);
            
            if (!adaptiveQuality)
                return;
            
            // Adjust quality based on performance
            if (performanceRatio < 0.9f) // Running below target
            {
                LowerQuality();
            }
            else if (performanceRatio > 1.1f) // Running above target
            {
                RaiseQuality();
            }
        }
        
        private void LowerQuality()
        {
            switch (performanceProfile)
            {
                case PerformanceProfile.Ultra:
                    performanceProfile = PerformanceProfile.High;
                    break;
                case PerformanceProfile.High:
                    performanceProfile = PerformanceProfile.Balanced;
                    break;
                case PerformanceProfile.Balanced:
                    performanceProfile = PerformanceProfile.Low;
                    break;
            }
            
            SetupPerformanceSettings();
        }
        
        private void RaiseQuality()
        {
            switch (performanceProfile)
            {
                case PerformanceProfile.Low:
                    performanceProfile = PerformanceProfile.Balanced;
                    break;
                case PerformanceProfile.Balanced:
                    performanceProfile = PerformanceProfile.High;
                    break;
                case PerformanceProfile.High:
                    performanceProfile = PerformanceProfile.Ultra;
                    break;
            }
            
            SetupPerformanceSettings();
        }
        
        #endregion
        
        #region Public API
        
        public GameObject GetLayer(SceneLayer.LayerType type)
        {
            SceneLayer layer = sceneLayers.Find(l => l.type == type);
            return layer?.layerRoot;
        }
        
        public void SetLayerVisibility(SceneLayer.LayerType type, bool visible)
        {
            SceneLayer layer = sceneLayers.Find(l => l.type == type);
            if (layer != null && layer.layerRoot != null)
            {
                layer.visible = visible;
                layer.layerRoot.SetActive(visible);
            }
        }
        
        public GameObject GetSceneObject(string id)
        {
            sceneObjects.TryGetValue(id, out GameObject obj);
            return obj;
        }
        
        public void RegisterSceneObject(string id, GameObject obj)
        {
            if (!string.IsNullOrEmpty(id) && obj != null)
            {
                sceneObjects[id] = obj;
            }
        }
        
        public void UnregisterSceneObject(string id)
        {
            sceneObjects.Remove(id);
        }
        
        public void SetPerformanceProfile(PerformanceProfile profile)
        {
            performanceProfile = profile;
            SetupPerformanceSettings();
        }
        
        public float GetCurrentFPS()
        {
            return averageFrameTime > 0 ? 1f / averageFrameTime : 0f;
        }
        
        public void LoadPreset(string presetName)
        {
            ScenePreset preset = scenePresets.Find(p => p.name == presetName);
            if (preset != null)
            {
                ApplyScenePreset(preset);
            }
            else
            {
                Debug.LogWarning($"Scene preset '{presetName}' not found");
            }
        }
        
        public void SaveCurrentAsPreset(string presetName)
        {
            ScenePreset newPreset = CreatePresetFromCurrentSettings();
            newPreset.name = presetName;
            scenePresets.Add(newPreset);
        }
        
        private ScenePreset CreatePresetFromCurrentSettings()
        {
            ScenePreset preset = new ScenePreset();
            
            // Capture current settings
            if (tiltShiftCamera != null)
            {
                preset.camera = new ScenePreset.CameraSettings
                {
                    cameraMode = HD2DTiltShiftCamera.CameraMode.Isometric,
                    cameraDistance = 20f,
                    cameraAngle = 45f,
                    fieldOfView = 30f,
                    enableTiltShift = true,
                    focusDistance = 15f
                };
            }
            
            // Add other settings capture here...
            
            return preset;
        }
        
        #endregion
        
        #region Cleanup
        
        private void CleanupScene()
        {
            // Stop any running coroutines
            if (transitionCoroutine != null)
            {
                StopCoroutine(transitionCoroutine);
            }
            
            // Clear scene objects
            sceneObjects.Clear();
            
            // Clear frame history
            frameTimeHistory.Clear();
        }
        
        #endregion
    }
    
    /// <summary>
    /// Simple parallax layer component
    /// </summary>
    public class ParallaxLayer : MonoBehaviour
    {
        public float parallaxFactor = 0.5f;
        private Vector3 startPosition;
        private Transform cameraTransform;
        
        void Start()
        {
            startPosition = transform.position;
            if (Camera.main != null)
            {
                cameraTransform = Camera.main.transform;
            }
        }
        
        void LateUpdate()
        {
            if (cameraTransform != null)
            {
                Vector3 distance = cameraTransform.position - startPosition;
                transform.position = startPosition + distance * parallaxFactor;
            }
        }
    }
}