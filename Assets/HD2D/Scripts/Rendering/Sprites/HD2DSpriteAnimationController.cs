using UnityEngine;
using System;
using System.Collections.Generic;
using HD2D.Core;

namespace HD2D.Rendering.Sprites
{
    /// <summary>
    /// Handles sprite animation playback for HD2D sprites
    /// </summary>
    public class HD2DSpriteAnimationController : MonoBehaviour
    {
        [Header("Animation Settings")]
        [SerializeField] private List<SpriteAnimation> animations = new List<SpriteAnimation>();
        [SerializeField] private string defaultAnimation = "Idle";
        [SerializeField] private bool playOnStart = true;
        [SerializeField] private bool autoLoop = true;
        
        [Header("Playback")]
        [SerializeField] private float playbackSpeed = 1.0f;
        [SerializeField] private bool randomStartFrame = false;
        
        // Components
        private HD2DSpriteRenderer spriteRenderer;
        
        // Animation state
        private SpriteAnimation currentAnimation;
        private int currentFrameIndex;
        private float frameTimer;
        private bool isPlaying;
        private bool isPaused;
        
        // Events
        public event Action<string> OnAnimationStart;
        public event Action<string> OnAnimationComplete;
        public event Action<string, int> OnFrameChanged;
        
        /// <summary>
        /// Represents a single sprite animation sequence
        /// </summary>
        [Serializable]
        public class SpriteAnimation
        {
            public string name;
            public List<Sprite> frames = new List<Sprite>();
            public float frameRate = 12f;
            public bool loop = true;
            public AnimationTransition[] transitions;
            
            [Serializable]
            public class AnimationTransition
            {
                public string toAnimation;
                public TransitionCondition condition;
                public float transitionTime = 0.1f;
                
                public enum TransitionCondition
                {
                    OnComplete,
                    Immediate,
                    OnTrigger
                }
            }
            
            public float FrameDuration => 1f / frameRate;
        }
        
        #region Properties
        
        public bool IsPlaying => isPlaying && !isPaused;
        
        public string CurrentAnimationName => currentAnimation?.name ?? "";
        
        public int CurrentFrame => currentFrameIndex;
        
        public float PlaybackSpeed
        {
            get => playbackSpeed;
            set => playbackSpeed = Mathf.Max(0, value);
        }
        
        #endregion
        
        #region Unity Lifecycle
        
        void Awake()
        {
            spriteRenderer = GetComponent<HD2DSpriteRenderer>();
            if (spriteRenderer == null)
            {
                Debug.LogError("HD2DSpriteAnimationController requires HD2DSpriteRenderer component!");
            }
        }
        
        void Start()
        {
            if (playOnStart && !string.IsNullOrEmpty(defaultAnimation))
            {
                Play(defaultAnimation);
            }
        }
        
        void Update()
        {
            if (isPlaying && !isPaused && currentAnimation != null)
            {
                UpdateAnimation();
            }
        }
        
        void OnValidate()
        {
            // Ensure frame rates are positive
            foreach (var anim in animations)
            {
                anim.frameRate = Mathf.Max(0.1f, anim.frameRate);
            }
        }
        
        #endregion
        
        #region Animation Playback
        
        /// <summary>
        /// Play an animation by name
        /// </summary>
        public void Play(string animationName, bool forceRestart = false)
        {
            var animation = GetAnimation(animationName);
            if (animation == null)
            {
                Debug.LogWarning($"Animation '{animationName}' not found!");
                return;
            }
            
            // Don't restart if already playing the same animation (unless forced)
            if (currentAnimation == animation && isPlaying && !forceRestart)
            {
                return;
            }
            
            currentAnimation = animation;
            isPlaying = true;
            isPaused = false;
            
            // Set starting frame
            if (randomStartFrame && animation.frames.Count > 0)
            {
                currentFrameIndex = UnityEngine.Random.Range(0, animation.frames.Count);
            }
            else
            {
                currentFrameIndex = 0;
            }
            
            frameTimer = 0f;
            
            // Set initial frame
            UpdateSpriteFrame();
            
            // Fire event
            OnAnimationStart?.Invoke(animationName);
        }
        
        /// <summary>
        /// Play an animation clip directly
        /// </summary>
        public void PlayAnimation(AnimationClip clip)
        {
            // Convert Unity AnimationClip to sprite animation if needed
            // This is a placeholder for integration with Unity's animation system
            Debug.Log($"Playing animation clip: {clip.name}");
        }
        
        /// <summary>
        /// Stop the current animation
        /// </summary>
        public void Stop()
        {
            isPlaying = false;
            isPaused = false;
            currentAnimation = null;
            currentFrameIndex = 0;
            frameTimer = 0f;
        }
        
        /// <summary>
        /// Pause the current animation
        /// </summary>
        public void Pause()
        {
            isPaused = true;
        }
        
        /// <summary>
        /// Resume the current animation
        /// </summary>
        public void Resume()
        {
            isPaused = false;
        }
        
        /// <summary>
        /// Set a specific frame of the current animation
        /// </summary>
        public void SetFrame(int frameIndex)
        {
            if (currentAnimation != null && frameIndex >= 0 && frameIndex < currentAnimation.frames.Count)
            {
                currentFrameIndex = frameIndex;
                UpdateSpriteFrame();
            }
        }
        
        #endregion
        
        #region Animation Management
        
        /// <summary>
        /// Add a new animation
        /// </summary>
        public void AddAnimation(SpriteAnimation animation)
        {
            if (animation != null && !string.IsNullOrEmpty(animation.name))
            {
                // Check if animation with same name exists
                var existing = GetAnimation(animation.name);
                if (existing != null)
                {
                    animations.Remove(existing);
                }
                
                animations.Add(animation);
            }
        }
        
        /// <summary>
        /// Remove an animation by name
        /// </summary>
        public void RemoveAnimation(string animationName)
        {
            var animation = GetAnimation(animationName);
            if (animation != null)
            {
                animations.Remove(animation);
            }
        }
        
        /// <summary>
        /// Get an animation by name
        /// </summary>
        public SpriteAnimation GetAnimation(string animationName)
        {
            return animations.Find(a => a.name == animationName);
        }
        
        /// <summary>
        /// Check if an animation exists
        /// </summary>
        public bool HasAnimation(string animationName)
        {
            return GetAnimation(animationName) != null;
        }
        
        /// <summary>
        /// Get all animation names
        /// </summary>
        public List<string> GetAnimationNames()
        {
            List<string> names = new List<string>();
            foreach (var anim in animations)
            {
                names.Add(anim.name);
            }
            return names;
        }
        
        #endregion
        
        #region Private Methods
        
        private void UpdateAnimation()
        {
            if (currentAnimation == null || currentAnimation.frames.Count == 0)
                return;
            
            // Update frame timer
            frameTimer += Time.deltaTime * playbackSpeed;
            
            // Check if we need to advance to next frame
            if (frameTimer >= currentAnimation.FrameDuration)
            {
                frameTimer -= currentAnimation.FrameDuration;
                AdvanceFrame();
            }
        }
        
        private void AdvanceFrame()
        {
            currentFrameIndex++;
            
            // Check if animation has completed
            if (currentFrameIndex >= currentAnimation.frames.Count)
            {
                if (currentAnimation.loop || autoLoop)
                {
                    // Loop back to start
                    currentFrameIndex = 0;
                }
                else
                {
                    // Animation complete
                    currentFrameIndex = currentAnimation.frames.Count - 1;
                    isPlaying = false;
                    
                    OnAnimationComplete?.Invoke(currentAnimation.name);
                    
                    // Check for transitions
                    CheckTransitions();
                    return;
                }
            }
            
            UpdateSpriteFrame();
        }
        
        private void UpdateSpriteFrame()
        {
            if (spriteRenderer != null && currentAnimation != null && 
                currentFrameIndex >= 0 && currentFrameIndex < currentAnimation.frames.Count)
            {
                Sprite frame = currentAnimation.frames[currentFrameIndex];
                spriteRenderer.Sprite = frame;
                
                OnFrameChanged?.Invoke(currentAnimation.name, currentFrameIndex);
            }
        }
        
        private void CheckTransitions()
        {
            if (currentAnimation == null || currentAnimation.transitions == null)
                return;
            
            foreach (var transition in currentAnimation.transitions)
            {
                if (transition.condition == SpriteAnimation.AnimationTransition.TransitionCondition.OnComplete)
                {
                    // Transition to next animation
                    Play(transition.toAnimation);
                    break;
                }
            }
        }
        
        #endregion
        
        #region Animation Builder
        
        /// <summary>
        /// Helper class to build animations from sprite sheets
        /// </summary>
        public static class AnimationBuilder
        {
            /// <summary>
            /// Create animation from a sprite sheet
            /// </summary>
            public static SpriteAnimation CreateFromSpriteSheet(
                string name, 
                Texture2D spriteSheet, 
                int frameCount, 
                int framesPerRow,
                float frameRate = 12f,
                bool loop = true)
            {
                SpriteAnimation animation = new SpriteAnimation
                {
                    name = name,
                    frameRate = frameRate,
                    loop = loop
                };
                
                int frameWidth = spriteSheet.width / framesPerRow;
                int frameHeight = spriteSheet.height / ((frameCount + framesPerRow - 1) / framesPerRow);
                
                for (int i = 0; i < frameCount; i++)
                {
                    int x = (i % framesPerRow) * frameWidth;
                    int y = spriteSheet.height - ((i / framesPerRow + 1) * frameHeight);
                    
                    Rect spriteRect = new Rect(x, y, frameWidth, frameHeight);
                    Vector2 pivot = new Vector2(0.5f, 0.5f);
                    
                    Sprite frame = Sprite.Create(
                        spriteSheet, 
                        spriteRect, 
                        pivot, 
                        HD2DConstants.DEFAULT_PIXELS_PER_UNIT
                    );
                    
                    frame.name = $"{name}_Frame_{i}";
                    animation.frames.Add(frame);
                }
                
                return animation;
            }
            
            /// <summary>
            /// Create animation from individual sprites
            /// </summary>
            public static SpriteAnimation CreateFromSprites(
                string name,
                Sprite[] sprites,
                float frameRate = 12f,
                bool loop = true)
            {
                SpriteAnimation animation = new SpriteAnimation
                {
                    name = name,
                    frameRate = frameRate,
                    loop = loop
                };
                
                animation.frames.AddRange(sprites);
                
                return animation;
            }
        }
        
        #endregion
    }
}