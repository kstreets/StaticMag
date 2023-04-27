using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Animations.Rigging;

public class RegularEnemy : MonoBehaviour {
    
    [SerializeField] private EnemyWeapon[] weapons;
    
    [Header("IK")] 
    [SerializeField] private RigBuilder rigBuilder;

    [Header("Stagger")] 
    [SerializeField] private float staggerAnimTime;
    
    [Header("PainLock")] 
    [SerializeField] private float painLockTime;

    [Header("Punched")] 
    [SerializeField] private float punchStunTime;

    [Header("Sidearm")] 
    [SerializeField] private float sidearmStateTime;
    [SerializeField] private float enableSidearmDelay;
    private Timer enableSidearmTimer;

    private float lastSeenPlayerTime;
        
    private float normalAcc;
    private const float HaltingAcc = 10000f;
    
    private CoverGroup coverGroup;
    private AttackCoordinator attackCoordinator;
    private AnchorPointGroup anchorPointGroup;
    
    private Enemy enemy;
    private NavMeshAgent navAgent;
    private RagdollManager ragdollManager;
    private EnemyRotator rotator;
    private RegularAttackSystem attackSystem;
    private RegularAnimator animator;
    private EnemyWeapon curWeapon;

    private StateMachine stateMachine = new();
    private State entryState;
    private State attackState;
    private State seekCoverState;
    private State repositionState;
    private State coverState;
    private State weaponPulledState;
    private State painLockedState;
    private State grabSidearmState;
    private State punchedState;

    private Vector3 stumbleTargetPos;
    
    private void Awake() {
        enemy = GetComponent<Enemy>();
        navAgent = GetComponent<NavMeshAgent>();
        ragdollManager = GetComponent<RagdollManager>();
        rotator = GetComponent<EnemyRotator>();
        attackSystem = GetComponent<RegularAttackSystem>();
        animator = GetComponent<RegularAnimator>();

        anchorPointGroup = EnemyUtil.GetAnchorPointManager(transform);
        coverGroup = EnemyUtil.GetCoverSystem(transform);
        attackCoordinator = EnemyUtil.GetAttackCoordinator(transform);
        
        normalAcc = navAgent.acceleration;
        
        for (int i = 0; i < weapons.Length; i++) {
            weapons[i].gameObject.SetActive(i == 0);
        }
        
        navAgent.SetDestination(transform.position);

        enemy.OnTakeDamageDir += OnTakeDamage;
        enemy.OnWeaponPulled += OnWeaponPulled;
        enemy.OnHitWithThrowable += OnHitWithThrowable;
        enemy.OnHitWithPunch += OnHitWithPunch;
        enemy.OnDeath += OnDeath;

        entryState = stateMachine.CreateState(null, null, null);
        attackState = stateMachine.CreateState(OnAttackUpdate, OnAttackEnter, OnAttackExit);
        seekCoverState = stateMachine.CreateState(null, OnSeekCoverEnter, OnSeekCoverExit);
        coverState = stateMachine.CreateState(null, OnCoverEnter, OnCoverExit);
        weaponPulledState = stateMachine.CreateState(null, OnWeaponPulledEnter, OnWeaponPulledExit);
        painLockedState = stateMachine.CreateState(null, OnPainLockEnter, OnPainLockExit);
        grabSidearmState = stateMachine.CreateState(null, OnGrabSidearmEnter, OnGrabSidearmExit);
        punchedState = stateMachine.CreateState(OnPunchedUpdate, OnPunchedEnter, OnPunchedExit);
        
        entryState.To(attackState).When(AnyToAttack);
        entryState.To(seekCoverState).When(AnyToSeekCover);
        
        attackState.To(seekCoverState).When(AnyToSeekCover);
        
        seekCoverState.To(attackState).When(AnyToAttack);
        seekCoverState.To(seekCoverState).AfterSeconds(1.5f).When(SeekCoverSelfTransition);
        seekCoverState.To(coverState).When(SeekCoverToCover);

        coverState.To(attackState).AfterSeconds(0.5f).When(AnyToAttack);
        coverState.To(seekCoverState).AfterSeconds(0.5f).When(CoverToSeekCover);
        
        weaponPulledState.To(grabSidearmState).AfterSeconds(staggerAnimTime).When(() => curWeapon == null);
        
        grabSidearmState.To(attackState).AfterSeconds(sidearmStateTime).When(AnyToAttack);
        grabSidearmState.To(seekCoverState).AfterSeconds(sidearmStateTime).When(AnyToSeekCover);
        
        painLockedState.To(grabSidearmState).AfterSeconds(painLockTime).When(() => curWeapon == null);
        painLockedState.To(attackState).AfterSeconds(painLockTime).When(AnyToAttack);
        painLockedState.To(seekCoverState).AfterSeconds(painLockTime).When(AnyToSeekCover);
        
        punchedState.To(grabSidearmState).AfterSeconds(punchStunTime).When(() => curWeapon == null);
        punchedState.To(attackState).AfterSeconds(punchStunTime).When(AnyToAttack);
        punchedState.To(seekCoverState).AfterSeconds(punchStunTime).When(AnyToSeekCover);
    }

    private void Start() {
        curWeapon = weapons[0];
        attackSystem.SetWeapon(curWeapon);
    }

    private void Update() {
        if (!enemy.Alerted) return;
        
        enableSidearmTimer.Tick();
        stateMachine.Tick();
    }
    
    /*------Attack State------*/

    private void OnAttackEnter() {
        attackSystem.EnableAttack();
        rotator.RotateToFacePlayer();
        coverGroup.DropCoverPosition(enemy);

        if (anchorPointGroup.TryGetNewPosition(enemy, out Vector3 pos)) {
            navAgent.SetDestination(pos);
        }
    }

    private void OnAttackExit() {
        attackSystem.CancelAttack();
        anchorPointGroup.DropTakenAnchorPoint(enemy);
    }

    private void OnAttackUpdate() {
        if (EnemyUtil.SimpleCanSeePlayer(enemy.PunchTarget.position)) {
            lastSeenPlayerTime = Time.time;
        }

        const float globalRepositionTime = 2.3f;
        if (!navAgent.pathPending && Time.time - lastSeenPlayerTime >= globalRepositionTime) {
            if (anchorPointGroup.TryGetNewPosition(enemy, out Vector3 pos)) {
                navAgent.SetDestination(pos);
            }
        }
    }
    
    /*------Seek Cover State------*/
    
    private void OnSeekCoverEnter() {
        rotator.RotateToFaceMovementDir();
        navAgent.SetDestination(coverGroup.GetCoverPoint(enemy).position);
        animator.OnSeekCover();
    }

    private void OnSeekCoverExit() {
        animator.OnEndSeekCover();
    }

    /*------Cover State------*/

    private void OnCoverEnter() {
        Transform coverPoint = coverGroup.GetCoverPoint(enemy);
        float dot = Vector3.Dot(transform.forward, coverPoint.right);
        Vector3 faceDir = (dot >= 0) ? coverPoint.right : -coverPoint.right;
        rotator.RotateToFaceDirection(faceDir); 
        animator.StartTakingCover(dot >= 0);
    }

    private void OnCoverExit() {
        animator.StopTakingCover();
    }

    /*------Weapon Pulled State------*/
    
    private void OnWeaponPulledEnter() {
        StopNavAgent();
        rotator.SnapToFacePlayer();
        animator.PlayWeaponPulled();
    }

    private void OnWeaponPulledExit() {
        ContinueNavAgent();
        animator.EndWeaponPulled();
    }

    /*------Grab Sidearm State------*/
    
    private void OnGrabSidearmEnter() {
        StopNavAgent();
        animator.PlayEquipSidearm();
        enableSidearmTimer.SetTime(enableSidearmDelay);
        enableSidearmTimer.endAction ??= () => {
            curWeapon = weapons[1];
            attackSystem.SetWeapon(curWeapon);
            curWeapon.gameObject.SetActive(true);
        };
    }

    private void OnGrabSidearmExit() {
        ContinueNavAgent();
    }

    /*------Pain Lock State------*/
    
    private void OnPainLockEnter() {
        StopNavAgent();
        animator.PlayHitReaction();
    }

    private void OnPainLockExit() {
        ContinueNavAgent();
    }

    /*------Punched State------*/
    
    private void OnPunchedEnter() {
        StopNavAgent();
        rotator.SnapToFacePlayer();
        stumbleTargetPos = transform.position - (transform.forward * 0.5f);
        animator.PlayHitReaction();
    }

    private void OnPunchedExit() {
        ContinueNavAgent();
    }

    private void OnPunchedUpdate() {
        transform.position = Vector3.MoveTowards(transform.position, stumbleTargetPos, 10f * Time.deltaTime);
    }

    private void StopNavAgent() {
        navAgent.acceleration = HaltingAcc;
        navAgent.isStopped = true;
    }

    private void ContinueNavAgent() {
        navAgent.acceleration = normalAcc;
        navAgent.isStopped = false;
    }
    
    private void DropCurrentWeapon() {
        if (curWeapon == null) return;
        
        Vector3 velocity = Vector3.up * 30f;
        
        // Spawn throwable in front of player
        float weaponHeight = curWeapon.transform.position.y - transform.position.y;
        Vector3 dirToPlayer = Vector3Ext.Flatten(PlayerPos.Bottom - transform.position).normalized;
        Vector3 spawnPos = transform.position + (dirToPlayer * 0f) + new Vector3(0f, weaponHeight, 0f);
        
        curWeapon.ThrowableInst.transform.SetPositionAndRotation(spawnPos, curWeapon.transform.rotation);
        curWeapon.ThrowableInst.EnemyThrow(velocity, true);
        curWeapon.gameObject.SetActive(false);
        curWeapon = null;
    }

    private void ThrowUpCurrentWeapon() {
        Vector3 upVelocity = Vector3.up * 48f;
        Vector3 forwardVelocity = (PlayerPos.Bottom - transform.position).normalized * 8f;
        Vector3 velocity = forwardVelocity + upVelocity;
        Transform weaponTrans = curWeapon.transform;
        curWeapon.ThrowableInst.transform.SetPositionAndRotation(weaponTrans.position, weaponTrans.rotation);
        curWeapon.ThrowableInst.EnemyThrow(velocity, true);
        curWeapon.gameObject.SetActive(false);
        curWeapon = null;
    }
    
    //Enemy Events
    
    private void OnTakeDamage(DamageDetails details) {
        stateMachine.SetState(painLockedState);
    }
    
    private void OnWeaponPulled() {
        curWeapon.gameObject.SetActive(false);
        curWeapon = null;
        stateMachine.SetState(weaponPulledState);
    }

    private void OnHitWithThrowable() {
        if (enemy.IsDead) return;
        stateMachine.SetState(painLockedState);
    }

    private void OnHitWithPunch() {
        if (enemy.IsDead) return;
        stateMachine.SetState(punchedState);
    }

    private void OnDeath(DamageDetails details) {
        DropCurrentWeapon();
        ragdollManager.Activate();
        anchorPointGroup.DropTakenAnchorPoint(enemy);
        coverGroup.DropCoverPosition(enemy);
        rigBuilder.enabled = false; 
        attackSystem.enabled = false;
        navAgent.enabled = false;
        animator.enabled = false;
        rotator.enabled = false;
        this.enabled = false;
    }

    // State Transition Conditions
    
    private bool AnyToSeekCover() {
        if (attackCoordinator.IsAttacker(enemy) || attackSystem.Attacking) return false;
        
        Transform coverPoint = coverGroup.GetNewCoverPoint(enemy);
        return (coverPoint != null);
    }

    private bool AnyToAttack() {
        return attackCoordinator.IsAttacker(enemy);
    }

    private bool SeekCoverSelfTransition() {
        if (!coverGroup.CoverPositionIsStillValid(enemy)) {
            Transform coverPoint = coverGroup.GetNewCoverPoint(enemy);
            return (coverPoint != null);
        }
        return false;
    }
    
    private bool SeekCoverToCover() { 
        return (!navAgent.pathPending && navAgent.remainingDistance < 0.1f);
    }
    
    private bool CoverToSeekCover() {
        if (!coverGroup.CoverPositionIsStillValid(enemy)) {
            Transform coverPoint = coverGroup.GetNewCoverPoint(enemy);
            return (coverPoint != null);
        }
        return false;
    }

#if UNITY_EDITOR
    [Header("Debug")]
    public bool drawDebug;
    
    private void OnDrawGizmos() {
        if (!drawDebug || stateMachine.CurState == null) return;
        
        GUIStyle style = new();
        style.fontSize = 25;
        style.normal.textColor = Color.green;
        
        string text = new(stateMachine.CurState.OnStateUpdateAction.ToString());
        Handles.Label(transform.position + new Vector3(0f, 2f, 0f), text, style);
    }
#endif
    
}