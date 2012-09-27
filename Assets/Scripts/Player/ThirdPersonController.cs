using UnityEngine;

[AddComponentMenu("Third Person Player/Third Person Controller")]
public class ThirdPersonController : MonoBehaviour
{
    
    public float WalkSpeed = 3.0f; // The speed when walking
    public float TrotSpeed = 4.0f; // after trotAfteerSeconds of walking we trot with trotSpeed
    public float RunSpeed = 6.0f; // when pressing "Fire3" button we start running

    public float InAirControlAcceleration = 3.0f;

    public float JumpHeight = 0.5f; // How high we jump when pressing jump and letting go immediately
    public float ExtraJumpHeight = 2.5f; // We add extraJumpHeight meters on top when holding the button down while jumping

    public float Gravity = 20.0f; // The gravity for the character
    public float ControlledDescentGravity = 2.0f; // The gravity in a controlled decent
    public float SpeedSmoothing = 1.0f;
    public float RotateSpeed = 500.0f;
    public float TrotAfterSeconds = 3.0f;

    public bool CanJump = true;
    public bool CanControlDescent = false;
    public bool CanWallJump = false;

    private const float JUMP_REPEAT_TIME = 0.5f;
    private const float WALL_JUMP_TIMEOUT = 0.15f;
    private const float JUMP_TIMEOUT = 0.15f;
    private float _groundedTimeout = 0.25f;

    //The camera doesn't start following the target immediately but waits for a split second to avoid too much waving around.
    private float _lockCameraTimer;

    private Vector3 _moveDirection = Vector3.zero; // The current move direction in x-z
    private float _verticalSpeed; // The current vertical speed
    private float _moveSpeed; // The current x-z move speed

    private CollisionFlags _collisionFlags;

    //Are we jumping? (Initated with jump button and not grounded yet)
    private bool _jumping;
    private bool _jumpingReachedApex;

    private bool _movingBack; //Are we moving backwards (This locks the camera to not do a 180 degree spin)
    private bool _isMoving; // Is the user pressing any keys?
    private float _walkTimeStart; // When did the user start walking (Used for going into trot after a while)
    private float _lastJumpButtonTime = -10.0f; // Last time the jump button was clicked down
    private float _lastJumpTime = -1.0f; // Last time we performed a jump
    private Vector3 _wallJumpContactNormal; // Average normal of the last touched geometry
    private float _wallJumpContactNormalHeight;

    private float _lastJumpStartHeight; // The height we jumped from (Used to determine for how long to apply extra power after jumping)
    private float _touchWallJumpTime = -1.0f; // When did we touch the wall the first time during this jump (Used for wall jumping)

    private Vector3 _inAirVelocity = Vector3.zero;

    private float _lastGroundedTime;

    private bool _slammed;

    private bool _isControllable = true;

    public void Awake()
    {
        _moveDirection = transform.TransformDirection(Vector3.forward);
    }

    // This next function responds to the "HidePlayer" message by hiding the player. 
    // The message is also 'replied to' by identically-named functions in the collision-handling scripts.
    // - Used by the LevelStatus script when the level completed animation is triggered.
    public void HidePlayer()
    {
        //Stop rendering the player
        GameObject.Find("rootJoint").GetComponent<SkinnedMeshRenderer>().enabled = false;
        _isControllable = false;
    }

    public void ShowPlayer()
    {
        //Start rendering the player
        GameObject.Find("rootJoint").GetComponent<SkinnedMeshRenderer>().enabled = true;
        _isControllable = true;
    }

    private void UpdateSmoothedMovementDirection()
    {
        var cameraTransform = Camera.main.transform;
        bool grounded = IsGrounded();

        // Forward vector relative to the camera along the x-z plane
        var forward = cameraTransform.TransformDirection(Vector3.forward);
        forward.y = 0;
        forward = forward.normalized;

        // Right vector relative to the camera
        // Always orthogonal to the forward vector
        var right = new Vector3(forward.z, 0, -forward.x);

        var v = Input.GetAxisRaw("Vertical");
        var h = Input.GetAxisRaw("Horizontal");

        // Are we moving backwards or looking backwards
        _movingBack = v < -0.2;

        var wasMoving = _isMoving;
        _isMoving = Mathf.Abs(h) > 0.1 || Mathf.Abs(v) > 0.1;

        // Target direction relative to the camera
        var targetDirection = h*right + v*forward;

        // Grounded Controls
        if (grounded)
        {
            // Lock camera for short period when transitioning moving & standing still
            _lockCameraTimer += Time.deltaTime;
            if (_isMoving != wasMoving)
                _lockCameraTimer = 0.0f;

            // We store speed and direction seperately, 
            // so that when the character stands still we still have a valid forward direction
            // moveDirection is always normalized, and we only update it if there is user input
            if (targetDirection != Vector3.zero)
            {
                // If we are really slow, just snap to the target direction
                if (_moveSpeed < WalkSpeed * 0.9f && grounded)
                {
                    _moveDirection = targetDirection.normalized;
                }
                // Otherwise smoothly turn towards it
                else
                {
                    _moveDirection = Vector3.RotateTowards(_moveDirection, targetDirection,
                                                           RotateSpeed*Mathf.Deg2Rad*Time.deltaTime, 1000);
                    _moveDirection = _moveDirection.normalized;
                }
            }

            // Smooth the speed based on the current target direction
            var curSmooth = SpeedSmoothing*Time.deltaTime;

            // Choose target speed
            // We want to support analog input but make sure you cant walk faster diagonally then just forward or sideways
            var targetSpeed = Mathf.Min(targetDirection.magnitude, 1.0f);

            // Pick speed modifier
            if (Input.GetButton("Fire3"))
            {
                targetSpeed *= RunSpeed;
            }
            else if (Time.time - TrotAfterSeconds > _walkTimeStart)
            {
                targetSpeed *= TrotSpeed;
            }
            else
            {
                targetSpeed *= WalkSpeed;
            }

            _moveSpeed = Mathf.Lerp(_moveSpeed, targetSpeed, curSmooth);

            // Reset walk time start when we slow down
            if (_moveSpeed < WalkSpeed * 0.3)
                _walkTimeStart = Time.time;
        }
        // In air controls
        else
        {
            // Lock camera while in air
            if (_jumping)
                _lockCameraTimer = 0.0f;

            if (_isMoving)
                _inAirVelocity += targetDirection.normalized*Time.deltaTime*InAirControlAcceleration;
        }
    }

    private void ApplyWallJump()
    {
        // We must actually jump against a wall for this to work
        if (!_jumping)
            return;

        // Store when we first touched a wall during this jump
        if (_collisionFlags == CollisionFlags.Sides)
        {
            _touchWallJumpTime = Time.time;
        }

        // The user can trigger a wall jump by hitting the button shortly before or shortly after hitting the wall the first time
        var mayJump = _lastJumpButtonTime > _touchWallJumpTime - WALL_JUMP_TIMEOUT &&
                      _lastJumpButtonTime < _touchWallJumpTime + WALL_JUMP_TIMEOUT;
        if (!mayJump)
            return;

        // Prevent jumping too fast after each other
        if (_lastJumpTime + JUMP_REPEAT_TIME > Time.time)
            return;

        if (Mathf.Abs(_wallJumpContactNormal.y) < 0.2)
        {
            _wallJumpContactNormal.y = 0;
            _moveDirection = _wallJumpContactNormal.normalized;
            // Wall jump gives us at least trotspeed
            _moveSpeed = Mathf.Clamp(_moveSpeed*1.5f, TrotSpeed, RunSpeed);
        }
        else
        {
            _moveSpeed = 0;
        }

        _verticalSpeed = CalculateJumpVerticalSpeed(JumpHeight);
        DidJump();
        SendMessage("DidWallJump", null, SendMessageOptions.DontRequireReceiver);
    }

    private void ApplyJumping()
    {
        // Prevent jumping too fast after each other
        if (_lastJumpTime + JUMP_REPEAT_TIME > Time.time)
            return;

        if (IsGrounded())
        {
            // Jump
            // - Only when pressing the button down
            // - With a timeout so you can press the button slightly before landing
            if (CanJump && Time.time < _lastJumpButtonTime + JUMP_TIMEOUT)
            {
                _verticalSpeed = CalculateJumpVerticalSpeed(JumpHeight);
                SendMessage("DidJump", SendMessageOptions.DontRequireReceiver);
            }
        }
    }

    private void ApplyGravity()
    {
        if (_isControllable) // Don't move player at all if not controllable
        {
            // Apply gravity
            var jumpButton = Input.GetButton("Jump");

            // When falling down we use controlledDescentGravity (only when holding down jump)
            var controlledDescent = CanControlDescent && _verticalSpeed < 0.0 && jumpButton && _jumping;

            // When we reach the apex of the jump we sound out a message
            if (_jumping && !_jumpingReachedApex && _verticalSpeed <= 0.0)
            {
                _jumpingReachedApex = true;
                SendMessage("DidJumpReachApex", SendMessageOptions.DontRequireReceiver);
            }

            // When jumping up we don't apply gravity for some time when the user is holding jump button
            // This gives more control over jump height by pressing the button longer
            var extraPowerJump = IsJumping() && _verticalSpeed > 0.0 && jumpButton &&
                                 transform.position.y < _lastJumpStartHeight + ExtraJumpHeight;

            if (controlledDescent)
                _verticalSpeed -= ControlledDescentGravity * Time.deltaTime;
            else if (extraPowerJump)
                return;
            else if (IsGrounded())
                _verticalSpeed = 0.0f;
            else
                _verticalSpeed -= Gravity*Time.deltaTime;
        }
    }

    private float CalculateJumpVerticalSpeed(float targetJumpHeight)
    {
        // From the jump height and gravity we deduce the upwards speed
        // for the character to reach at the apex.
        return Mathf.Sqrt(2*targetJumpHeight*Gravity);
    }

    private void DidJump()
    {
        _jumping = true;
        _jumpingReachedApex = false;
        _lastJumpTime = Time.time;
        _lastJumpStartHeight = transform.position.y;
        _touchWallJumpTime = -1;
        _lastJumpButtonTime = -10;
    }

    public void Update()
    {
        if (!_isControllable)
        {
            // kill all inputs if not controllable
            Input.ResetInputAxes();
        }

        if (Input.GetButtonDown("Jump"))
        {
            _lastJumpButtonTime = Time.time;
        }

        UpdateSmoothedMovementDirection();

        // Apply gravity
        // - extra power jump modifies gravity
        // - controlledDescent mode modifies gravity
        ApplyGravity();

        // Perform a wall jump logic
        // - Make sure we are jumping against wall etc.
        // - Then apply jump in the right direction
        if (CanWallJump)
            ApplyWallJump();

        // Apply jumping logic
        ApplyJumping();

        // Calculate actual motion
        var movement = _moveDirection*_moveSpeed + new Vector3(0, _verticalSpeed, 0) + _inAirVelocity;
        movement *= Time.deltaTime;

        // Move the controller
        var controller = GetComponent<CharacterController>();
        _wallJumpContactNormal = Vector3.zero;
        _collisionFlags = controller.Move(movement);

        // Set rotation to the move direction
        if (IsGrounded())
        {
            if (_slammed) // we got knocked over by an enemy. We need to reset some stuff
            {
                _slammed = false;
                controller.height = 2;
                transform.
                transform.position = new Vector3(0,0.75f,0);
            }

            transform.rotation = Quaternion.LookRotation(_moveDirection);
        }
        else
        {
            if (!_slammed)
            {
                var xzMove = movement;
                xzMove.y = 0;
                if (xzMove.sqrMagnitude > 0.001)
                {
                    transform.rotation = Quaternion.LookRotation(xzMove);
                }
            }
        }

        // We are in jump mode but just became grounded
        if (IsGrounded())
        {
            _lastGroundedTime = Time.time;
            _inAirVelocity = Vector3.zero;
            if (_jumping)
            {
                _jumping = false;
                SendMessage("DidLand", SendMessageOptions.DontRequireReceiver);
            }
        }
    }

    public void OnControllerColliderHit(ControllerColliderHit hit)
    {
        // Debug.DrawRay(hit.point, hit.normal);
        if (hit.moveDirection.y > 0.01)
            return;
        _wallJumpContactNormal = hit.normal;
    }

    public float GetSpeed()
    {
        return _moveSpeed;
    }

    public bool IsJumping()
    {
        return _jumping && !_slammed;
    }

    public bool IsGrounded()
    {
        return (_collisionFlags & CollisionFlags.CollidedBelow) != 0;
    }

    public void SuperJump(float height)
    {
        _verticalSpeed = CalculateJumpVerticalSpeed(height);
        _collisionFlags = CollisionFlags.None;
        SendMessage("DidJump", SendMessageOptions.DontRequireReceiver);
    }

    public void SuperJump(float height, Vector3 jumpVelocity)
    {
        _verticalSpeed = CalculateJumpVerticalSpeed(height);
        _inAirVelocity = jumpVelocity;

        _collisionFlags = CollisionFlags.None;
        SendMessage("DidJump", SendMessageOptions.DontRequireReceiver);
    }

    public void Slam(Vector3 direction)
    {
        _verticalSpeed = CalculateJumpVerticalSpeed(1);
        _inAirVelocity = direction*6;
        direction.y = 0.6f;

        Quaternion.LookRotation(-direction);
        var controller = GetComponent<CharacterController>();
        controller.height = 0.5f;
        _slammed = true;
        _collisionFlags = CollisionFlags.None;
        SendMessage("DidJump", SendMessageOptions.DontRequireReceiver);
    }

    public Vector3 GetDirection()
    {
        return _moveDirection;
    }

    public bool IsMovingBackwards()
    {
        return _movingBack;
    }

    public float GetLockCameraTimer()
    {
        return _lockCameraTimer;
    }

    public bool IsMoving()
    {
        return Mathf.Abs(Input.GetAxisRaw("Vertical")) + Mathf.Abs(Input.GetAxisRaw("Horizontal")) > 0.5f;
    }

    public bool HasJumpReachedApex()
    {
        return _jumpingReachedApex;
    }

    public bool IsGroundedWithTimeout()
    {
        return _lastGroundedTime + _groundedTimeout > Time.time;
    }

    public bool IsControlledDescent()
    {
        //TODO: This is something maybe we want if the character has a leaf on one type of level?
        // When falling down we use controlledDescentGravity (only when holding down jump)
        var jumpButton = Input.GetButton("Jump");
        return CanControlDescent && _verticalSpeed < 0.0f && jumpButton && _jumping;
    }

    public void Reset()
    {
        gameObject.tag = "Player";
    }
}
