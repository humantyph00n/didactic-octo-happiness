using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;
using System.Linq;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace HD2D.Rendering
{
    /// <summary>
    /// Manages materials and shaders for HD-2D rendering system
    /// Handles material pooling, shader variants, and runtime material modifications
    /// </summary>
    public class HD2DMaterialManager : MonoBehaviour
    {
        private static HD2DMaterialManager instance;
        public static HD2DMaterialManager Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindObjectOfType<HD2DMaterialManager>();
                    if (instance == null)
                    {
                        GameObject go = new GameObject("HD2D Material Manager");
                        instance = go.AddComponent<HD2DMaterialManager>();
                    }
                }
                return instance;
            }
        }
        
        [Header("Shader References")]
        [SerializeField] private Shader spriteBillboardShader;
        [SerializeField] private Shader environmentShader;
        [SerializeField] private Shader waterShader;
        [SerializeField] private Shader foliageShader;
        [SerializeField] private Shader particleShader;
        [SerializeField] private Shader uiShader;
        [SerializeField] private Shader outlineShader;
        [SerializeField] private Shader dissolveShader;
        
        [Header("Material Templates")]
        [SerializeField] private Material defaultSpriteMaterial;
        [SerializeField] private Material defaultEnvironmentMaterial;
        [SerializeField] private Material defaultWaterMaterial;
        [SerializeField] private Material defaultFoliageMaterial;
        [SerializeField] private Material defaultParticleMaterial;
        [SerializeField] private Material defaultUIMaterial;
        
        [Header("Material Libraries")]
        [SerializeField] private List<MaterialLibrary> materialLibraries = new List<MaterialLibrary>();
        [SerializeField] private int activeLibraryIndex = 0;
        
        [Header("Texture Management")]
        [SerializeField] private List<TextureAtlas> textureAtlases = new List<TextureAtlas>();
        [SerializeField] private bool useTextureArrays = true;
        [SerializeField] private int maxTextureArraySize = 16;
        [SerializeField] private TextureFormat textureArrayFormat = TextureFormat.RGBA32;
        
        [Header("Material Pooling")]
        [SerializeField] private bool enableMaterialPooling = true;
        [SerializeField] private int initialPoolSize = 50;
        [SerializeField] private int maxPoolSize = 200;
        [SerializeField] private float poolCleanupInterval = 30f;
        
        [Header("Shader Properties")]
        [SerializeField] private List<GlobalShaderProperty> globalProperties = new List<GlobalShaderProperty>();
        [SerializeField] private bool autoUpdateGlobalProperties = true;
        
        [Header("Performance")]
        [SerializeField] private bool enableMaterialBatching = true;
        [SerializeField] private bool enableGPUInstancing = true;
        [SerializeField] private bool enableSRPBatcher = true;
        [SerializeField] private int maxMaterialVariants = 100;
        
        [Header("Debug")]
        [SerializeField] private bool showMaterialStats = false;
        [SerializeField] private bool logMaterialCreation = false;
        
        // Runtime data
        private Dictionary<string, Material> materialCache = new Dictionary<string, Material>();
        private Dictionary<Material, MaterialPool> materialPools = new Dictionary<Material, MaterialPool>();
        private Dictionary<string, Shader> shaderCache = new Dictionary<string, Shader>();
        private Dictionary<string, Texture2DArray> textureArrays = new Dictionary<string, Texture2DArray>();
        private List<MaterialPropertyBlock> propertyBlockPool = new List<MaterialPropertyBlock>();
        private float lastCleanupTime;
        private int totalMaterialsCreated;
        private int activeMaterialCount;
        
        #region Data Structures
        
        [System.Serializable]
        public class MaterialLibrary
        {
            public string name = "New Library";
            public List<MaterialEntry> materials = new List<MaterialEntry>();
            
            [System.Serializable]
            public class MaterialEntry
            {
                public string id;
                public Material material;
                public MaterialType type = MaterialType.Sprite;
                public List<string> tags = new List<string>();
                public bool allowInstancing = true;
                public bool allowBatching = true;
            }
        }
        
        [System.Serializable]
        public class TextureAtlas
        {
            public string name = "New Atlas";
            public Texture2D atlasTexture;
            public List<AtlasEntry> entries = new List<AtlasEntry>();
            public int padding = 2;
            public FilterMode filterMode = FilterMode.Point;
            public TextureWrapMode wrapMode = TextureWrapMode.Clamp;
            
            [System.Serializable]
            public class AtlasEntry
            {
                public string id;
                public Rect uvRect;
                public Vector2 pivot = new Vector2(0.5f, 0.5f);
                public Vector4 border = Vector4.zero;
            }
        }
        
        [System.Serializable]
        public class GlobalShaderProperty
        {
            public string propertyName;
            public ShaderPropertyType type;
            public float floatValue;
            public Vector4 vectorValue;
            public Color colorValue;
            public Texture textureValue;
            public Matrix4x4 matrixValue;
            
            public void Apply()
            {
                switch (type)
                {
                    case ShaderPropertyType.Float:
                        Shader.SetGlobalFloat(propertyName, floatValue);
                        break;
                    case ShaderPropertyType.Vector:
                        Shader.SetGlobalVector(propertyName, vectorValue);
                        break;
                    case ShaderPropertyType.Color:
                        Shader.SetGlobalColor(propertyName, colorValue);
                        break;
                    case ShaderPropertyType.Texture:
                        if (textureValue != null)
                            Shader.SetGlobalTexture(propertyName, textureValue);
                        break;
                    case ShaderPropertyType.Matrix:
                        Shader.SetGlobalMatrix(propertyName, matrixValue);
                        break;
                }
            }
        }
        
        public enum ShaderPropertyType
        {
            Float,
            Vector,
            Color,
            Texture,
            Matrix
        }
        
        public enum MaterialType
        {
            Sprite,
            Environment,
            Water,
            Foliage,
            Particle,
            UI,
            Special
        }
        
        private class MaterialPool
        {
            public Queue<Material> available = new Queue<Material>();
            public HashSet<Material> inUse = new HashSet<Material>();
            public Material template;
            public int maxSize;
            public float lastUsedTime;
            
            public MaterialPool(Material template, int initialSize, int maxSize)
            {
                this.template = template;
                this.maxSize = maxSize;
                this.lastUsedTime = Time.time;
                
                for (int i = 0; i < initialSize; i++)
                {
                    Material mat = new Material(template);
                    available.Enqueue(mat);
                }
            }
            
            public Material Get()
            {
                lastUsedTime = Time.time;
                
                Material mat;
                if (available.Count > 0)
                {
                    mat = available.Dequeue();
                }
                else if (inUse.Count < maxSize)
                {
                    mat = new Material(template);
                }
                else
                {
                    Debug.LogWarning($"Material pool for {template.name} is full");
                    return new Material(template);
                }
                
                inUse.Add(mat);
                return mat;
            }
            
            public void Return(Material mat)
            {
                if (inUse.Remove(mat))
                {
                    // Reset material properties to default
                    mat.CopyPropertiesFromMaterial(template);
                    available.Enqueue(mat);
                }
            }
            
            public void Cleanup()
            {
                // Remove excess materials
                while (available.Count > maxSize / 2)
                {
                    Material mat = available.Dequeue();
                    Object.Destroy(mat);
                }
            }
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
            
            InitializeManager();
        }
        
        void Start()
        {
            LoadShaders();
            CreateDefaultMaterials();
            InitializeMaterialPools();
            ApplyGlobalShaderProperties();
        }
        
        void Update()
        {
            if (autoUpdateGlobalProperties)
            {
                UpdateDynamicGlobalProperties();
            }
            
            if (enableMaterialPooling && Time.time - lastCleanupTime > poolCleanupInterval)
            {
                CleanupMaterialPools();
                lastCleanupTime = Time.time;
            }
        }
        
        void OnDestroy()
        {
            CleanupAllMaterials();
        }
        
        #endregion
        
        #region Initialization
        
        private void InitializeManager()
        {
            // Initialize caches
            materialCache = new Dictionary<string, Material>();
            shaderCache = new Dictionary<string, Shader>();
            materialPools = new Dictionary<Material, MaterialPool>();
            textureArrays = new Dictionary<string, Texture2DArray>();
            propertyBlockPool = new List<MaterialPropertyBlock>();
            
            // Pre-allocate property blocks
            for (int i = 0; i < 20; i++)
            {
                propertyBlockPool.Add(new MaterialPropertyBlock());
            }
        }
        
        private void LoadShaders()
        {
            // Load shaders from resources or references
            if (spriteBillboardShader == null)
                spriteBillboardShader = Shader.Find("HD2D/SpriteBillboard");
            
            if (environmentShader == null)
                environmentShader = Shader.Find("HD2D/Environment");
            
            if (waterShader == null)
                waterShader = Shader.Find("HD2D/Water");
            
            if (foliageShader == null)
                foliageShader = Shader.Find("HD2D/Foliage");
            
            if (particleShader == null)
                particleShader = Shader.Find("HD2D/Particle");
            
            if (uiShader == null)
                uiShader = Shader.Find("HD2D/UI");
            
            if (outlineShader == null)
                outlineShader = Shader.Find("HD2D/Outline");
            
            if (dissolveShader == null)
                dissolveShader = Shader.Find("HD2D/Dissolve");
            
            // Cache shaders
            CacheShader("SpriteBillboard", spriteBillboardShader);
            CacheShader("Environment", environmentShader);
            CacheShader("Water", waterShader);
            CacheShader("Foliage", foliageShader);
            CacheShader("Particle", particleShader);
            CacheShader("UI", uiShader);
            CacheShader("Outline", outlineShader);
            CacheShader("Dissolve", dissolveShader);
        }
        
        private void CreateDefaultMaterials()
        {
            // Create default materials if not assigned
            if (defaultSpriteMaterial == null && spriteBillboardShader != null)
            {
                defaultSpriteMaterial = new Material(spriteBillboardShader);
                defaultSpriteMaterial.name = "Default Sprite Material";
                ConfigureSpriteMaterial(defaultSpriteMaterial);
            }
            
            if (defaultEnvironmentMaterial == null && environmentShader != null)
            {
                defaultEnvironmentMaterial = new Material(environmentShader);
                defaultEnvironmentMaterial.name = "Default Environment Material";
                ConfigureEnvironmentMaterial(defaultEnvironmentMaterial);
            }
            
            if (defaultWaterMaterial == null && waterShader != null)
            {
                defaultWaterMaterial = new Material(waterShader);
                defaultWaterMaterial.name = "Default Water Material";
                ConfigureWaterMaterial(defaultWaterMaterial);
            }
            
            if (defaultFoliageMaterial == null && foliageShader != null)
            {
                defaultFoliageMaterial = new Material(foliageShader);
                defaultFoliageMaterial.name = "Default Foliage Material";
                ConfigureFoliageMaterial(defaultFoliageMaterial);
            }
            
            if (defaultParticleMaterial == null && particleShader != null)
            {
                defaultParticleMaterial = new Material(particleShader);
                defaultParticleMaterial.name = "Default Particle Material";
                ConfigureParticleMaterial(defaultParticleMaterial);
            }
            
            if (defaultUIMaterial == null && uiShader != null)
            {
                defaultUIMaterial = new Material(uiShader);
                defaultUIMaterial.name = "Default UI Material";
                ConfigureUIMaterial(defaultUIMaterial);
            }
        }
        
        private void InitializeMaterialPools()
        {
            if (!enableMaterialPooling)
                return;
            
            // Create pools for default materials
            if (defaultSpriteMaterial != null)
            {
                CreateMaterialPool(defaultSpriteMaterial, initialPoolSize, maxPoolSize);
            }
            
            if (defaultEnvironmentMaterial != null)
            {
                CreateMaterialPool(defaultEnvironmentMaterial, initialPoolSize / 2, maxPoolSize / 2);
            }
            
            // Create pools for library materials
            foreach (var library in materialLibraries)
            {
                foreach (var entry in library.materials)
                {
                    if (entry.material != null && entry.allowInstancing)
                    {
                        CreateMaterialPool(entry.material, 5, 20);
                    }
                }
            }
        }
        
        #endregion
        
        #region Material Configuration
        
        private void ConfigureSpriteMaterial(Material mat)
        {
            mat.SetFloat("_Cutoff", 0.5f);
            mat.SetFloat("_OutlineWidth", 0f);
            mat.SetColor("_OutlineColor", Color.black);
            mat.SetFloat("_EmissionIntensity", 0f);
            
            if (enableGPUInstancing)
            {
                mat.enableInstancing = true;
            }
        }
        
        private void ConfigureEnvironmentMaterial(Material mat)
        {
            mat.SetFloat("_Smoothness", 0.5f);
            mat.SetFloat("_Metallic", 0f);
            mat.SetFloat("_OcclusionStrength", 1f);
            
            if (enableGPUInstancing)
            {
                mat.enableInstancing = true;
            }
        }
        
        private void ConfigureWaterMaterial(Material mat)
        {
            mat.SetColor("_WaterColor", new Color(0.2f, 0.5f, 0.7f, 0.8f));
            mat.SetFloat("_WaveSpeed", 1f);
            mat.SetFloat("_WaveAmplitude", 0.1f);
            mat.SetFloat("_Transparency", 0.8f);
            mat.SetFloat("_ReflectionStrength", 0.5f);
        }
        
        private void ConfigureFoliageMaterial(Material mat)
        {
            mat.SetFloat("_WindStrength", 0.5f);
            mat.SetFloat("_WindSpeed", 1f);
            mat.SetColor("_TintColor", Color.white);
            mat.SetFloat("_Cutoff", 0.3f);
            
            if (enableGPUInstancing)
            {
                mat.enableInstancing = true;
            }
        }
        
        private void ConfigureParticleMaterial(Material mat)
        {
            mat.SetFloat("_SoftParticles", 1f);
            mat.SetFloat("_InvFade", 1f);
            mat.SetColor("_TintColor", Color.white);
            mat.SetFloat("_EmissionGain", 1f);
        }
        
        private void ConfigureUIMaterial(Material mat)
        {
            mat.SetFloat("_StencilComp", 8f);
            mat.SetFloat("_Stencil", 0f);
            mat.SetFloat("_StencilOp", 0f);
            mat.SetFloat("_StencilWriteMask", 255f);
            mat.SetFloat("_StencilReadMask", 255f);
            mat.SetFloat("_ColorMask", 15f);
        }
        
        #endregion
        
        #region Material Management
        
        public Material GetMaterial(string materialId)
        {
            // Check cache first
            if (materialCache.TryGetValue(materialId, out Material cached))
            {
                return cached;
            }
            
            // Search in libraries
            foreach (var library in materialLibraries)
            {
                var entry = library.materials.FirstOrDefault(m => m.id == materialId);
                if (entry != null && entry.material != null)
                {
                    materialCache[materialId] = entry.material;
                    return entry.material;
                }
            }
            
            Debug.LogWarning($"Material with ID '{materialId}' not found");
            return defaultSpriteMaterial;
        }
        
        public Material CreateMaterial(Shader shader, string name = null)
        {
            if (shader == null)
            {
                Debug.LogError("Cannot create material with null shader");
                return null;
            }
            
            Material mat = new Material(shader);
            if (!string.IsNullOrEmpty(name))
            {
                mat.name = name;
            }
            
            totalMaterialsCreated++;
            activeMaterialCount++;
            
            if (logMaterialCreation)
            {
                Debug.Log($"Created material: {mat.name} (Total: {totalMaterialsCreated}, Active: {activeMaterialCount})");
            }
            
            return mat;
        }
        
        public Material CreateMaterialVariant(Material baseMaterial, string variantName = null)
        {
            if (baseMaterial == null)
                return null;
            
            Material variant = new Material(baseMaterial);
            variant.name = variantName ?? $"{baseMaterial.name}_Variant";
            
            totalMaterialsCreated++;
            activeMaterialCount++;
            
            return variant;
        }
        
        public Material GetPooledMaterial(Material template)
        {
            if (!enableMaterialPooling || template == null)
                return new Material(template);
            
            if (!materialPools.TryGetValue(template, out MaterialPool pool))
            {
                pool = CreateMaterialPool(template, 5, 20);
            }
            
            return pool.Get();
        }
        
        public void ReturnPooledMaterial(Material material)
        {
            if (!enableMaterialPooling || material == null)
                return;
            
            // Find the pool that contains this material
            foreach (var kvp in materialPools)
            {
                if (kvp.Value.inUse.Contains(material))
                {
                    kvp.Value.Return(material);
                    return;
                }
            }
        }
        
        private MaterialPool CreateMaterialPool(Material template, int initialSize, int maxSize)
        {
            if (materialPools.ContainsKey(template))
                return materialPools[template];
            
            MaterialPool pool = new MaterialPool(template, initialSize, maxSize);
            materialPools[template] = pool;
            return pool;
        }
        
        #endregion
        
        #region Shader Management
        
        public Shader GetShader(string shaderName)
        {
            if (shaderCache.TryGetValue(shaderName, out Shader cached))
            {
                return cached;
            }
            
            Shader shader = Shader.Find(shaderName);
            if (shader != null)
            {
                shaderCache[shaderName] = shader;
            }
            
            return shader;
        }
        
        private void CacheShader(string name, Shader shader)
        {
            if (shader != null)
            {
                shaderCache[name] = shader;
            }
        }
        
        public void WarmupShaders()
        {
            // Warmup shader variants to avoid hitches
            foreach (var shader in shaderCache.Values)
            {
                if (shader != null)
                {
                    ShaderWarmup.WarmupShader(shader);
                }
            }
        }
        
        #endregion
        
        #region Texture Management
        
        public Texture2DArray CreateTextureArray(string name, List<Texture2D> textures)
        {
            if (textures == null || textures.Count == 0)
                return null;
            
            // Get dimensions from first texture
            Texture2D first = textures[0];
            int width = first.width;
            int height = first.height;
            TextureFormat format = first.format;
            
            // Create texture array
            Texture2DArray array = new Texture2DArray(
                width, 
                height, 
                Mathf.Min(textures.Count, maxTextureArraySize),
                format,
                true
            );
            
            // Copy textures to array
            for (int i = 0; i < Mathf.Min(textures.Count, maxTextureArraySize); i++)
            {
                if (textures[i] != null)
                {
                    Graphics.CopyTexture(textures[i], 0, 0, array, i, 0);
                }
            }
            
            array.Apply();
            textureArrays[name] = array;
            
            return array;
        }
        
        public Rect GetAtlasUVRect(string atlasName, string entryId)
        {
            TextureAtlas atlas = textureAtlases.FirstOrDefault(a => a.name == atlasName);
            if (atlas != null)
            {
                TextureAtlas.AtlasEntry entry = atlas.entries.FirstOrDefault(e => e.id == entryId);
                if (entry != null)
                {
                    return entry.uvRect;
                }
            }
            
            return new Rect(0, 0, 1, 1);
        }
        
        #endregion
        
        #region Global Properties
        
        private void ApplyGlobalShaderProperties()
        {
            foreach (var property in globalProperties)
            {
                property.Apply();
            }
        }
        
        private void UpdateDynamicGlobalProperties()
        {
            // Update time-based properties
            Shader.SetGlobalFloat("_Time", Time.time);
            Shader.SetGlobalFloat("_SinTime", Mathf.Sin(Time.time));
            Shader.SetGlobalFloat("_CosTime", Mathf.Cos(Time.time));
            
            // Update camera properties if main camera exists
            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                Shader.SetGlobalVector("_WorldSpaceCameraPos", mainCam.transform.position);
                Shader.SetGlobalMatrix("_CameraToWorld", mainCam.cameraToWorldMatrix);
                Shader.SetGlobalMatrix("_WorldToCamera", mainCam.worldToCameraMatrix);
            }
        }
        
        public void SetGlobalFloat(string propertyName, float value)
        {
            Shader.SetGlobalFloat(propertyName, value);
            
            // Update in list if exists
            var property = globalProperties.FirstOrDefault(p => p.propertyName == propertyName);
            if (property != null)
            {
                property.floatValue = value;
            }
        }
        
        public void SetGlobalVector(string propertyName, Vector4 value)
        {
            Shader.SetGlobalVector(propertyName, value);
            
            var property = globalProperties.FirstOrDefault(p => p.propertyName == propertyName);
            if (property != null)
            {
                property.vectorValue = value;
            }
        }
        
        public void SetGlobalColor(string propertyName, Color value)
        {
            Shader.SetGlobalColor(propertyName, value);
            
            var property = globalProperties.FirstOrDefault(p => p.propertyName == propertyName);
            if (property != null)
            {
                property.colorValue = value;
            }
        }
        
        public void SetGlobalTexture(string propertyName, Texture value)
        {
            Shader.SetGlobalTexture(propertyName, value);
            
            var property = globalProperties.FirstOrDefault(p => p.propertyName == propertyName);
            if (property != null)
            {
                property.textureValue = value;
            }
        }
        
        #endregion
        
        #region Property Blocks
        
        public MaterialPropertyBlock GetPropertyBlock()
        {
            if (propertyBlockPool.Count > 0)
            {
                MaterialPropertyBlock block = propertyBlockPool[0];
                propertyBlockPool.RemoveAt(0);
                block.Clear();
                return block;
            }
            
            return new MaterialPropertyBlock();
        }
        
        public void ReturnPropertyBlock(MaterialPropertyBlock block)
        {
            if (block != null && propertyBlockPool.Count < 50)
            {
                block.Clear();
                propertyBlockPool.Add(block);
            }
        }
        
        #endregion
        
        #region Cleanup
        
        private void CleanupMaterialPools()
        {
            List<Material> poolsToRemove = new List<Material>();
            
            foreach (var kvp in materialPools)
            {
                MaterialPool pool = kvp.Value;
                
                // Remove unused pools
                if (Time.time - pool.lastUsedTime > 60f && pool.inUse.Count == 0)
                {
                    poolsToRemove.Add(kvp.Key);
                }
                else
                {
                    // Cleanup excess materials in pool
                    pool.Cleanup();
                }
            }
            
            // Remove unused pools
            foreach (var template in poolsToRemove)
            {
                if (materialPools.TryGetValue(template, out MaterialPool pool))
                {
                    while (pool.available.Count > 0)
                    {
                        Material mat = pool.available.Dequeue();
                        Destroy(mat);
                        activeMaterialCount--;
                    }
                    materialPools.Remove(template);
                }
            }
        }
        
        private void CleanupAllMaterials()
        {
            // Cleanup all pooled materials
            foreach (var pool in materialPools.Values)
            {
                foreach (var mat in pool.available)
                {
                    if (mat != null)
                        Destroy(mat);
                }
                foreach (var mat in pool.inUse)
                {
                    if (mat != null)
                        Destroy(mat);
                }
            }
            materialPools.Clear();
            
            // Clear caches
            materialCache.Clear();
            shaderCache.Clear();
            
            // Destroy texture arrays
            foreach (var array in textureArrays.Values)
            {
                if (array != null)
                    Destroy(array);
            }
            textureArrays.Clear();
        }
        
        #endregion
        
        #region Public API
        
        public Material GetDefaultMaterial(MaterialType type)
        {
            switch (type)
            {
                case MaterialType.Sprite:
                    return defaultSpriteMaterial;
                case MaterialType.Environment:
                    return defaultEnvironmentMaterial;
                case MaterialType.Water:
                    return defaultWaterMaterial;
                case MaterialType.Foliage:
                    return defaultFoliageMaterial;
                case MaterialType.Particle:
                    return defaultParticleMaterial;
                case MaterialType.UI:
                    return defaultUIMaterial;
                default:
                    return defaultSpriteMaterial;
            }
        }
        
        public void RegisterMaterial(string id, Material material)
        {
            if (!string.IsNullOrEmpty(id) && material != null)
            {
                materialCache[id] = material;
            }
        }
        
        public void UnregisterMaterial(string id)
        {
            materialCache.Remove(id);
        }
        
        public bool HasMaterial(string id)
        {
            return materialCache.ContainsKey(id) || 
                   materialLibraries.Any(lib => lib.materials.Any(m => m.id == id));
        }
        
        public void EnableBatching(bool enable)
        {
            enableMaterialBatching = enable;
            
            // Update all materials
            foreach (var mat in materialCache.Values)
            {
                if (mat != null)
                {
                    mat.enableInstancing = enable && enableGPUInstancing;
                }
            }
        }
        
        public void SetQualityLevel(int level)
        {
            // Adjust material quality based on level
            foreach (var mat in materialCache.Values)
            {
                if (mat != null)
                {
                    if (level <= 1) // Low quality
                    {
                        mat.SetFloat("_DetailLevel", 0);
                        mat.DisableKeyword("_NORMALMAP");
                        mat.DisableKeyword("_PARALLAXMAP");
                    }
                    else if (level == 2) // Medium quality
                    {
                        mat.SetFloat("_DetailLevel", 0.5f);
                        mat.EnableKeyword("_NORMALMAP");
                        mat.DisableKeyword("_PARALLAXMAP");
                    }
                    else // High quality
                    {
                        mat.SetFloat("_DetailLevel", 1);
                        mat.EnableKeyword("_NORMALMAP");
                        mat.EnableKeyword("_PARALLAXMAP");
                    }
                }
            }
        }
        
        public MaterialStats GetStats()
        {
            return new MaterialStats
            {
                totalCreated = totalMaterialsCreated,
                currentActive = activeMaterialCount,
                cachedMaterials = materialCache.Count,
                pooledMaterials = materialPools.Sum(p => p.Value.available.Count + p.Value.inUse.Count),
                textureArrays = textureArrays.Count
            };
        }
        
        [System.Serializable]
        public struct MaterialStats
        {
            public int totalCreated;
            public int currentActive;
            public int cachedMaterials;
            public int pooledMaterials;
            public int textureArrays;
        }
        
        #endregion
        
        #region Debug
        
        void OnGUI()
        {
            if (!showMaterialStats)
                return;
            
            MaterialStats stats = GetStats();
            
            GUILayout.BeginArea(new Rect(10, 10, 300, 200));
            GUILayout.BeginVertical("box");
            GUILayout.Label("HD2D Material Manager Stats");
            GUILayout.Label($"Total Created: {stats.totalCreated}");
            GUILayout.Label($"Active: {stats.currentActive}");
            GUILayout.Label($"Cached: {stats.cachedMaterials}");
            GUILayout.Label($"Pooled: {stats.pooledMaterials}");
            GUILayout.Label($"Texture Arrays: {stats.textureArrays}");
            GUILayout.Label($"Property Blocks Available: {propertyBlockPool.Count}");
            GUILayout.EndVertical();
            GUILayout.EndArea();
        }
        
        #endregion
    }
}