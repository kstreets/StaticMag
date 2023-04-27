using UnityEngine;

public struct Limitter {

    private float lastTime;

    public bool Limit(float time) {
        if (Time.time - lastTime < time) return false;
        lastTime = Time.time;
        return true;
    }

}
