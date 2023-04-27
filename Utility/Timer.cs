using System;
using UnityEngine;

public struct Timer {

    public float CurTime { get; private set; }
    public Action endAction;
    public Action updateAction;

    private float startTime;

    public bool IsFinished => (CurTime <= 0f); 
    
    public bool Tick() {
        if (CurTime <= 0f) return true;

        CurTime -= Time.deltaTime;
        updateAction?.Invoke();
        
        if (CurTime > 0f) return false;
        
        endAction?.Invoke();
        return false;
    }

    public void SetTime(float newTime) {
        CurTime = newTime;
        startTime = newTime;
    }

    public void Stop() {
        CurTime = 0f;
    }

    public float Comp() {
        return Mathf.Clamp(1f - (CurTime / startTime), 0f, 1f);
    }
    
    public float InvComp() {
        return Mathf.Clamp(CurTime / startTime, 0f, 1f);
    }

}