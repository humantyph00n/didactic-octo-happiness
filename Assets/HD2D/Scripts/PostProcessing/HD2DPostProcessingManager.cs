using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.Collections.Generic;
using System.Collections;

namespace HD2D.PostProcessing
{
    /// <summary>
    /// Manages post-processing effects for HD-2D visual style
    /// Handles bloom, color grading, vignette, and custom effects
    /// </summary>
    public class HD2DPostProcessingManager : MonoBehaviour
    {
        [Header("Volume Configuration")]
        [SerializeField] private Volume globalVolume;
        [SerializeField] private VolumeProfile defaultProfile;
        [SerializeField] private float transitionDuration = 1f;
        [SerializeField] private AnimationCurve transitionCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        
        [Header("Bloom Settings")]
        [SerializeField] private bool enableBloom = true;
        [SerializeField] private float bloomIntensity = 0.5f;
        [SerializeField] private float bloomThreshold = 1.1f;
        [SerializeField] private float bloomScatter = 0.7f;
        [SerializeField] private Color bloomTint = Color.white;
        [SerializeField] private bool bloomHighQuality = true;
        
        [Header("Color Grading")]
        [SerializeField] private bool enableColorGrading = true;
        [SerializeField] private float temperature = 0f;
        [SerializeField] private float tint = 0f;
        [SerializeField] private Color colorFilter = Color.white;
        [SerializeField] private float hueShift = 0f;
        [SerializeField] private float saturation = 0f;
        [SerializeField] private float brightness = 0f;
        [SerializeField] private float contrast = 0f;
        [SerializeField] private Vector4 lift = Vector4.zero;
        [SerializeField] private Vector4 gamma = Vector4.zero;
        [SerializeField] private Vector4 gain = Vector4.zero;
        
        [Header("Tonemapping")]
        [SerializeField] private TonemappingMode tonemappingMode = TonemappingMode.ACES;
        
        [Header("Vignette")]
        [SerializeField] private bool enableVignette = true;
        [SerializeField] private float vignetteIntensity = 0.3f;
        [SerializeField] private float vignetteSmoothness = 0.5f;
        [SerializeField] private bool vignetteRounded = false;
        [SerializeField] private Color vignetteColor = Color.black;
        
        [Header("Chromatic Aberration")]
        [SerializeField] private bool enableChromaticAberration = false;
        [SerializeField] private float chromaticAberrationIntensity = 0.1f;
        
        [Header("Film Grain")]
        [SerializeField] private bool enableFilmGrain = false;
        [SerializeField] private FilmGrainLookup grainType = FilmGrainLookup.Thin1;
        [SerializeField] private float grainIntensity = 0.5f;
        [SerializeField] private float grainResponse = 0.8f;
        
        [Header("Depth of Field")]
        [SerializeField] private bool enableDepthOfField = true;
        [SerializeField] private DepthOfFieldMode dofMode = DepthOfFieldMode.Bokeh;
        [SerializeField] private float focusDistance = 10f;
        [SerializeField] private float focalLength = 50f;
        [SerializeField] private float aperture = 5.6f;
        [SerializeField] private int bladeCount = 5;
        [SerializeField] private float bladeCurvature = 1f;
        [SerializeField] private float bladeRotation = 0f;
        
        [Header("Motion Blur")]
        [SerializeField] private bool enableMotionBlur = false;
        [SerializeField] private MotionBlurMode motionBlurMode = MotionBlurMode.CameraAndObjects;
        [SerializeField] private MotionBlurQuality motionBlurQuality = MotionBlurQuality.Medium;
        [SerializeField] private float motionBlurIntensity = 0.5f;
        [SerializeField] private float motionBlurClamp = 0.05f;
        
        [Header("Screen Space Reflections")]
        [SerializeField] private bool enableSSR = false;
        [SerializeField] private ScreenSpaceReflectionPreset ssrPreset = ScreenSpaceReflectionPreset.Medium;
        [SerializeField] private float ssrMaxDistance = 50f;
        [SerializeField] private float ssrFadeDistance = 50f;
        
        [Header("Ambient Occlusion")]
        [SerializeField] private bool enableAmbientOcclusion = true;
        [SerializeField] private float aoIntensity = 0.4f;
        [SerializeField] private float aoRadius = 0.3f;
        [SerializeField] private AmbientOcclusionQuality aoQuality = AmbientOcclusionQuality.Medium;
        [SerializeField] private Color aoColor = Color.black;
        
        [Header("Custom Effects")]
        [SerializeField] private bool enablePixelation = false;
        [SerializeField] private int pixelationSize = 4;
        [SerializeField] private bool enableDithering = false;
        [SerializeField] private float ditheringIntensity = 0.5f;
        [SerializeField] private bool enableOutlines = false;
        [SerializeField] private float outlineThickness = 1f;
        [SerializeField] private Color outlineColor = Color.black;
        
        [Header("Presets")]
        [SerializeField] private List<PostProcessPreset> presets = new List<PostProcessPreset>();
        [SerializeField] private int activePresetIndex = -1;
        
        // Components
        private Bloom bloomEffect;
        private ColorAdjustments colorAdjustmentsEffect;
        private ColorCurves colorCurvesEffect;
        private Tonemapping tonemappingEffect;
        private Vignette vignetteEffect;
        private ChromaticAberration chromaticAberrationEffect;
        private FilmGrain filmGrainEffect;
        private DepthOfField depthOfFieldEffect;
        private MotionBlur motionBlurEffect;
        private ScreenSpaceReflection ssrEffect;
        private ScreenSpaceAmbientOcclusion aoEffect;
        private LiftGammaGain liftGammaGainEffect;
        
        // Runtime state
        private Coroutine transitionCoroutine;
        private Dictionary<string, float> effectWeights = new Dictionary<string, float>();
        private bool isTransitioning;
        
        [System.Serializable]
        public class PostProcessPreset
        {
            public string name = "New Preset";
            public VolumeProfile profile;
            public float transitionTime = 1f;
            public AnimationCurve transitionCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
            
            // Quick settings
            public float bloomIntensity = 0.5f;
            public float vignetteIntensity = 0.3f;
            public float saturation = 0f;
            public float contrast = 0f;
            public Color colorFilter = Color.white;
        }
        
        #region Unity Lifecycle
        
        void Awake()
        {
            InitializeVolume();
            CacheEffects();
        }
        
        void Start()
        {
            ApplySettings();
        }
        
        void Update()
        {
            if (!isTransitioning)
            {
                UpdateEffects();
            }
        }
        
        void OnValidate()
        {
            bloomIntensity = Mathf.Max(0f, bloomIntensity);
            bloomThreshold = Mathf.Max(0f, bloomThreshold);
            bloomScatter = Mathf.Clamp01(bloomScatter);
            vignetteIntensity = Mathf.Clamp01(vignetteIntensity);
            vignetteSmoothness = Mathf.Clamp01(vignetteSmoothness);
            chromaticAberrationIntensity = Mathf.Clamp(chromaticAberrationIntensity, 0f, 1f);
            grainIntensity = Mathf.Clamp01(grainIntensity);
            grainResponse = Mathf.Clamp01(grainResponse);
            aperture = Mathf.Clamp(aperture, 1f, 32f);
            focalLength = Mathf.Clamp(focalLength, 1f, 300f);
            bladeCount = Mathf.Clamp(bladeCount, 3, 9);
            motionBlurIntensity = Mathf.Clamp01(motionBlurIntensity);
            aoIntensity = Mathf.Clamp(aoIntensity, 0f, 4f);
            aoRadius = Mathf.Clamp(aoRadius, 0.0001f, 1f);
            pixelationSize = Mathf.Max(1, pixelationSize);
            ditheringIntensity = Mathf.Clamp01(ditheringIntensity);
            outlineThickness = Mathf.Max(0f, outlineThickness);
        }
        
        #endregion
        
        #region Initialization
        
        private void InitializeVolume()
        {
            // Find or create global volume
            if (globalVolume == null)
            {
                globalVolume = GetComponent<Volume>();
                if (globalVolume == null)
                {
                    globalVolume = gameObject.AddComponent<Volume>();
                }
            }
            
            // Set up volume
            globalVolume.isGlobal = true;
            
            // Create or assign profile
            if (globalVolume.profile == null)
            {
                if (defaultProfile != null)
                {
                    globalVolume.profile = Instantiate(defaultProfile);
                }
                else
                {
                    globalVolume.profile = ScriptableObject.CreateInstance<VolumeProfile>();
                }
            }
        }
        
        private void CacheEffects()
        {
            if (globalVolume.profile == null)
                return;
            
            // Cache all effect references
            globalVolume.profile.TryGet(out bloomEffect);
            globalVolume.profile.TryGet(out colorAdjustmentsEffect);
            globalVolume.profile.TryGet(out colorCurvesEffect);
            globalVolume.profile.TryGet(out tonemappingEffect);
            globalVolume.profile.TryGet(out vignetteEffect);
            globalVolume.profile.TryGet(out chromaticAberrationEffect);
            globalVolume.profile.TryGet(out filmGrainEffect);
            globalVolume.profile.TryGet(out depthOfFieldEffect);
            globalVolume.profile.TryGet(out motionBlurEffect);
            globalVolume.profile.TryGet(out ssrEffect);
            globalVolume.profile.TryGet(out aoEffect);
            globalVolume.profile.TryGet(out liftGammaGainEffect);
            
            // Create missing effects
            CreateMissingEffects();
        }
        
        private void CreateMissingEffects()
        {
            // Note: In actual implementation, you'd need to add effects to the profile
            // This requires the profile to be an instance, not a shared asset
            
            if (bloomEffect == null && enableBloom)
            {
                Debug.LogWarning("Bloom effect not found in profile. Please add it manually.");
            }
            
            if (colorAdjustmentsEffect == null && enableColorGrading)
            {
                Debug.LogWarning("Color Adjustments effect not found in profile. Please add it manually.");
            }
            
            // ... similar for other effects
        }
        
        #endregion
        
        #region Effect Management
        
        private void ApplySettings()
        {
            // Apply bloom settings
            if (bloomEffect != null)
            {
                bloomEffect.active = enableBloom;
                bloomEffect.intensity.value = bloomIntensity;
                bloomEffect.threshold.value = bloomThreshold;
                bloomEffect.scatter.value = bloomScatter;
                bloomEffect.tint.value = bloomTint;
                bloomEffect.highQualityFiltering.value = bloomHighQuality;
            }
            
            // Apply color grading
            if (colorAdjustmentsEffect != null)
            {
                colorAdjustmentsEffect.active = enableColorGrading;
                colorAdjustmentsEffect.postExposure.value = brightness;
                colorAdjustmentsEffect.contrast.value = contrast;
                colorAdjustmentsEffect.colorFilter.value = colorFilter;
                colorAdjustmentsEffect.hueShift.value = hueShift;
                colorAdjustmentsEffect.saturation.value = saturation;
            }
            
            // Apply lift/gamma/gain
            if (liftGammaGainEffect != null)
            {
                liftGammaGainEffect.active = enableColorGrading;
                liftGammaGainEffect.lift.value = lift;
                liftGammaGainEffect.gamma.value = gamma;
                liftGammaGainEffect.gain.value = gain;
            }
            
            // Apply tonemapping
            if (tonemappingEffect != null)
            {
                tonemappingEffect.active = true;
                tonemappingEffect.mode.value = tonemappingMode;
            }
            
            // Apply vignette
            if (vignetteEffect != null)
            {
                vignetteEffect.active = enableVignette;
                vignetteEffect.intensity.value = vignetteIntensity;
                vignetteEffect.smoothness.value = vignetteSmoothness;
                vignetteEffect.rounded.value = vignetteRounded;
                vignetteEffect.color.value = vignetteColor;
            }
            
            // Apply chromatic aberration
            if (chromaticAberrationEffect != null)
            {
                chromaticAberrationEffect.active = enableChromaticAberration;
                chromaticAberrationEffect.intensity.value = chromaticAberrationIntensity;
            }
            
            // Apply film grain
            if (filmGrainEffect != null)
            {
                filmGrainEffect.active = enableFilmGrain;
                filmGrainEffect.type.value = grainType;
                filmGrainEffect.intensity.value = grainIntensity;
                filmGrainEffect.response.value = grainResponse;
            }
            
            // Apply depth of field
            if (depthOfFieldEffect != null)
            {
                depthOfFieldEffect.active = enableDepthOfField;
                depthOfFieldEffect.mode.value = dofMode;
                depthOfFieldEffect.focusDistance.value = focusDistance;
                depthOfFieldEffect.focalLength.value = focalLength;
                depthOfFieldEffect.aperture.value = aperture;
                depthOfFieldEffect.bladeCount.value = bladeCount;
                depthOfFieldEffect.bladeCurvature.value = bladeCurvature;
                depthOfFieldEffect.bladeRotation.value = bladeRotation;
            }
            
            // Apply motion blur
            if (motionBlurEffect != null)
            {
                motionBlurEffect.active = enableMotionBlur;
                motionBlurEffect.mode.value = motionBlurMode;
                motionBlurEffect.quality.value = motionBlurQuality;
                motionBlurEffect.intensity.value = motionBlurIntensity;
                motionBlurEffect.clamp.value = motionBlurClamp;
            }
            
            // Apply SSR
            if (ssrEffect != null)
            {
                ssrEffect.active = enableSSR;
                ssrEffect.preset.value = ssrPreset;
                ssrEffect.maxDistance.value = ssrMaxDistance;
                ssrEffect.fadeDistance.value = ssrFadeDistance;
            }
            
            // Apply ambient occlusion
            if (aoEffect != null)
            {
                aoEffect.active = enableAmbientOcclusion;
                aoEffect.intensity.value = aoIntensity;
                aoEffect.radius.value = aoRadius;
                aoEffect.quality.value = aoQuality;
                aoEffect.color.value = aoColor;
            }
        }
        
        private void UpdateEffects()
        {
            // Update any dynamic effects here
            // This is called every frame for real-time adjustments
        }
        
        #endregion
        
        #region Preset Management
        
        public void LoadPreset(int index)
        {
            if (index < 0 || index >= presets.Count)
                return;
            
            PostProcessPreset preset = presets[index];
            activePresetIndex = index;
            
            if (transitionCoroutine != null)
            {
                StopCoroutine(transitionCoroutine);
            }
            
            transitionCoroutine = StartCoroutine(TransitionToPreset(preset));
        }
        
        public void LoadPreset(string presetName)
        {
            for (int i = 0; i < presets.Count; i++)
            {
                if (presets[i].name == presetName)
                {
                    LoadPreset(i);
                    return;
                }
            }
            
            Debug.LogWarning($"Preset '{presetName}' not found");
        }
        
        private IEnumerator TransitionToPreset(PostProcessPreset preset)
        {
            isTransitioning = true;
            float elapsed = 0f;
            
            // Cache initial values
            float startBloom = bloomIntensity;
            float startVignette = vignetteIntensity;
            float startSaturation = saturation;
            float startContrast = contrast;
            Color startColorFilter = colorFilter;
            
            while (elapsed < preset.transitionTime)
            {
                elapsed += Time.deltaTime;
                float t = preset.transitionCurve.Evaluate(elapsed / preset.transitionTime);
                
                // Lerp values
                bloomIntensity = Mathf.Lerp(startBloom, preset.bloomIntensity, t);
                vignetteIntensity = Mathf.Lerp(startVignette, preset.vignetteIntensity, t);
                saturation = Mathf.Lerp(startSaturation, preset.saturation, t);
                contrast = Mathf.Lerp(startContrast, preset.contrast, t);
                colorFilter = Color.Lerp(startColorFilter, preset.colorFilter, t);
                
                ApplySettings();
                yield return null;
            }
            
            // Apply final values
            bloomIntensity = preset.bloomIntensity;
            vignetteIntensity = preset.vignetteIntensity;
            saturation = preset.saturation;
            contrast = preset.contrast;
            colorFilter = preset.colorFilter;
            
            ApplySettings();
            
            // Load full profile if available
            if (preset.profile != null)
            {
                globalVolume.profile = Instantiate(preset.profile);
                CacheEffects();
            }
            
            isTransitioning = false;
        }
        
        public void SaveCurrentAsPreset(string presetName)
        {
            PostProcessPreset newPreset = new PostProcessPreset
            {
                name = presetName,
                bloomIntensity = bloomIntensity,
                vignetteIntensity = vignetteIntensity,
                saturation = saturation,
                contrast = contrast,
                colorFilter = colorFilter,
                transitionTime = transitionDuration,
                transitionCurve = transitionCurve
            };
            
            presets.Add(newPreset);
        }
        
        #endregion
        
        #region Public API
        
        // Bloom controls
        public void SetBloomIntensity(float intensity)
        {
            bloomIntensity = Mathf.Max(0f, intensity);
            if (bloomEffect != null)
            {
                bloomEffect.intensity.value = bloomIntensity;
            }
        }
        
        public void SetBloomThreshold(float threshold)
        {
            bloomThreshold = Mathf.Max(0f, threshold);
            if (bloomEffect != null)
            {
                bloomEffect.threshold.value = bloomThreshold;
            }
        }
        
        // Color grading controls
        public void SetSaturation(float value)
        {
            saturation = value;
            if (colorAdjustmentsEffect != null)
            {
                colorAdjustmentsEffect.saturation.value = saturation;
            }
        }
        
        public void SetContrast(float value)
        {
            contrast = value;
            if (colorAdjustmentsEffect != null)
            {
                colorAdjustmentsEffect.contrast.value = contrast;
            }
        }
        
        public void SetBrightness(float value)
        {
            brightness = value;
            if (colorAdjustmentsEffect != null)
            {
                colorAdjustmentsEffect.postExposure.value = brightness;
            }
        }
        
        public void SetColorFilter(Color color)
        {
            colorFilter = color;
            if (colorAdjustmentsEffect != null)
            {
                colorAdjustmentsEffect.colorFilter.value = colorFilter;
            }
        }
        
        // Vignette controls
        public void SetVignetteIntensity(float intensity)
        {
            vignetteIntensity = Mathf.Clamp01(intensity);
            if (vignetteEffect != null)
            {
                vignetteEffect.intensity.value = vignetteIntensity;
            }
        }
        
        public void SetVignetteColor(Color color)
        {
            vignetteColor = color;
            if (vignetteEffect != null)
            {
                vignetteEffect.color.value = vignetteColor;
            }
        }
        
        // Depth of field controls
        public void SetFocusDistance(float distance)
        {
            focusDistance = Mathf.Max(0.1f, distance);
            if (depthOfFieldEffect != null)
            {
                depthOfFieldEffect.focusDistance.value = focusDistance;
            }
        }
        
        public void SetAperture(float value)
        {
            aperture = Mathf.Clamp(value, 1f, 32f);
            if (depthOfFieldEffect != null)
            {
                depthOfFieldEffect.aperture.value = aperture;
            }
        }
        
        // Ambient occlusion controls
        public void SetAmbientOcclusionIntensity(float intensity)
        {
            aoIntensity = Mathf.Clamp(intensity, 0f, 4f);
            if (aoEffect != null)
            {
                aoEffect.intensity.value = aoIntensity;
            }
        }
        
        // Effect toggles
        public void ToggleBloom()
        {
            enableBloom = !enableBloom;
            if (bloomEffect != null)
            {
                bloomEffect.active = enableBloom;
            }
        }
        
        public void ToggleVignette()
        {
            enableVignette = !enableVignette;
            if (vignetteEffect != null)
            {
                vignetteEffect.active = enableVignette;
            }
        }
        
        public void ToggleDepthOfField()
        {
            enableDepthOfField = !enableDepthOfField;
            if (depthOfFieldEffect != null)
            {
                depthOfFieldEffect.active = enableDepthOfField;
            }
        }
        
        public void ToggleAmbientOcclusion()
        {
            enableAmbientOcclusion = !enableAmbientOcclusion;
            if (aoEffect != null)
            {
                aoEffect.active = enableAmbientOcclusion;
            }
        }
        
        // Utility methods
        public void ResetToDefaults()
        {
            bloomIntensity = 0.5f;
            bloomThreshold = 1.1f;
            bloomScatter = 0.7f;
            bloomTint = Color.white;
            
            saturation = 0f;
            contrast = 0f;
            brightness = 0f;
            colorFilter = Color.white;
            
            vignetteIntensity = 0.3f;
            vignetteSmoothness = 0.5f;
            vignetteColor = Color.black;
            
            focusDistance = 10f;
            aperture = 5.6f;
            
            aoIntensity = 0.4f;
            aoRadius = 0.3f;
            
            ApplySettings();
        }
        
        public VolumeProfile GetCurrentProfile()
        {
            return globalVolume?.profile;
        }
        
        public void SetProfile(VolumeProfile profile)
        {
            if (globalVolume != null && profile != null)
            {
                globalVolume.profile = profile;
                CacheEffects();
                ApplySettings();
            }
        }
        
        #endregion
        
        #region Custom Effects
        
        public void EnablePixelation(int pixelSize)
        {
            enablePixelation = true;
            pixelationSize = Mathf.Max(1, pixelSize);
            // Custom pixelation implementation would go here
        }
        
        public void DisablePixelation()
        {
            enablePixelation = false;
        }
        
        public void EnableDithering(float intensity)
        {
            enableDithering = true;
            ditheringIntensity = Mathf.Clamp01(intensity);
            // Custom dithering implementation would go here
        }
        
        public void DisableDithering()
        {
            enableDithering = false;
        }
        
        public void EnableOutlines(float thickness, Color color)
        {
            enableOutlines = true;
            outlineThickness = Mathf.Max(0f, thickness);
            outlineColor = color;
            // Custom outline implementation would go here
        }
        
        public void DisableOutlines()
        {
            enableOutlines = false;
        }
        
        #endregion
    }
}