using UnityEngine;

public interface IMagnetic {
    
    public enum Id { Throwable, EnemyWeapon, Zipline, Olive }
    
    public float ChargeCost { get; }
    public Id MagId { get; }

    public T GetComponent<T>();
    public Vector3 GetMagnetTargetPos();
    
}