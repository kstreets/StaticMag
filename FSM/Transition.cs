using System;

public class Transition {
    
    public State NextState { get; }
    public float Seconds { get; private set; }
    private Func<bool> condition;

    public Transition(State nextState) {
        this.NextState = nextState;
    }
    
    public bool EvaluateTransition() {
        if (condition == null) return true;
        return condition.Invoke();
    }
    
    public void When(Func<bool> condition) {
        this.condition = condition;
    }
    
    public Transition AfterSeconds(float seconds) {
        Seconds = seconds;
        return this;
    }
    
}