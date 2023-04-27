using UnityEngine;

public class CamController : MonoBehaviour {

    public PlayerController playerController;
    public float mouseSensitivity;

    [SerializeField] private float zipFaceTime;
    [SerializeField] private float wallRunCurveRotSpeed;

    private bool lockYAngle;
    private Vector3 intoWallDir;

    private float wallRunCurveDir;
    private float wallRunCurveAngle;

    private Timer trackTimer;
    private Transform trackTrans;
    private Vector3 trackPos;
    private float trackSpeed;

    private Vector3 faceDir;
    private Quaternion initialFaceDir;
    private Timer faceDirTimer;

    public float CamXAngle { get; private set; }
    private float camYAngle;

    private void OnEnable() {
        Evnts.OnEnterZipline.Subscribe(OnZipline);
        Evnts.OnPreOliveYank.Subscribe(OnPreOliveYank);
        Evnts.OnOlivePunch.Subscribe(OnOlivePunch);
        Evnts.OnPreZiplineYank.Subscribe(OnPreZiplineYank);
    }

    private void OnDisable() {
        Evnts.OnEnterZipline.Unsubscribe(OnZipline);
        Evnts.OnPreOliveYank.Unsubscribe(OnPreOliveYank);
        Evnts.OnOlivePunch.Unsubscribe(OnOlivePunch);
        Evnts.OnPreZiplineYank.Unsubscribe(OnPreZiplineYank);
    }

    private void Update() {
        if (!trackTimer.Tick()) {
            TrackUpdate();
            return;
        }
        if (!faceDirTimer.Tick()) {
            FaceDirUpdate();    
            return;
        }
        RotateCamera();
    }

    public void AdjustCameraXAngle(float angle) {
        CamXAngle += angle;
    }

    public void AdjustCameraYAngle(float angle) {
        camYAngle += angle;
    }
    
    public void OnWallRunNormalChange(Vector3 newWallRunNormal, Vector3 lastWallRunNormal) {
        lockYAngle = false;
        intoWallDir = -Vector3Ext.Flatten(newWallRunNormal).normalized;
        wallRunCurveAngle = 0f;

        // Calulate for wall curves 
        if (newWallRunNormal != Vector3.zero && lastWallRunNormal != Vector3.zero) {
            float adjustAngle = Vector3.SignedAngle(lastWallRunNormal, newWallRunNormal, Vector3.up);
            wallRunCurveDir = Mathf.Sign(adjustAngle);
            wallRunCurveAngle = Mathf.Abs(adjustAngle);
        }
    }
    
    public void TrackTrans(Transform trans, float speed, float time) {
        trackTrans = trans;
        trackSpeed = speed;
        trackTimer.SetTime(time);
        trackPos = Vector3.zero;
    }

    public void TrackPosition(Vector3 position, float speed, float time) {
        trackPos = position;
        trackSpeed = speed;
        trackTimer.SetTime(time);
    }

    private void TrackUpdate() {
        Vector3 pos = (trackPos == Vector3.zero) ? trackTrans.position : trackPos;
        Vector3 lookDir = (pos - transform.position).normalized;
        Quaternion targetRot = Quaternion.LookRotation(lookDir, Vector3.up);
        Quaternion worldRot = Quaternion.RotateTowards(transform.rotation, targetRot, trackSpeed * Time.deltaTime);

        Quaternion localRot = Quaternion.Inverse(transform.parent.rotation) * worldRot;
        Vector3 localAngles = localRot.eulerAngles;
        
        // Don't rotate z axis
        transform.localEulerAngles = new(localAngles.x, localAngles.y, 0f);
        CamXAngle = ConvertAngleTo180Range(localAngles.x) % 89f;
        camYAngle = localAngles.y;
    }

    private void FaceDirUpdate() {
        Quaternion worldRot = Quaternion.Lerp(initialFaceDir, Quaternion.LookRotation(faceDir), faceDirTimer.Comp());
        
        Quaternion localRot = Quaternion.Inverse(transform.parent.rotation) * worldRot;
        Vector3 localAngles = localRot.eulerAngles;
        
        // Don't rotate z axis
        transform.localEulerAngles = new(localAngles.x, localAngles.y, 0f);
        CamXAngle = ConvertAngleTo180Range(localAngles.x) % 89f;
        camYAngle = localAngles.y;
    }

    // Transfroms 0 to 360 angle into -180 to 180
    private float ConvertAngleTo180Range(float angle) {
        float newAngle = angle % 360f;
        if (newAngle > 180f) newAngle -= 360f;
        return newAngle;
    }

    // Keeps angle in range of 0 to 360
    private float WrapAt360(float angle) {
        if (angle > 360f) return angle - 360f;
        if (angle < 0f) return angle + 360f;
        return angle;
    }

    private void RotateCamera() {
        float mouseX = Input.GetAxisRaw("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxisRaw("Mouse Y") * mouseSensitivity;

        CalculateYAngle(mouseX);
        CamXAngle = Mathf.Clamp(CamXAngle - mouseY, -89f, 89f);
        transform.localRotation = Quaternion.AngleAxis(camYAngle, Vector3.up) * Quaternion.AngleAxis(CamXAngle, Vector3.right);
    }

    private void CalculateYAngle(float mouseX) {
        if (playerController.CurState != playerController.WallRunning) {
            camYAngle = WrapAt360(camYAngle += mouseX);
            return;
        }
        
        // Don't wrap Y angle because it makes adjustments overcomplicated
        camYAngle += mouseX;

        // Rotate Y angle along the curve of the wall
        if (wallRunCurveAngle > 0f) {
            float curveRotSpeed = wallRunCurveRotSpeed * Time.deltaTime;
            float adjustAmount = (wallRunCurveAngle - curveRotSpeed > 0) ? curveRotSpeed : wallRunCurveAngle;
            wallRunCurveAngle -= adjustAmount;
            camYAngle += adjustAmount * wallRunCurveDir;
        }

        // Rotate / Clamp Y angle so we look away from the wall
        Vector3 updatedForward = Quaternion.AngleAxis(camYAngle, Vector3.up) * Vector3.forward;
        float wallAngle = Vector3.SignedAngle(intoWallDir, updatedForward, Vector3.up);

        if (lockYAngle && Mathf.Abs(wallAngle) < 90f) {
            camYAngle += (90f - Mathf.Abs(wallAngle)) * Mathf.Sign(wallAngle);
            return;
        }

        if (Mathf.Abs(wallAngle) < 90f) {
            camYAngle += 70f * Mathf.Sign(wallAngle) * Time.deltaTime;
        }
        else {
            lockYAngle = true;
        }
    }

    private void OnPreOliveYank(Olive olive) {
        TrackTrans(olive.transform, 150f, Mathf.Infinity);
    }
    
    private void OnOlivePunch() {
        trackTimer.Stop();
    }

    private void OnPreZiplineYank(Zipline zipline) {
        TrackPosition(zipline.CachedTargetPos, 150f, 0.1f);
    }

    private void OnZipline() {
        if (playerController.Zipline.IsVertical()) return;
                
        initialFaceDir = transform.rotation;
        faceDir = playerController.ZipMoveDir;
        faceDirTimer.SetTime(zipFaceTime);
    }
    
}