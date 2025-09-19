using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Burst;
using HD2D.Core;

namespace HD2D.Rendering.Sprites
{
    /// <summary>
    /// Optimized sprite batching system for HD2D rendering
    /// </summary>
    public class HD2DSpriteBatcher : MonoBehaviour
    {
        [Header("Batching Settings")]
        [SerializeField] private int maxBatchSize = HD2DConstants.MAX_BATCH_SIZE;
        [SerializeField] private float cullingDistance = HD2DConstants.DEFAULT_CULLING_DISTANCE;
        [SerializeField] private bool useFrustumCulling = true;
        [SerializeField] private bool useOcclusionCulling = true;
        [SerializeField] private bool useGPUInstancing = true;
        
        [Header("Performance")]
        [SerializeField] private bool useJobSystem = true;
        [SerializeField] private int jobBatchSize = 32;
        [SerializeField] private bool showDebugInfo = false;
        
        [Header("LOD Settings")]
        [SerializeField] private bool useLOD = true;
        [SerializeField] private float[] lodDistances = { 10f, 20f, 40f };
        [SerializeField] private float[] lodBias = { 1.0f, 0.5f, 0.25f };
        
        // Batch data structures
        private Dictionary<Material, BatchGroup> batchGroups = new Dictionary<Material, BatchGroup>();
        private List<HD2DSpriteRenderer> registeredSprites = new List<HD2DSpriteRenderer>();
        private Camera mainCamera;
        private Plane[] frustumPlanes;
        
        // Mesh data
        private Mesh quadMesh;
        private ComputeBuffer argsBuffer;
        private MaterialPropertyBlock propertyBlock;
        
        // Performance tracking
        private int totalBatchCount;
        private int totalSpriteCount;
        private int culledSpriteCount;
        
        /// <summary>
        /// Represents a group of sprites that can be batched together
        /// </summary>
        public class BatchGroup
        {
            public Material material;
            public List<Matrix4x4> matrices = new List<Matrix4x4>();
            public List<Vector4> uvOffsets = new List<Vector4>();
            public List<Vector4> colors = new List<Vector4>();
            public ComputeBuffer matrixBuffer;
            public ComputeBuffer uvBuffer;
            public ComputeBuffer colorBuffer;
            public MaterialPropertyBlock propertyBlock;
            
            public void Clear()
            {
                matrices.Clear();
                uvOffsets.Clear();
                colors.Clear();
            }
            
            public void Dispose()
            {
                matrixBuffer?.Release();
                uvBuffer?.Release();
                colorBuffer?.Release();
            }
        }
        
        #region Unity Lifecycle
        
        void Awake()
        {
            mainCamera = Camera.main;
            propertyBlock = new MaterialPropertyBlock();
            CreateQuadMesh();
            
            if (useGPUInstancing)
            {
                InitializeComputeBuffers();
            }
        }
        
        void Start()
        {
            // Auto-register all sprites in scene
            AutoRegisterSprites();
        }
        
        void Update()
        {
            UpdateFrustumPlanes();
            
            if (useJobSystem)
            {
                UpdateBatchesWithJobs();
            }
            else
            {
                UpdateBatches();
            }
        }
        
        void LateUpdate()
        {
            RenderBatches();
            
            if (showDebugInfo)
            {
                DisplayDebugInfo();
            }
        }
        
        void OnDestroy()
        {
            foreach (var group in batchGroups.Values)
            {
                group.Dispose();
            }
            
            argsBuffer?.Release();
            
            if (quadMesh != null)
            {
                DestroyImmediate(quadMesh);
            }
        }
        
        #endregion
        
        #region Initialization
        
        private void CreateQuadMesh()
        {
            quadMesh = new Mesh();
            quadMesh.name = "HD2D Sprite Quad";
            
            Vector3[] vertices = new Vector3[]
            {
                new Vector3(-0.5f, -0.5f, 0),
                new Vector3(0.5f, -0.5f, 0),
                new Vector3(0.5f, 0.5f, 0),
                new Vector3(-0.5f, 0.5f, 0)
            };
            
            Vector2[] uvs = new Vector2[]
            {
                new Vector2(0, 0),
                new Vector2(1, 0),
                new Vector2(1, 1),
                new Vector2(0, 1)
            };
            
            int[] triangles = new int[] { 0, 2, 1, 0, 3, 2 };
            
            Vector3[] normals = new Vector3[]
            {
                -Vector3.forward,
                -Vector3.forward,
                -Vector3.forward,
                -Vector3.forward
            };
            
            quadMesh.vertices = vertices;
            quadMesh.uv = uvs;
            quadMesh.triangles = triangles;
            quadMesh.normals = normals;
            quadMesh.RecalculateBounds();
        }
        
        private void InitializeComputeBuffers()
        {
            // Initialize indirect args buffer for GPU instancing
            argsBuffer = new ComputeBuffer(1, 5 * sizeof(uint), ComputeBufferType.IndirectArguments);
            uint[] args = new uint[5] { 0, 0, 0, 0, 0 };
            
            // Arguments for DrawMeshInstancedIndirect
            args[0] = (uint)quadMesh.GetIndexCount(0);
            args[1] = 0; // Instance count (set per frame)
            args[2] = (uint)quadMesh.GetIndexStart(0);
            args[3] = (uint)quadMesh.GetBaseVertex(0);
            args[4] = 0;
            
            argsBuffer.SetData(args);
        }
        
        private void AutoRegisterSprites()
        {
            HD2DSpriteRenderer[] sprites = FindObjectsOfType<HD2DSpriteRenderer>();
            foreach (var sprite in sprites)
            {
                RegisterSprite(sprite);
            }
        }
        
        #endregion
        
        #region Sprite Registration
        
        /// <summary>
        /// Register a sprite for batching
        /// </summary>
        public void RegisterSprite(HD2DSpriteRenderer sprite)
        {
            if (sprite == null || registeredSprites.Contains(sprite))
                return;
            
            registeredSprites.Add(sprite);
            
            Material mat = sprite.GetMaterial();
            if (mat != null && !batchGroups.ContainsKey(mat))
            {
                CreateBatchGroup(mat);
            }
        }
        
        /// <summary>
        /// Unregister a sprite from batching
        /// </summary>
        public void UnregisterSprite(HD2DSpriteRenderer sprite)
        {
            registeredSprites.Remove(sprite);
        }
        
        private void CreateBatchGroup(Material material)
        {
            BatchGroup group = new BatchGroup
            {
                material = material,
                propertyBlock = new MaterialPropertyBlock()
            };
            
            if (useGPUInstancing)
            {
                // Pre-allocate buffers for GPU instancing
                group.matrixBuffer = new ComputeBuffer(maxBatchSize, sizeof(float) * 16);
                group.uvBuffer = new ComputeBuffer(maxBatchSize, sizeof(float) * 4);
                group.colorBuffer = new ComputeBuffer(maxBatchSize, sizeof(float) * 4);
            }
            
            batchGroups[material] = group;
        }
        
        #endregion
        
        #region Batch Updates
        
        private void UpdateFrustumPlanes()
        {
            if (mainCamera != null && useFrustumCulling)
            {
                frustumPlanes = GeometryUtility.CalculateFrustumPlanes(mainCamera);
            }
        }
        
        private void UpdateBatches()
        {
            // Clear previous frame data
            foreach (var group in batchGroups.Values)
            {
                group.Clear();
            }
            
            totalSpriteCount = 0;
            culledSpriteCount = 0;
            
            // Process each sprite
            foreach (var sprite in registeredSprites)
            {
                if (sprite == null || !sprite.gameObject.activeInHierarchy)
                    continue;
                
                totalSpriteCount++;
                
                // Culling checks
                if (!ShouldRenderSprite(sprite))
                {
                    culledSpriteCount++;
                    continue;
                }
                
                // Add to appropriate batch
                Material mat = sprite.GetMaterial();
                if (mat != null && batchGroups.ContainsKey(mat))
                {
                    AddSpriteToBatch(sprite, batchGroups[mat]);
                }
            }
            
            // Update GPU buffers if using instancing
            if (useGPUInstancing)
            {
                UpdateGPUBuffers();
            }
        }
        
        private void UpdateBatchesWithJobs()
        {
            // Implementation with Unity Job System for better performance
            // This would use Burst-compiled jobs for culling and matrix calculations
            
            // For now, fall back to regular update
            UpdateBatches();
        }
        
        private bool ShouldRenderSprite(HD2DSpriteRenderer sprite)
        {
            // Distance culling
            float distance = Vector3.Distance(mainCamera.transform.position, sprite.transform.position);
            if (distance > cullingDistance)
                return false;
            
            // Frustum culling
            if (useFrustumCulling && frustumPlanes != null)
            {
                Bounds bounds = sprite.GetBounds();
                if (!GeometryUtility.TestPlanesAABB(frustumPlanes, bounds))
                    return false;
            }
            
            // Occlusion culling (simplified)
            if (useOcclusionCulling)
            {
                // Could implement more sophisticated occlusion culling here
                // For now, just use Unity's built-in occlusion
            }
            
            return true;
        }
        
        private void AddSpriteToBatch(HD2DSpriteRenderer sprite, BatchGroup group)
        {
            // Calculate billboard matrix
            Matrix4x4 matrix = CalculateBillboardMatrix(sprite);
            
            // Get sprite properties
            Vector4 uvOffset = sprite.GetUVOffset();
            Color color = sprite.Color;
            
            // Add to batch
            group.matrices.Add(matrix);
            group.uvOffsets.Add(uvOffset);
            group.colors.Add(color);
        }
        
        private Matrix4x4 CalculateBillboardMatrix(HD2DSpriteRenderer sprite)
        {
            Transform spriteTransform = sprite.transform;
            Vector3 position = spriteTransform.position;
            Vector3 scale = spriteTransform.localScale;
            
            // Calculate billboard rotation
            Vector3 cameraPos = mainCamera.transform.position;
            Vector3 lookDir = (cameraPos - position).normalized;
            
            // Preserve Y-axis for character sprites
            lookDir.y = 0;
            if (lookDir != Vector3.zero)
            {
                Quaternion billboardRotation = Quaternion.LookRotation(-lookDir, Vector3.up);
                return Matrix4x4.TRS(position, billboardRotation, scale);
            }
            
            return Matrix4x4.TRS(position, spriteTransform.rotation, scale);
        }
        
        private void UpdateGPUBuffers()
        {
            foreach (var group in batchGroups.Values)
            {
                if (group.matrices.Count > 0)
                {
                    group.matrixBuffer.SetData(group.matrices);
                    group.uvBuffer.SetData(group.uvOffsets);
                    group.colorBuffer.SetData(group.colors);
                    
                    group.propertyBlock.SetBuffer("_MatrixBuffer", group.matrixBuffer);
                    group.propertyBlock.SetBuffer("_UVBuffer", group.uvBuffer);
                    group.propertyBlock.SetBuffer("_ColorBuffer", group.colorBuffer);
                }
            }
        }
        
        #endregion
        
        #region Rendering
        
        private void RenderBatches()
        {
            totalBatchCount = 0;
            
            foreach (var group in batchGroups.Values)
            {
                if (group.matrices.Count == 0)
                    continue;
                
                if (useGPUInstancing && SystemInfo.supportsInstancing)
                {
                    RenderBatchGPUInstanced(group);
                }
                else
                {
                    RenderBatchStandard(group);
                }
            }
        }
        
        private void RenderBatchGPUInstanced(BatchGroup group)
        {
            // Update indirect args
            uint[] args = new uint[] { 
                (uint)quadMesh.GetIndexCount(0), 
                (uint)group.matrices.Count, 
                (uint)quadMesh.GetIndexStart(0), 
                (uint)quadMesh.GetBaseVertex(0), 
                0 
            };
            argsBuffer.SetData(args);
            
            // Draw with GPU instancing
            Graphics.DrawMeshInstancedIndirect(
                quadMesh,
                0,
                group.material,
                new Bounds(Vector3.zero, Vector3.one * 1000f),
                argsBuffer,
                0,
                group.propertyBlock,
                ShadowCastingMode.On,
                true
            );
            
            totalBatchCount++;
        }
        
        private void RenderBatchStandard(BatchGroup group)
        {
            // Split into chunks if exceeding max batch size
            for (int i = 0; i < group.matrices.Count; i += maxBatchSize)
            {
                int count = Mathf.Min(maxBatchSize, group.matrices.Count - i);
                
                List<Matrix4x4> batchMatrices = group.matrices.GetRange(i, count);
                
                // Set properties for this batch
                group.propertyBlock.SetVectorArray("_UVOffsets", group.uvOffsets.GetRange(i, count));
                group.propertyBlock.SetVectorArray("_Colors", group.colors.GetRange(i, count));
                
                // Draw instanced
                Graphics.DrawMeshInstanced(
                    quadMesh,
                    0,
                    group.material,
                    batchMatrices,
                    group.propertyBlock,
                    ShadowCastingMode.On,
                    true
                );
                
                totalBatchCount++;
            }
        }
        
        #endregion
        
        #region Debug
        
        private void DisplayDebugInfo()
        {
            string debugText = $"HD2D Sprite Batcher\n";
            debugText += $"Total Sprites: {totalSpriteCount}\n";
            debugText += $"Culled Sprites: {culledSpriteCount}\n";
            debugText += $"Rendered Sprites: {totalSpriteCount - culledSpriteCount}\n";
            debugText += $"Batch Count: {totalBatchCount}\n";
            debugText += $"Batch Groups: {batchGroups.Count}\n";
            
            if (useGPUInstancing)
            {
                debugText += "GPU Instancing: Enabled\n";
            }
            
            Debug.Log(debugText);
        }
        
        void OnDrawGizmosSelected()
        {
            if (mainCamera != null)
            {
                // Draw culling distance
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(mainCamera.transform.position, cullingDistance);
                
                // Draw LOD distances
                if (useLOD)
                {
                    for (int i = 0; i < lodDistances.Length; i++)
                    {
                        Gizmos.color = Color.Lerp(Color.green, Color.red, (float)i / lodDistances.Length);
                        Gizmos.DrawWireSphere(mainCamera.transform.position, lodDistances[i]);
                    }
                }
            }
        }
        
        #endregion
        
        #region Burst Jobs
        
        /// <summary>
        /// Burst-compiled job for culling calculations
        /// </summary>
        [BurstCompile]
        struct CullingJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<float3> positions;
            [ReadOnly] public float3 cameraPosition;
            [ReadOnly] public float cullingDistance;
            [ReadOnly] public NativeArray<float4> frustumPlanes;
            
            [WriteOnly] public NativeArray<bool> isVisible;
            
            public void Execute(int index)
            {
                float3 position = positions[index];
                float distance = math.distance(cameraPosition, position);
                
                // Distance culling
                if (distance > cullingDistance)
                {
                    isVisible[index] = false;
                    return;
                }
                
                // Simplified frustum culling
                // Would need proper plane testing here
                isVisible[index] = true;
            }
        }
        
        #endregion
    }
}