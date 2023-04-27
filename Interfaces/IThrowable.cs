using UnityEngine;

public interface IThrowable {
    
    public enum ThrowableId { Auj, Pds57, Nds12, Cp61, Lmg, Tick, Sniper }
    
    public ThrowableId ThrowId { get; }
    public Transform transform { get; } 
    
    public void Throw(Vector3 velocity);
    public void OnPulled();
    public void OnPickUp();
    public void OnDrop();
    
}