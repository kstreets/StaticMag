using System;
using System.Collections.Generic;
using UnityEngine;

public class StateMachine {

    public State CurState { get; private set; }
    public State PrevState { get; private set; }

    private List<State> states = new();
    private List<Transition> anyStateTransitions = new();
    private float timeSinceStateStart;

    public State CreateState(Action update, Action enter, Action exit) {
        State newState = new(update, enter, exit);
        if (states.Count == 0) {
            CurState = newState;
            PrevState = newState;
            CurState.OnStateEnterAction?.Invoke();
        }
        states.Add(newState);
        return newState;
    }

    public Transition FromAny(State state) {
        Transition transition = new(state);
        anyStateTransitions.Add(transition);
        return transition;
    }

    public void SetState(State state) {
        PrevState = CurState;
        CurState = state;
        PrevState.OnStateExitAction?.Invoke();
        CurState.OnStateEnterAction?.Invoke();
        timeSinceStateStart = 0f;
    }

    public bool SetStateIfNotCurrent(State state) {
        if (CurState == state) return false;
        SetState(state);
        return true;
    }
    
    public void Tick() {
        timeSinceStateStart += Time.deltaTime;
        UpdateState(anyStateTransitions);
        UpdateState(CurState.Transitions);
        CurState.OnStateUpdateAction?.Invoke();
    }

    private void UpdateState(List<Transition> transitions) {
        foreach (Transition transition in transitions) {
            if (timeSinceStateStart >= transition.Seconds && transition.EvaluateTransition()) {
                SetState(transition.NextState);    
                break;
            }
        }
    }
    
}