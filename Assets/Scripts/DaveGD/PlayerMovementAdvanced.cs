using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovementAdvanced : MonoBehaviour
{
    [Header("Movement")]
    private float moveSpeed;
    public float walkSpeed;
    public float sprintSpeed;
    public float swingSpeed;

    public float dashSpeed;
    public float dashSpeedChangeFactor;

    public float slideSpeed;
    public float wallrunSpeed;
    public float climbSpeed;
    public float airMinSpeed;
    public float maxYSpeed;


    private float desiredMoveSpeed;
    private float lastDesiredMoveSpeed;
    private MovementState lastState;

    public float speedIncreaseMultiplier;
    public float slopeIncreaseMultiplier;

    public float groundDrag;

    [Header("Jumping")]
    public float jumpForce;
    public float jumpCooldown;
    public float airMultiplier;
    bool readyToJump;

    [Header("Crouching")]
    public float crouchSpeed;
    public float crouchYScale;
    private float startYScale;

    [Header("Keybinds")]
    public KeyCode jumpKey = KeyCode.Space;
    public KeyCode sprintKey = KeyCode.LeftShift;
    public KeyCode crouchKey = KeyCode.LeftControl;

    [Header("Ground Check")]
    public float playerHeight;
    public LayerMask whatIsGround;
    public bool grounded;

    [Header("Slope Handling")]
    public float maxSlopeAngle;
    private RaycastHit slopeHit;
    private bool exitingSlope;

    [Header("Camera Effects")]
    public ThirdPersonCam cam;
    public float grappleFov = 95f;

    [Header("References")]
    public Transform playerCollider;
    public PlayerInput playerInput;
    public Climbing climbingScript;
    public LedgeGrabbing ledgeGrabScript;
    public Grappling grappleScript;
    public DualHooks dualHooksScript;
    public WallRunning wallRunScript;
    public GameObject colliderStanding;
    public GameObject colliderCrouching;



    public Transform orientation;

    Vector3 moveDirection;

    Vector3 inputMovement;
    public Vector3 GetInputMovement()
    {
        return inputMovement;
    }

    public Rigidbody rb;

    public MovementState state;
    public enum MovementState
    {
        freeze,
        unlimited,
        grappling,
        swinging,
        walking,
        sprinting,
        wallrunning,
        climbing,
        crouching,
        dashing,
        sliding,
        air,
        ledge
    }

    public bool dashing;
    public bool sliding;
    public bool wallrunning;
    public bool climbing;
    public bool crouching;
    public bool hanging;

    public bool activeGrapple;
    public bool swinging;

    public bool freeze;
    public bool unlimited;

    public bool restricted;

    public float MoveSpeed { get => moveSpeed;}
    public float DesiredMoveSpeed { get => desiredMoveSpeed;}

    public float GetVelocity() { return rb.velocity.magnitude; }
    public Vector3 GetVelocityVector3() { return rb.velocity; }

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true;

        readyToJump = true;

        startYScale = transform.localScale.y;

        Debug.Log("Fix spamming the swing button giving tons of momentum when aiming up/down, use cooldown?");
        Debug.Log("Dash: Add a midair flip action that recharges your dash.");
    }

    private void Update()
    {
        // ground check
        grounded = Physics.Raycast(transform.position, Vector3.down, playerHeight * 0.5f + 0.2f, whatIsGround);

        MyInput();
        SpeedControl();
        StateHandler();

        // handle drag
        if(transform.parent)
        {
            rb.drag = 0;
        }
        else if ((grounded) && !activeGrapple)
            rb.drag = groundDrag;
        else
            rb.drag = 0;
    }

    private void FixedUpdate()
    {
        MovePlayer();
    }

    private void MyInput()
    {
        // when to jump
        if (playerInput.actions["Jump"].IsPressed() && readyToJump && grounded)
        {
            readyToJump = false;

            Jump();

            Invoke(nameof(ResetJump), jumpCooldown);
        }

        InputSprintLogic();

        // start crouch
        
        
        if (playerInput.actions["CrouchSlide"].WasPressedThisFrame() && !(activeGrapple || swinging || wallrunning))
        {
            crouching = true;
            colliderStanding.SetActive(false);
            colliderCrouching.SetActive(true);
            
            playerCollider.localScale = new Vector3(playerCollider.localScale.x, crouchYScale, playerCollider.localScale.z);
            //rb.AddForce(Vector3.down * 5f, ForceMode.Impulse);
        }

        // stop crouch
        if (playerInput.actions["CrouchSlide"].WasReleasedThisFrame())
        {
            crouching = false;
            colliderCrouching.SetActive(false);
            colliderStanding.SetActive(true);
            
            playerCollider.localScale = new Vector3(playerCollider.localScale.x, startYScale, playerCollider.localScale.z);
        }

       

    }

    

    public bool keepMomentum;

    private void StateHandler()
    {
        // Mode - Freeze
        if (freeze)
        {
            state = MovementState.freeze;
            rb.velocity = Vector3.zero;
            desiredMoveSpeed = 0;
        }

        // Mode - Unlimited
        else if(unlimited)
        {
            state = MovementState.unlimited;
            desiredMoveSpeed = 999f;
        }

        // Mode - Climbing
        else if(climbing)
        {
            state = MovementState.climbing;
            desiredMoveSpeed = climbSpeed;
        }


        // Mode - Wallrunning
        else if(wallrunning)
        {
            state = MovementState.wallrunning;
            desiredMoveSpeed = wallrunSpeed;
        }

        // Mode - Sliding
        else if (sliding)
        {
            state = MovementState.sliding;

            if (OnSlope() && rb.velocity.y < 0.1f)
            {
                desiredMoveSpeed = slideSpeed;
                keepMomentum = true;
            }
            else
                desiredMoveSpeed = sprintSpeed;
        }

        // Mode - Dashing
        else if (dashing)
        {
            state = MovementState.dashing;
            desiredMoveSpeed = dashSpeed;
            speedChangeFactor = dashSpeedChangeFactor;
        }

        // Mode - Grappling
        else if (activeGrapple)
        {
            state = MovementState.grappling;
            desiredMoveSpeed = sprintSpeed;
        }

        // Mode - Swinging
        else if (swinging)
        {
            state = MovementState.swinging;
            desiredMoveSpeed = swingSpeed;
        }

        // Mode - Crouching
        else if (playerInput.actions["CrouchSlide"].IsPressed() && grounded)
        {
            state = MovementState.crouching;
            desiredMoveSpeed = crouchSpeed;
        }

        // Mode - Sprinting
        else if (grounded && sprintInputToggle)
        {
            state = MovementState.sprinting;
            desiredMoveSpeed = sprintSpeed;
        }

        // Mode - Walking
        else if (grounded)
        {
            state = MovementState.walking;
            desiredMoveSpeed = walkSpeed;
        }

        // Mode - Air
        else
        {
            state = MovementState.air;
            //desiredMoveSpeed = climbSpeed;
            //if (desiredMoveSpeed < sprintSpeed)
            //    desiredMoveSpeed = walkSpeed;
            //else 
            desiredMoveSpeed = sprintSpeed;
        }

        bool desiredMoveSpeedHasChanged = desiredMoveSpeed != lastDesiredMoveSpeed;

        if (rb.velocity.magnitude > 10)
            keepMomentum = true;
        else
            keepMomentum = false;

        

        if (lastState == MovementState.dashing) keepMomentum = true;
        //if (lastState == MovementState.swinging) keepMomentum = true;

        keepMomentum = false;

        // check if desiredMoveSpeed has changed drastically
        if (desiredMoveSpeedHasChanged)
        {
            if(keepMomentum)
            {
                StopAllCoroutines();
                StartCoroutine(SmoothlyLerpMoveSpeed());
            }
            else
            {
                //StopAllCoroutines();
                moveSpeed = desiredMoveSpeed;
            }
        }

        lastDesiredMoveSpeed = desiredMoveSpeed;
        lastState = state;
    }

    private float speedChangeFactor;
    private IEnumerator SmoothlyLerpMoveSpeed()
    {
        // smoothly lerp movementSpeed to desired value
        float time = 0;
        float difference = Mathf.Abs(desiredMoveSpeed - moveSpeed);
        float startValue = moveSpeed;

        float boostFactor = speedChangeFactor;

        while (time < difference)
        {
            moveSpeed = Mathf.Lerp(startValue, desiredMoveSpeed, time / difference);

            if (OnSlope())
            {
                float slopeAngle = Vector3.Angle(Vector3.up, slopeHit.normal);
                float slopeAngleIncrease = 1 + (slopeAngle / 90f);

                time += Time.deltaTime * boostFactor * slopeIncreaseMultiplier * slopeAngleIncrease;
            }
            else
                time += Time.deltaTime * boostFactor;

            yield return null;
        }

        moveSpeed = desiredMoveSpeed;
        speedChangeFactor = speedIncreaseMultiplier;
        keepMomentum = false;
    }

    private void MovePlayer()
    {
        if (activeGrapple) return;
        if (swinging) return;
        
        if (climbingScript.exitingWall) return;

        if (state == MovementState.dashing) return;

        if (restricted) return;

        // calculate movement direction
        moveDirection = orientation.forward * inputMovement.y + orientation.right * inputMovement.x;

        // on slope
        if (OnSlope() && !exitingSlope)
        {
            rb.AddForce(GetSlopeMoveDirection(moveDirection) * moveSpeed * 20f, ForceMode.Force);
            // Since gravity is off while on a slope, this is to prevent bobbing while going up slopes.
            //if (rb.velocity.y > 0)
            //rb.AddForce(Vector3.down * 80f, ForceMode.Force);
        }

        // on ground
        else if (grounded)
            rb.AddForce(moveDirection.normalized * moveSpeed * 10f, ForceMode.Force);





        // in air
        else if (!grounded)
            rb.AddForce(moveDirection.normalized * moveSpeed * 10f * airMultiplier, ForceMode.Force);

        // turn gravity off while on slope
        rb.useGravity = !OnSlope();
    }

    void AirControlLogic()
    {
        // Moving faster than we should be, No extra momentum from us.
        if (rb.velocity.magnitude > desiredMoveSpeed)
        {
            Vector3 moveDirVelocity = moveDirection.normalized * rb.velocity.magnitude;
            Vector3 newAirVelocity = Vector3.Lerp(rb.velocity, moveDirVelocity, 0.01f);
            Debug.Log(rb.velocity + " : " + moveDirVelocity + " , makes: " + newAirVelocity);
            rb.velocity = newAirVelocity;
        }

        // We can add momentum.
        else
        {
            rb.AddForce(moveDirection.normalized * moveSpeed * 10f * airMultiplier, ForceMode.Force);
        }

            
    }

    private void SpeedControl()
    {
        if (activeGrapple) return;

        // Velocities get funky when parenting.
        if (transform.parent)
            return;

        bool ledgeJump = false;
        if(ledgeGrabScript)
        {
            if (ledgeGrabScript.exitingLedge)
                ledgeJump = true;
        }

        // limiting speed on slope
        if (OnSlope() && !exitingSlope)
        {
            if (rb.velocity.magnitude > moveSpeed)
                rb.velocity = rb.velocity.normalized * moveSpeed;
        }

        else if (ledgeJump)
        {

        }

        // limiting speed on ground or in air
        else
        {
            

            Vector3 flatVel = new Vector3(rb.velocity.x, 0f, rb.velocity.z);

            // limit velocity if needed
            if (flatVel.magnitude > moveSpeed)
            {
                Vector3 limitedVel = flatVel.normalized * moveSpeed;
                rb.velocity = new Vector3(limitedVel.x, rb.velocity.y, limitedVel.z);
            }
        }

        if (maxYSpeed != 0 && rb.velocity.y > maxYSpeed)
            rb.velocity = new Vector3(rb.velocity.x, maxYSpeed, rb.velocity.z);
    }

    private void Jump()
    {
        exitingSlope = true;

        // reset y velocity
        rb.velocity = new Vector3(rb.velocity.x, 0f, rb.velocity.z);

        rb.AddForce(transform.up * jumpForce, ForceMode.Impulse);
    }
    private void ResetJump()
    {
        readyToJump = true;

        exitingSlope = false;
    }

    bool enableMovementOnNextTouch;
    public void JumpToPosition(Vector3 targetPosition, float trajectoryHeight)
    {
        activeGrapple = true;
        
        velocityToSet = CalculateJumpVelocity(transform.position, targetPosition, trajectoryHeight);
        Invoke(nameof(SetVelocity), 0.1f);
        if(grappleScript)
            Invoke(nameof(ResetRestrictions), grappleScript.debugBreakGrappleTimer);
    }

    private Vector3 velocityToSet;
    private void SetVelocity()
    {
        enableMovementOnNextTouch = true;
        rb.velocity = velocityToSet;
        cam.DoFov(grappleFov);
    }

    public void ResetRestrictions()
    {
        activeGrapple = false;
        cam.DoFov(cam.baseFOV);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if(enableMovementOnNextTouch)
        {
            enableMovementOnNextTouch = false;
            ResetRestrictions();

            if(grappleScript)
                grappleScript.StopGrapple();
            
            if(TryGetComponent<DualHooks>(out DualHooks hooks))
            {
                StartCoroutine(hooks.StopGrapple(0));
                StartCoroutine(hooks.StopGrapple(1));
            }
        }
    }

    public bool OnSlope()
    {
        if (!gameObject.activeInHierarchy)
            Debug.Log("WHAT");

        if (Physics.Raycast(transform.position, Vector3.down, out slopeHit, playerHeight * 0.5f + 0.3f, whatIsGround))
        {
            
            float angle = Vector3.Angle(Vector3.up, slopeHit.normal);
            return angle < maxSlopeAngle && angle != 0;
        }
        return false;
    }

    public Vector3 GetSlopeMoveDirection(Vector3 direction)
    {
        return Vector3.ProjectOnPlane(direction, slopeHit.normal).normalized;
    }

    public Vector3 CalculateJumpVelocity(Vector3 startPoint, Vector3 endPoint, float trajectoryHeight)
    {
        float gravity = Physics.gravity.y;
        float displacementY = endPoint.y - startPoint.y;
        Vector3 displacementXZ = new Vector3(endPoint.x - startPoint.x, 0f, endPoint.z - startPoint.z);

        Vector3 velocityY = Vector3.up * Mathf.Sqrt(-2 * gravity * trajectoryHeight);
        Vector3 velocityXZ = displacementXZ / (Mathf.Sqrt(-2 * trajectoryHeight / gravity)
            + Mathf.Sqrt(2 * (displacementY - trajectoryHeight) / gravity));

        return velocityXZ + velocityY;

    }

    #region inputfunctions

    public void Movement(InputAction.CallbackContext context)
    {
        var inputValue = context.ReadValue<Vector2>();
        inputMovement = inputValue;
    }

    bool sprintInputToggle = false;
    public void InputSprintLogic()
    {
        // Toggle sprint on
        if (playerInput.actions["Sprint"].WasPressedThisFrame())
            sprintInputToggle = true;

        // Stop sprinting when no longer inputting movement.
        if (inputMovement.magnitude <= 0.1f)
            sprintInputToggle = false;
    }

    #endregion


}


