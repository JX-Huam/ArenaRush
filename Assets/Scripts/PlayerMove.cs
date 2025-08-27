using System;
using System.Collections;
using Unity.Collections;
using Unity.Netcode;
using Unity.Services.Lobbies.Models;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class PlayerMove : NetworkBehaviour
{
    public static PlayerMove Instance;

    public NetworkVariable<FixedString64Bytes> avatarUrl = new NetworkVariable<FixedString64Bytes>(
    string.Empty,
    NetworkVariableReadPermission.Everyone,
    NetworkVariableWritePermission.Owner);

    public NetworkVariable<FixedString64Bytes> playerName = new NetworkVariable<FixedString64Bytes>(
    "Unknown Player",
    NetworkVariableReadPermission.Everyone,
    NetworkVariableWritePermission.Owner);

    public NetworkVariable<float> speedMultiplier = new NetworkVariable<float>(
    1.0f,
    NetworkVariableReadPermission.Everyone,
    NetworkVariableWritePermission.Server);

    public NetworkVariable<int> playerLives = new NetworkVariable<int>(
    3, // Start with 3 lives
    NetworkVariableReadPermission.Everyone,
    NetworkVariableWritePermission.Server);

    public NetworkVariable<bool> canMove = new NetworkVariable<bool>(
    false,
    NetworkVariableReadPermission.Everyone,
    NetworkVariableWritePermission.Server);

    private bool isInvulnerable = false;
    [SerializeField] private float invulnerabilityDuration = 2f;

    private Animator avatarAnimator;
    private Camera cameraView;
    [SerializeField] private float cameraFollowSpeed = 3f; // Add this field to your class
    private Image effectBorder;


    [Header("Movement Settings")]
    private float moveSpeed = 6f;
    private float speedIncreaseAmount = 0.5f;
    private float speedIncreaseInterval = 10f;
    private float speedIncreaseTimer = 0f;
    [SerializeField] private float leftRightSpace = 1.5f;
    //public bool canMove = false;
    private bool hasCrashed = false;
    private bool isJumping = false;
    [SerializeField] private float jumpHeight = 1f;
    [SerializeField] private float jumpDuration = 0.5f;


    [Header("Input Settings")]
    [SerializeField] private float swipeVerticalThreshold = 100f; // Specific threshold for vertical swipes
    [SerializeField] private float swipeHorizontalThreshold = 50f; // Specific threshold for horizontal swipes
    private PlayerInput playerInput;
    private Vector2 touchStartPos;
    private Vector2 touchEndPos;

    /// Events
    public event EventHandler OnPlayerObjectSpawned;
    public event EventHandler OnPlayerCrashedToObstacle;

    private void Awake() 
    {
        playerInput = GetComponent<PlayerInput>();
        effectBorder = GameObject.Find("EffectBorder").GetComponent<Image>();

        RPMAvatarManager.Instance.OnAvatarLoaded += OnAvatarLoaded;
    }

    private void Start()
    {   
        if (IsLocalPlayer)
        {
            cameraView = GameObject.FindGameObjectWithTag("MainCamera").GetComponent<Camera>();
        }
    }

    private void Update()
    {
        if (canMove.Value)
        {
            MoveForward();
            UpdateSpeedIncreaseTimer();
        }
        
        // Update camera position for local player only
        if (IsLocalPlayer && cameraView != null && canMove.Value)
        {
            UpdateCameraPosition();
        }
    }

    public override void OnDestroy()
    {
        base.OnDestroy();
        RPMAvatarManager.Instance.OnAvatarLoaded -= OnAvatarLoaded;
    }

#region Movement

    private void MoveForward()
    {
        float currentSpeed = moveSpeed * speedMultiplier.Value;
        transform.Translate(Vector3.forward * Time.deltaTime * currentSpeed, Space.World);
    }

    private void UpdateSpeedIncreaseTimer()
    {
        speedIncreaseTimer += Time.deltaTime;
        
        if (speedIncreaseTimer >= speedIncreaseInterval && moveSpeed < 12f)
        {
            // Reset timer
            speedIncreaseTimer = 0f;
            
            // Increase the original speed value (not the multiplier)
            moveSpeed += speedIncreaseAmount;
            
            // Log the speed increase for debugging
            Debug.Log($"Player speed increased to {moveSpeed}");
        }
    }

    private void UpdateCameraPosition()
    {
        // Calculate a dampened x-position for the camera (40% of the player's x-position)
        float targetX = transform.position.x * 0.6f;
        
        // Get current camera position
        Vector3 currentCamPos = cameraView.transform.position;
        
        // Smoothly interpolate the x position
        float newX = Mathf.Lerp(currentCamPos.x, targetX, Time.deltaTime * cameraFollowSpeed);
        
        Vector3 newCameraPosition = new Vector3(
            newX,
            currentCamPos.y,
            transform.position.z - 5f);
            
        cameraView.transform.position = newCameraPosition;
    }

    // Server RPC for movement state with network time synchronization
    [Rpc(SendTo.Server)]
    private void SetCanMoveServerRpc(bool value, float delayInSeconds = 0)
    {
        // Calculate when to actually start moving
        float startTime = NetworkManager.ServerTime.TimeAsFloat + delayInSeconds;
        
        // Tell all clients when to start
        SetCanMoveClientRpc(value, startTime);
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void SetCanMoveClientRpc(bool value, float startTime)
    {
        // Start a coroutine to apply the movement at the synchronized time
        StartCoroutine(ApplyMovementAtTime(value, startTime));
    }

    private IEnumerator ApplyMovementAtTime(bool shouldMove, float targetTime)
    {
        // Wait until the network time reaches the target
        while (NetworkManager.LocalTime.TimeAsFloat < targetTime)
        {
            yield return null;
        }
        
        // Apply movement for everyone at the same time
        canMove.Value = shouldMove;
        
        if (shouldMove)
        {
            Debug.Log($"Player {OwnerClientId} started moving at network time: {NetworkManager.LocalTime.TimeAsFloat}");
            PlayAnimation("Running");
        }
        else
        {
            PlayAnimation("Stumble Backwards");
        }
    }

    private void MoveHorizontal(float direction)
    {
        float newXPosition = transform.position.x + (leftRightSpace * direction);

        if (newXPosition >= -1.5f && newXPosition <= 1.5f)
        {
            Vector3 movement = new Vector3(leftRightSpace * direction, 0f, 0f);
            transform.position += movement;
        }

        MoveHorizontalServerRpc(direction);
    }

    [Rpc(SendTo.Server)]
    private void MoveHorizontalServerRpc(float direction)
    {   
        MoveHorizontalClientRpc(direction);
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void MoveHorizontalClientRpc(float direction)
    {
        if(IsOwner) return;

        float newXPosition = transform.position.x + (leftRightSpace * direction);

        if (newXPosition >= -1.5f && newXPosition <= 1.5f)
        {
            Vector3 movement = new Vector3(leftRightSpace * direction, 0f, 0f);
            transform.position += movement;
        }
    }
    
    [Rpc(SendTo.Server)]
    private void JumpServerRpc()
    {
        JumpClientRpc(NetworkManager.Singleton.LocalClientId);
    }
    
    [Rpc(SendTo.ClientsAndHost)]
    private void JumpClientRpc(ulong initiatorId)
    {
        if (IsOwner && OwnerClientId == initiatorId) return;
        
        if (!isJumping)
        {
            StartCoroutine(JumpSequence(false));
        }
    }

    IEnumerator JumpSequence(bool isInitiator)
    {
        if (isJumping) yield break;
        
        isJumping = true;
        
        // Play jump animation
        PlayAnimation("Jump");
        
        // Store starting y position (height)
        float startY = transform.position.y;
        float peakY = startY + jumpHeight;
        
        // Going up phase
        float timeElapsed = 0;
        float upDuration = jumpDuration * 0.4f; // 40% of time going up
        
        while (timeElapsed < upDuration)
        {
            // Calculate progress (0 to 1)
            float t = timeElapsed / upDuration;
            // Use smoothstep for more natural motion
            float smoothT = t * t * (3f - 2f * t);
            
            // Only modify Y position, let regular movement handle X and Z
            Vector3 newPosition = transform.position;
            newPosition.y = Mathf.Lerp(startY, peakY, smoothT);
            transform.position = newPosition;
            
            timeElapsed += Time.deltaTime;
            yield return null;
        }
        
        // Small pause at the top (still moving forward)
        float pauseTime = 0;
        float totalPauseTime = jumpDuration * 0.1f;
        while (pauseTime < totalPauseTime)
        {
            // Keep Y at peak height
            Vector3 newPosition = transform.position;
            newPosition.y = peakY;
            transform.position = newPosition;
            
            pauseTime += Time.deltaTime;
            yield return null;
        }
        
        // Going down phase
        timeElapsed = 0;
        float downDuration = jumpDuration * 0.5f; // 50% of time going down
        
        while (timeElapsed < downDuration)
        {
            // Calculate progress (0 to 1)
            float t = timeElapsed / downDuration;
            // Use smoothstep for more natural motion
            float smoothT = t * t * (3f - 2f * t);
            
            // Only modify Y position
            Vector3 newPosition = transform.position;
            newPosition.y = Mathf.Lerp(peakY, startY, smoothT);
            transform.position = newPosition;
            
            timeElapsed += Time.deltaTime;
            yield return null;
        }
        
        // Ensure we return to the correct height
        Vector3 finalPosition = transform.position;
        finalPosition.y = startY;
        transform.position = finalPosition;
        
        isJumping = false;
        
        // Return to running animation if still moving
        if (canMove.Value)
        {
            PlayAnimation("Running");
        }
    }

    public void OnMoveLeft(InputAction.CallbackContext context)
    {
        if (!IsOwner) return;

        if (context.performed && canMove.Value)
        {
            Debug.Log("Move Left Attempt - IsOwner: " + IsOwner);
            
            if (transform.position.x > -1.5f)
            {  
                MoveHorizontal(-1f);
            }
        }
    }

    public void OnMoveRight(InputAction.CallbackContext context)
    {
        if (!IsOwner) return;
        
        if (context.performed && canMove.Value)
        {
            Debug.Log("Move Right Attempt - IsOwner: " + IsOwner);
            
            if (transform.position.x < 1.5f)
            {
                MoveHorizontal(1f);
            }
        }
    }

    public void OnJump(InputAction.CallbackContext context)
    {
        if (!IsOwner) return;

        if (context.performed && canMove.Value && !isJumping)
        {
            Debug.Log("Jump Attempt - IsOwner: " + IsOwner);
            
            StartCoroutine(JumpSequence(true)); 
            
            JumpServerRpc();
        }
    }

    public void OnTouchContact(InputAction.CallbackContext context)
    {
        if (!IsOwner) return;
        
        if (context.started)
        {
            touchStartPos = playerInput.actions["TouchPosition"].ReadValue<Vector2>();
            Debug.Log($"Touch Start: {touchStartPos}");
        }
        else if (context.canceled)
        {
            touchEndPos = playerInput.actions["TouchPosition"].ReadValue<Vector2>();
            Debug.Log($"Touch End: {touchEndPos}");
            DetectSwipe();
        }
    }

    private void DetectSwipe()
    {
        if (!IsOwner || !canMove.Value) return;

        // Calculate swipe delta
        Vector2 swipeDelta = touchEndPos - touchStartPos;
        float verticalSwipe = swipeDelta.y;
        float horizontalSwipe = swipeDelta.x;

        // Log swipe values for debugging
        Debug.Log($"Swipe Delta: {swipeDelta}, Vertical: {verticalSwipe}, Horizontal: {horizontalSwipe}");
        
        // Determine if this is primarily a vertical or horizontal swipe
        bool isVerticalSwipe = Mathf.Abs(verticalSwipe) > Mathf.Abs(horizontalSwipe) && 
                              Mathf.Abs(verticalSwipe) > swipeVerticalThreshold;
        
        bool isHorizontalSwipe = Mathf.Abs(horizontalSwipe) > swipeHorizontalThreshold &&
                                !isVerticalSwipe; // Only consider horizontal if not vertical

        // Handle vertical swipe (jump)
        if (isVerticalSwipe && verticalSwipe > 0 && !isJumping)
        {
            Debug.Log("Vertical Swipe UP Detected - JUMP");
            // Local player starts jump immediately for responsiveness
            StartCoroutine(JumpSequence(true));
            // Then tell server about the jump
            JumpServerRpc();
        }
        // Handle horizontal swipe (move left/right)
        else if (isHorizontalSwipe)
        {
            if (horizontalSwipe > 0) // Swipe Right
            {
                Debug.Log("Horizontal Swipe RIGHT Detected");
                if (transform.position.x < 1.5f)
                {
                    MoveHorizontal(1f);
                }
            }
            else // Swipe Left
            {
                Debug.Log("Horizontal Swipe LEFT Detected");
                if (transform.position.x > -1.5f)
                {
                    MoveHorizontal(-1f);
                }
            }
        }
        
        // Reset swipe positions
        touchStartPos = Vector2.zero;
        touchEndPos = Vector2.zero;
    }

    

#endregion
    
#region Animation
    private void PlayAnimation(string animationName)
    {
        // If animator isn't ready, queue the animation for when it becomes available
        if (avatarAnimator == null || !avatarAnimator.isActiveAndEnabled)
        {
            StartCoroutine(PlayAnimationWhenReady(animationName));
            return;
        }

        // First try to play the animation using the exact name provided
        avatarAnimator.Play(animationName);
        Debug.Log($"Playing animation: {animationName}");
    }

    private IEnumerator PlayAnimationWhenReady(string animationName)
    {
        Debug.Log($"Waiting for animator to be ready to play: {animationName}");
        
        // Wait until we have a valid animator (timeout after 10 seconds)
        float timeoutTime = Time.time + 10f;
        while ((avatarAnimator == null || !avatarAnimator.isActiveAndEnabled) && Time.time < timeoutTime)
        {
            // Try to find animator if we don't have one
            if (avatarAnimator == null)
            {
                avatarAnimator = GetComponentInChildren<Animator>();
            }
            
            yield return null;
        }
        
        // If we got a valid animator, play the animation
        if (avatarAnimator != null && avatarAnimator.isActiveAndEnabled)
        {
            avatarAnimator.Play(animationName);
            Debug.Log($"Playing delayed animation: {animationName}");
        }
        else
        {
            Debug.LogError("Timed out waiting for animator to be ready");
        }
    }

    private IEnumerator PlayAnimationSequence(string firstAnimation, string secondAnimation)
    {
        // If animator isn't ready, wait for it
        if (avatarAnimator == null || !avatarAnimator.isActiveAndEnabled)
        {
            yield return StartCoroutine(PlayAnimationWhenReady(firstAnimation));
        }
        else
        {
            avatarAnimator.Play(firstAnimation);
            Debug.Log($"Playing animation: {firstAnimation}");
        }
        
        // Get the current animation clip info
        if (avatarAnimator != null && avatarAnimator.isActiveAndEnabled)
        {
            AnimatorClipInfo[] clipInfo = avatarAnimator.GetCurrentAnimatorClipInfo(0);
            if (clipInfo.Length > 0)
            {
                // Wait for the animation to finish
                float clipLength = clipInfo[0].clip.length;
                yield return new WaitForSeconds(clipLength);
                
                // Play the second animation
                avatarAnimator.Play(secondAnimation);
                Debug.Log($"Playing animation: {secondAnimation}");
            }
            else
            {
                // Fallback if we can't get clip info - wait a short time then play the second animation
                yield return new WaitForSeconds(0.5f);
                avatarAnimator.Play(secondAnimation);
                Debug.Log($"Playing animation (fallback): {secondAnimation}");
            }
        }
    }

#endregion
    
#region Networking
    public override void OnNetworkSpawn()
    {
        if (IsOwner)
        {
            Instance = this;
            
            // Subscribe to crash events
            OnPlayerCrashedToObstacle += GameManager.Instance.OnPlayerCrashed;
            OnPlayerCrashedToObstacle += SoundEffectManager.Instance.OnPlayerCrashed;
            
            // Set the avatar URL network variable when we spawn
            string localAvatarUrl = PlayerPrefs.GetString("AvatarUrl", "https://models.readyplayer.me/67b417f62600df572bf39f64.glb");
            avatarUrl.Value = localAvatarUrl;
            
            Debug.Log($"Player {OwnerClientId} setting avatar URL: {localAvatarUrl}");
            
            string localPlayerName = PlayerPrefs.GetString("PlayerName", "Player");
            playerName.Value = localPlayerName;

            // Fire the spawned event
            StartCoroutine(DelayedPlayerObjectSpawned());
        }
    }

    public override void OnNetworkDespawn()
    {
        if (IsOwner)
        {
            OnPlayerCrashedToObstacle -= GameManager.Instance.OnPlayerCrashed;
            OnPlayerCrashedToObstacle -= SoundEffectManager.Instance.OnPlayerCrashed;
        }
    }

    private IEnumerator DelayedPlayerObjectSpawned()
    {
        yield return new WaitForSeconds(0.5f); // Ensure GameManager has subscribed
        OnPlayerObjectSpawned?.Invoke(this, EventArgs.Empty);
        Debug.Log("OnPlayerObjectSpawned event fired.");
    }

#endregion

#region Events
    private void OnAvatarLoaded(object sender, RPMAvatarManager.AvatarLoadedEventArgs e)
    {
        // Only process if this is our avatar - check if parent matches this object
        if (IsOwner && e.AvatarObject != null && e.AvatarObject.transform.parent == transform)
        {
            // Get the animator from the loaded avatar
            avatarAnimator = e.AvatarObject.GetComponentInChildren<Animator>();
            
            if (avatarAnimator != null)
            {
                Debug.Log($"Player {OwnerClientId}: Avatar loaded and animator reference updated");
                if (canMove.Value)
                {
                    avatarAnimator.Play("Running");
                }
            }
            else
            {
                Debug.LogWarning($"Player {OwnerClientId}: Failed to find animator in avatar");
            }
        }
    }

    public void TriggerOnPlayerCrashedToObstacle(ulong stunId)
    {
        if (IsOwner && !hasCrashed && !isInvulnerable)
        {
            Debug.Log($"Player hit obstacle! Lives remaining: {playerLives.Value - 1}");
            
            // Request server to deduct a life
            DeductLifeServerRpc(stunId);
        }
    }

    [Rpc(SendTo.Server)]
    private void DeductLifeServerRpc(ulong stunId)
    {
        if (playerLives.Value > 0)
        {
            playerLives.Value--;
            Debug.Log($"Server: Player {OwnerClientId} lost a life. Lives remaining: {playerLives.Value}");
            
            // Notify all clients about life loss
            OnLifeLostClientRpc();
            
            if (playerLives.Value <= 0)
            {
                // No lives left - trigger full crash
                hasCrashed = true;
                OnPlayerCrashed();
                OnPlayerCrashedToObstacle?.Invoke(this, EventArgs.Empty);
                Debug.Log($"Player {OwnerClientId} has no lives left - fully crashed!");
            }
            else
            {
                foreach (NetworkObject networkObj in NetworkManager.Singleton.ConnectedClients[stunId].OwnedObjects)
                {
                    PlayerMove playerMove = networkObj.GetComponent<PlayerMove>();
                    if (playerMove != null)
                    {
                        OnTemporaryStun(playerMove);
                    }
                }
                // Still has lives - just temporary stun
            }
        }
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void OnLifeLostClientRpc()
    {
        // Visual/audio feedback for losing a life
        if (IsOwner)
        {
            Debug.Log("💔 Life lost! Remaining: " + playerLives.Value);
            
            // Play hurt animation if available
            PlayAnimation("Stumble Backwards");
            
            // Start temporary invulnerability
            StartCoroutine(TemporaryInvulnerability());
        }
    }


    private void OnTemporaryStun(PlayerMove playerMove)
    {
        // Brief movement disable for impact effect
        StartCoroutine(TemporaryStun(playerMove));

    }
    
    private IEnumerator TemporaryInvulnerability()
    {
        isInvulnerable = true;
        
        // Visual feedback - flash the player
        StartCoroutine(FlashPlayer());
        
        yield return new WaitForSeconds(invulnerabilityDuration);
        
        isInvulnerable = false;
        Debug.Log("Invulnerability ended");
    }

    private IEnumerator FlashPlayer()
    {
        // Simple approach: Just disable/enable the entire avatar
        Transform avatarRoot = GetAvatarRoot();

        if (avatarRoot != null)
        {
            GameObject avatarGameObject = avatarRoot.gameObject;

            for (int i = 0; i < 6; i++) // Flash 3 times
            {
                avatarGameObject.SetActive(i % 2 != 0); // Alternate visibility
                yield return new WaitForSeconds(0.15f);
            }

            // Ensure avatar is visible at the end
            avatarGameObject.SetActive(true);
            PlayAnimation("Running"); // Reset to running animation
        }
        else
        {
            Debug.LogWarning("Avatar root not found for flashing");
        }
    }
    
    public Transform GetAvatarRoot()
    {
        // Look for child with multiple renderers (avatar structure)
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            Renderer[] renderers = child.GetComponentsInChildren<Renderer>();
            
            if (renderers.Length > 3) // Likely the avatar if it has multiple renderers
            {
                return child;
            }
        }
        
        return null;
    }

    private IEnumerator TemporaryStun(PlayerMove playerMove)
    {
        bool originalCanMove = playerMove.canMove.Value;
        playerMove.canMove.Value = false;
        Debug.Log($"Player {OwnerClientId} temporarily stunned!");
        yield return new WaitForSeconds(0.5f); // Half second stun

        if (!hasCrashed) // Only restore movement if not fully crashed
        {
            playerMove.canMove.Value = originalCanMove;
            if (playerMove.canMove.Value)
            {
                PlayAnimation("Running");
            }
        }
    }

    public void OnPlayerCrashed()
    {
        SetCanMoveServerRpc(false);
        NotifyServerPlayerCrashedRpc();
    }

    [Rpc(SendTo.Server)]
    private void NotifyServerPlayerCrashedRpc()
    {
        //GameManager.Instance.playerCrashedNumber.Value++;
        Debug.Log($"Number of crashed player in server {GameManager.Instance.playerCrashedNumber.Value}");
        // This allows the server to track which players have crashed
        Debug.Log($"Server notified that player {OwnerClientId} has crashed");
    }

    public void OnCountDownEnd(object sender, EventArgs e)
    {
        if (IsOwner)
        {
            SetCanMoveServerRpc(true, 0.5f);
        }
    }

    public void TriggerOnPowerUpCollected(PowerUp.PowerUpType powerUpType, ulong collectorId)
    {
        if (IsOwner)
        {
            switch (powerUpType)
            {
                case PowerUp.PowerUpType.SpeedUp:
                    SpeedUpServerRpc(collectorId);
                    break;
                case PowerUp.PowerUpType.SlowDown:
                    SlowDownServerRpc(collectorId);
                    break;
            }
        }
    }


    [Rpc(SendTo.Server)]
    private void SpeedUpServerRpc(ulong collectorId)
    {
        // The server controls the NetworkVariable - find the player object for the collector
        foreach (NetworkObject networkObj in NetworkManager.Singleton.ConnectedClients[collectorId].OwnedObjects)
        {
            PlayerMove playerMove = networkObj.GetComponent<PlayerMove>();
            if (playerMove != null)
            {
                // Set the speed multiplier for the collector
                playerMove.speedMultiplier.Value = 2.0f;
                
                // Start a server-side coroutine to reset speed after duration
                StartCoroutine(ResetSpeedAfterDelayServer(playerMove, 3f));
                break;
            }
        }
    
        // Visual effect RPC to clients
        SpeedUpVisualClientRpc(collectorId);
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void SpeedUpVisualClientRpc(ulong collectorId)
    {
        Debug.Log($"SpeedUpVisual: I am ClientID {OwnerClientId}, Collector is {collectorId}");
        
        // ONLY the collector should see the green border
        if (NetworkManager.Singleton.LocalClientId == collectorId)
        {
            Debug.Log($"Showing green border for local client {NetworkManager.Singleton.LocalClientId}");
            
            if (effectBorder != null)
            {
                effectBorder.enabled = true;
                effectBorder.color = Color.green;
                StartCoroutine(HideEffectBorderAfterDelay(3f));
            }
            else
            {
                Debug.LogWarning("Effect border is null!");
            }
        }
    }

    [Rpc(SendTo.Server)]
    private void SlowDownServerRpc(ulong collectorId)
    {
        // Apply slow down to everyone except the collector
        foreach (NetworkClient client in NetworkManager.Singleton.ConnectedClientsList)
        {
            if (client.ClientId != collectorId)
            {
                foreach (NetworkObject networkObj in client.OwnedObjects)
                {
                    PlayerMove playerMove = networkObj.GetComponent<PlayerMove>();
                    if (playerMove != null)
                    {
                        // Set the speed multiplier for non-collectors
                        playerMove.speedMultiplier.Value = 0.5f;
                        
                        // Start server-side coroutine to reset
                        StartCoroutine(ResetSpeedAfterDelayServer(playerMove, 3f));
                        break;
                    }
                }
            }
        }
        
        // Visual effect RPC to clients
        SlowDownVisualClientRpc(collectorId);
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void SlowDownVisualClientRpc(ulong collectorId)
    {
        Debug.Log($"SlowDownVisual: I am ClientID {OwnerClientId}, Collector is {collectorId}");
        
        // Everyone EXCEPT the collector should see the red border
        if (NetworkManager.Singleton.LocalClientId != collectorId)
        {
            Debug.Log($"Showing red border for local client {NetworkManager.Singleton.LocalClientId}");
            
            if (effectBorder != null)
            {
                effectBorder.enabled = true;
                effectBorder.color = Color.red;
                StartCoroutine(HideEffectBorderAfterDelay(3f));
            }
            else
            {
                Debug.LogWarning("Effect border is null!");
            }
        }
    }

    private IEnumerator ResetSpeedAfterDelayServer(PlayerMove playerMove, float duration)
    {
        yield return new WaitForSeconds(duration);
        playerMove.speedMultiplier.Value = 1.0f;
    }

    private IEnumerator ReenableColliderAfterDelay(Collider collider, float duration)
    {
        yield return new WaitForSeconds(duration);
        collider.enabled = true;
    }

    // Client-side coroutine to hide visual effect
    private IEnumerator HideEffectBorderAfterDelay(float duration)
    {
        yield return new WaitForSeconds(duration);
        if (effectBorder != null)
        {
            effectBorder.enabled = false;
        }
    }
    #endregion
}