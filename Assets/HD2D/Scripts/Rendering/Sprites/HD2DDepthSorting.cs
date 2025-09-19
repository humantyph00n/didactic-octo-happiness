using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using HD2D.Core;

namespace HD2D.Rendering.Sprites
{
    /// <summary>
    /// Manages depth sorting for HD2D sprites in 3D space
    /// </summary>
    public class HD2DDepthSorting : MonoBehaviour
    {
        [Header("Sorting Settings")]
        [SerializeField] private SortingMode sortingMode = SortingMode.ZDepth;
        [SerializeField] private bool dynamicSorting = true;
        [SerializeField] private float sortingUpdateInterval = 0.1f;
        [SerializeField] private bool useYAxisOffset = true;
        [SerializeField] private float yAxisWeight = 0.1f;
        
        [Header("Layer Configuration")]
        [SerializeField] private LayerSettings[] layerSettings = new LayerSettings[]
        {
            new LayerSettings { layerName = "Background", sortingOrder = 0, depthOffset = -100f },
            new LayerSettings { layerName = "Default", sortingOrder = 100, depthOffset = 0f },
            new LayerSettings { layerName = "Foreground", sortingOrder = 200, depthOffset = 100f }
        };
        
        [Header("Performance")]
        [SerializeField] private bool useSpatialPartitioning = true;
        [SerializeField] private float gridCellSize = 10f;
        [SerializeField] private int maxSpritesPerCell = 50;
        
        // Sorting modes
        public enum SortingMode
        {
            ZDepth,           // Sort by Z position
            YPosition,        // Sort by Y position (classic 2D RPG style)
            CameraDistance,   // Sort by distance from camera
            Custom,           // Custom sorting function
            Hybrid            // Combination of Z and Y
        }
        
        [System.Serializable]
        public class LayerSettings
        {
            public string layerName;
            public int sortingOrder;
            public float depthOffset;
            public bool overrideAutoSort;
        }
        
        // Sprite management
        private List<HD2DSpriteRenderer> managedSprites = new List<HD2DSpriteRenderer>();
        private Dictionary<string, LayerSettings> layerLookup = new Dictionary<string, LayerSettings>();
        private Camera mainCamera;
        private float lastSortTime;
        
        // Spatial partitioning
        private Dictionary<Vector2Int, List<HD2DSpriteRenderer>> spatialGrid;
        private Bounds worldBounds;
        
        // Custom sorting delegate
        public delegate float CustomSortFunction(HD2DSpriteRenderer sprite, Camera camera);
        public CustomSortFunction customSortFunction;
        
        #region Unity Lifecycle
        
        void Awake()
        {
            mainCamera = Camera.main;
            InitializeLayerLookup();
            
            if (useSpatialPartitioning)
            {
                InitializeSpatialGrid();
            }
        }
        
        void Start()
        {
            // Auto-register all sprites in scene
            AutoRegisterSprites();
        }
        
        void Update()
        {
            if (dynamicSorting)
            {
                if (Time.time - lastSortTime >= sortingUpdateInterval)
                {
                    UpdateSorting();
                    lastSortTime = Time.time;
                }
            }
        }
        
        void OnValidate()
        {
            // Ensure unique layer names
            HashSet<string> usedNames = new HashSet<string>();
            foreach (var layer in layerSettings)
            {
                if (usedNames.Contains(layer.layerName))
                {
                    Debug.LogWarning($"Duplicate layer name: {layer.layerName}");
                }
                usedNames.Add(layer.layerName);
            }
        }
        
        #endregion
        
        #region Initialization
        
        private void InitializeLayerLookup()
        {
            layerLookup.Clear();
            foreach (var layer in layerSettings)
            {
                layerLookup[layer.layerName] = layer;
            }
        }
        
        private void InitializeSpatialGrid()
        {
            spatialGrid = new Dictionary<Vector2Int, List<HD2DSpriteRenderer>>();
            
            // Calculate world bounds (can be adjusted based on your world size)
            worldBounds = new Bounds(Vector3.zero, Vector3.one * 1000f);
        }
        
        private void AutoRegisterSprites()
        {
            HD2DSpriteRenderer[] sprites = FindObjectsOfType<HD2DSpriteRenderer>();
            foreach (var sprite in sprites)
            {
                RegisterSprite(sprite);
            }
            
            // Perform initial sort
            UpdateSorting();
        }
        
        #endregion
        
        #region Sprite Registration
        
        /// <summary>
        /// Register a sprite for depth sorting
        /// </summary>
        public void RegisterSprite(HD2DSpriteRenderer sprite)
        {
            if (sprite == null || managedSprites.Contains(sprite))
                return;
            
            managedSprites.Add(sprite);
            
            // Add to spatial grid if enabled
            if (useSpatialPartitioning)
            {
                AddToSpatialGrid(sprite);
            }
            
            // Apply initial sorting
            ApplySortingToSprite(sprite);
        }
        
        /// <summary>
        /// Unregister a sprite from depth sorting
        /// </summary>
        public void UnregisterSprite(HD2DSpriteRenderer sprite)
        {
            managedSprites.Remove(sprite);
            
            if (useSpatialPartitioning)
            {
                RemoveFromSpatialGrid(sprite);
            }
        }
        
        /// <summary>
        /// Set the layer for a sprite
        /// </summary>
        public void SetSpriteLayer(HD2DSpriteRenderer sprite, string layerName)
        {
            if (sprite != null && layerLookup.ContainsKey(layerName))
            {
                sprite.SortingLayerName = layerName;
                ApplySortingToSprite(sprite);
            }
        }
        
        #endregion
        
        #region Sorting Logic
        
        /// <summary>
        /// Update sorting for all managed sprites
        /// </summary>
        public void UpdateSorting()
        {
            if (managedSprites.Count == 0)
                return;
            
            // Update spatial grid if needed
            if (useSpatialPartitioning)
            {
                UpdateSpatialGrid();
            }
            
            // Sort sprites based on mode
            switch (sortingMode)
            {
                case SortingMode.ZDepth:
                    SortByZDepth();
                    break;
                case SortingMode.YPosition:
                    SortByYPosition();
                    break;
                case SortingMode.CameraDistance:
                    SortByCameraDistance();
                    break;
                case SortingMode.Custom:
                    SortByCustomFunction();
                    break;
                case SortingMode.Hybrid:
                    SortByHybrid();
                    break;
            }
        }
        
        private void SortByZDepth()
        {
            var sortedSprites = managedSprites
                .Where(s => s != null && s.gameObject.activeInHierarchy)
                .OrderBy(s => s.transform.position.z)
                .ToList();
            
            ApplySortingOrders(sortedSprites);
        }
        
        private void SortByYPosition()
        {
            var sortedSprites = managedSprites
                .Where(s => s != null && s.gameObject.activeInHierarchy)
                .OrderByDescending(s => s.transform.position.y)
                .ToList();
            
            ApplySortingOrders(sortedSprites);
        }
        
        private void SortByCameraDistance()
        {
            if (mainCamera == null)
                return;
            
            Vector3 cameraPos = mainCamera.transform.position;
            
            var sortedSprites = managedSprites
                .Where(s => s != null && s.gameObject.activeInHierarchy)
                .OrderByDescending(s => Vector3.Distance(s.transform.position, cameraPos))
                .ToList();
            
            ApplySortingOrders(sortedSprites);
        }
        
        private void SortByCustomFunction()
        {
            if (customSortFunction == null)
            {
                Debug.LogWarning("Custom sort function not set!");
                return;
            }
            
            var sortedSprites = managedSprites
                .Where(s => s != null && s.gameObject.activeInHierarchy)
                .OrderBy(s => customSortFunction(s, mainCamera))
                .ToList();
            
            ApplySortingOrders(sortedSprites);
        }
        
        private void SortByHybrid()
        {
            // Combine Z depth and Y position for sorting
            var sortedSprites = managedSprites
                .Where(s => s != null && s.gameObject.activeInHierarchy)
                .OrderBy(s => 
                {
                    float zValue = s.transform.position.z;
                    float yValue = useYAxisOffset ? s.transform.position.y * yAxisWeight : 0;
                    return zValue - yValue;
                })
                .ToList();
            
            ApplySortingOrders(sortedSprites);
        }
        
        private void ApplySortingOrders(List<HD2DSpriteRenderer> sortedSprites)
        {
            // Group by layer
            var layerGroups = sortedSprites.GroupBy(s => s.SortingLayerName);
            
            foreach (var group in layerGroups)
            {
                LayerSettings layer = null;
                if (layerLookup.TryGetValue(group.Key, out layer))
                {
                    if (layer.overrideAutoSort)
                        continue;
                    
                    int baseOrder = layer.sortingOrder;
                    int index = 0;
                    
                    foreach (var sprite in group)
                    {
                        sprite.SortingOrder = baseOrder + index;
                        index++;
                    }
                }
                else
                {
                    // Default layer
                    int index = 0;
                    foreach (var sprite in group)
                    {
                        sprite.SortingOrder = 100 + index;
                        index++;
                    }
                }
            }
        }
        
        private void ApplySortingToSprite(HD2DSpriteRenderer sprite)
        {
            if (sprite == null)
                return;
            
            // Get layer settings
            LayerSettings layer = null;
            if (layerLookup.TryGetValue(sprite.SortingLayerName, out layer))
            {
                if (!layer.overrideAutoSort)
                {
                    // Will be set by next sorting update
                    return;
                }
                
                sprite.SortingOrder = layer.sortingOrder;
            }
        }
        
        #endregion
        
        #region Spatial Partitioning
        
        private void UpdateSpatialGrid()
        {
            // Clear and rebuild grid
            spatialGrid.Clear();
            
            foreach (var sprite in managedSprites)
            {
                if (sprite != null && sprite.gameObject.activeInHierarchy)
                {
                    AddToSpatialGrid(sprite);
                }
            }
        }
        
        private void AddToSpatialGrid(HD2DSpriteRenderer sprite)
        {
            Vector2Int gridPos = GetGridPosition(sprite.transform.position);
            
            if (!spatialGrid.ContainsKey(gridPos))
            {
                spatialGrid[gridPos] = new List<HD2DSpriteRenderer>();
            }
            
            spatialGrid[gridPos].Add(sprite);
        }
        
        private void RemoveFromSpatialGrid(HD2DSpriteRenderer sprite)
        {
            Vector2Int gridPos = GetGridPosition(sprite.transform.position);
            
            if (spatialGrid.ContainsKey(gridPos))
            {
                spatialGrid[gridPos].Remove(sprite);
                
                if (spatialGrid[gridPos].Count == 0)
                {
                    spatialGrid.Remove(gridPos);
                }
            }
        }
        
        private Vector2Int GetGridPosition(Vector3 worldPos)
        {
            int x = Mathf.FloorToInt(worldPos.x / gridCellSize);
            int z = Mathf.FloorToInt(worldPos.z / gridCellSize);
            return new Vector2Int(x, z);
        }
        
        /// <summary>
        /// Get sprites in a specific area
        /// </summary>
        public List<HD2DSpriteRenderer> GetSpritesInArea(Bounds area)
        {
            if (!useSpatialPartitioning)
            {
                return managedSprites.Where(s => 
                    s != null && area.Contains(s.transform.position)
                ).ToList();
            }
            
            List<HD2DSpriteRenderer> result = new List<HD2DSpriteRenderer>();
            
            // Calculate grid bounds
            Vector2Int minGrid = GetGridPosition(area.min);
            Vector2Int maxGrid = GetGridPosition(area.max);
            
            // Check all relevant grid cells
            for (int x = minGrid.x; x <= maxGrid.x; x++)
            {
                for (int y = minGrid.y; y <= maxGrid.y; y++)
                {
                    Vector2Int gridPos = new Vector2Int(x, y);
                    if (spatialGrid.ContainsKey(gridPos))
                    {
                        foreach (var sprite in spatialGrid[gridPos])
                        {
                            if (area.Contains(sprite.transform.position))
                            {
                                result.Add(sprite);
                            }
                        }
                    }
                }
            }
            
            return result;
        }
        
        #endregion
        
        #region Utility Methods
        
        /// <summary>
        /// Force an immediate sorting update
        /// </summary>
        public void ForceSortingUpdate()
        {
            UpdateSorting();
        }
        
        /// <summary>
        /// Get the current sorting order for a position
        /// </summary>
        public int GetSortingOrderForPosition(Vector3 position, string layerName = "Default")
        {
            float sortValue = 0;
            
            switch (sortingMode)
            {
                case SortingMode.ZDepth:
                    sortValue = position.z;
                    break;
                case SortingMode.YPosition:
                    sortValue = -position.y;
                    break;
                case SortingMode.CameraDistance:
                    if (mainCamera != null)
                        sortValue = -Vector3.Distance(position, mainCamera.transform.position);
                    break;
                case SortingMode.Hybrid:
                    sortValue = position.z - (useYAxisOffset ? position.y * yAxisWeight : 0);
                    break;
            }
            
            // Get base order from layer
            int baseOrder = 100;
            if (layerLookup.TryGetValue(layerName, out LayerSettings layer))
            {
                baseOrder = layer.sortingOrder;
            }
            
            // Convert float sort value to int order
            return baseOrder + Mathf.RoundToInt(sortValue * 10);
        }
        
        /// <summary>
        /// Set custom sorting function
        /// </summary>
        public void SetCustomSortFunction(CustomSortFunction function)
        {
            customSortFunction = function;
            if (sortingMode == SortingMode.Custom)
            {
                UpdateSorting();
            }
        }
        
        #endregion
        
        #region Debug
        
        void OnDrawGizmosSelected()
        {
            if (useSpatialPartitioning && spatialGrid != null)
            {
                // Draw spatial grid
                Gizmos.color = new Color(0, 1, 0, 0.2f);
                
                foreach (var kvp in spatialGrid)
                {
                    Vector3 cellCenter = new Vector3(
                        kvp.Key.x * gridCellSize + gridCellSize * 0.5f,
                        0,
                        kvp.Key.y * gridCellSize + gridCellSize * 0.5f
                    );
                    
                    Gizmos.DrawWireCube(cellCenter, new Vector3(gridCellSize, 10, gridCellSize));
                    
                    // Draw sprite count
                    if (kvp.Value.Count > 0)
                    {
                        Gizmos.color = Color.Lerp(Color.green, Color.red, 
                            (float)kvp.Value.Count / maxSpritesPerCell);
                        Gizmos.DrawCube(cellCenter, Vector3.one);
                    }
                }
            }
            
            // Draw sorting order visualization
            if (Application.isPlaying)
            {
                Gizmos.color = Color.cyan;
                foreach (var sprite in managedSprites)
                {
                    if (sprite != null)
                    {
                        Vector3 pos = sprite.transform.position;
                        Gizmos.DrawLine(pos, pos + Vector3.up * (sprite.SortingOrder * 0.01f));
                    }
                }
            }
        }
        
        #endregion
    }
}