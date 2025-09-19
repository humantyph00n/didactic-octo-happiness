using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.Collections.Generic;
using System.Linq;

namespace HD2D.Lighting
{
    /// <summary>
    /// Manages lighting for HD-2D visual style
    /// Handles dynamic lighting, shadows, and atmospheric effects
    /// </summary>
    public class HD2DLightingManager : MonoBehaviour
    {
        [Header("Global Lighting")]
        [SerializeField] private Light mainDirectionalLight;
        [SerializeField] private float mainLightIntensity = 1.0f;
        [SerializeField] private Color mainLightColor = Color.white;
        [SerializeField] private Vector3 mainLightRotation = new Vector3(45f, -30f, 0f);
        [SerializeField] private bool enableMainLightShadows = true;
        
        [Header("Ambient Lighting")]
        [SerializeField] private AmbientMode ambientMode = AmbientMode.Trilight;
        [SerializeField] private Color ambientSkyColor = new Color(0.5f, 0.7f, 0.9f);
        [SerializeField] private Color ambientEquatorColor = new Color(0.4f, 0.5f, 0.6f);
        [SerializeField] private Color ambientGroundColor = new Color(0.2f, 0.3f, 0.4f);
        [SerializeField] private float ambientIntensity = 1.0f;
        
        [Header("Shadow Settings")]
        [SerializeField] private ShadowResolution shadowResolution = ShadowResolution._2048;
        [SerializeField] private float shadowDistance = 50f;
        [SerializeField] private int shadowCascades = 4;
        [SerializeField] private Vector3 shadowCascadeRatios = new Vector3(0.1f, 0.25f, 0.5f);
        [SerializeField] private float shadowBias = 0.05f;
        [SerializeField] private float shadowNormalBias = 0.4f;
        [SerializeField] private float shadowNearPlane = 0.2f;
        [SerializeField] private bool softShadows = true;
        
        [Header("Sprite Lighting")]
        [SerializeField] private bool enableSpriteLighting = true;
        [SerializeField] private float spriteLightIntensity = 1.0f;
        [SerializeField] private float spriteAmbientIntensity = 0.5f;
        [SerializeField] private bool spritesReceiveShadows = true;
        [SerializeField] private bool spritesCastShadows = true;
        
        [Header("Point Lights")]
        [SerializeField] private int maxPointLights = 8;
        [SerializeField] private float pointLightRange = 10f;
        [SerializeField] private float pointLightIntensity = 1f;
        [SerializeField] private bool pointLightShadows = false;
        [SerializeField] private List<PointLightConfig> pointLights = new List<PointLightConfig>();
        
        [Header("Spot Lights")]
        [SerializeField] private int maxSpotLights = 4;
        [SerializeField] private float spotLightRange = 15f;
        [SerializeField] private float spotLightAngle = 30f;
        [SerializeField] private float spotLightIntensity = 2f;
        [SerializeField] private bool spotLightShadows = true;
        [SerializeField] private List<SpotLightConfig> spotLights = new List<SpotLightConfig>();
        
        [Header("Area Lights")]
        [SerializeField] private bool enableAreaLights = false;
        [SerializeField] private int maxAreaLights = 2;
        [SerializeField] private List<AreaLightConfig> areaLights = new List<AreaLightConfig>();
        
        [Header("Light Probes")]
        [SerializeField] private bool useLightProbes = true;
        [SerializeField] private LightProbeUsage lightProbeUsage = LightProbeUsage.BlendProbes;
        [SerializeField] private float lightProbeIntensity = 1.0f;
        
        [Header("Reflection Probes")]
        [SerializeField] private bool useReflectionProbes = true;
        [SerializeField] private ReflectionProbeUsage reflectionProbeUsage = ReflectionProbeUsage.BlendProbes;
        [SerializeField] private float reflectionIntensity = 1.0f;
        
        [Header("Fog")]
        [SerializeField] private bool enableFog = true;
        [SerializeField] private FogMode fogMode = FogMode.Linear;
        [SerializeField] private Color fogColor = new Color(0.5f, 0.6f, 0.7f);
        [SerializeField] private float fogDensity = 0.01f;
        [SerializeField] private float fogStartDistance = 10f;
        [SerializeField] private float fogEndDistance = 100f;
        
        [Header("Time of Day")]
        [SerializeField] private bool enableTimeOfDay = false;
        [SerializeField] private float currentTimeOfDay = 12f; // 0-24 hours
        [SerializeField] private float timeSpeed = 1f; // Time multiplier
        [SerializeField] private AnimationCurve sunIntensityCurve;
        [SerializeField] private Gradient sunColorGradient;
        [SerializeField] private AnimationCurve moonIntensityCurve;
        [SerializeField] private Gradient moonColorGradient;
        [SerializeField] private Gradient ambientColorGradient;
        [SerializeField] private Gradient fogColorGradient;
        
        [Header("Weather Effects")]
        [SerializeField] private WeatherType currentWeather = WeatherType.Clear;
        [SerializeField] private float weatherTransitionDuration = 5f;
        [SerializeField] private List<WeatherPreset> weatherPresets = new List<WeatherPreset>();
        
        [Header("Performance")]
        [SerializeField] private bool dynamicBatching = true;
        [SerializeField] private bool gpuInstancing = true;
        [SerializeField] private int pixelLightCount = 4;
        [SerializeField] private QualityLevel qualityLevel = QualityLevel.High;
        
        // Runtime components
        private Light sunLight;
        private Light moonLight;
        private List<Light> activePointLights = new List<Light>();
        private List<Light> activeSpotLights = new List<Light>();
        private List<Light> activeAreaLights = new List<Light>();
        private UniversalRenderPipelineAsset urpAsset;
        
        // Runtime state
        private float currentWeatherBlend = 0f;
        private WeatherPreset currentWeatherPreset;
        private WeatherPreset targetWeatherPreset;
        private bool isTransitioningWeather = false;
        
        #region Data Structures
        
        [System.Serializable]
        public class PointLightConfig
        {
            public string name = "Point Light";
            public Vector3 position;
            public Color color = Color.white;
            public float intensity = 1f;
            public float range = 10f;
            public bool castShadows = false;
            public Light lightComponent;
        }
        
        [System.Serializable]
        public class SpotLightConfig
        {
            public string name = "Spot Light";
            public Vector3 position;
            public Vector3 rotation;
            public Color color = Color.white;
            public float intensity = 2f;
            public float range = 15f;
            public float spotAngle = 30f;
            public bool castShadows = true;
            public Light lightComponent;
        }
        
        [System.Serializable]
        public class AreaLightConfig
        {
            public string name = "Area Light";
            public Vector3 position;
            public Vector3 rotation;
            public Vector2 size = Vector2.one;
            public Color color = Color.white;
            public float intensity = 1f;
            public Light lightComponent;
        }
        
        [System.Serializable]
        public class WeatherPreset
        {
            public WeatherType weatherType;
            public float lightIntensityMultiplier = 1f;
            public Color lightColorTint = Color.white;
            public float fogDensityMultiplier = 1f;
            public Color fogColorOverride = Color.gray;
            public float shadowStrength = 1f;
            public float ambientIntensityMultiplier = 1f;
            public ParticleSystem weatherParticles;
        }
        
        public enum WeatherType
        {
            Clear,
            Cloudy,
            Rainy,
            Stormy,
            Foggy,
            Snowy
        }
        
        public enum QualityLevel
        {
            Low,
            Medium,
            High,
            Ultra
        }
        
        public enum ShadowResolution
        {
            _256 = 256,
            _512 = 512,
            _1024 = 1024,
            _2048 = 2048,
            _4096 = 4096
        }
        
        #endregion
        
        #region Unity Lifecycle
        
        void Awake()
        {
            InitializeLighting();
            SetupRenderPipeline();
        }
        
        void Start()
        {
            CreateLights();
            ApplyLightingSettings();
            
            if (enableTimeOfDay)
            {
                InitializeTimeOfDay();
            }
        }
        
        void Update()
        {
            if (enableTimeOfDay)
            {
                UpdateTimeOfDay();
            }
            
            if (isTransitioningWeather)
            {
                UpdateWeatherTransition();
            }
            
            UpdateDynamicLights();
        }
        
        void OnValidate()
        {
            mainLightIntensity = Mathf.Max(0f, mainLightIntensity);
            ambientIntensity = Mathf.Max(0f, ambientIntensity);
            shadowDistance = Mathf.Max(0f, shadowDistance);
            shadowBias = Mathf.Max(0f, shadowBias);
            shadowNormalBias = Mathf.Max(0f, shadowNormalBias);
            shadowNearPlane = Mathf.Max(0.01f, shadowNearPlane);
            pointLightRange = Mathf.Max(0f, pointLightRange);
            pointLightIntensity = Mathf.Max(0f, pointLightIntensity);
            spotLightRange = Mathf.Max(0f, spotLightRange);
            spotLightAngle = Mathf.Clamp(spotLightAngle, 1f, 179f);
            spotLightIntensity = Mathf.Max(0f, spotLightIntensity);
            fogDensity = Mathf.Max(0f, fogDensity);
            fogStartDistance = Mathf.Max(0f, fogStartDistance);
            fogEndDistance = Mathf.Max(fogStartDistance, fogEndDistance);
            currentTimeOfDay = Mathf.Repeat(currentTimeOfDay, 24f);
            timeSpeed = Mathf.Max(0f, timeSpeed);
        }
        
        #endregion
        
        #region Initialization
        
        private void InitializeLighting()
        {
            // Find or create main directional light
            if (mainDirectionalLight == null)
            {
                GameObject[] lights = GameObject.FindGameObjectsWithTag("MainLight");
                if (lights.Length > 0)
                {
                    mainDirectionalLight = lights[0].GetComponent<Light>();
                }
                
                if (mainDirectionalLight == null)
                {
                    GameObject lightObject = new GameObject("Main Directional Light");
                    mainDirectionalLight = lightObject.AddComponent<Light>();
                    mainDirectionalLight.type = LightType.Directional;
                    lightObject.tag = "MainLight";
                }
            }
            
            sunLight = mainDirectionalLight;
            
            // Create moon light for time of day
            if (enableTimeOfDay)
            {
                GameObject moonObject = new GameObject("Moon Light");
                moonLight = moonObject.AddComponent<Light>();
                moonLight.type = LightType.Directional;
                moonLight.enabled = false;
            }
        }
        
        private void SetupRenderPipeline()
        {
            // Get URP asset
            urpAsset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            
            if (urpAsset != null)
            {
                // Configure shadow settings
                urpAsset.shadowDistance = shadowDistance;
                urpAsset.shadowCascadeCount = shadowCascades;
                urpAsset.cascade2Split = shadowCascadeRatios.x;
                urpAsset.cascade3Split = new Vector2(shadowCascadeRatios.x, shadowCascadeRatios.y);
                urpAsset.cascade4Split = shadowCascadeRatios;
                urpAsset.shadowDepthBias = shadowBias;
                urpAsset.shadowNormalBias = shadowNormalBias;
                urpAsset.softShadowsSupported = softShadows;
            }
        }
        
        private void CreateLights()
        {
            // Create point lights
            for (int i = 0; i < Mathf.Min(pointLights.Count, maxPointLights); i++)
            {
                CreatePointLight(pointLights[i]);
            }
            
            // Create spot lights
            for (int i = 0; i < Mathf.Min(spotLights.Count, maxSpotLights); i++)
            {
                CreateSpotLight(spotLights[i]);
            }
            
            // Create area lights (if supported)
            if (enableAreaLights)
            {
                for (int i = 0; i < Mathf.Min(areaLights.Count, maxAreaLights); i++)
                {
                    CreateAreaLight(areaLights[i]);
                }
            }
        }
        
        #endregion
        
        #region Light Management
        
        private void ApplyLightingSettings()
        {
            // Apply main light settings
            if (mainDirectionalLight != null)
            {
                mainDirectionalLight.intensity = mainLightIntensity;
                mainDirectionalLight.color = mainLightColor;
                mainDirectionalLight.transform.rotation = Quaternion.Euler(mainLightRotation);
                mainDirectionalLight.shadows = enableMainLightShadows ? LightShadows.Soft : LightShadows.None;
            }
            
            // Apply ambient lighting
            RenderSettings.ambientMode = ambientMode;
            RenderSettings.ambientSkyColor = ambientSkyColor * ambientIntensity;
            RenderSettings.ambientEquatorColor = ambientEquatorColor * ambientIntensity;
            RenderSettings.ambientGroundColor = ambientGroundColor * ambientIntensity;
            RenderSettings.ambientIntensity = ambientIntensity;
            
            // Apply fog settings
            RenderSettings.fog = enableFog;
            RenderSettings.fogMode = fogMode;
            RenderSettings.fogColor = fogColor;
            RenderSettings.fogDensity = fogDensity;
            RenderSettings.fogStartDistance = fogStartDistance;
            RenderSettings.fogEndDistance = fogEndDistance;
            
            // Apply quality settings
            ApplyQualitySettings();
        }
        
        private void ApplyQualitySettings()
        {
            switch (qualityLevel)
            {
                case QualityLevel.Low:
                    QualitySettings.pixelLightCount = 2;
                    QualitySettings.shadows = ShadowQuality.Disable;
                    QualitySettings.shadowResolution = UnityEngine.ShadowResolution.Low;
                    break;
                    
                case QualityLevel.Medium:
                    QualitySettings.pixelLightCount = 4;
                    QualitySettings.shadows = ShadowQuality.HardOnly;
                    QualitySettings.shadowResolution = UnityEngine.ShadowResolution.Medium;
                    break;
                    
                case QualityLevel.High:
                    QualitySettings.pixelLightCount = 8;
                    QualitySettings.shadows = ShadowQuality.All;
                    QualitySettings.shadowResolution = UnityEngine.ShadowResolution.High;
                    break;
                    
                case QualityLevel.Ultra:
                    QualitySettings.pixelLightCount = 16;
                    QualitySettings.shadows = ShadowQuality.All;
                    QualitySettings.shadowResolution = UnityEngine.ShadowResolution.VeryHigh;
                    break;
            }
        }
        
        #endregion
        
        #region Dynamic Light Creation
        
        private void CreatePointLight(PointLightConfig config)
        {
            GameObject lightObject = new GameObject($"PointLight_{config.name}");
            lightObject.transform.position = config.position;
            
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = config.color;
            light.intensity = config.intensity;
            light.range = config.range;
            light.shadows = config.castShadows ? LightShadows.Soft : LightShadows.None;
            
            config.lightComponent = light;
            activePointLights.Add(light);
        }
        
        private void CreateSpotLight(SpotLightConfig config)
        {
            GameObject lightObject = new GameObject($"SpotLight_{config.name}");
            lightObject.transform.position = config.position;
            lightObject.transform.rotation = Quaternion.Euler(config.rotation);
            
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Spot;
            light.color = config.color;
            light.intensity = config.intensity;
            light.range = config.range;
            light.spotAngle = config.spotAngle;
            light.shadows = config.castShadows ? LightShadows.Soft : LightShadows.None;
            
            config.lightComponent = light;
            activeSpotLights.Add(light);
        }
        
        private void CreateAreaLight(AreaLightConfig config)
        {
            GameObject lightObject = new GameObject($"AreaLight_{config.name}");
            lightObject.transform.position = config.position;
            lightObject.transform.rotation = Quaternion.Euler(config.rotation);
            
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Area;
            light.color = config.color;
            light.intensity = config.intensity;
            light.areaSize = config.size;
            
            config.lightComponent = light;
            activeAreaLights.Add(light);
        }
        
        #endregion
        
        #region Time of Day
        
        private void InitializeTimeOfDay()
        {
            // Initialize curves and gradients if not set
            if (sunIntensityCurve == null || sunIntensityCurve.length == 0)
            {
                sunIntensityCurve = AnimationCurve.Linear(0, 0, 24, 0);
                sunIntensityCurve.AddKey(6, 0);
                sunIntensityCurve.AddKey(12, 1);
                sunIntensityCurve.AddKey(18, 0);
            }
            
            if (sunColorGradient == null)
            {
                sunColorGradient = new Gradient();
                GradientColorKey[] colorKeys = new GradientColorKey[4];
                colorKeys[0] = new GradientColorKey(new Color(0.2f, 0.2f, 0.4f), 0f);
                colorKeys[1] = new GradientColorKey(new Color(1f, 0.8f, 0.6f), 0.25f);
                colorKeys[2] = new GradientColorKey(Color.white, 0.5f);
                colorKeys[3] = new GradientColorKey(new Color(1f, 0.6f, 0.4f), 0.75f);
                
                GradientAlphaKey[] alphaKeys = new GradientAlphaKey[2];
                alphaKeys[0] = new GradientAlphaKey(1f, 0f);
                alphaKeys[1] = new GradientAlphaKey(1f, 1f);
                
                sunColorGradient.SetKeys(colorKeys, alphaKeys);
            }
        }
        
        private void UpdateTimeOfDay()
        {
            // Update time
            currentTimeOfDay += Time.deltaTime * timeSpeed / 3600f; // Convert to hours
            currentTimeOfDay = Mathf.Repeat(currentTimeOfDay, 24f);
            
            float normalizedTime = currentTimeOfDay / 24f;
            
            // Update sun
            if (sunLight != null)
            {
                float sunIntensity = sunIntensityCurve.Evaluate(currentTimeOfDay);
                sunLight.intensity = sunIntensity * mainLightIntensity;
                sunLight.color = sunColorGradient.Evaluate(normalizedTime);
                
                // Rotate sun
                float sunAngle = (normalizedTime - 0.25f) * 360f;
                sunLight.transform.rotation = Quaternion.Euler(sunAngle, mainLightRotation.y, 0);
            }
            
            // Update moon
            if (moonLight != null && moonIntensityCurve != null)
            {
                float moonIntensity = moonIntensityCurve.Evaluate(currentTimeOfDay);
                moonLight.intensity = moonIntensity * 0.3f;
                
                if (moonColorGradient != null)
                {
                    moonLight.color = moonColorGradient.Evaluate(normalizedTime);
                }
                
                // Rotate moon (opposite to sun)
                float moonAngle = (normalizedTime + 0.25f) * 360f;
                moonLight.transform.rotation = Quaternion.Euler(moonAngle, mainLightRotation.y + 180f, 0);
                
                moonLight.enabled = moonIntensity > 0.01f;
            }
            
            // Update ambient
            if (ambientColorGradient != null)
            {
                Color ambientColor = ambientColorGradient.Evaluate(normalizedTime);
                RenderSettings.ambientSkyColor = ambientColor * ambientIntensity;
                RenderSettings.ambientEquatorColor = ambientColor * 0.8f * ambientIntensity;
                RenderSettings.ambientGroundColor = ambientColor * 0.5f * ambientIntensity;
            }
            
            // Update fog
            if (fogColorGradient != null)
            {
                RenderSettings.fogColor = fogColorGradient.Evaluate(normalizedTime);
            }
        }
        
        #endregion
        
        #region Weather System
        
        public void SetWeather(WeatherType weather, float transitionDuration = 5f)
        {
            WeatherPreset preset = weatherPresets.FirstOrDefault(p => p.weatherType == weather);
            if (preset != null)
            {
                currentWeatherPreset = GetCurrentWeatherState();
                targetWeatherPreset = preset;
                weatherTransitionDuration = transitionDuration;
                currentWeatherBlend = 0f;
                isTransitioningWeather = true;
                currentWeather = weather;
            }
        }
        
        private void UpdateWeatherTransition()
        {
            currentWeatherBlend += Time.deltaTime / weatherTransitionDuration;
            
            if (currentWeatherBlend >= 1f)
            {
                currentWeatherBlend = 1f;
                isTransitioningWeather = false;
                ApplyWeatherPreset(targetWeatherPreset);
            }
            else
            {
                BlendWeatherPresets(currentWeatherPreset, targetWeatherPreset, currentWeatherBlend);
            }
        }
        
        private void ApplyWeatherPreset(WeatherPreset preset)
        {
            if (preset == null)
                return;
            
            // Apply light modifications
            if (mainDirectionalLight != null)
            {
                mainDirectionalLight.intensity = mainLightIntensity * preset.lightIntensityMultiplier;
                mainDirectionalLight.color = mainLightColor * preset.lightColorTint;
            }
            
            // Apply fog modifications
            RenderSettings.fogDensity = fogDensity * preset.fogDensityMultiplier;
            RenderSettings.fogColor = preset.fogColorOverride;
            
            // Apply ambient modifications
            float ambientMult = preset.ambientIntensityMultiplier;
            RenderSettings.ambientSkyColor = ambientSkyColor * ambientIntensity * ambientMult;
            RenderSettings.ambientEquatorColor = ambientEquatorColor * ambientIntensity * ambientMult;
            RenderSettings.ambientGroundColor = ambientGroundColor * ambientIntensity * ambientMult;
            
            // Enable weather particles
            if (preset.weatherParticles != null)
            {
                preset.weatherParticles.gameObject.SetActive(true);
                preset.weatherParticles.Play();
            }
        }
        
        private void BlendWeatherPresets(WeatherPreset from, WeatherPreset to, float t)
        {
            if (from == null || to == null)
                return;
            
            // Blend light settings
            if (mainDirectionalLight != null)
            {
                float intensity = Mathf.Lerp(
                    mainLightIntensity * from.lightIntensityMultiplier,
                    mainLightIntensity * to.lightIntensityMultiplier,
                    t
                );
                mainDirectionalLight.intensity = intensity;
                
                Color color = Color.Lerp(
                    mainLightColor * from.lightColorTint,
                    mainLightColor * to.lightColorTint,
                    t
                );
                mainDirectionalLight.color = color;
            }
            
            // Blend fog
            float fogDens = Mathf.Lerp(
                fogDensity * from.fogDensityMultiplier,
                fogDensity * to.fogDensityMultiplier,
                t
            );
            RenderSettings.fogDensity = fogDens;
            RenderSettings.fogColor = Color.Lerp(from.fogColorOverride, to.fogColorOverride, t);
            
            // Blend ambient
            float ambientMult = Mathf.Lerp(from.ambientIntensityMultiplier, to.ambientIntensityMultiplier, t);
            RenderSettings.ambientIntensity = ambientIntensity * ambientMult;
        }
        
        private WeatherPreset GetCurrentWeatherState()
        {
            return new WeatherPreset
            {
                weatherType = currentWeather,
                lightIntensityMultiplier = mainDirectionalLight != null ? mainDirectionalLight.intensity / mainLightIntensity : 1f,
                lightColorTint = mainDirectionalLight != null ? mainDirectionalLight.color / mainLightColor : Color.white,
                fogDensityMultiplier = RenderSettings.fogDensity / fogDensity,
                fogColorOverride = RenderSettings.fogColor,
                ambientIntensityMultiplier = RenderSettings.ambientIntensity / ambientIntensity
            };
        }
        
        #endregion
        
        #region Dynamic Light Updates
        
        private void UpdateDynamicLights()
        {
            // Update point lights
            foreach (var config in pointLights)
            {
                if (config.lightComponent != null)
                {
                    config.lightComponent.color = config.color;
                    config.lightComponent.intensity = config.intensity * spriteLightIntensity;
                    config.lightComponent.range = config.range;
                }
            }
            
            // Update spot lights
            foreach (var config in spotLights)
            {
                if (config.lightComponent != null)
                {
                    config.lightComponent.color = config.color;
                    config.lightComponent.intensity = config.intensity * spriteLightIntensity;
                    config.lightComponent.range = config.range;
                    config.lightComponent.spotAngle = config.spotAngle;
                }
            }
        }
        
        #endregion
        
        #region Public API
        
        public void SetMainLightIntensity(float intensity)
        {
            mainLightIntensity = Mathf.Max(0f, intensity);
            if (mainDirectionalLight != null)
            {
                mainDirectionalLight.intensity = mainLightIntensity;
            }
        }
        
        public void SetMainLightColor(Color color)
        {
            mainLightColor = color;
            if (mainDirectionalLight != null)
            {
                mainDirectionalLight.color = mainLightColor;
            }
        }
        
        public void SetMainLightRotation(Vector3 rotation)
        {
            mainLightRotation = rotation;
            if (mainDirectionalLight != null)
            {
                mainDirectionalLight.transform.rotation = Quaternion.Euler(mainLightRotation);
            }
        }
        
        public void SetAmbientIntensity(float intensity)
        {
            ambientIntensity = Mathf.Max(0f, intensity);
            RenderSettings.ambientIntensity = ambientIntensity;
        }
        
        public void SetFogDensity(float density)
        {
            fogDensity = Mathf.Max(0f, density);
            RenderSettings.fogDensity = fogDensity;
        }
        
        public void EnableShadows(bool enable)
        {
            enableMainLightShadows = enable;
            if (mainDirectionalLight != null)
            {
                mainDirectionalLight.shadows = enable ? LightShadows.Soft : LightShadows.None;
            }
        }
        
        public void SetTimeOfDay(float hours)
        {
            currentTimeOfDay = Mathf.Repeat(hours, 24f);
        }
        
        public void SetTimeSpeed(float speed)
        {
            timeSpeed = Mathf.Max(0f, speed);
        }
        
        public Light AddPointLight(Vector3 position, Color color, float intensity, float range)
        {
            if (activePointLights.Count >= maxPointLights)
            {
                Debug.LogWarning("Maximum point lights reached");
                return null;
            }
            
            PointLightConfig config = new PointLightConfig
            {
                name = $"DynamicPoint_{activePointLights.Count}",
                position = position,
                color = color,
                intensity = intensity,
                range = range
            };
            
            CreatePointLight(config);
            pointLights.Add(config);
            
            return config.lightComponent;
        }
        
        public Light AddSpotLight(Vector3 position, Vector3 rotation, Color color, float intensity, float range, float angle)
        {
            if (activeSpotLights.Count >= maxSpotLights)
            {
                Debug.LogWarning("Maximum spot lights reached");
                return null;
            }
            
            SpotLightConfig config = new SpotLightConfig
            {
                name = $"DynamicSpot_{activeSpotLights.Count}",
                position = position,
                rotation = rotation,
                color = color,
                intensity = intensity,
                range = range,
                spotAngle = angle
            };
            
            CreateSpotLight(config);
            spotLights.Add(config);
            
            return config.lightComponent;
        }
        
        public void RemoveLight(Light light)
        {
            if (light == null)
                return;
            
            // Remove from point lights
            activePointLights.Remove(light);
            pointLights.RemoveAll(c => c.lightComponent == light);
            
            // Remove from spot lights
            activeSpotLights.Remove(light);
            spotLights.RemoveAll(c => c.lightComponent == light);
            
            // Remove from area lights
            activeAreaLights.Remove(light);
            areaLights.RemoveAll(c => c.lightComponent == light);
            
            // Destroy the light object
            if (light.gameObject != null)
            {
                Destroy(light.gameObject);
            }
        }
        
        public void SetQualityLevel(QualityLevel level)
        {
            qualityLevel = level;
            ApplyQualitySettings();
        }
        
        #endregion
    }
}