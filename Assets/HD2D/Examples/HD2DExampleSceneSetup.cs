using UnityEngine;
using UnityEngine.UI;
using HD2D.Core;
using HD2D.Rendering;
using HD2D.Rendering.Sprites;
using HD2D.Environment;
using HD2D.Camera;
using HD2D.PostProcessing;
using HD2D.Lighting;
using System.Collections;
using System.Collections.Generic;

namespace HD2D.Examples
{
    /// <summary>
    /// Example scene setup demonstrating HD-2D rendering system
    /// Shows how to configure and use all components together
    /// </summary>
    public class HD2DExampleSceneSetup : MonoBehaviour
    {
        [Header("Scene Configuration")]
        [SerializeField] private SceneTemplate sceneTemplate = SceneTemplate.TownSquare;
        [SerializeField] private bool autoSetup = true;
        [SerializeField] private bool createDemoCharacters = true;
        [SerializeField] private bool enableInteractiveDemo = true;
        
        [Header("Demo Assets")]
        [SerializeField] private Sprite[] characterSprites;
        [SerializeField] private Sprite[] environmentTiles;
        [SerializeField] private Material spriteMaterial;
        [SerializeField] private Material environmentMaterial;
        
        [Header("UI References")]
        [SerializeField] private Canvas uiCanvas;
        [SerializeField] private Text fpsText;
        [SerializeField] private Text infoText;
        [SerializeField] private Slider timeOfDaySlider;
        [SerializeField] private Dropdown weatherDropdown;
        [SerializeField] private Dropdown qualityDropdown;
        
        // Component references
        private HD2DSceneComposer sceneComposer;
        private HD2DEnvironmentBuilder environmentBuilder;
        private HD2DTiltShiftCamera tiltShiftCamera;
        private HD2DPostProcessingManager postProcessing;
        private HD2DLightingManager lighting;
        private HD2DMaterialManager materialManager;
        
        // Demo objects
        private List<GameObject> demoCharacters = new List<GameObject>();
        private GameObject playerCharacter;
        private bool isInitialized = false;
        
        public enum SceneTemplate
        {
            TownSquare,
            Forest,
            Dungeon,
            Castle,
            Beach,
            Mountain
        }
        
        #region Unity Lifecycle
        
        void Awake()
        {
            if (autoSetup)
            {
                StartCoroutine(SetupScene());
            }
        }
        
        void Update()
        {
            if (!isInitialized)
                return;
            
            UpdateUI();
            HandleInput();
        }
        
        #endregion
        
        #region Scene Setup
        
        private IEnumerator SetupScene()
        {
            Debug.Log("=== HD-2D Example Scene Setup Starting ===");
            
            // Step 1: Create core systems
            yield return StartCoroutine(CreateCoreSystems());
            
            // Step 2: Configure scene based on template
            yield return StartCoroutine(ConfigureSceneTemplate());
            
            // Step 3: Build environment
            yield return StartCoroutine(BuildEnvironment());
            
            // Step 4: Setup lighting
            yield return StartCoroutine(SetupLighting());
            
            // Step 5: Configure post-processing
            yield return StartCoroutine(SetupPostProcessing());
            
            // Step 6: Create demo characters
            if (createDemoCharacters)
            {
                yield return StartCoroutine(CreateCharacters());
            }
            
            // Step 7: Setup UI
            if (enableInteractiveDemo)
            {
                SetupDemoUI();
            }
            
            isInitialized = true;
            Debug.Log("=== HD-2D Example Scene Setup Complete ===");
        }
        
        private IEnumerator CreateCoreSystems()
        {
            Debug.Log("Creating core HD-2D systems...");
            
            // Create Scene Composer
            GameObject composerGO = new GameObject("HD2D Scene Composer");
            sceneComposer = composerGO.AddComponent<HD2DSceneComposer>();
            
            // Create Environment Builder
            GameObject envGO = new GameObject("HD2D Environment");
            environmentBuilder = envGO.AddComponent<HD2DEnvironmentBuilder>();
            
            // Setup Camera
            GameObject cameraGO = new GameObject("HD2D Camera");
            UnityEngine.Camera cam = cameraGO.AddComponent<UnityEngine.Camera>();
            tiltShiftCamera = cameraGO.AddComponent<HD2DTiltShiftCamera>();
            cameraGO.tag = "MainCamera";
            
            // Create Post Processing Manager
            postProcessing = cameraGO.AddComponent<HD2DPostProcessingManager>();
            
            // Create Lighting Manager
            GameObject lightingGO = new GameObject("HD2D Lighting");
            lighting = lightingGO.AddComponent<HD2DLightingManager>();
            
            // Get Material Manager instance
            materialManager = HD2DMaterialManager.Instance;
            
            yield return null;
        }
        
        private IEnumerator ConfigureSceneTemplate()
        {
            Debug.Log($"Configuring scene template: {sceneTemplate}");
            
            switch (sceneTemplate)
            {
                case SceneTemplate.TownSquare:
                    ConfigureTownSquare();
                    break;
                    
                case SceneTemplate.Forest:
                    ConfigureForest();
                    break;
                    
                case SceneTemplate.Dungeon:
                    ConfigureDungeon();
                    break;
                    
                case SceneTemplate.Castle:
                    ConfigureCastle();
                    break;
                    
                case SceneTemplate.Beach:
                    ConfigureBeach();
                    break;
                    
                case SceneTemplate.Mountain:
                    ConfigureMountain();
                    break;
            }
            
            yield return null;
        }
        
        #endregion
        
        #region Template Configurations
        
        private void ConfigureTownSquare()
        {
            // Camera settings
            tiltShiftCamera.SetCameraMode(HD2DTiltShiftCamera.CameraMode.Isometric);
            tiltShiftCamera.SetCameraDistance(25f);
            tiltShiftCamera.SetCameraAngle(45f);
            tiltShiftCamera.SetFieldOfView(30f);
            tiltShiftCamera.SetFocusDistance(15f);
            
            // Lighting - Bright daylight
            lighting.SetMainLightIntensity(1.2f);
            lighting.SetMainLightColor(new Color(1f, 0.95f, 0.8f));
            lighting.SetMainLightRotation(new Vector3(45f, -30f, 0f));
            lighting.SetAmbientIntensity(0.8f);
            lighting.SetFogDensity(0.005f);
            
            // Post-processing - Vibrant
            postProcessing.SetBloomIntensity(0.5f);
            postProcessing.SetVignetteIntensity(0.2f);
            postProcessing.SetSaturation(0.1f);
            postProcessing.SetContrast(0.05f);
            postProcessing.SetColorFilter(new Color(1f, 0.98f, 0.95f));
        }
        
        private void ConfigureForest()
        {
            // Camera settings
            tiltShiftCamera.SetCameraMode(HD2DTiltShiftCamera.CameraMode.ThirdPerson);
            tiltShiftCamera.SetCameraDistance(20f);
            tiltShiftCamera.SetCameraAngle(35f);
            tiltShiftCamera.SetFieldOfView(35f);
            tiltShiftCamera.SetFocusDistance(12f);
            
            // Lighting - Filtered sunlight
            lighting.SetMainLightIntensity(0.8f);
            lighting.SetMainLightColor(new Color(0.9f, 1f, 0.7f));
            lighting.SetMainLightRotation(new Vector3(60f, -45f, 0f));
            lighting.SetAmbientIntensity(0.6f);
            lighting.SetFogDensity(0.02f);
            
            // Post-processing - Natural
            postProcessing.SetBloomIntensity(0.3f);
            postProcessing.SetVignetteIntensity(0.35f);
            postProcessing.SetSaturation(0.2f);
            postProcessing.SetContrast(-0.05f);
            postProcessing.SetColorFilter(new Color(0.95f, 1f, 0.9f));
        }
        
        private void ConfigureDungeon()
        {
            // Camera settings
            tiltShiftCamera.SetCameraMode(HD2DTiltShiftCamera.CameraMode.Isometric);
            tiltShiftCamera.SetCameraDistance(18f);
            tiltShiftCamera.SetCameraAngle(50f);
            tiltShiftCamera.SetFieldOfView(25f);
            tiltShiftCamera.SetFocusDistance(10f);
            
            // Lighting - Dark and moody
            lighting.SetMainLightIntensity(0.3f);
            lighting.SetMainLightColor(new Color(0.7f, 0.8f, 1f));
            lighting.SetMainLightRotation(new Vector3(30f, -60f, 0f));
            lighting.SetAmbientIntensity(0.2f);
            lighting.SetFogDensity(0.05f);
            
            // Add torch lights
            lighting.AddPointLight(new Vector3(5, 2, 5), new Color(1f, 0.6f, 0.2f), 2f, 8f);
            lighting.AddPointLight(new Vector3(-5, 2, 5), new Color(1f, 0.6f, 0.2f), 2f, 8f);
            
            // Post-processing - Dark and gritty
            postProcessing.SetBloomIntensity(0.8f);
            postProcessing.SetVignetteIntensity(0.5f);
            postProcessing.SetSaturation(-0.3f);
            postProcessing.SetContrast(0.2f);
            postProcessing.SetColorFilter(new Color(0.8f, 0.85f, 1f));
        }
        
        private void ConfigureCastle()
        {
            // Camera settings
            tiltShiftCamera.SetCameraMode(HD2DTiltShiftCamera.CameraMode.Isometric);
            tiltShiftCamera.SetCameraDistance(30f);
            tiltShiftCamera.SetCameraAngle(40f);
            tiltShiftCamera.SetFieldOfView(28f);
            tiltShiftCamera.SetFocusDistance(20f);
            
            // Lighting - Royal atmosphere
            lighting.SetMainLightIntensity(1f);
            lighting.SetMainLightColor(new Color(1f, 0.9f, 0.8f));
            lighting.SetMainLightRotation(new Vector3(50f, -20f, 0f));
            lighting.SetAmbientIntensity(0.7f);
            lighting.SetFogDensity(0.008f);
            
            // Post-processing - Regal
            postProcessing.SetBloomIntensity(0.6f);
            postProcessing.SetVignetteIntensity(0.25f);
            postProcessing.SetSaturation(0f);
            postProcessing.SetContrast(0.1f);
            postProcessing.SetColorFilter(new Color(1f, 0.95f, 0.9f));
        }
        
        private void ConfigureBeach()
        {
            // Camera settings
            tiltShiftCamera.SetCameraMode(HD2DTiltShiftCamera.CameraMode.Isometric);
            tiltShiftCamera.SetCameraDistance(28f);
            tiltShiftCamera.SetCameraAngle(35f);
            tiltShiftCamera.SetFieldOfView(35f);
            tiltShiftCamera.SetFocusDistance(18f);
            
            // Lighting - Bright tropical
            lighting.SetMainLightIntensity(1.5f);
            lighting.SetMainLightColor(new Color(1f, 1f, 0.9f));
            lighting.SetMainLightRotation(new Vector3(40f, -10f, 0f));
            lighting.SetAmbientIntensity(1f);
            lighting.SetFogDensity(0.003f);
            
            // Post-processing - Tropical
            postProcessing.SetBloomIntensity(0.7f);
            postProcessing.SetVignetteIntensity(0.15f);
            postProcessing.SetSaturation(0.3f);
            postProcessing.SetContrast(0.05f);
            postProcessing.SetColorFilter(new Color(1f, 0.98f, 0.85f));
        }
        
        private void ConfigureMountain()
        {
            // Camera settings
            tiltShiftCamera.SetCameraMode(HD2DTiltShiftCamera.CameraMode.Isometric);
            tiltShiftCamera.SetCameraDistance(35f);
            tiltShiftCamera.SetCameraAngle(55f);
            tiltShiftCamera.SetFieldOfView(25f);
            tiltShiftCamera.SetFocusDistance(25f);
            
            // Lighting - High altitude
            lighting.SetMainLightIntensity(1.3f);
            lighting.SetMainLightColor(new Color(0.9f, 0.95f, 1f));
            lighting.SetMainLightRotation(new Vector3(65f, -35f, 0f));
            lighting.SetAmbientIntensity(0.9f);
            lighting.SetFogDensity(0.015f);
            
            // Post-processing - Crisp mountain air
            postProcessing.SetBloomIntensity(0.4f);
            postProcessing.SetVignetteIntensity(0.3f);
            postProcessing.SetSaturation(-0.1f);
            postProcessing.SetContrast(0.15f);
            postProcessing.SetColorFilter(new Color(0.95f, 0.97f, 1f));
        }
        
        #endregion
        
        #region Environment Building
        
        private IEnumerator BuildEnvironment()
        {
            Debug.Log("Building HD-2D environment...");
            
            // Create a simple grid-based floor
            int gridSize = 20;
            float tileSize = 1f;
            
            GameObject floorContainer = new GameObject("Floor");
            floorContainer.transform.SetParent(environmentBuilder.transform);
            
            for (int x = 0; x < gridSize; x++)
            {
                for (int z = 0; z < gridSize; z++)
                {
                    GameObject tile = CreateFloorTile(x, z, tileSize);
                    tile.transform.SetParent(floorContainer.transform);
                }
            }
            
            // Add some walls
            CreateWalls(gridSize, tileSize);
            
            // Add decorative elements based on template
            AddEnvironmentDecorations();
            
            yield return null;
        }
        
        private GameObject CreateFloorTile(int x, int z, float size)
        {
            GameObject tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tile.name = $"Floor_{x}_{z}";
            tile.transform.position = new Vector3(x * size, -0.5f, z * size);
            tile.transform.localScale = new Vector3(size, 0.1f, size);
            
            // Apply material
            if (environmentMaterial != null)
            {
                tile.GetComponent<Renderer>().material = environmentMaterial;
            }
            else if (materialManager != null)
            {
                tile.GetComponent<Renderer>().material = materialManager.GetDefaultMaterial(
                    HD2DMaterialManager.MaterialType.Environment
                );
            }
            
            // Add some variation
            Color tileColor = Color.Lerp(Color.gray, Color.white, Random.Range(0.8f, 1f));
            tile.GetComponent<Renderer>().material.color = tileColor;
            
            return tile;
        }
        
        private void CreateWalls(int gridSize, float tileSize)
        {
            GameObject wallContainer = new GameObject("Walls");
            wallContainer.transform.SetParent(environmentBuilder.transform);
            
            // Create perimeter walls
            for (int i = 0; i < gridSize; i++)
            {
                // North wall
                GameObject northWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                northWall.transform.position = new Vector3(i * tileSize, 1.5f, gridSize * tileSize);
                northWall.transform.localScale = new Vector3(tileSize, 3f, 0.2f);
                northWall.transform.SetParent(wallContainer.transform);
                
                // South wall
                GameObject southWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                southWall.transform.position = new Vector3(i * tileSize, 1.5f, -tileSize);
                southWall.transform.localScale = new Vector3(tileSize, 3f, 0.2f);
                southWall.transform.SetParent(wallContainer.transform);
                
                // East wall
                GameObject eastWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                eastWall.transform.position = new Vector3(gridSize * tileSize, 1.5f, i * tileSize);
                eastWall.transform.localScale = new Vector3(0.2f, 3f, tileSize);
                eastWall.transform.SetParent(wallContainer.transform);
                
                // West wall
                GameObject westWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                westWall.transform.position = new Vector3(-tileSize, 1.5f, i * tileSize);
                westWall.transform.localScale = new Vector3(0.2f, 3f, tileSize);
                westWall.transform.SetParent(wallContainer.transform);
            }
        }
        
        private void AddEnvironmentDecorations()
        {
            GameObject decorContainer = new GameObject("Decorations");
            decorContainer.transform.SetParent(environmentBuilder.transform);
            
            // Add template-specific decorations
            switch (sceneTemplate)
            {
                case SceneTemplate.TownSquare:
                    // Add fountain in center
                    GameObject fountain = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    fountain.transform.position = new Vector3(10, 0.5f, 10);
                    fountain.transform.localScale = new Vector3(3, 1, 3);
                    fountain.transform.SetParent(decorContainer.transform);
                    break;
                    
                case SceneTemplate.Forest:
                    // Add trees
                    for (int i = 0; i < 10; i++)
                    {
                        GameObject tree = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                        tree.transform.position = new Vector3(
                            Random.Range(2, 18),
                            2,
                            Random.Range(2, 18)
                        );
                        tree.transform.localScale = new Vector3(0.5f, 4, 0.5f);
                        tree.transform.SetParent(decorContainer.transform);
                    }
                    break;
                    
                case SceneTemplate.Dungeon:
                    // Add pillars
                    for (int x = 5; x < 20; x += 5)
                    {
                        for (int z = 5; z < 20; z += 5)
                        {
                            GameObject pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                            pillar.transform.position = new Vector3(x, 1.5f, z);
                            pillar.transform.localScale = new Vector3(1, 3, 1);
                            pillar.transform.SetParent(decorContainer.transform);
                        }
                    }
                    break;
            }
        }
        
        #endregion
        
        #region Lighting Setup
        
        private IEnumerator SetupLighting()
        {
            Debug.Log("Setting up HD-2D lighting...");
            
            // Enable shadows
            lighting.EnableShadows(true);
            
            // Add additional lights based on template
            switch (sceneTemplate)
            {
                case SceneTemplate.TownSquare:
                    // Add street lamps
                    lighting.AddPointLight(new Vector3(5, 3, 5), Color.yellow, 0.5f, 10f);
                    lighting.AddPointLight(new Vector3(15, 3, 5), Color.yellow, 0.5f, 10f);
                    lighting.AddPointLight(new Vector3(5, 3, 15), Color.yellow, 0.5f, 10f);
                    lighting.AddPointLight(new Vector3(15, 3, 15), Color.yellow, 0.5f, 10f);
                    break;
                    
                case SceneTemplate.Dungeon:
                    // Torches already added in configuration
                    break;
                    
                case SceneTemplate.Castle:
                    // Add chandelier lights
                    lighting.AddSpotLight(
                        new Vector3(10, 5, 10),
                        new Vector3(90, 0, 0),
                        new Color(1f, 0.9f, 0.7f),
                        2f, 15f, 60f
                    );
                    break;
            }
            
            yield return null;
        }
        
        #endregion
        
        #region Post-Processing Setup
        
        private IEnumerator SetupPostProcessing()
        {
            Debug.Log("Setting up HD-2D post-processing...");
            
            // Enable depth of field for tilt-shift effect
            postProcessing.ToggleDepthOfField();
            
            // Enable ambient occlusion
            postProcessing.ToggleAmbientOcclusion();
            
            yield return null;
        }
        
        #endregion
        
        #region Character Creation
        
        private IEnumerator CreateCharacters()
        {
            Debug.Log("Creating demo characters...");
            
            // Create player character
            playerCharacter = CreateCharacter(
                "Player",
                new Vector3(10, 0, 10),
                true,
                Color.blue
            );
            
            // Set camera to follow player
            if (tiltShiftCamera != null)
            {
                tiltShiftCamera.SetFollowTarget(playerCharacter.transform);
            }
            
            // Create NPCs
            for (int i = 0; i < 5; i++)
            {
                Vector3 position = new Vector3(
                    Random.Range(5, 15),
                    0,
                    Random.Range(5, 15)
                );
                
                GameObject npc = CreateCharacter(
                    $"NPC_{i}",
                    position,
                    false,
                    Random.ColorHSV()
                );
                
                demoCharacters.Add(npc);
            }
            
            yield return null;
        }
        
        private GameObject CreateCharacter(string name, Vector3 position, bool isPlayer, Color color)
        {
            GameObject character = new GameObject(name);
            character.transform.position = position;
            
            // Add sprite renderer
            HD2DSpriteRenderer spriteRenderer = character.AddComponent<HD2DSpriteRenderer>();
            spriteRenderer.billboardMode = HD2DSpriteRenderer.BillboardMode.YAxis;
            spriteRenderer.castShadows = true;
            spriteRenderer.receiveShadows = true;
            
            // Create a simple quad for the sprite
            GameObject spriteQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            spriteQuad.transform.SetParent(character.transform);
            spriteQuad.transform.localPosition = new Vector3(0, 1, 0);
            spriteQuad.transform.localScale = new Vector3(1, 2, 1);
            
            // Apply material
            Renderer renderer = spriteQuad.GetComponent<Renderer>();
            if (spriteMaterial != null)
            {
                renderer.material = spriteMaterial;
            }
            else if (materialManager != null)
            {
                renderer.material = materialManager.GetDefaultMaterial(
                    HD2DMaterialManager.MaterialType.Sprite
                );
            }
            renderer.material.color = color;
            
            // Add animation controller
            HD2DSpriteAnimationController animController = character.AddComponent<HD2DSpriteAnimationController>();
            
            // Add shadow caster
            HD2DSpriteShadowCaster shadowCaster = character.AddComponent<HD2DSpriteShadowCaster>();
            shadowCaster.shadowType = HD2DSpriteShadowCaster.ShadowType.Simple;
            
            // Add simple movement for player
            if (isPlayer)
            {
                character.AddComponent<SimpleCharacterController>();
            }
            else
            {
                // Add simple AI movement for NPCs
                character.AddComponent<SimpleNPCController>();
            }
            
            return character;
        }
        
        #endregion
        
        #region UI Setup
        
        private void SetupDemoUI()
        {
            Debug.Log("Setting up demo UI...");
            
            if (uiCanvas == null)
            {
                GameObject canvasGO = new GameObject("Demo UI Canvas");
                uiCanvas = canvasGO.AddComponent<Canvas>();
                uiCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvasGO.AddComponent<CanvasScaler>();
                canvasGO.AddComponent<GraphicRaycaster>();
            }
            
            // Create FPS display
            if (fpsText == null)
            {
                GameObject fpsGO = new GameObject("FPS Text");
                fpsGO.transform.SetParent(uiCanvas.transform);
                fpsText = fpsGO.AddComponent<Text>();
                fpsText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                fpsText.fontSize = 20;
                fpsText.color = Color.white;
                RectTransform rect = fpsGO.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0, 1);
                rect.anchorMax = new Vector2(0, 1);
                rect.anchoredPosition = new Vector2(10, -10);
                rect.sizeDelta = new Vector2(200, 30);
            }
            
            // Create info text
            if (infoText == null)
            {
                GameObject infoGO = new GameObject("Info Text");
                infoGO.transform.SetParent(uiCanvas.transform);
                infoText = infoGO.AddComponent<Text>();
                infoText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                infoText.fontSize = 16;
                infoText.color = Color.white;
                infoText.text = $"HD-2D Demo - {sceneTemplate}\n" +
                               "WASD: Move | Q/E: Rotate Camera | Mouse Wheel: Zoom\n" +
                               "1-6: Change Scene | Space: Toggle Effects";
                RectTransform rect = infoGO.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0.5f, 1);
                rect.anchorMax = new Vector2(0.5f, 1);
                rect.anchoredPosition = new Vector2(0, -30);
                rect.sizeDelta = new Vector2(600, 60);
                infoText.alignment = TextAnchor.MiddleCenter;
            }
        }
        
        private void UpdateUI()
        {
            if (fpsText != null && sceneComposer != null)
            {
                float fps = sceneComposer.GetCurrentFPS();
                fpsText.text = $"FPS: {fps:F1}";
            }
        }
        
        #endregion
        
        #region Input Handling
        
        private void HandleInput()
        {
            // Camera controls
            if (tiltShiftCamera != null)
            {
                // Rotate camera
                if (Input.GetKey(KeyCode.Q))
                {
                    tiltShiftCamera.transform.Rotate(Vector3.up, -45 * Time.deltaTime);
                }
                if (Input.GetKey(KeyCode.E))
                {
                    tiltShiftCamera.transform.Rotate(Vector3.up, 45 * Time.deltaTime);
                }
                
                // Zoom
                float scroll = Input.GetAxis("Mouse ScrollWheel");
                if (scroll != 0)
                {
                    float currentDistance = tiltShiftCamera.GetCameraDistance();
                    tiltShiftCamera.SetCameraDistance(currentDistance - scroll * 10);
                }
            }
            
            // Scene switching
            if (Input.GetKeyDown(KeyCode.Alpha1))
                SwitchToTemplate(SceneTemplate.TownSquare);
            if (Input.GetKeyDown(KeyCode.Alpha2))
                SwitchToTemplate(SceneTemplate.Forest);
            if (Input.GetKeyDown(KeyCode.Alpha3))
                SwitchToTemplate(SceneTemplate.Dungeon);
            if (Input.GetKeyDown(KeyCode.Alpha4))
                SwitchToTemplate(SceneTemplate.Castle);
            if (Input.GetKeyDown(KeyCode.Alpha5))
                SwitchToTemplate(SceneTemplate.Beach);
            if (Input.GetKeyDown(KeyCode.Alpha6))
                SwitchToTemplate(SceneTemplate.Mountain);
            
            // Toggle effects
            if (Input.GetKeyDown(KeyCode.Space))
            {
                if (postProcessing != null)
                {
                    postProcessing.ToggleBloom();
                    postProcessing.ToggleVignette();
                }
            }
            
            // Weather control
            if (Input.GetKeyDown(KeyCode.R) && lighting != null)
            {
                lighting.SetWeather(HD2DLightingManager.WeatherType.Rainy, 2f);
            }
            if (Input.GetKeyDown(KeyCode.C) && lighting != null)
            {
                lighting.SetWeather(HD2DLightingManager.WeatherType.Clear, 2f);
            }
        }
        
        private void SwitchToTemplate(SceneTemplate template)
        {
            sceneTemplate = template;
            ConfigureSceneTemplate();
        }
        
        #endregion
        
        #region Public Methods
        
        public void ResetScene()
        {
            // Clear existing objects
            foreach (var character in demoCharacters)
            {
                if (character != null)
                    Destroy(character);
            }
            demoCharacters.Clear();
            
            if (playerCharacter != null)
                Destroy(playerCharacter);
            
            // Restart setup
            StartCoroutine(SetupScene());
        }
        
        public void SetQuality(int level)
        {
            if (sceneComposer != null)
            {
                HD2DSceneComposer.PerformanceProfile profile = (HD2DSceneComposer.PerformanceProfile)level;
                sceneComposer.SetPerformanceProfile(profile);
            }
        }
        
        public void SetTimeOfDay(float hours)
        {
            if (lighting != null)
            {
                lighting.SetTimeOfDay(hours);
            }
        }
        
        #endregion
    }
    
    /// <summary>
    /// Simple character controller for demo
    /// </summary>
    public class SimpleCharacterController : MonoBehaviour
    {
        public float moveSpeed = 5f;
        public float rotateSpeed = 180f;
        
        void Update()
        {
            float horizontal = Input.GetAxis("Horizontal");
            float vertical = Input.GetAxis("Vertical");
            
            Vector3 movement = new Vector3(horizontal, 0, vertical);
            movement = movement.normalized * moveSpeed * Time.deltaTime;
            
            transform.Translate(movement, Space.World);
            
            if (movement.magnitude > 0)
            {
                Quaternion targetRotation = Quaternion.LookRotation(movement);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotateSpeed * Time.deltaTime);
            }
        }
    }
    
    /// <summary>
    /// Simple NPC controller for demo
    /// </summary>
    public class SimpleNPCController : MonoBehaviour
    {
        private Vector3 targetPosition;
        private float moveSpeed = 2f;
        private float waitTime = 0;
        
        void Start()
        {
            ChooseNewTarget();
        }
        
        void Update()
        {
            if (waitTime > 0)
            {
                waitTime -= Time.deltaTime;
                return;
            }
            
            Vector3 direction = targetPosition - transform.position;
            direction.y = 0;
            
            if (direction.magnitude < 0.5f)
            {
                waitTime = Random.Range(1f, 3f);
                ChooseNewTarget();
            }
            else
            {
                transform.Translate(direction.normalized * moveSpeed * Time.deltaTime, Space.World);
            }
        }
        
        void ChooseNewTarget()
        {
            targetPosition = new Vector3(
                Random.Range(5, 15),
                0,
                Random.Range(5, 15)
            );
        }
    }
}