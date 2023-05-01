using UnityEngine;
using Random = UnityEngine.Random;

public class PlayerAudio : MonoBehaviour {

    [Header("Sources")]
    [SerializeField] private AudioSource mainSource;
    [SerializeField] public AudioSource gunSource;
    
    [Header("Movement")] 
    [SerializeField] private AudioClip[] footStepClips;
    [SerializeField] private AudioClip jumpClip;
    [SerializeField] private AudioClip landClip;

    [Header("Gunshot Volume Reduction")] 
    [SerializeField] private float minShotVolume;
    [SerializeField] private float perShotVolumeReduction;
    [SerializeField] private float timeBeforeVolumeReset;

    [Header("Arm Whooshes")] 
    [SerializeField] private AudioClip[] armWhooshes;

    [Header("Magnet")] 
    [SerializeField] private AudioClip magnetPullClip;

    [Header("Impacts")]
    [SerializeField] private AudioClip hitMarkerClip;
    
    [Header("Misc")] 
    [SerializeField] private AudioClip takeDamageClip;

    private Timer footStepTimer;
    private const float footStepDelay = 0.4f;

    private Timer rapidGunShotTimer;
    private float initGunSourceVolume;

    private PlayerController playerController;

    private void Start() {
        playerController = GetComponent<PlayerController>();
        initGunSourceVolume = gunSource.volume;
    }

    private void OnEnable() {
        Evnts.OnEnterGrounded.Subscribe(OnEnterGrounded);
        Evnts.OnEnterSlide.Subscribe(OnEnterSlide);
        Evnts.OnEnterJump.Subscribe(OnEnterJump);
        Evnts.OnPrePull.Subscribe(OnPrePull);
        Evnts.OnThrow.Subscribe(OnThrow);
        Evnts.OnEnemyDeath.Subscribe(OnEnemyDeath);
        Evnts.OnTakeDamage.Subscribe(OnTakeDamage);
    }
    
    private void OnDisable() {
        Evnts.OnEnterGrounded.Unsubscribe(OnEnterGrounded);
        Evnts.OnEnterSlide.Unsubscribe(OnEnterSlide);
        Evnts.OnEnterJump.Unsubscribe(OnEnterJump);
        Evnts.OnPrePull.Unsubscribe(OnPrePull);
        Evnts.OnThrow.Unsubscribe(OnThrow);
        Evnts.OnEnemyDeath.Unsubscribe(OnEnemyDeath);
        Evnts.OnTakeDamage.Unsubscribe(OnTakeDamage);
    }

    public void Update() {
        PlayFootStep();
        if (rapidGunShotTimer.Tick()) {
            gunSource.volume = initGunSourceVolume;
        }
    }

    public void PlayOneShotOnGunAudioSource(AudioClip clip) {
        rapidGunShotTimer.SetTime(timeBeforeVolumeReset);
        gunSource.volume = Mathf.Clamp(gunSource.volume - perShotVolumeReduction, minShotVolume, initGunSourceVolume);
        gunSource.pitch = Random.Range(0.95f, 1.05f);
        gunSource.PlayOneShot(clip);
    }
    
    public void PlayArmWhooshSound() {
        mainSource.PlayOneShot(GetRandomClip(armWhooshes), 0.7f);
    }

    private void OnEnterGrounded() {
        mainSource.PlayOneShot(landClip);
        footStepTimer.SetTime(footStepDelay);
    }
    
    private void OnEnterSlide() {
        mainSource.PlayOneShot(landClip);
        footStepTimer.SetTime(footStepDelay);
    }

    private void OnEnterJump() {
        mainSource.PlayOneShot(jumpClip);
    }

    private void OnPrePull(IMagnetic.Id id) {
        mainSource.PlayOneShot(magnetPullClip);
    }

    private void OnThrow() {
        mainSource.PlayOneShot(GetRandomClip(armWhooshes), 0.7f);
    }

    private void OnEnemyDeath() {
        mainSource.PlayOneShot(hitMarkerClip, 1.3f);
    }

    private void OnTakeDamage() {
        mainSource.PlayOneShot(takeDamageClip);
    }

    private void PlayFootStep() {
        State curState = playerController.CurState;
        if ((curState != playerController.Grounded && curState != playerController.WallRunning) || playerController.CurXZSpeed == 0f) {
            footStepTimer.SetTime(0f);
            return;
        }
        if (footStepTimer.Tick()) {
            mainSource.PlayOneShot(GetRandomClip(footStepClips), 2f);
            footStepTimer.SetTime(footStepDelay);
        }
    }
   
    private AudioClip GetRandomClip(AudioClip[] clips) {
        int index = Random.Range(0, footStepClips.Length);
        return clips[index];
    }
    
}
