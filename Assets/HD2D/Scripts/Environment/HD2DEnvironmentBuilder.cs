using UnityEngine;
using System.Collections.Generic;
using System.Linq;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace HD2D.Environment
{
    /// <summary>
    /// Grid-based environment builder for HD-2D style levels
    /// </summary>
    public class HD2DEnvironmentBuilder : MonoBehaviour
    {
        [Header("Grid Configuration")]
        [SerializeField] private float gridSize = 1.0f;
        [SerializeField] private int gridWidth = 50;
        [SerializeField] private int gridHeight = 50;
        [SerializeField] private bool snapToGrid = true;
        [SerializeField] private bool showGrid = true;
        [SerializeField] private Color gridColor = new Color(1, 1, 1, 0.2f);
        
        [Header("Tile Sets")]
        [SerializeField] private List<TileSet> tileSets = new List<TileSet>();
        [SerializeField] private int activeTileSetIndex = 0;
        [SerializeField] private int selectedTileIndex = 0;
        
        [Header("Layers")]
        [SerializeField] private List<EnvironmentLayer> layers = new List<EnvironmentLayer>();
        [SerializeField] private int activeLayerIndex = 0;
        [SerializeField] private bool showAllLayers = true;
        
        [Header("Decoration")]
        [SerializeField] private List<PropSet> propSets = new List<PropSet>();
        [SerializeField] private float propDensity = 0.3f;
        [SerializeField] private bool randomRotation = true;
        [SerializeField] private bool randomScale = true;
        [SerializeField] private Vector2 scaleRange = new Vector2(0.8f, 1.2f);
        
        [Header("Materials")]
        [SerializeField] private Material defaultEnvironmentMaterial;
        [SerializeField] private bool useVertexColors = true;
        [SerializeField] private bool enableTriplanarMapping = true;
        
        [Header("Optimization")]
        [SerializeField] private bool combineMeshes = true;
        [SerializeField] private bool generateLODs = true;
        [SerializeField] private float[] lodDistances = { 20f, 40f, 80f };
        [SerializeField] private bool useOcclusionCulling = true;
        
        // Runtime data
        private Dictionary<Vector3Int, TileInstance> tileMap = new Dictionary<Vector3Int, TileInstance>();
        private List<GameObject> propInstances = new List<GameObject>();
        private GameObject environmentRoot;
        private MeshCombiner meshCombiner;
        
        #region Data Structures
        
        [System.Serializable]
        public class TileSet
        {
            public string name = "New Tileset";
            public List<TilePrefab> tiles = new List<TilePrefab>();
            public Material overrideMaterial;
            public bool useCustomShader;
            
            [System.Serializable]
            public class TilePrefab
            {
                public GameObject prefab;
                public Texture2D preview;
                public TileType type = TileType.Floor;
                public bool allowRotation = true;
                public bool allowMirroring = false;
                public float weight = 1.0f;
            }
            
            public enum TileType
            {
                Floor,
                Wall,
                Corner,
                Stairs,
                Platform,
                Decoration,
                Special
            }
        }
        
        [System.Serializable]
        public class EnvironmentLayer
        {
            public string name = "Layer";
            public float height = 0f;
            public bool visible = true;
            public bool locked = false;
            public Color tintColor = Color.white;
            public int sortingOrder = 0;
        }
        
        [System.Serializable]
        public class PropSet
        {
            public string name = "Props";
            public List<GameObject> propPrefabs = new List<GameObject>();
            public float minScale = 0.8f;
            public float maxScale = 1.2f;
            public bool alignToSurface = true;
            public LayerMask placementMask = -1;
        }
        
        private class TileInstance
        {
            public GameObject gameObject;
            public int tileSetIndex;
            public int tileIndex;
            public int rotation;
            public bool mirrored;
            public int layerIndex;
            public Vector3Int gridPosition;
        }
        
        #endregion
        
        #region Unity Lifecycle
        
        void Awake()
        {
            InitializeBuilder();
        }
        
        void Start()
        {
            if (layers.Count == 0)
            {
                CreateDefaultLayers();
            }
            
            if (Application.isPlaying)
            {
                BuildEnvironment();
            }
        }
        
        void OnValidate()
        {
            gridSize = Mathf.Max(0.1f, gridSize);
            gridWidth = Mathf.Max(1, gridWidth);
            gridHeight = Mathf.Max(1, gridHeight);
            propDensity = Mathf.Clamp01(propDensity);
        }
        
        #endregion
        
        #region Initialization
        
        private void InitializeBuilder()
        {
            // Create root object for environment
            if (environmentRoot == null)
            {
                environmentRoot = new GameObject("HD2D Environment");
                environmentRoot.transform.SetParent(transform);
                environmentRoot.transform.localPosition = Vector3.zero;
                environmentRoot.transform.localRotation = Quaternion.identity;
            }
            
            // Initialize mesh combiner
            if (combineMeshes)
            {
                meshCombiner = gameObject.AddComponent<MeshCombiner>();
            }
        }
        
        private void CreateDefaultLayers()
        {
            layers.Add(new EnvironmentLayer { name = "Ground", height = 0f });
            layers.Add(new EnvironmentLayer { name = "Level 1", height = 3f });
            layers.Add(new EnvironmentLayer { name = "Level 2", height = 6f });
        }
        
        #endregion
        
        #region Building
        
        public void BuildEnvironment()
        {
            ClearEnvironment();
            
            // Build each layer
            for (int i = 0; i < layers.Count; i++)
            {
                if (layers[i].visible)
                {
                    BuildLayer(i);
                }
            }
            
            // Place props
            if (propSets.Count > 0 && propDensity > 0)
            {
                PlaceProps();
            }
            
            // Optimize
            if (combineMeshes)
            {
                CombineMeshes();
            }
            
            if (generateLODs)
            {
                GenerateLODs();
            }
        }
        
        private void BuildLayer(int layerIndex)
        {
            EnvironmentLayer layer = layers[layerIndex];
            GameObject layerObject = new GameObject($"Layer_{layer.name}");
            layerObject.transform.SetParent(environmentRoot.transform);
            layerObject.transform.localPosition = new Vector3(0, layer.height, 0);
            
            // Get tiles for this layer from the tile map
            var layerTiles = tileMap.Where(kvp => kvp.Value.layerIndex == layerIndex);
            
            foreach (var kvp in layerTiles)
            {
                PlaceTileAtPosition(kvp.Key, kvp.Value, layerObject.transform);
            }
        }
        
        public void ClearEnvironment()
        {
            // Clear existing tiles
            foreach (var tile in tileMap.Values)
            {
                if (tile.gameObject != null)
                {
                    DestroyImmediate(tile.gameObject);
                }
            }
            tileMap.Clear();
            
            // Clear props
            foreach (var prop in propInstances)
            {
                if (prop != null)
                {
                    DestroyImmediate(prop);
                }
            }
            propInstances.Clear();
            
            // Clear layer objects
            if (environmentRoot != null)
            {
                for (int i = environmentRoot.transform.childCount - 1; i >= 0; i--)
                {
                    DestroyImmediate(environmentRoot.transform.GetChild(i).gameObject);
                }
            }
        }
        
        #endregion
        
        #region Tile Placement
        
        public void PlaceTile(Vector3 worldPosition)
        {
            if (activeTileSetIndex >= tileSets.Count || selectedTileIndex >= tileSets[activeTileSetIndex].tiles.Count)
                return;
            
            Vector3Int gridPos = WorldToGrid(worldPosition);
            PlaceTileAtGrid(gridPos, activeTileSetIndex, selectedTileIndex, 0, false);
        }
        
        public void PlaceTileAtGrid(Vector3Int gridPosition, int tileSetIndex, int tileIndex, int rotation, bool mirrored)
        {
            // Remove existing tile at this position
            RemoveTileAtGrid(gridPosition);
            
            // Create new tile instance
            TileInstance instance = new TileInstance
            {
                gridPosition = gridPosition,
                tileSetIndex = tileSetIndex,
                tileIndex = tileIndex,
                rotation = rotation,
                mirrored = mirrored,
                layerIndex = activeLayerIndex
            };
            
            // Add to map
            tileMap[gridPosition] = instance;
            
            // Create game object if in play mode
            if (Application.isPlaying)
            {
                GameObject layerObject = GetLayerObject(activeLayerIndex);
                if (layerObject != null)
                {
                    PlaceTileAtPosition(gridPosition, instance, layerObject.transform);
                }
            }
        }
        
        private void PlaceTileAtPosition(Vector3Int gridPos, TileInstance instance, Transform parent)
        {
            TileSet tileSet = tileSets[instance.tileSetIndex];
            TileSet.TilePrefab tilePrefab = tileSet.tiles[instance.tileIndex];
            
            if (tilePrefab.prefab == null)
                return;
            
            // Instantiate tile
            GameObject tileObject = Instantiate(tilePrefab.prefab, parent);
            Vector3 worldPos = GridToWorld(gridPos);
            tileObject.transform.position = worldPos;
            
            // Apply rotation
            if (tilePrefab.allowRotation)
            {
                tileObject.transform.rotation = Quaternion.Euler(0, instance.rotation * 90, 0);
            }
            
            // Apply mirroring
            if (tilePrefab.allowMirroring && instance.mirrored)
            {
                Vector3 scale = tileObject.transform.localScale;
                scale.x *= -1;
                tileObject.transform.localScale = scale;
            }
            
            // Apply material
            ApplyMaterialToTile(tileObject, tileSet);
            
            // Store reference
            instance.gameObject = tileObject;
        }
        
        public void RemoveTileAtGrid(Vector3Int gridPosition)
        {
            if (tileMap.TryGetValue(gridPosition, out TileInstance instance))
            {
                if (instance.gameObject != null)
                {
                    DestroyImmediate(instance.gameObject);
                }
                tileMap.Remove(gridPosition);
            }
        }
        
        #endregion
        
        #region Prop Placement
        
        private void PlaceProps()
        {
            System.Random random = new System.Random();
            
            foreach (var propSet in propSets)
            {
                if (propSet.propPrefabs.Count == 0)
                    continue;
                
                int propCount = Mathf.RoundToInt(gridWidth * gridHeight * propDensity);
                
                for (int i = 0; i < propCount; i++)
                {
                    PlaceRandomProp(propSet, random);
                }
            }
        }
        
        private void PlaceRandomProp(PropSet propSet, System.Random random)
        {
            // Select random prop
            GameObject propPrefab = propSet.propPrefabs[random.Next(propSet.propPrefabs.Count)];
            if (propPrefab == null)
                return;
            
            // Find random valid position
            Vector3 position = FindRandomPropPosition(random);
            
            // Instantiate prop
            GameObject prop = Instantiate(propPrefab, environmentRoot.transform);
            prop.transform.position = position;
            
            // Random rotation
            if (randomRotation)
            {
                prop.transform.rotation = Quaternion.Euler(0, random.Next(360), 0);
            }
            
            // Random scale
            if (randomScale)
            {
                float scale = Mathf.Lerp(propSet.minScale, propSet.maxScale, (float)random.NextDouble());
                prop.transform.localScale = Vector3.one * scale;
            }
            
            // Align to surface
            if (propSet.alignToSurface)
            {
                AlignToSurface(prop, propSet.placementMask);
            }
            
            propInstances.Add(prop);
        }
        
        private Vector3 FindRandomPropPosition(System.Random random)
        {
            float x = (float)(random.NextDouble() * gridWidth * gridSize);
            float z = (float)(random.NextDouble() * gridHeight * gridSize);
            
            // Find ground height at this position
            float y = 0;
            RaycastHit hit;
            if (Physics.Raycast(new Vector3(x, 100, z), Vector3.down, out hit, 200f))
            {
                y = hit.point.y;
            }
            
            return new Vector3(x, y, z);
        }
        
        private void AlignToSurface(GameObject obj, LayerMask mask)
        {
            RaycastHit hit;
            Vector3 origin = obj.transform.position + Vector3.up * 10;
            
            if (Physics.Raycast(origin, Vector3.down, out hit, 20f, mask))
            {
                obj.transform.position = hit.point;
                
                // Align rotation to surface normal
                Vector3 forward = Vector3.Cross(obj.transform.right, hit.normal);
                if (forward != Vector3.zero)
                {
                    obj.transform.rotation = Quaternion.LookRotation(forward, hit.normal);
                }
            }
        }
        
        #endregion
        
        #region Material Management
        
        private void ApplyMaterialToTile(GameObject tile, TileSet tileSet)
        {
            Renderer[] renderers = tile.GetComponentsInChildren<Renderer>();
            
            Material materialToApply = tileSet.overrideMaterial ?? defaultEnvironmentMaterial;
            
            if (materialToApply != null)
            {
                foreach (var renderer in renderers)
                {
                    renderer.material = materialToApply;
                    
                    // Apply vertex colors if enabled
                    if (useVertexColors)
                    {
                        ApplyVertexColors(renderer);
                    }
                }
            }
        }
        
        private void ApplyVertexColors(Renderer renderer)
        {
            MeshFilter meshFilter = renderer.GetComponent<MeshFilter>();
            if (meshFilter != null && meshFilter.mesh != null)
            {
                Mesh mesh = meshFilter.mesh;
                Color[] colors = new Color[mesh.vertexCount];
                
                // Apply layer tint color
                EnvironmentLayer layer = layers[activeLayerIndex];
                for (int i = 0; i < colors.Length; i++)
                {
                    colors[i] = layer.tintColor;
                }
                
                mesh.colors = colors;
            }
        }
        
        #endregion
        
        #region Optimization
        
        private void CombineMeshes()
        {
            if (meshCombiner != null)
            {
                meshCombiner.CombineEnvironmentMeshes(environmentRoot);
            }
        }
        
        private void GenerateLODs()
        {
            // Generate LOD groups for combined meshes
            MeshRenderer[] renderers = environmentRoot.GetComponentsInChildren<MeshRenderer>();
            
            foreach (var renderer in renderers)
            {
                LODGroup lodGroup = renderer.gameObject.GetComponent<LODGroup>();
                if (lodGroup == null)
                {
                    lodGroup = renderer.gameObject.AddComponent<LODGroup>();
                }
                
                // Create LOD levels
                LOD[] lods = new LOD[lodDistances.Length + 1];
                
                // Full detail LOD
                lods[0] = new LOD(0.5f, new Renderer[] { renderer });
                
                // Reduced detail LODs
                for (int i = 0; i < lodDistances.Length; i++)
                {
                    float screenRelativeHeight = 1.0f / (lodDistances[i] / 10f);
                    lods[i + 1] = new LOD(screenRelativeHeight, new Renderer[] { renderer });
                }
                
                lodGroup.SetLODs(lods);
                lodGroup.RecalculateBounds();
            }
        }
        
        #endregion
        
        #region Utility Methods
        
        public Vector3Int WorldToGrid(Vector3 worldPos)
        {
            int x = Mathf.RoundToInt(worldPos.x / gridSize);
            int y = Mathf.RoundToInt(worldPos.y / gridSize);
            int z = Mathf.RoundToInt(worldPos.z / gridSize);
            return new Vector3Int(x, y, z);
        }
        
        public Vector3 GridToWorld(Vector3Int gridPos)
        {
            return new Vector3(
                gridPos.x * gridSize,
                gridPos.y * gridSize,
                gridPos.z * gridSize
            );
        }
        
        public bool IsValidGridPosition(Vector3Int gridPos)
        {
            return gridPos.x >= 0 && gridPos.x < gridWidth &&
                   gridPos.z >= 0 && gridPos.z < gridHeight;
        }
        
        private GameObject GetLayerObject(int layerIndex)
        {
            if (layerIndex >= 0 && layerIndex < layers.Count)
            {
                string layerName = $"Layer_{layers[layerIndex].name}";
                Transform layerTransform = environmentRoot.transform.Find(layerName);
                return layerTransform?.gameObject;
            }
            return null;
        }
        
        public void SetActiveLayer(int index)
        {
            activeLayerIndex = Mathf.Clamp(index, 0, layers.Count - 1);
        }
        
        public void SetActiveTileSet(int index)
        {
            activeTileSetIndex = Mathf.Clamp(index, 0, tileSets.Count - 1);
        }
        
        #endregion
        
        #region Gizmos
        
        void OnDrawGizmos()
        {
            if (!showGrid)
                return;
            
            Gizmos.color = gridColor;
            
            // Draw grid
            for (int x = 0; x <= gridWidth; x++)
            {
                Vector3 start = new Vector3(x * gridSize, 0, 0);
                Vector3 end = new Vector3(x * gridSize, 0, gridHeight * gridSize);
                Gizmos.DrawLine(start, end);
            }
            
            for (int z = 0; z <= gridHeight; z++)
            {
                Vector3 start = new Vector3(0, 0, z * gridSize);
                Vector3 end = new Vector3(gridWidth * gridSize, 0, z * gridSize);
                Gizmos.DrawLine(start, end);
            }
            
            // Draw layer heights
            if (showAllLayers)
            {
                foreach (var layer in layers)
                {
                    if (layer.visible)
                    {
                        Gizmos.color = new Color(layer.tintColor.r, layer.tintColor.g, layer.tintColor.b, 0.1f);
                        Vector3 center = new Vector3(gridWidth * gridSize * 0.5f, layer.height, gridHeight * gridSize * 0.5f);
                        Vector3 size = new Vector3(gridWidth * gridSize, 0.1f, gridHeight * gridSize);
                        Gizmos.DrawCube(center, size);
                    }
                }
            }
        }
        
        void OnDrawGizmosSelected()
        {
            // Draw tile positions
            Gizmos.color = Color.yellow;
            foreach (var kvp in tileMap)
            {
                Vector3 worldPos = GridToWorld(kvp.Key);
                Gizmos.DrawWireCube(worldPos, Vector3.one * gridSize * 0.9f);
            }
        }
        
        #endregion
    }
    
    /// <summary>
    /// Helper class for combining meshes
    /// </summary>
    public class MeshCombiner : MonoBehaviour
    {
        public void CombineEnvironmentMeshes(GameObject root)
        {
            // Get all mesh filters in children
            MeshFilter[] meshFilters = root.GetComponentsInChildren<MeshFilter>();
            
            // Group by material
            Dictionary<Material, List<MeshFilter>> materialGroups = new Dictionary<Material, List<MeshFilter>>();
            
            foreach (var filter in meshFilters)
            {
                Renderer renderer = filter.GetComponent<Renderer>();
                if (renderer != null && renderer.sharedMaterial != null)
                {
                    Material mat = renderer.sharedMaterial;
                    if (!materialGroups.ContainsKey(mat))
                    {
                        materialGroups[mat] = new List<MeshFilter>();
                    }
                    materialGroups[mat].Add(filter);
                }
            }
            
            // Combine meshes for each material
            foreach (var kvp in materialGroups)
            {
                CombineMeshGroup(kvp.Key, kvp.Value, root.transform);
            }
        }
        
        private void CombineMeshGroup(Material material, List<MeshFilter> filters, Transform parent)
        {
            if (filters.Count == 0)
                return;
            
            // Create combine instances
            CombineInstance[] combine = new CombineInstance[filters.Count];
            
            for (int i = 0; i < filters.Count; i++)
            {
                combine[i].mesh = filters[i].sharedMesh;
                combine[i].transform = filters[i].transform.localToWorldMatrix;
                
                // Disable original renderer
                filters[i].GetComponent<Renderer>().enabled = false;
            }
            
            // Create combined mesh
            Mesh combinedMesh = new Mesh();
            combinedMesh.name = $"Combined_{material.name}";
            combinedMesh.CombineMeshes(combine, true, true);
            
            // Create new game object for combined mesh
            GameObject combinedObject = new GameObject($"CombinedMesh_{material.name}");
            combinedObject.transform.SetParent(parent);
            combinedObject.transform.localPosition = Vector3.zero;
            combinedObject.transform.localRotation = Quaternion.identity;
            
            // Add components
            MeshFilter meshFilter = combinedObject.AddComponent<MeshFilter>();
            meshFilter.mesh = combinedMesh;
            
            MeshRenderer meshRenderer = combinedObject.AddComponent<MeshRenderer>();
            meshRenderer.material = material;
            
            // Add mesh collider if needed
            MeshCollider collider = combinedObject.AddComponent<MeshCollider>();
            collider.sharedMesh = combinedMesh;
        }
    }
}