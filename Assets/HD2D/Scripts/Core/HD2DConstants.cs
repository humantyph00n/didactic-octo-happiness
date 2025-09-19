using UnityEngine;

namespace HD2D.Core
{
    /// <summary>
    /// Global constants and configuration for the HD-2D rendering system
    /// </summary>
    public static class HD2DConstants
    {
        // Rendering Layers
        public const string LAYER_ENVIRONMENT = "HD2D_Environment";
        public const string LAYER_SPRITES = "HD2D_Sprites";
        public const string LAYER_EFFECTS = "HD2D_Effects";
        public const string LAYER_UI = "HD2D_UI";
        
        // Sorting Layers
        public const string SORTING_BACKGROUND = "Background";
        public const string SORTING_ENVIRONMENT = "Environment";
        public const string SORTING_CHARACTERS = "Characters";
        public const string SORTING_FOREGROUND = "Foreground";
        public const string SORTING_EFFECTS = "Effects";
        
        // Shader Properties
        public const string SHADER_MAIN_TEX = "_MainTex";
        public const string SHADER_COLOR = "_Color";
        public const string SHADER_OUTLINE_WIDTH = "_OutlineWidth";
        public const string SHADER_OUTLINE_COLOR = "_OutlineColor";
        public const string SHADER_EMISSION_MAP = "_EmissionMap";
        public const string SHADER_EMISSION_COLOR = "_EmissionColor";
        public const string SHADER_CUTOFF = "_Cutoff";
        public const string SHADER_PIXELS_PER_UNIT = "_PixelsPerUnit";
        
        // Default Values
        public const int DEFAULT_PIXELS_PER_UNIT = 16;
        public const float DEFAULT_OUTLINE_WIDTH = 1.0f;
        public const float DEFAULT_ALPHA_CUTOFF = 0.5f;
        
        // Performance
        public const int MAX_BATCH_SIZE = 1023;
        public const float DEFAULT_CULLING_DISTANCE = 50f;
    }
}