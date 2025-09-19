using UnityEngine;
using UnityEngine.Rendering;
using System;
using HD2D.Core;

namespace HD2D.Rendering.Sprites
{
    /// <summary>
    /// Core sprite renderer component for HD-2D style sprites with billboard support
    /// </summary>
    [RequireComponent(typeof(MeshRenderer), typeof(MeshFilter))]
    public class HD2DSpriteRenderer : MonoBehaviour
    {
        [Header("Sprite Settings")]
        [SerializeField] private Sprite sprite;
        [SerializeField] private Color color = Color.white;
        [SerializeField] private int pixelsPerUnit = HD2DConstants.DEFAULT_PIXELS_PER_UNIT;
        [SerializeField] private bool flipX = false;
        [SerializeField] private bool flipY = false;
        
        [Header("Billboard Settings")]
        [SerializeField] private bool billboardMode = true;
        [SerializeField] private BillboardType billboardType = BillboardType.Full;
        [SerializeField] private bool lockYAxis = false;
        
        [Header("Rendering")]
        [SerializeField] private int sortingOrder = 0;
        [SerializeField] private string sortingLayerName = HD2DConstants.SORTING_CHARACTERS;
        [SerializeField] private bool castShadows = true;
        [SerializeField] private bool receiveShadows = false;
        
        [Header("Outline")]
        [SerializeField] private bool enableOutline = true;
        [SerializeField] private Color outlineColor = Color.black;
        [SerializeField] private float outlineWidth = HD2DConstants.DEFAULT_OUTLINE_WIDTH;
        
        [Header("Emission")]
        [SerializeField] private bool enableEmission = false;
        [SerializeField] private Texture2D emissionMap;
        [SerializeField] private Color emissionColor = Color.white;
        [SerializeField] private float emissionIntensity = 1.0f;
        
        [Header("Animation")]
        [SerializeField] private HD2DSpriteAnimationController animationController;
        
        // Components
        private MeshRenderer meshRenderer;
        private MeshFilter meshFilter;
        private MaterialPropertyBlock propertyBlock;
        private Mesh spriteMesh;
        private Camera mainCamera;
        
        // State
        private Vector3 lastCameraPosition;
        private Quaternion originalRotation;
        private bool isDirty = true;
        
        public enum BillboardType
        {
            Full,           // Full 3D billboard
            YAxis,          // Only rotate around Y axis
            ScreenAligned   // Always face screen
        }
        
        #region Properties
        
        public Sprite Sprite
        {
            get => sprite;
            set
            {
                if (sprite != value)
                {
                    sprite = value;
                    isDirty = true;
                    UpdateSprite();
                }
            }
        }
        
        public Color Color
        {
            get => color;
            set
            {
                color = value;
                UpdateMaterialProperties();
            }
        }
        
        public int SortingOrder
        {
            get => sortingOrder;
            set
            {
                sortingOrder = value;
                UpdateSortingOrder();
            }
        }
        
        public bool FlipX
        {
            get => flipX;
            set
            {
                flipX = value;
                UpdateSprite();
            }
        }
        
        public bool FlipY
        {
            get => flipY;
            set
            {
                flipY = value;
                UpdateSprite();
            }
        }
        
        #endregion
        
        #region Unity Lifecycle
        
        void Awake()
        {
            InitializeComponents();
            originalRotation = transform.rotation;
        }
        
        void Start()
        {
            mainCamera = Camera.main;
            if (mainCamera == null)
            {
                Debug.LogWarning("HD2DSpriteRenderer: No main camera found!");
            }
            
            CreateSpriteMesh();
            UpdateSprite();
            UpdateMaterialProperties();
        }
        
        void Update()
        {
            if (billboardMode && mainCamera != null)
            {
                UpdateBillboard();
            }
            
            if (isDirty)
            {
                UpdateSprite();
                isDirty = false;
            }
        }
        
        void OnValidate()
        {
            if (Application.isPlaying)
            {
                isDirty = true;
                UpdateMaterialProperties();
            }
        }
        
        void OnDestroy()
        {
            if (spriteMesh != null)
            {
                DestroyImmediate(spriteMesh);
            }
        }
        
        #endregion
        
        #region Initialization
        
        private void InitializeComponents()
        {
            meshRenderer = GetComponent<MeshRenderer>();
            meshFilter = GetComponent<MeshFilter>();
            
            if (meshRenderer == null)
            {
                meshRenderer = gameObject.AddComponent<MeshRenderer>();
            }
            
            if (meshFilter == null)
            {
                meshFilter = gameObject.AddComponent<MeshFilter>();
            }
            
            // Configure renderer settings
            meshRenderer.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            meshRenderer.receiveShadows = receiveShadows;
            
            // Create material property block for efficient property updates
            propertyBlock = new MaterialPropertyBlock();
            
            // Set up animation controller if not assigned
            if (animationController == null)
            {
                animationController = GetComponent<HD2DSpriteAnimationController>();
            }
        }
        
        private void CreateSpriteMesh()
        {
            if (spriteMesh == null)
            {
                spriteMesh = new Mesh();
                spriteMesh.name = "HD2D Sprite Mesh";
            }
            
            // Create a simple quad
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
            
            spriteMesh.vertices = vertices;
            spriteMesh.uv = uvs;
            spriteMesh.triangles = triangles;
            spriteMesh.normals = normals;
            spriteMesh.RecalculateBounds();
            
            meshFilter.mesh = spriteMesh;
        }
        
        #endregion
        
        #region Sprite Updates
        
        private void UpdateSprite()
        {
            if (sprite == null || spriteMesh == null)
                return;
            
            // Update mesh UVs based on sprite
            Vector2[] spriteUVs = sprite.uv;
            Vector2[] meshUVs = new Vector2[4];
            
            if (!flipX && !flipY)
            {
                meshUVs[0] = spriteUVs[0]; // Bottom-left
                meshUVs[1] = spriteUVs[1]; // Bottom-right
                meshUVs[2] = spriteUVs[2]; // Top-right
                meshUVs[3] = spriteUVs[3]; // Top-left
            }
            else if (flipX && !flipY)
            {
                meshUVs[0] = spriteUVs[1];
                meshUVs[1] = spriteUVs[0];
                meshUVs[2] = spriteUVs[3];
                meshUVs[3] = spriteUVs[2];
            }
            else if (!flipX && flipY)
            {
                meshUVs[0] = spriteUVs[3];
                meshUVs[1] = spriteUVs[2];
                meshUVs[2] = spriteUVs[1];
                meshUVs[3] = spriteUVs[0];
            }
            else // Both flipped
            {
                meshUVs[0] = spriteUVs[2];
                meshUVs[1] = spriteUVs[3];
                meshUVs[2] = spriteUVs[0];
                meshUVs[3] = spriteUVs[1];
            }
            
            spriteMesh.uv = meshUVs;
            
            // Update mesh size based on sprite dimensions
            float width = sprite.rect.width / pixelsPerUnit;
            float height = sprite.rect.height / pixelsPerUnit;
            
            Vector3[] vertices = new Vector3[]
            {
                new Vector3(-width * 0.5f, -height * 0.5f, 0),
                new Vector3(width * 0.5f, -height * 0.5f, 0),
                new Vector3(width * 0.5f, height * 0.5f, 0),
                new Vector3(-width * 0.5f, height * 0.5f, 0)
            };
            
            spriteMesh.vertices = vertices;
            spriteMesh.RecalculateBounds();
            
            // Update material texture
            if (propertyBlock != null && sprite.texture != null)
            {
                propertyBlock.SetTexture(HD2DConstants.SHADER_MAIN_TEX, sprite.texture);
                meshRenderer.SetPropertyBlock(propertyBlock);
            }
        }
        
        private void UpdateMaterialProperties()
        {
            if (propertyBlock == null || meshRenderer == null)
                return;
            
            // Update color
            propertyBlock.SetColor(HD2DConstants.SHADER_COLOR, color);
            
            // Update outline
            if (enableOutline)
            {
                propertyBlock.SetFloat(HD2DConstants.SHADER_OUTLINE_WIDTH, outlineWidth);
                propertyBlock.SetColor(HD2DConstants.SHADER_OUTLINE_COLOR, outlineColor);
            }
            else
            {
                propertyBlock.SetFloat(HD2DConstants.SHADER_OUTLINE_WIDTH, 0);
            }
            
            // Update emission
            if (enableEmission && emissionMap != null)
            {
                propertyBlock.SetTexture(HD2DConstants.SHADER_EMISSION_MAP, emissionMap);
                propertyBlock.SetColor(HD2DConstants.SHADER_EMISSION_COLOR, emissionColor * emissionIntensity);
            }
            
            // Update pixels per unit for pixel-perfect rendering
            propertyBlock.SetFloat(HD2DConstants.SHADER_PIXELS_PER_UNIT, pixelsPerUnit);
            
            meshRenderer.SetPropertyBlock(propertyBlock);
        }
        
        private void UpdateSortingOrder()
        {
            if (meshRenderer != null)
            {
                meshRenderer.sortingLayerName = sortingLayerName;
                meshRenderer.sortingOrder = sortingOrder;
            }
        }
        
        #endregion
        
        #region Billboard
        
        private void UpdateBillboard()
        {
            Vector3 cameraPosition = mainCamera.transform.position;
            
            // Only update if camera has moved
            if (cameraPosition == lastCameraPosition)
                return;
            
            lastCameraPosition = cameraPosition;
            
            switch (billboardType)
            {
                case BillboardType.Full:
                    ApplyFullBillboard();
                    break;
                case BillboardType.YAxis:
                    ApplyYAxisBillboard();
                    break;
                case BillboardType.ScreenAligned:
                    ApplyScreenAlignedBillboard();
                    break;
            }
        }
        
        private void ApplyFullBillboard()
        {
            Vector3 lookDirection = mainCamera.transform.position - transform.position;
            
            if (lockYAxis)
            {
                lookDirection.y = 0;
            }
            
            if (lookDirection != Vector3.zero)
            {
                transform.rotation = Quaternion.LookRotation(lookDirection, Vector3.up);
            }
        }
        
        private void ApplyYAxisBillboard()
        {
            Vector3 lookDirection = mainCamera.transform.position - transform.position;
            lookDirection.y = 0;
            
            if (lookDirection != Vector3.zero)
            {
                Quaternion rotation = Quaternion.LookRotation(lookDirection, Vector3.up);
                transform.rotation = Quaternion.Euler(originalRotation.eulerAngles.x, 
                                                      rotation.eulerAngles.y, 
                                                      originalRotation.eulerAngles.z);
            }
        }
        
        private void ApplyScreenAlignedBillboard()
        {
            transform.rotation = mainCamera.transform.rotation;
        }
        
        #endregion
        
        #region Public Methods
        
        /// <summary>
        /// Set the sprite material
        /// </summary>
        public void SetMaterial(Material material)
        {
            if (meshRenderer != null)
            {
                meshRenderer.material = material;
                UpdateMaterialProperties();
            }
        }
        
        /// <summary>
        /// Get the current material
        /// </summary>
        public Material GetMaterial()
        {
            return meshRenderer != null ? meshRenderer.sharedMaterial : null;
        }
        
        /// <summary>
        /// Get the sprite bounds in world space
        /// </summary>
        public Bounds GetBounds()
        {
            if (meshRenderer != null)
            {
                return meshRenderer.bounds;
            }
            
            return new Bounds(transform.position, Vector3.one);
        }
        
        /// <summary>
        /// Get UV offset for sprite atlas
        /// </summary>
        public Vector4 GetUVOffset()
        {
            if (sprite != null)
            {
                Rect rect = sprite.rect;
                Texture2D texture = sprite.texture;
                
                if (texture != null)
                {
                    return new Vector4(
                        rect.x / texture.width,
                        rect.y / texture.height,
                        rect.width / texture.width,
                        rect.height / texture.height
                    );
                }
            }
            
            return new Vector4(0, 0, 1, 1);
        }
        
        /// <summary>
        /// Play a sprite animation
        /// </summary>
        public void PlayAnimation(string animationName)
        {
            if (animationController != null)
            {
                animationController.Play(animationName);
            }
        }
        
        /// <summary>
        /// Stop current animation
        /// </summary>
        public void StopAnimation()
        {
            if (animationController != null)
            {
                animationController.Stop();
            }
        }
        
        #endregion
    }
}