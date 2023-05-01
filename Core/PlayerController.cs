using System;
using CMF;
using UnityEngine;

public class PlayerController : MonoBehaviour {

    public static PlayerController instance;
    private Mover mover;
    private CapsuleCollider capsuleCollider;
    private Rigidbody rbody;
    
    [Header("Refs")]
    [SerializeField] private GameObject cam;
    [SerializeField] private Transform camRoot;
    [SerializeField] private CamController camController;
    
    [Header("Running")]
    [SerializeField] private float groundAcc;
    [SerializeField] private float airAcc;
    [SerializeField] private float runSpeed;
    [SerializeField] private float groundFriction;

    [Header("WallRunning")] 
    [SerializeField] private AnimationCurve wallRunGravityCurve;
    [SerializeField] private float wallRunTime;
    [SerializeField] private float wallRunSpeed;
    [SerializeField] private float minWallRunSpeed;
    [SerializeField] private float wallRunAcc;
    [SerializeField] private float wallRunRayDist;
    [SerializeField] private float wallRunJumpSideSpeed;
    [SerializeField] private float wallRunBumpOffSideSpeed;
    [SerializeField] private float wallRunFriction;
    [SerializeField] private float wallRunCoolDownTime;
    [SerializeField] private float minAngleFromWallToWallRun;

    private Timer wallRunLimitingTimer;
    private Timer wallRunCooldownTimer;
    private Vector3 wallRunForwardVector;
    private Vector3 wallRunWallNormal;
    private float wallRunAngleFromWall;

    [Header("Climbing")] 
    [SerializeField] private AnimationCurve climbSpeedCurve;
    [SerializeField] private float wallClimbSpeed;
    [SerializeField] private float climbTime;
    [SerializeField] private float wallClimbAcc;
    [SerializeField] private float wallClimbFriction;
    [SerializeField] private float wallClimbJumpOffForce;
    [SerializeField] private float onWallClimbRaycastDist;

    private Timer climbLimitingTimer;
    private Vector3 climbForwardVector;
    private Vector3 climbRightVector;
    private bool canClimb = true;

    [Header("Sliding")] 
    [SerializeField] private float slideSpeed;
    [SerializeField] private float slideHeightReduction;
    [SerializeField] private float slideFriction;
    [SerializeField] private float crouchSpeed;
    [SerializeField] private float maxSpeedSlideBoost;
    [SerializeField] private float autoStopSlideSpeed;
    [SerializeField] private float slideSlopeAcc;

    private Timer slideCooldownTimer;
    private Timer slidePrimeTimer;
    private const float SlideCooldownDelay = 0.5f;
    private const float SlidePrimeDelay = 0.15f;
    private const float NormalHeight = 1.65f;
    private float curSlideHeight;
    private float camRootHeight;
    private bool slidePrimed;
    
    [Header("Jumping")]
    [SerializeField] private float jumpForce;

    [Header("Ziplining")] 
    [SerializeField] private float zipSpeed;
    [SerializeField] private float zipAcc;
    [SerializeField] private float zipFriction;

    public Vector3 ZipMoveDir { get; private set; }
    public Zipline Zipline { get; private set; }
    private Vector3 startZipPos;

    [Header("Mantle")] 
    [SerializeField] private float mantleRaycastDist;
    [SerializeField] private float mantleSpeed;
    [SerializeField] private float mantleForwardScaler;

    private Vector3 mantleTargetPos = Vector3.zero;
    
    [Header("Gravity")]
    [SerializeField] private float gravityForce;
    [SerializeField] private float wallRunningSlidingUpGravityForce;
    [SerializeField] private float wallRunningGravityForce;
    [SerializeField] private float wallSlippingGravityForce;

    [Header("Air Control")] 
    [SerializeField] private float suspendAirControlClimbTime;
    [SerializeField] private float suspendAirControlWallRunTime;

    private float curAirControlScaler = 1f;
    private Timer suspendAirControlTimer;

    private bool jumpNextFixedUpdate;
    private bool slideNextFixedUpdate;
    
    private Vector3 velocity = Vector3.zero;
    private Vector3 frameVelocity = Vector3.zero;
    private Vector3 right;
    private Vector3 forward;
    private Vector3 headPos;
    private Vector3 centerPos;
    private Vector3 stepPos;
    private float xzSpeedPreCorrection;
    private float targetMaxSpeed;
    private float curMaxSpeed;
    private bool isGrounded;
    private bool velocityCorrected;
    
    private Vector3 predictedPos;
    private Vector3 lastPos;
    
    private Timer lerpTimer;
    private Vector3 startLerpPos;
    private Vector3 lerpPos;

    private Timer pauseTimer;

    public enum LaunchRefPoint { Camera, Rigidbody };
    private float endLaunchTime;

    public State CurState => stateMachine.CurState;
    public Vector3 Velocity => (isGrounded) ? Vector3Ext.Flatten(velocity) : velocity;

    public float CurXZSpeed => Vector3Ext.Flatten(velocity).magnitude;
    public float RunSpeed => runSpeed;

    private StateMachine stateMachine = new();
    public State Grounded { get; private set; }
    public State Jumping { get; private set; }
    public State InAir { get; private set; }
    public State WallRunning { get; private set; }
    public State Climbing { get; private set; }
    public State Mantling { get; private set; }
    public State Sliding { get; private set; }
    public State Ziplining { get; private set; }
    public State Launching { get; private set; }
    public State Lerping { get; private set; }
    public State Paused { get; private set; }

    private void Awake() {
        instance = this;
        mover = GetComponent<Mover>();
        capsuleCollider = GetComponent<CapsuleCollider>();
        rbody = GetComponent<Rigidbody>();
        Cursor.lockState = CursorLockMode.Locked;

        Grounded = stateMachine.CreateState(UpdateGrounded, OnEnterGrounded, null);
        Jumping = stateMachine.CreateState(null, OnEnterJump, null);
        InAir = stateMachine.CreateState(UpdateInAir, OnEnterInAir, OnExitInAir);
        WallRunning = stateMachine.CreateState(UpdateWallRun, OnEnterWallRun, OnExitWallRun);
        Climbing = stateMachine.CreateState(UpdateClimbing, OnEnterClimb, OnExitClimb);
        Mantling = stateMachine.CreateState(UpdateMantling, OnEnterMantle, OnExitMantle);
        Sliding = stateMachine.CreateState(UpdateSliding, OnEnterSlide, OnExitSlide);
        Ziplining = stateMachine.CreateState(UpdateZiplining, OnEnterZipline, OnExitZipline);
        Launching = stateMachine.CreateState(null, OnEnterLaunch, OnExitLaunch);
        Lerping = stateMachine.CreateState(UpdateLerping, null, null);
        Paused = stateMachine.CreateState(UpdatePaused, null, null);
    }

    private void Start() {
        camRootHeight = camRoot.localPosition.y;
    }

    private void OnEnable() {
        Evnts.PlayerLaunchToPoint.Subscribe(LaunchToPoint);
        Evnts.OnZiplineYank.Subscribe(OnZiplineYank);
        Evnts.OnMoveToZipline.Subscribe(OnMoveToZipline);
    }
    
    private void OnDisable() {
        Evnts.PlayerLaunchToPoint.Unsubscribe(LaunchToPoint);
        Evnts.OnZiplineYank.Unsubscribe(OnZiplineYank);
        Evnts.OnMoveToZipline.Unsubscribe(OnMoveToZipline);
    }

    private void Update() {
        TickTimers();
        StoreInputsForNextFixedUpdate();
        UpdatePlayerPositions();
        UpdateSlideHeight();
    }

    private void FixedUpdate() {
        CorrectVelocity();
        CheckForGround(); 
        CalculateDirections();
        UpdateState();
        ApplyFrameVelocity();
        ApplyGravity();
        ApplyFriction(); 
        ClampVelocity();
        ApplyVelocity();
        ClearInputsAfterFixedUpdate();
    }

    public bool CannotLaunchToPos(Vector3 pos) {
        Vector3 heightOffset = new(0f, -NormalHeight, 0f);
        pos += heightOffset;
            
        float dist = Vector3.Distance(pos, rbody.position);
        Vector3 dir = (pos - rbody.position).normalized;
        return Physics.CapsuleCast(stepPos, headPos, capsuleCollider.radius, dir, dist, Masks.GroundMask);
    }

    public void PauseController(float time) {
        velocity = Vector3.zero;
        pauseTimer.SetTime(time);
    }
    
    public void LerpToPosition(Vector3 pos, float time) {
        velocity = Vector3.zero;
        lerpPos = pos;
        startLerpPos = rbody.position;
        lerpTimer.SetTime(time);
    }
    
    public int GetSideOfWallOnWallRun() {
        return -(int)Mathf.Sign(wallRunAngleFromWall);
    }
    
    private void CorrectVelocity() {
        const float maxError = 0.015f;
        Vector3 curXZPos = Vector3Ext.Flatten(rbody.position);
        Vector3 predictedXZPos = Vector3Ext.Flatten(predictedPos);

        xzSpeedPreCorrection = CurXZSpeed;
        velocityCorrected = false;
        
        // Check if we are running into a wall or something
        if (Vector3.Distance(curXZPos, predictedXZPos) > maxError) {
            float yVel = velocity.y;
            float realSpeed = ((curXZPos - Vector3Ext.Flatten(lastPos)) / Time.fixedDeltaTime).magnitude;
            velocity = velocity.normalized * realSpeed;
            velocity.y = yVel;
            velocityCorrected = true;
        }

        // Check if we hit our head or feet
        if (!isGrounded && Mathf.Abs(predictedPos.y - rbody.position.y) > maxError) {
            velocity.y = 0f;
            velocityCorrected = true;
        }
    }
    
    private void CheckForGround() {
        // Allow stepping makes jumping up onto platforms smoother
        bool allowStepping = (velocity.y <= 0f);
        
        // Setting collider height must be done in fixed update
        mover.SetColliderHeight(curSlideHeight);
        
        mover.CheckForGround(allowStepping);
        isGrounded = mover.IsGrounded();
    }
    
    private void CalculateDirections() {
        right = Vector3.ProjectOnPlane(cam.transform.right, transform.up).normalized;
        forward = Vector3.ProjectOnPlane(cam.transform.forward, transform.up).normalized;
    }
    
    private void UpdateState() {
        if (!lerpTimer.IsFinished)       stateMachine.SetStateIfNotCurrent(Lerping);
        else if (!pauseTimer.IsFinished) stateMachine.SetStateIfNotCurrent(Paused);
        else if (IsLaunching())          stateMachine.SetStateIfNotCurrent(Launching);
        else if (IsJumping())            stateMachine.SetStateIfNotCurrent(Jumping);
        else if (IsZiplining())          stateMachine.SetStateIfNotCurrent(Ziplining);
        else if (IsMantling())           stateMachine.SetStateIfNotCurrent(Mantling);
        else if (IsSliding())            stateMachine.SetStateIfNotCurrent(Sliding);
        else if (isGrounded)             stateMachine.SetStateIfNotCurrent(Grounded);
        else if (IsClimbing())           stateMachine.SetStateIfNotCurrent(Climbing);
        else if (IsWallRunning())        stateMachine.SetStateIfNotCurrent(WallRunning);
        else                             stateMachine.SetStateIfNotCurrent(InAir);
    }
    
    private void ApplyFrameVelocity() {
        frameVelocity = Vector3.zero;
        stateMachine.Tick();
        velocity += frameVelocity;
    }
    
    private void ApplyGravity() {
        State curState = stateMachine.CurState;
        if (curState == Lerping || curState == Mantling) return;
        
        if ((curState == Grounded || curState == Sliding) && velocity.y <= 0f) {
            velocity.y = 0f;
            return;
        }
        
        if (curState == WallRunning) {
            if (CurXZSpeed < minWallRunSpeed) {
                velocity.y += wallSlippingGravityForce * Time.fixedDeltaTime;
                return;
            } 
            if (velocity.y > 0f) {
                velocity.y += wallRunningSlidingUpGravityForce * Time.fixedDeltaTime;
                velocity.y = Mathf.Clamp(velocity.y, 0f, Mathf.Infinity);
                return;
            } 
            float eval = 1f - (wallRunLimitingTimer.CurTime / wallRunTime);
            float gravityScaler = wallRunGravityCurve.Evaluate(eval);
            velocity.y += wallRunningGravityForce * gravityScaler * Time.fixedDeltaTime;
            return;
        }
        
        velocity.y += gravityForce * Time.fixedDeltaTime;
    }
    
    private void ApplyFriction() {
        State curState = stateMachine.CurState;
        if (curState == InAir || curState == Mantling || curState == Ziplining || curState == Lerping) return;
        
        float friction = GetFriction();
        Vector3 xzVelocity = new(velocity.x, 0f, velocity.z);

        float speed = CurXZSpeed;
        speed -= friction * Time.fixedDeltaTime;
        speed = Mathf.Clamp(speed, 0f, Mathf.Infinity);

        float ySpeed = velocity.y;
        if (stateMachine.CurState == Climbing) {
            float yDir = Mathf.Sign(ySpeed);
            ySpeed = Mathf.Abs(ySpeed);
            ySpeed -= friction * Time.fixedDeltaTime;
            ySpeed = Mathf.Clamp(ySpeed, 0f, Mathf.Infinity);
            ySpeed *= yDir;
        }
        
        velocity = xzVelocity.normalized * speed;
        velocity.y = ySpeed;
    }
    
    private void ClampVelocity() {
        bool clampYSpeed = (stateMachine.CurState == Ziplining);
        float speed = (clampYSpeed) ? velocity.magnitude : CurXZSpeed;
        
        // Current max speed should never be more than actual speed,
        // otherwise we can potentially accelerate beyond the target max speed temporarily  
        if (curMaxSpeed > speed) curMaxSpeed = speed;
        
        // Reduce current max speed by friction if necessary
        if (curMaxSpeed > targetMaxSpeed) {
            float friction = GetFriction() * Time.fixedDeltaTime;
            curMaxSpeed = Mathf.Clamp(curMaxSpeed - friction, targetMaxSpeed, Mathf.Infinity) ;
        }
        else {
            curMaxSpeed = targetMaxSpeed;
        }
    
        // Clamp velocity to current max speed if necessary
        if (speed > curMaxSpeed) {
            if (clampYSpeed) {
                velocity = velocity.normalized * curMaxSpeed;
            }
            else {
                float ySpeed = velocity.y;
                velocity = Vector3Ext.Flatten(velocity).normalized * curMaxSpeed;
                velocity.y = ySpeed;
            }
        }
        
        if (stateMachine.CurState == Climbing) {
            float ySpeed = velocity.y;
            float yDir = Mathf.Sign(ySpeed);
            if (Mathf.Abs(ySpeed) > targetMaxSpeed) {
                ySpeed = targetMaxSpeed * yDir;
            }
            velocity.y = ySpeed;
        }
    }
    
    private void ApplyVelocity() {
        mover.SetExtendSensorRange(isGrounded);
        mover.SetVelocity(velocity);
        lastPos = rbody.position;
        
        // Must use rbody.velocity to account for up and down velocity
        // applied to the rigidbody when calling mover.SetVelocity
        predictedPos = lastPos + (rbody.velocity * Time.fixedDeltaTime);
    }
    
    private void ClearInputsAfterFixedUpdate() {
        jumpNextFixedUpdate = false;
        slideNextFixedUpdate = false;
    }
    
    private void TickTimers() {
        if (stateMachine.CurState == WallRunning) wallRunLimitingTimer.Tick();
        if (stateMachine.CurState == Climbing && climbLimitingTimer.Tick()) canClimb = false;
        if (suspendAirControlTimer.Tick()) curAirControlScaler = 1f;
        
        wallRunCooldownTimer.Tick();
        slideCooldownTimer.Tick();
        lerpTimer.Tick();
        pauseTimer.Tick();
    }
    
    private void StoreInputsForNextFixedUpdate() {
        if (Input.GetKeyDown(KeyCode.Space)) jumpNextFixedUpdate = true;
        if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.LeftAlt)) slideNextFixedUpdate = true;
    }
    
    // This method requires PlayerController to update before default time
    private void UpdatePlayerPositions() {
        Vector3 halfCol = new(0f, (capsuleCollider.height / 2f), 0f);
        centerPos = transform.position + capsuleCollider.center;
        headPos = centerPos + halfCol;
        stepPos = centerPos - halfCol;
        PlayerPos.SetPositions(headPos, centerPos, stepPos, rbody.position, capsuleCollider.radius);
    }
    
    private void UpdateSlideHeight() {
        if (stateMachine.CurState != Sliding && curSlideHeight == NormalHeight) return;

        float colTargetHeight = (stateMachine.CurState == Sliding) ? NormalHeight - slideHeightReduction : NormalHeight;
        float camTargetHeight = (stateMachine.CurState == Sliding) ? camRootHeight - slideHeightReduction : camRootHeight;

        camRoot.localPosition = Vector3.MoveTowards(camRoot.localPosition, new(0f, camTargetHeight, 0f), crouchSpeed * Time.deltaTime); 
        curSlideHeight = Mathf.MoveTowards(curSlideHeight, colTargetHeight, crouchSpeed * Time.deltaTime);
    }

    private void OnEnterGrounded() {
        canClimb = true;
        climbLimitingTimer.SetTime(climbTime);
        wallRunLimitingTimer.SetTime(wallRunTime);
        targetMaxSpeed = runSpeed;
        Evnts.OnEnterGrounded.Invoke();
    }
    
    private void UpdateGrounded() {
        float speed = groundAcc * Time.fixedDeltaTime; 
        if (Input.GetKey(KeyCode.W)) frameVelocity += forward * speed;
        if (Input.GetKey(KeyCode.S)) frameVelocity -= forward * speed;
        if (Input.GetKey(KeyCode.D)) frameVelocity += right * speed;
        if (Input.GetKey(KeyCode.A)) frameVelocity -= right * speed;
    }

    private void OnEnterJump() {
        Evnts.OnEnterJump.Invoke();
        if (stateMachine.PrevState == Grounded || stateMachine.PrevState == Sliding) {
            velocity.y = jumpForce;
            isGrounded = false;
            targetMaxSpeed = Mathf.Clamp(CurXZSpeed, runSpeed, Mathf.Infinity);
            return;
        }
        if (stateMachine.PrevState == Ziplining) {
            velocity.y = jumpForce;
            isGrounded = false;
            return;
        }
        if (stateMachine.PrevState == WallRunning) {
            velocity += wallRunWallNormal.normalized * wallRunJumpSideSpeed; 
            velocity.y = jumpForce;
            wallRunCooldownTimer.SetTime(wallRunCoolDownTime);
            SuspendAirControl(suspendAirControlWallRunTime);
            return;
        }
        if (stateMachine.PrevState == Climbing) {
            canClimb = false;
            velocity += wallRunWallNormal.normalized * wallClimbJumpOffForce;
            SuspendAirControl(suspendAirControlClimbTime);
        } 
    }
    
    private bool IsJumping() {
        return (jumpNextFixedUpdate && stateMachine.CurState != InAir);
    }

    private void OnEnterInAir() {
        targetMaxSpeed = (stateMachine.PrevState == Mantling) ? runSpeed : Mathf.Clamp(CurXZSpeed, runSpeed, Mathf.Infinity);
        Evnts.OnEnterInAir.Invoke();
    }

    private void OnExitInAir() {
        ResetAirControl();
        Evnts.OnExitInAir.Invoke();
    }

    private void UpdateInAir() {
        float speed = airAcc * curAirControlScaler * Time.fixedDeltaTime; 
        if (Input.GetKey(KeyCode.W)) frameVelocity += forward * speed;
        if (Input.GetKey(KeyCode.S)) frameVelocity -= forward * speed;
        if (Input.GetKey(KeyCode.D)) frameVelocity += right * speed;
        if (Input.GetKey(KeyCode.A)) frameVelocity -= right * speed;
    }

    private void OnEnterWallRun() {
        camController.OnWallRunNormalChange(wallRunWallNormal, Vector3.zero);
        targetMaxSpeed = wallRunSpeed;
        velocity = wallRunForwardVector * xzSpeedPreCorrection;
            
        Vector3 startRayPos = centerPos;
        Vector3 endRayPos = centerPos + new Vector3(0f, 2f, 0f);
            
        // Calculate Y velocity boost
        float rotAngle = (wallRunAngleFromWall >= 0f) ? -90f : 90f;
        Vector3 dir = Quaternion.AngleAxis(rotAngle, Vector3.up) * wallRunForwardVector;
        if (Raycast.Ladder(startRayPos, endRayPos, dir, out LadderData ladder, 10, Masks.GroundMask)) {
            float yVelBoost = Mathf.Sqrt(2f * -wallRunningSlidingUpGravityForce * (ladder.highestPos.y - centerPos.y));
            if (yVelBoost > 0.1f) velocity.y = yVelBoost;
        }
        
        Evnts.OnEnterWallRun.Invoke();
    }

    private void OnExitWallRun() {
        wallRunLimitingTimer.SetTime(wallRunTime);
        Evnts.OnExitWallRun.Invoke();
    }
    
    private void UpdateWallRun() {
        // Recalculate wall run forward vector for curves 
        wallRunForwardVector = (wallRunAngleFromWall >= 0f) ? 
            new(-wallRunWallNormal.normalized.z, 0f, wallRunWallNormal.normalized.x) :
            new(wallRunWallNormal.normalized.z, 0f, -wallRunWallNormal.normalized.x);
            
        // Stick player to wall
        Vector3 centerOffsetFromBase = new(0f, centerPos.y - rbody.position.y, 0f);
            
        // Need to calculate our next center pos because setting rbody.position does
        // not get applied until the NEXT fixed update, so this prevents jitters
        Vector3 nextCenterPos = predictedPos + centerOffsetFromBase;

        // Just in case nextCenterPos ends up inside a wall, make it not be in the wall
        float nextPosDist = Vector3.Distance(centerPos, nextCenterPos);
        Vector3 nextPosDir = (nextCenterPos - centerPos).normalized;
        if (Physics.Raycast(centerPos, nextPosDir, out RaycastHit nextPosHit, nextPosDist, Masks.GroundMask)) {
            nextCenterPos = nextPosHit.point + (nextPosHit.normal * capsuleCollider.radius);
        }
           
        // Stick player to wall by raycasting from next center position and offseting by player radius
        float rotAngle = (wallRunAngleFromWall >= 0f) ? -90f : 90f;
        Vector3 dir = Quaternion.AngleAxis(rotAngle, Vector3.up) * wallRunForwardVector;
        if (Raycast.WallCast(nextCenterPos, dir, out RaycastHit hit, wallRunRayDist, Masks.GroundMask)) {
            Vector3 newPos = hit.point + (hit.normal * capsuleCollider.radius) - centerOffsetFromBase;
            rbody.position = newPos;
        }

        // Set wallrun velocity
        if (!Input.GetKey(KeyCode.W) && !Input.GetKey(KeyCode.S)) return;
        
        float newSpeed = 0f;
        float acc = wallRunAcc * Time.fixedDeltaTime;
        if (Input.GetKey(KeyCode.W)) newSpeed = CurXZSpeed + acc;
        if (Input.GetKey(KeyCode.S)) newSpeed = Mathf.Clamp(CurXZSpeed - acc, 0f, Mathf.Infinity);
        
        float ySpeed = velocity.y;
        velocity = wallRunForwardVector * newSpeed;
        velocity.y = ySpeed;
    }
    
    private bool IsWallRunning() {
        if (!canClimb || wallRunLimitingTimer.IsFinished || !wallRunCooldownTimer.IsFinished) return false;

        bool radarHitWall = false;
        Vector3 lastRadarWallNormal = wallRunWallNormal;
        Span<float> radarAngles = stackalloc float[] { 0f, 180f, 30f, 150f, 60f, 120f }; 
        
        // Scan for walls
        for (int i = 0; i < radarAngles.Length; i++) {
            Vector3 vec = Quaternion.AngleAxis(radarAngles[i], Vector3.up) * -right;
            if (Raycast.WallCast(centerPos, vec.normalized, out RaycastHit hit, wallRunRayDist, Masks.GroundMask)) {
                
                // Ignore wall if angle change is greater than 45, this prevents wallrunning around sharp corners
                if (Vector3.Angle(wallRunWallNormal, hit.normal) > 45f) continue;
                
                radarHitWall = true;
                wallRunWallNormal = hit.normal;
                wallRunAngleFromWall = Vector3.SignedAngle(-Vector3Ext.Flatten(wallRunWallNormal), forward, Vector3.up);
                break;
            }

            // If scan doesn't hit anything
            if (i == radarAngles.Length - 1) {
                wallRunAngleFromWall = 0f;
                wallRunWallNormal = Vector3.zero;
            }
        }
        
        // Bump player off wall when there is no longer a wall to run on
        if (stateMachine.CurState == WallRunning && !radarHitWall) {
            velocity += lastRadarWallNormal.normalized * wallRunBumpOffSideSpeed; 
            wallRunCooldownTimer.SetTime(wallRunCoolDownTime);
            SuspendAirControl(suspendAirControlWallRunTime);
            return false;
        }
        
        if (!radarHitWall) return false;
        if (lastRadarWallNormal != wallRunWallNormal) camController.OnWallRunNormalChange(wallRunWallNormal, lastRadarWallNormal);
        if (stateMachine.CurState == WallRunning) return true;
        
        // Don't wall run if we are moving primarly backwards
        float dot = Vector3.Dot(Vector3Ext.Flatten(velocity).normalized, forward);
        if (dot < -0.25f) return false;

        // Check if we should start wallrunning
        if (stateMachine.CurState != WallRunning && velocityCorrected) {
            wallRunForwardVector = (wallRunAngleFromWall >= 0f) ? 
                new(-wallRunWallNormal.normalized.z, 0f, wallRunWallNormal.normalized.x) :
                new(wallRunWallNormal.normalized.z, 0f, -wallRunWallNormal.normalized.x);
            
            float rotAngle = (wallRunAngleFromWall >= 0f) ? -90f : 90f;
            Vector3 playerToWallDir = Quaternion.AngleAxis(rotAngle, Vector3.up) * wallRunForwardVector;
            
            if (!Raycast.Ladder(centerPos, headPos, playerToWallDir, out LadderData ladder, 3, Masks.GroundMask)) return false;
            return (ladder.highestPos == headPos && Mathf.Abs(wallRunAngleFromWall) >= minAngleFromWallToWallRun);
        }

        return (stateMachine.CurState == WallRunning);
    }

    private void OnEnterClimb() {
        velocity = Vector3.zero;
        targetMaxSpeed = wallClimbSpeed;
        Evnts.OnEnterClimb.Invoke();
    }
    
    private void OnExitClimb() {
        Evnts.OnExitClimb.Invoke();
    }
    
    private void UpdateClimbing() {
        float eval = 1f - (climbLimitingTimer.CurTime / climbTime);
        targetMaxSpeed = wallClimbSpeed * climbSpeedCurve.Evaluate(eval);

        float speed = wallClimbAcc * Time.fixedDeltaTime;
        if (Input.GetKey(KeyCode.W)) frameVelocity += transform.up * speed;
        if (Input.GetKey(KeyCode.S)) frameVelocity -= transform.up * speed;
        if (Input.GetKey(KeyCode.D)) frameVelocity += climbRightVector * speed;
        if (Input.GetKey(KeyCode.A)) frameVelocity -= climbRightVector * speed;
    }
    
    private bool IsClimbing() {
        if (stateMachine.CurState == Grounded || !canClimb) return false;
        
        // For jumping onto a wall don't start climbing until at the jump peak, ignore if we are transitioning from wallrun to climb
        if (stateMachine.CurState != Climbing && stateMachine.CurState != WallRunning && velocity.y > 0f) return false;
        
        // Fall off the wall if we stop climbing 
        if (stateMachine.CurState == Climbing && velocity.y <= 0f) {
            canClimb = false;
            return false;
        }

        // Fall off the wall if we stop looking at the wall
        if (stateMachine.CurState == Climbing && !Physics.Raycast(centerPos, forward, onWallClimbRaycastDist, Masks.GroundMask)) {
            canClimb = false;
            return false;
        }

        // Determine if we should start climbing
        if (stateMachine.CurState != Climbing && velocityCorrected) {
            Vector3 startRayPos = stepPos;
            Vector3 endRayPos = stepPos + new Vector3(0f, 1.95f, 0f);
            if (!Raycast.Ladder(startRayPos, endRayPos, forward, out LadderData ladder, 10, Masks.GroundMask)) return false;
            float angleFromWall = Vector3.Angle(-Vector3Ext.Flatten(ladder.hit.normal), forward);
            return (ladder.highestPos == endRayPos && angleFromWall < minAngleFromWallToWallRun);
        } 
        
        return (stateMachine.CurState == Climbing);
    }

    private void OnEnterMantle() {
        rbody.velocity = Vector3.zero;
        velocity = Vector3.zero;
        capsuleCollider.enabled = false;
        jumpNextFixedUpdate = false;
        Evnts.OnEnterMantle.Invoke();
    }

    private void OnExitMantle() {
        capsuleCollider.enabled = true;
        mantleTargetPos = Vector3.zero;
    }
    
    private void UpdateMantling() {
        rbody.MovePosition(Vector3.MoveTowards(rbody.position, mantleTargetPos, mantleSpeed * Time.fixedDeltaTime));
        if (Vector3.Distance(rbody.position, mantleTargetPos) < 0.02f) mantleTargetPos = Vector3.zero;
    }
    
    private bool IsMantling() {
        // If we are already mantling, then continue until finished
        if (mantleTargetPos != Vector3.zero) return true;
        
        // If we are not in the air or we did not jump, don't try to mantle
        if (stateMachine.CurState != Climbing && stateMachine.CurState != InAir && !jumpNextFixedUpdate) return false;

        // If we are in the air but still going up, don't try to mantle
        if (stateMachine.CurState == InAir && velocity.y > 0f) return false;
        
        // If wall is at our head position, then it is too tall to mantle
        if (Raycast.WallCast(headPos, forward, out _, mantleRaycastDist, Masks.GroundMask)) return false;

        // Scan from cam position to just above step height, if we hit a wall then mantle it
        const int mantleScanCount = 10;
        Vector3 startRayPos = cam.transform.position;
        Vector3 endRayPos = stepPos + new Vector3(0f, 0.025f, 0f);

        for (int i = 0; i < mantleScanCount; i++) {
            float comp = (float) i / (mantleScanCount - 1); 
            Vector3 rayPos = Vector3.Lerp(startRayPos, endRayPos, comp);
            if (!Physics.Raycast(rayPos, forward, out RaycastHit wallHit, mantleRaycastDist, Masks.GroundMask) || !Raycast.HitWall(wallHit)) continue;
            
            // Don't mantle if we are not looking at the wall
            float dot = Vector3.Dot(-Vector3Ext.Flatten(wallHit.normal), forward);
            if (stateMachine.CurState != Climbing && dot < 0.6f) return false;
                
            // Find the position in which we mantle to
            Vector3 rayStartPos = headPos + (forward * mantleForwardScaler);
            if (Physics.Raycast(rayStartPos, Vector3.down, out RaycastHit groundHit, NormalHeight, Masks.GroundMask)) {
                mantleTargetPos = groundHit.point;
                return true;
            }

            return false;
        }

        return false;
    }
    
    private void OnEnterSlide() {
        if (CurXZSpeed < maxSpeedSlideBoost) {
            float newSpeed = maxSpeedSlideBoost;
            velocity = new Vector3(velocity.x, 0f, velocity.z).normalized * newSpeed;
        }
        targetMaxSpeed = slideSpeed;
        Evnts.OnEnterSlide.Invoke();
    }
    
    private void OnExitSlide() {
        Evnts.OnExitSlide.Invoke();
    }

    private void UpdateSliding() {
        if (!isGrounded) return;
        
        Vector3 surfaceNormal = mover.GetGroundNormal();
        float surfaceSteepness = Vector3.Dot(Vector3.down, surfaceNormal);
        Vector3 downSlopeDir = (Vector3.down - (surfaceNormal * surfaceSteepness)).normalized;
        
        const float maxSpeedSlopeAngle = 50f;
        float slopeAngle = Vector3.Angle(Vector3.up, downSlopeDir) - 90f;
        float slopeSpeedScaler = Mathf.Clamp(slopeAngle / maxSpeedSlopeAngle, 0f, 1f);

        frameVelocity = downSlopeDir * (slideSlopeAcc * slopeSpeedScaler * Time.fixedDeltaTime);
    }
    
    private bool IsSliding() {
        // Sliding and then fell off ledge
        if (stateMachine.CurState == Sliding && !isGrounded) {
            slidePrimed = false;
            slidePrimeTimer.SetTime(SlidePrimeDelay);
            slideCooldownTimer.SetTime(SlideCooldownDelay);
            return false;
        }
        
        // Slide priming
        if (stateMachine.CurState != Sliding && stateMachine.CurState != Grounded) {
            if (slidePrimeTimer.Tick()) slidePrimed = false;
            if (slideNextFixedUpdate) {
                slidePrimed = true;
                slidePrimeTimer.SetTime(SlidePrimeDelay);
            }
            return false;
        }

        // Stop sliding if going too slow
        if (stateMachine.CurState == Sliding && CurXZSpeed <= autoStopSlideSpeed) return false;
        
        // If we are already sliding then everything is chillin
        if (stateMachine.CurState == Sliding) return true;

        // Check to start sliding
        bool inputForSlide = slideNextFixedUpdate || slidePrimed;
        if (inputForSlide && CurXZSpeed >= runSpeed && slideCooldownTimer.IsFinished) {
            slideCooldownTimer.SetTime(SlideCooldownDelay);
            slidePrimed = false;
            return true;
        }

        return false;
    }

    private void OnEnterZipline() {
        rbody.position = startZipPos;
        ZipMoveDir = Zipline.CalculatePlayerMoveDir(headPos, cam.transform.forward);
        velocity = ZipMoveDir * Mathf.Clamp(xzSpeedPreCorrection, 0f, zipSpeed);
        targetMaxSpeed = zipSpeed;
        Evnts.OnEnterZipline.Invoke();
    }

    private void OnExitZipline() {
        Zipline = null;
        Evnts.OnExitZipline.Invoke();
    }
    
    private void UpdateZiplining() {
        float newSpeed = velocity.magnitude + (zipAcc * Time.fixedDeltaTime);
        velocity = ZipMoveDir * newSpeed;
    }
    
    private bool IsZiplining() {
        if (Zipline == null) return false;
        
        // If we were launching, then start ziplining 
        if (stateMachine.CurState == Launching) return true;

        // If we hit something while ziplining stop, ex moving down zipline and hit ground
        if (velocityCorrected) return false;
        
        // Check if we are at the end of the zipline
        return (Zipline.WithinZiplineLength(headPos));
    }
    
    private void OnEnterLaunch() {
        targetMaxSpeed = Mathf.Infinity;
    }

    private void OnExitLaunch() {
        // If the launch failed its better to halt the player
        if (velocityCorrected) velocity = Vector3.zero;
        endLaunchTime = 0f;
    }
    
    private bool IsLaunching() {
        // As long as the end launch time has not been reached, 
        // and we haven't hit anything, then were launching
        return (Time.time < endLaunchTime && !velocityCorrected);
    }

    private void UpdateLerping() {
        rbody.MovePosition(Vector3.Lerp(startLerpPos, lerpPos, lerpTimer.Comp()));
    } 
    
    private void UpdatePaused() {
        velocity = Vector3.zero;
    } 
        
    private void SuspendAirControl(float suspendedime) {
        curAirControlScaler = 0f;
        suspendAirControlTimer.SetTime(suspendedime);
    }

    private void ResetAirControl() {
        curAirControlScaler = 1f;
        suspendAirControlTimer.Stop();
    }

    private float GetFriction() {
        State curState = stateMachine.CurState;
        if (curState == Grounded)    return groundFriction;
        if (curState == Sliding)     return slideFriction;
        if (curState == WallRunning) return wallRunFriction;
        if (curState == Climbing)    return wallClimbFriction;
        if (curState == Ziplining)   return zipFriction;
        return 0f;
    }
    
    private void OnMoveToZipline(Zipline zip) {
        Zipline = zip;
        SetStartZipPos(Zipline.GetClosestPointOnZipline(headPos));
        const float lerpTime = 0.1f;
        LerpToPosition(startZipPos, lerpTime);
    }
    
    private void OnZiplineYank(Zipline zip) {
        Zipline = zip;
        SetStartZipPos(Zipline.CachedTargetPos);
        LaunchToPoint(startZipPos, LaunchRefPoint.Rigidbody);
    }

    private void SetStartZipPos(Vector3 posOnZip) {
        Vector3 verticalOffset = new(0f, -1.8f, 0f);
        Vector3 heightOffset = new(0f, -NormalHeight, 0f);
        Vector3 horizontalOffset = Vector3Ext.Flatten(rbody.position - posOnZip).normalized * 0.35f;
        startZipPos = (Zipline.IsVertical()) ? (posOnZip + horizontalOffset + heightOffset) : (posOnZip + verticalOffset);
    }

    private void LaunchToPoint(Vector3 target, LaunchRefPoint refPoint, float initialSpeed = 0f) {
        Vector3 refPos = (refPoint == LaunchRefPoint.Camera) ? cam.transform.position : rbody.position;

        const float defaultSpeed = 22f;
        velocity = (target - refPos).normalized * ((initialSpeed == 0f) ? defaultSpeed : initialSpeed);

        float horizontalSpeed = new Vector3(velocity.x, 0f, velocity.z).magnitude;
        
        float dist = Vector3.Distance(Vector3Ext.Flatten(refPos), Vector3Ext.Flatten(target));
        float time = dist / horizontalSpeed;
        
        float yDelta = target.y - refPos.y;
        float initYSpeed = (yDelta + (0.5f * Mathf.Abs(gravityForce) * time * time)) / time;
        velocity.y = initYSpeed;
        
        endLaunchTime = Time.time + time;
        SuspendAirControl(time);
    }
    
}
