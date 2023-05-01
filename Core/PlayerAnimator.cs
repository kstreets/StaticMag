using UnityEngine;

public class PlayerAnimator : MonoBehaviour {

    [Header("Animators")]
    [SerializeField] private Animator armsAnim;
    [SerializeField] private Animator gunAnim;
    [SerializeField] private Animator cameraAnim;
    [SerializeField] private Animator[] weaponAnims;
    
    [Header("GameObjects")]
    [SerializeField] private GameObject camTiltParent;
    [SerializeField] private GameObject arms;
    
    [Header("Overrides")]
    [SerializeField] private RuntimeAnimatorController[] armControllerOverrides;
    [SerializeField] private RuntimeAnimatorController[] weaponControllerOverrides;
    
    [Header("WallRun")]
    [SerializeField] private float wallRunTiltAngle;
    [SerializeField] private float wallRunTiltSpeed;

    [Header("Landing")] 
    [SerializeField] private float distToFallForMediumLand;
    [SerializeField] private float distToFallForHardLand;
    
    [Header("SlideFovIncrease")]
    [SerializeField] private float slideFOVIncrease;
    [SerializeField] private float slideFOVSpeed;
    
    [SerializeField] private Camera mainCam;
    private float regularMainCamFOV;

    private int curPunchIndex;
    private int[] punchSequence = { 1, 2, 4, 3, 5, 0 };
    private Timer showWeaponAfterPunchTimer;
    
    private PlayerController playerController;
    private PlayerWeaponManager playerWeaponManager;
    private Animator curWeaponAnim;
    private float peakHeight;
    private float curGunPivotReturnSpeed;
    private const float MinDistForFall = 1.5f;
    private bool dontTriggerFall;
    
    // Arm anim params
    private static readonly int Default = Animator.StringToHash("Default");
    private static readonly int Punch = Animator.StringToHash("Punch");
    private static readonly int PunchId = Animator.StringToHash("PunchId");
    private static readonly int Throw = Animator.StringToHash("Throw");
    private static readonly int Pull = Animator.StringToHash("Pull");
    private static readonly int Yank = Animator.StringToHash("Yank");
    private static readonly int GrabWeapon = Animator.StringToHash("GrabWeapon");
    private static readonly int Equip = Animator.StringToHash("Equip");
    private static readonly int Speed = Animator.StringToHash("Speed");
    private static readonly int HideArmLeft = Animator.StringToHash("HideArmLeft");
    private static readonly int HideArmRight = Animator.StringToHash("HideArmRight");
    
    // Gun anim params
    private static readonly int Jump = Animator.StringToHash("Jump");
    private static readonly int SoftLand = Animator.StringToHash("SoftLand");
    private static readonly int MedLand = Animator.StringToHash("MedLand");
    private static readonly int HardLand = Animator.StringToHash("HardLand");
    private static readonly int Fall = Animator.StringToHash("Fall");
    private static readonly int Run = Animator.StringToHash("Run");
    private static readonly int Slide = Animator.StringToHash("Slide");
    
    // Camera anim params
    private static readonly int Catch = Animator.StringToHash("Catch");
    private static readonly int CamWalkBlend = Animator.StringToHash("CamWalkBlend");
    private static readonly int CamMedLand = Animator.StringToHash("CamMedLand");
    private static readonly int CamHardLand = Animator.StringToHash("CamHardLand");
    private static readonly int PunchLeft = Animator.StringToHash("PunchLeft");
    private static readonly int PunchRight = Animator.StringToHash("PunchRight");
    
    private void Awake() {
        playerController = GetComponent<PlayerController>();
        playerWeaponManager = GetComponent<PlayerWeaponManager>();
        armsAnim.runtimeAnimatorController = armControllerOverrides[0];
        regularMainCamFOV = mainCam.fieldOfView;
    }

    private void OnEnable() {
        Evnts.OnPrePull.Subscribe(OnPrePull);
        Evnts.OnThrow.Subscribe(OnThrow);
        Evnts.OnPickUp.Subscribe(PlayPickUpAnim);    
        Evnts.OnHeldTickExplode.Subscribe(PlayDefault);
        
        Evnts.OnEnterGrounded.Subscribe(OnEnterGrounded);
        Evnts.OnEnterJump.Subscribe(OnEnterJump);
        Evnts.OnEnterInAir.Subscribe(OnEnterInAir);
        Evnts.OnExitInAir.Subscribe(OnExitInAir);
        Evnts.OnEnterMantle.Subscribe(OnEnterMantle);
        Evnts.OnEnterWallRun.Subscribe(OnEnterWallRun);
        Evnts.OnExitWallRun.Subscribe(OnExitWallRun);
        Evnts.OnEnterSlide.Subscribe(OnEnterSlide);
        Evnts.OnExitSlide.Subscribe(OnExitSlide);
        Evnts.OnEnterZipline.Subscribe(OnEnterZipline);
        Evnts.OnExitZipline.Subscribe(OnExitZipline);
        Evnts.OnExitClimb.Subscribe(OnExitClimb);
    }

    private void OnDisable() {
        Evnts.OnPrePull.Unsubscribe(OnPrePull);
        Evnts.OnThrow.Unsubscribe(OnThrow);
        Evnts.OnPickUp.Unsubscribe(PlayPickUpAnim);    
        Evnts.OnHeldTickExplode.Unsubscribe(PlayDefault);
        
        Evnts.OnEnterGrounded.Unsubscribe(OnEnterGrounded);
        Evnts.OnEnterJump.Unsubscribe(OnEnterJump);
        Evnts.OnEnterInAir.Unsubscribe(OnEnterInAir);
        Evnts.OnExitInAir.Unsubscribe(OnExitInAir);
        Evnts.OnEnterMantle.Unsubscribe(OnEnterMantle);
        Evnts.OnEnterWallRun.Unsubscribe(OnEnterWallRun);
        Evnts.OnExitWallRun.Unsubscribe(OnExitWallRun);
        Evnts.OnEnterSlide.Unsubscribe(OnEnterSlide);
        Evnts.OnExitSlide.Unsubscribe(OnExitSlide);
        Evnts.OnEnterZipline.Unsubscribe(OnEnterZipline);
        Evnts.OnExitZipline.Unsubscribe(OnExitZipline);
        Evnts.OnExitClimb.Unsubscribe(OnExitClimb);
    }

    private void Update() {
        showWeaponAfterPunchTimer.Tick();
        CheckForWallRunTilt();
        CheckForSlideFOV();
        AnimateGun();
        AnimateCamera();
    }
    
    public void PlayCatchAnim(int throwableID) {
        SetRuntimeController(armsAnim, armControllerOverrides[throwableID]);
        armsAnim.SetTrigger(GrabWeapon);
        
        curWeaponAnim = weaponAnims[throwableID];
        SetRuntimeController(curWeaponAnim, weaponControllerOverrides[throwableID]);
        curWeaponAnim.SetTrigger(Catch);
        
        cameraAnim.SetTrigger(Catch);
    }

    public void PlayPunchAnim() {
        int punch = punchSequence[curPunchIndex];
        curPunchIndex = (curPunchIndex + 1) % punchSequence.Length;
        
        armsAnim.SetInteger(PunchId, punch);
        armsAnim.SetTrigger(Punch);

        // Left punchIds are even and right punchIds are odd
        int cameraPunchAnim = (punch % 2 == 0) ? PunchLeft : PunchRight;
        cameraAnim.SetTrigger(cameraPunchAnim);

        if (!playerWeaponManager.HasWeapon) return;
        
        playerWeaponManager.HideEquipedWeapon();
        showWeaponAfterPunchTimer.SetTime(0.5f);
        showWeaponAfterPunchTimer.endAction ??= () => {
            if (curWeaponAnim == null || !playerWeaponManager.HasWeapon) return;
            playerWeaponManager.ShowEquipedWeapon(); 
            curWeaponAnim.SetTrigger(Equip);
            armsAnim.SetTrigger(Equip);            
        };
    }
    
    private void OnThrow() {
        armsAnim.SetTrigger(Throw);
    }
    
    private void OnPrePull(IMagnetic.Id id) {
        if (id == IMagnetic.Id.Zipline || id == IMagnetic.Id.Olive) {
            armsAnim.SetTrigger(Yank);
            return;
        }
        armsAnim.SetTrigger(Pull);
    }

    private void PlayDefault() {
        armsAnim.SetTrigger(Default);
    }

    private void PlayPickUpAnim(IThrowable throwable) {
        int throwableID = (int)throwable.ThrowId;
        SetRuntimeController(armsAnim, armControllerOverrides[throwableID]);
        armsAnim.SetTrigger(Equip);
        
        curWeaponAnim = weaponAnims[throwableID];
        SetRuntimeController(curWeaponAnim, weaponControllerOverrides[throwableID]);
        curWeaponAnim.SetTrigger(Equip);
    }

    // TODO: Whenever I add the option for fov sliders this function needs to get called
    public void OnFOVChange(float newFOV) {
        regularMainCamFOV = newFOV;
    }

    private void SetRuntimeController(Animator anim, RuntimeAnimatorController runtime) {
        anim.runtimeAnimatorController = runtime;
        // Update is called, otherwise animations disappear for 1 frame, very cool Unity
        anim.Update(Time.deltaTime);
    }

    private void OnEnterGrounded() {
        float distFell = peakHeight - transform.position.y;
        peakHeight = 0f;
        if (distFell <= MinDistForFall) {
            gunAnim.SetTrigger(Run);
            return;
        }

        if (distFell < distToFallForMediumLand) {
            gunAnim.SetTrigger(SoftLand);
            return;
        }
        if (distFell >= distToFallForMediumLand && distFell < distToFallForHardLand) {
            gunAnim.SetTrigger(MedLand);
            cameraAnim.SetTrigger(CamMedLand);
            return;
        }
        gunAnim.SetTrigger(HardLand);
        cameraAnim.SetTrigger(CamHardLand);
    }

    private void OnEnterJump() {
        gunAnim.SetTrigger(Jump);
        dontTriggerFall = true;
    }

    private void OnEnterInAir() {
        if (dontTriggerFall) return;
        if (!Physics.Raycast(transform.position, Vector3.down, out _, MinDistForFall, Masks.GroundMask)) {
            gunAnim.SetTrigger(Fall);
        }
    }

    private void OnExitInAir() {
        if (dontTriggerFall) dontTriggerFall = false;
    }
    
    private void OnEnterMantle() {
        peakHeight = 0f;
    }

    private void OnEnterWallRun() {
        gunAnim.SetTrigger(Run);
        peakHeight = 0f;
        int side = playerController.GetSideOfWallOnWallRun();
        armsAnim.SetBool((side > 0f) ? HideArmRight : HideArmLeft, true);
    }
    
    private void OnExitWallRun() {
        armsAnim.SetBool(HideArmLeft, false);
        armsAnim.SetBool(HideArmRight, false);
    }

    private void OnEnterSlide() {
        gunAnim.SetTrigger(Slide);
        armsAnim.SetBool(HideArmLeft, true);
    }
    
    private void OnExitSlide() {
        armsAnim.SetBool(HideArmLeft, false);
    }

    private void OnEnterZipline() {
        if (!playerWeaponManager.HasWeapon) {
            armsAnim.SetTrigger(Default);
            return;
        }
        playerWeaponManager.ShowEquipedWeapon();
        curWeaponAnim.SetTrigger(Equip);
        armsAnim.SetTrigger(Equip);            
        armsAnim.SetBool(HideArmLeft, true);
    }
    
    private void OnExitZipline() {
        armsAnim.SetBool(HideArmLeft, false);
        armsAnim.SetBool(HideArmRight, false);
    }
    
    private void OnExitClimb() {
        arms.SetActive(true);
        playerWeaponManager.ShowEquipedWeapon();
        if (playerWeaponManager.HasWeapon) {
            armsAnim.SetTrigger(Equip);
            curWeaponAnim.SetTrigger(Equip);
        }
    }
    
    private void CheckForWallRunTilt() {
        float targetAngle = (playerController.CurState == playerController.WallRunning) ? 
            wallRunTiltAngle * playerController.GetSideOfWallOnWallRun() : 0f;
        
        if (camTiltParent.transform.localRotation.z == targetAngle) return;
        
        Quaternion targetRot = Quaternion.AngleAxis(targetAngle, Vector3.forward);
        camTiltParent.transform.localRotation = Quaternion.RotateTowards(camTiltParent.transform.localRotation, targetRot, wallRunTiltSpeed * Time.deltaTime);
    }

    private void CheckForSlideFOV() {
        float targetFov = (playerController.CurState == playerController.Sliding) ? regularMainCamFOV + slideFOVIncrease : regularMainCamFOV;
        if (mainCam.fieldOfView == targetFov) return;
        mainCam.fieldOfView = Mathf.MoveTowards(mainCam.fieldOfView, targetFov, slideFOVSpeed * Time.deltaTime);
    }

    private void AnimateGun() {
        State controllerState = playerController.CurState;
        if (controllerState == playerController.Grounded || controllerState == playerController.WallRunning) {
            float blend = playerController.CurXZSpeed / playerController.RunSpeed; 
            gunAnim.SetFloat(Speed, blend);
            return;
        } 
        
        gunAnim.SetFloat(Speed, 0f);
        
        if (controllerState == playerController.InAir) {
            float ySpeed = playerController.Velocity.y;
            if (ySpeed < 0f && peakHeight == 0f) {
                peakHeight = transform.position.y;
            }
        }
    }

    private void AnimateCamera() {
        float blend = playerController.CurXZSpeed / playerController.RunSpeed; 
        cameraAnim.SetFloat(CamWalkBlend, blend);
    }
    
}
