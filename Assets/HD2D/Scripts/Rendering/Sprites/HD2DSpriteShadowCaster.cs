using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;
using HD2D.Core;

namespace HD2D.Rendering.Sprites
{
    /// <summary>
    /// Enables 2D sprites to cast shadows on 3D geometry
    /// </summary>
    [RequireComponent(typeof(HD2DSpriteRenderer))]
    public class HD2DSpriteShadowCaster : MonoBehaviour
    {
        [Header("Shadow Settings")]
        [SerializeField] private bool castShadows = true;
        [SerializeField] private ShadowCastingMode shadowMode = ShadowCastingMode.On;
        [SerializeField] private bool receiveShadows = true;
        [SerializeField] private float shadowStrength = 1.0f;
        [SerializeField] private float shadowBias = 0.05f;
        
        [Header("Shadow Mesh")]
        [SerializeField] private ShadowMeshType meshType = ShadowMeshType.Quad;
        [SerializeField] private bool useSpriteSilhouette = false;
        [SerializeField] private float meshDepth = 0.1f;
        [SerializeField] private bool autoUpdateMesh = true;
        
        [Header("Shadow Projection")]
        [SerializeField] private ProjectionMode projectionMode = ProjectionMode.Planar;
        [SerializeField] private Vector3 shadowDirection = new Vector3(0.2f, -1f, 0.3f).normalized;
        [SerializeField] private float shadowDistance = 10f;
        [SerializeField] private LayerMask groundLayers = -1;
        
        [Header("Performance")]
        [SerializeField] private bool useLOD = true;
        [SerializeField] private float[] lodDistances = { 10f, 20f, 40f };
        [SerializeField] private bool cullBackfaces = true;
        
        public enum ShadowMeshType
        {
            Quad,           // Simple quad mesh
            Box,            // Box volume for better shadows
            Silhouette,     // Sprite silhouette mesh
            Custom          // Custom mesh
        }
        
        public enum ProjectionMode
        {
            Standard,       // Unity's standard shadow casting
            Planar,         // Planar projection onto ground
            Volumetric      // Volumetric shadow mesh
        }
        
        // Components
        private HD2DSpriteRenderer spriteRenderer;
        private MeshRenderer shadowRenderer;
        private MeshFilter shadowMeshFilter;
        private Mesh shadowMesh;
        private Material shadowMaterial;
        
        // Shadow projection
        private GameObject shadowProjector;
        private Matrix4x4 shadowMatrix;
        private RaycastHit groundHit;
        
        // LOD
        private int currentLOD = 0;
        private Camera mainCamera;
        
        #region Unity Lifecycle
        
        void Awake()
        {
            spriteRenderer = GetComponent<HD2DSpriteRenderer>();
            mainCamera = Camera.main;
            
            InitializeShadowComponents();
        }
        
        void Start()
        {
            if (castShadows)
            {
                CreateShadowMesh();
                SetupShadowMaterial();
            }
        }
        
        void Update()
        {
            if (!castShadows)
                return;
            
            // Update LOD
            if (useLOD)
            {
                UpdateLOD();
            }
            
            // Update shadow mesh if needed
            if (autoUpdateMesh && spriteRenderer.Sprite != null)
            {
                UpdateShadowMesh();
            }
            
            // Update shadow projection
            if (projectionMode == ProjectionMode.Planar)
            {
                UpdatePlanarProjection();
            }
        }
        
        void OnDestroy()
        {
            CleanupShadowComponents();
        }
        
        void OnValidate()
        {
            shadowDirection.Normalize();
            shadowStrength = Mathf.Clamp01(shadowStrength);
            shadowBias = Mathf.Max(0, shadowBias);
        }
        
        #endregion
        
        #region Initialization
        
        private void InitializeShadowComponents()
        {
            // Create shadow renderer object
            GameObject shadowObj = new GameObject("ShadowCaster");
            shadowObj.transform.SetParent(transform);
            shadowObj.transform.localPosition = Vector3.zero;
            shadowObj.transform.localRotation = Quaternion.identity;
            shadowObj.transform.localScale = Vector3.one;
            
            // Add mesh components
            shadowMeshFilter = shadowObj.AddComponent<MeshFilter>();
            shadowRenderer = shadowObj.AddComponent<MeshRenderer>();
            
            // Configure renderer
            shadowRenderer.shadowCastingMode = shadowMode;
            shadowRenderer.receiveShadows = receiveShadows;
            
            // Set layer for shadow only rendering
            shadowObj.layer = LayerMask.NameToLayer(HD2DConstants.SHADOW_CASTER_LAYER);
        }
        
        private void SetupShadowMaterial()
        {
            // Create shadow-only material
            Shader shadowShader = Shader.Find("HD2D/ShadowCaster");
            if (shadowShader == null)
            {
                // Fallback to standard shadow caster
                shadowShader = Shader.Find("Universal Render Pipeline/Lit");
            }
            
            shadowMaterial = new Material(shadowShader);
            shadowMaterial.name = "HD2D Shadow Material";
            
            // Configure material for shadow casting only
            shadowMaterial.SetFloat("_Mode", 1); // Cutout mode
            shadowMaterial.SetFloat("_Cutoff", 0.5f);
            shadowMaterial.SetFloat("_ShadowStrength", shadowStrength);
            
            shadowRenderer.material = shadowMaterial;
        }
        
        #endregion
        
        #region Shadow Mesh Creation
        
        private void CreateShadowMesh()
        {
            switch (meshType)
            {
                case ShadowMeshType.Quad:
                    shadowMesh = CreateQuadMesh();
                    break;
                case ShadowMeshType.Box:
                    shadowMesh = CreateBoxMesh();
                    break;
                case ShadowMeshType.Silhouette:
                    shadowMesh = CreateSilhouetteMesh();
                    break;
                case ShadowMeshType.Custom:
                    // User provides custom mesh
                    break;
            }
            
            if (shadowMesh != null)
            {
                shadowMeshFilter.mesh = shadowMesh;
            }
        }
        
        private Mesh CreateQuadMesh()
        {
            Mesh mesh = new Mesh();
            mesh.name = "Shadow Quad";
            
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
            
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            
            return mesh;
        }
        
        private Mesh CreateBoxMesh()
        {
            Mesh mesh = new Mesh();
            mesh.name = "Shadow Box";
            
            float halfDepth = meshDepth * 0.5f;
            
            Vector3[] vertices = new Vector3[]
            {
                // Front face
                new Vector3(-0.5f, -0.5f, -halfDepth),
                new Vector3(0.5f, -0.5f, -halfDepth),
                new Vector3(0.5f, 0.5f, -halfDepth),
                new Vector3(-0.5f, 0.5f, -halfDepth),
                
                // Back face
                new Vector3(-0.5f, -0.5f, halfDepth),
                new Vector3(0.5f, -0.5f, halfDepth),
                new Vector3(0.5f, 0.5f, halfDepth),
                new Vector3(-0.5f, 0.5f, halfDepth)
            };
            
            int[] triangles = new int[]
            {
                // Front
                0, 2, 1, 0, 3, 2,
                // Back
                4, 5, 6, 4, 6, 7,
                // Left
                0, 4, 7, 0, 7, 3,
                // Right
                1, 2, 6, 1, 6, 5,
                // Top
                3, 7, 6, 3, 6, 2,
                // Bottom
                0, 1, 5, 0, 5, 4
            };
            
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            
            return mesh;
        }
        
        private Mesh CreateSilhouetteMesh()
        {
            if (spriteRenderer.Sprite == null)
                return CreateQuadMesh(); // Fallback to quad
            
            Sprite sprite = spriteRenderer.Sprite;
            Mesh mesh = new Mesh();
            mesh.name = "Shadow Silhouette";
            
            // Get sprite vertices
            Vector2[] spriteVertices = sprite.vertices;
            ushort[] spriteTriangles = sprite.triangles;
            
            // Create 3D vertices from sprite
            List<Vector3> vertices = new List<Vector3>();
            List<int> triangles = new List<int>();
            
            // Front face
            foreach (Vector2 v2 in spriteVertices)
            {
                vertices.Add(new Vector3(v2.x, v2.y, -meshDepth * 0.5f));
            }
            
            // Back face
            foreach (Vector2 v2 in spriteVertices)
            {
                vertices.Add(new Vector3(v2.x, v2.y, meshDepth * 0.5f));
            }
            
            // Front face triangles
            for (int i = 0; i < spriteTriangles.Length; i += 3)
            {
                triangles.Add(spriteTriangles[i]);
                triangles.Add(spriteTriangles[i + 1]);
                triangles.Add(spriteTriangles[i + 2]);
            }
            
            // Back face triangles (reversed winding)
            int vertexOffset = spriteVertices.Length;
            for (int i = 0; i < spriteTriangles.Length; i += 3)
            {
                triangles.Add(spriteTriangles[i + 2] + vertexOffset);
                triangles.Add(spriteTriangles[i + 1] + vertexOffset);
                triangles.Add(spriteTriangles[i] + vertexOffset);
            }
            
            // Create edge faces
            CreateEdgeFaces(vertices, triangles, spriteVertices);
            
            mesh.vertices = vertices.ToArray();
            mesh.triangles = triangles.ToArray();
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            
            return mesh;
        }
        
        private void CreateEdgeFaces(List<Vector3> vertices, List<int> triangles, Vector2[] spriteVertices)
        {
            // Find edge vertices and create side faces
            HashSet<Edge> edges = new HashSet<Edge>();
            
            // Collect all edges
            for (int i = 0; i < triangles.Count; i += 3)
            {
                edges.Add(new Edge(triangles[i], triangles[i + 1]));
                edges.Add(new Edge(triangles[i + 1], triangles[i + 2]));
                edges.Add(new Edge(triangles[i + 2], triangles[i]));
            }
            
            // Create side faces for boundary edges
            foreach (Edge edge in edges)
            {
                if (edge.v1 < spriteVertices.Length && edge.v2 < spriteVertices.Length)
                {
                    int v1Front = edge.v1;
                    int v2Front = edge.v2;
                    int v1Back = edge.v1 + spriteVertices.Length;
                    int v2Back = edge.v2 + spriteVertices.Length;
                    
                    // Create quad for edge
                    triangles.Add(v1Front);
                    triangles.Add(v2Front);
                    triangles.Add(v2Back);
                    
                    triangles.Add(v1Front);
                    triangles.Add(v2Back);
                    triangles.Add(v1Back);
                }
            }
        }
        
        private struct Edge
        {
            public int v1, v2;
            
            public Edge(int v1, int v2)
            {
                this.v1 = Mathf.Min(v1, v2);
                this.v2 = Mathf.Max(v1, v2);
            }
            
            public override int GetHashCode()
            {
                return v1.GetHashCode() ^ (v2.GetHashCode() << 2);
            }
            
            public override bool Equals(object obj)
            {
                if (!(obj is Edge))
                    return false;
                
                Edge other = (Edge)obj;
                return v1 == other.v1 && v2 == other.v2;
            }
        }
        
        #endregion
        
        #region Shadow Updates
        
        private void UpdateShadowMesh()
        {
            if (shadowMesh == null)
                return;
            
            // Update mesh based on sprite changes
            if (useSpriteSilhouette && meshType == ShadowMeshType.Silhouette)
            {
                // Recreate silhouette if sprite changed
                Mesh newMesh = CreateSilhouetteMesh();
                if (newMesh != null)
                {
                    DestroyImmediate(shadowMesh);
                    shadowMesh = newMesh;
                    shadowMeshFilter.mesh = shadowMesh;
                }
            }
            
            // Update shadow material with sprite texture
            if (shadowMaterial != null && spriteRenderer.Sprite != null)
            {
                shadowMaterial.mainTexture = spriteRenderer.Sprite.texture;
                
                // Update UV offset for sprite atlas
                Vector4 uvOffset = spriteRenderer.GetUVOffset();
                shadowMaterial.SetVector("_MainTex_ST", uvOffset);
            }
            
            // Update transform to match sprite
            if (shadowRenderer != null)
            {
                shadowRenderer.transform.localScale = Vector3.one;
                shadowRenderer.transform.localRotation = Quaternion.identity;
                
                // Apply billboard rotation if needed
                if (spriteRenderer.BillboardMode != HD2DSpriteRenderer.BillboardType.None)
                {
                    ApplyBillboardRotation();
                }
            }
        }
        
        private void ApplyBillboardRotation()
        {
            if (mainCamera == null)
                return;
            
            Vector3 lookDir = mainCamera.transform.position - transform.position;
            
            switch (spriteRenderer.BillboardMode)
            {
                case HD2DSpriteRenderer.BillboardType.Full:
                    shadowRenderer.transform.rotation = Quaternion.LookRotation(-lookDir);
                    break;
                    
                case HD2DSpriteRenderer.BillboardType.YAxis:
                    lookDir.y = 0;
                    if (lookDir != Vector3.zero)
                    {
                        shadowRenderer.transform.rotation = Quaternion.LookRotation(-lookDir, Vector3.up);
                    }
                    break;
            }
        }
        
        #endregion
        
        #region Planar Projection
        
        private void UpdatePlanarProjection()
        {
            if (shadowProjector == null)
            {
                CreateShadowProjector();
            }
            
            // Cast ray to find ground
            Vector3 rayOrigin = transform.position;
            if (Physics.Raycast(rayOrigin, shadowDirection, out groundHit, shadowDistance, groundLayers))
            {
                // Position projector at ground hit point
                shadowProjector.transform.position = groundHit.point + groundHit.normal * shadowBias;
                
                // Calculate projection matrix
                CalculatePlanarProjectionMatrix(groundHit.normal, groundHit.point);
                
                // Apply projection to shadow mesh
                ApplyProjectionToMesh();
            }
            else
            {
                // No ground found, hide shadow
                if (shadowProjector != null)
                {
                    shadowProjector.SetActive(false);
                }
            }
        }
        
        private void CreateShadowProjector()
        {
            shadowProjector = new GameObject("Shadow Projector");
            shadowProjector.transform.SetParent(transform);
            
            // Add mesh components for projected shadow
            MeshFilter projectorFilter = shadowProjector.AddComponent<MeshFilter>();
            MeshRenderer projectorRenderer = shadowProjector.AddComponent<MeshRenderer>();
            
            // Create flat quad for projection
            projectorFilter.mesh = CreateQuadMesh();
            
            // Setup projector material
            Material projectorMat = new Material(Shader.Find("HD2D/PlanarShadow"));
            projectorMat.SetFloat("_ShadowStrength", shadowStrength);
            projectorRenderer.material = projectorMat;
            
            // Configure renderer
            projectorRenderer.shadowCastingMode = ShadowCastingMode.Off;
            projectorRenderer.receiveShadows = false;
        }
        
        private void CalculatePlanarProjectionMatrix(Vector3 planeNormal, Vector3 planePoint)
        {
            // Calculate planar projection matrix
            Vector4 plane = new Vector4(planeNormal.x, planeNormal.y, planeNormal.z, 
                -Vector3.Dot(planeNormal, planePoint));
            
            Vector3 lightDir = -shadowDirection;
            
            shadowMatrix = Matrix4x4.zero;
            shadowMatrix[0, 0] = plane.y * lightDir.y + plane.z * lightDir.z;
            shadowMatrix[0, 1] = -plane.x * lightDir.y;
            shadowMatrix[0, 2] = -plane.x * lightDir.z;
            shadowMatrix[0, 3] = -plane.x * plane.w;
            
            shadowMatrix[1, 0] = -plane.y * lightDir.x;
            shadowMatrix[1, 1] = plane.x * lightDir.x + plane.z * lightDir.z;
            shadowMatrix[1, 2] = -plane.y * lightDir.z;
            shadowMatrix[1, 3] = -plane.y * plane.w;
            
            shadowMatrix[2, 0] = -plane.z * lightDir.x;
            shadowMatrix[2, 1] = -plane.z * lightDir.y;
            shadowMatrix[2, 2] = plane.x * lightDir.x + plane.y * lightDir.y;
            shadowMatrix[2, 3] = -plane.z * plane.w;
            
            shadowMatrix[3, 3] = 1;
        }
        
        private void ApplyProjectionToMesh()
        {
            // Apply projection matrix to shadow mesh vertices
            // This would deform the mesh to create planar shadow
        }
        
        #endregion
        
        #region LOD System
        
        private void UpdateLOD()
        {
            if (mainCamera == null)
                return;
            
            float distance = Vector3.Distance(transform.position, mainCamera.transform.position);
            
            // Determine LOD level
            int newLOD = 0;
            for (int i = 0; i < lodDistances.Length; i++)
            {
                if (distance > lodDistances[i])
                {
                    newLOD = i + 1;
                }
            }
            
            // Apply LOD changes
            if (newLOD != currentLOD)
            {
                currentLOD = newLOD;
                ApplyLOD(currentLOD);
            }
        }
        
        private void ApplyLOD(int lodLevel)
        {
            switch (lodLevel)
            {
                case 0: // Full quality
                    shadowRenderer.shadowCastingMode = ShadowCastingMode.On;
                    shadowRenderer.enabled = true;
                    break;
                    
                case 1: // Reduced quality
                    shadowRenderer.shadowCastingMode = ShadowCastingMode.On;
                    shadowRenderer.enabled = true;
                    // Could simplify mesh here
                    break;
                    
                case 2: // Minimal quality
                    shadowRenderer.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
                    break;
                    
                default: // No shadows
                    shadowRenderer.enabled = false;
                    break;
            }
        }
        
        #endregion
        
        #region Public Methods
        
        /// <summary>
        /// Enable or disable shadow casting
        /// </summary>
        public void SetShadowCasting(bool enabled)
        {
            castShadows = enabled;
            if (shadowRenderer != null)
            {
                shadowRenderer.enabled = enabled;
            }
        }
        
        /// <summary>
        /// Set shadow strength
        /// </summary>
        public void SetShadowStrength(float strength)
        {
            shadowStrength = Mathf.Clamp01(strength);
            if (shadowMaterial != null)
            {
                shadowMaterial.SetFloat("_ShadowStrength", shadowStrength);
            }
        }
        
        /// <summary>
        /// Set custom shadow mesh
        /// </summary>
        public void SetCustomShadowMesh(Mesh mesh)
        {
            if (mesh != null)
            {
                meshType = ShadowMeshType.Custom;
                shadowMesh = mesh;
                if (shadowMeshFilter != null)
                {
                    shadowMeshFilter.mesh = shadowMesh;
                }
            }
        }
        
        #endregion
        
        #region Cleanup
        
        private void CleanupShadowComponents()
        {
            if (shadowMesh != null)
            {
                DestroyImmediate(shadowMesh);
            }
            
            if (shadowMaterial != null)
            {
                DestroyImmediate(shadowMaterial);
            }
            
            if (shadowProjector != null)
            {
                DestroyImmediate(shadowProjector);
            }
        }
        
        #endregion
        
        #region Debug
        
        void OnDrawGizmosSelected()
        {
            // Draw shadow direction
            Gizmos.color = Color.black;
            Gizmos.DrawRay(transform.position, shadowDirection * shadowDistance);
            
            // Draw ground hit point
            if (Application.isPlaying && projectionMode == ProjectionMode.Planar)
            {
                if (groundHit.collider != null)
                {
                    Gizmos.color = Color.red;
                    Gizmos.DrawWireSphere(groundHit.point, 0.1f);
                    Gizmos.DrawRay(groundHit.point, groundHit.normal);
                }
            }
            
            // Draw LOD distances
            if (useLOD)
            {
                for (int i = 0; i < lodDistances.Length; i++)
                {
                    Gizmos.color = Color.Lerp(Color.green, Color.red, (float)i / lodDistances.Length);
                    Gizmos.DrawWireSphere(transform.position, lodDistances[i]);
                }
            }
        }
        
        #endregion
    }
}