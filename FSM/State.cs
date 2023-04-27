using System;
using System.Collections.Generic;

public class State {

    public readonly List<Transition> Transitions = new();

    public Action OnStateUpdateAction;
    public Action OnStateEnterAction;
    public Action OnStateExitAction;

    public State(Action update, Action enter, Action exit) {
        OnStateUpdateAction = update;
        OnStateEnterAction = enter;
        OnStateExitAction = exit;
    }
    
    public Transition To(State state) {
        Transition transition = new(state);
        Transitions.Add(transition);
        return transition;
    }

}