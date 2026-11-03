using System;

/// <summary>A single fullscreen attempt may complete, cancel, fail or close only once.</summary>
public sealed class RewardedAdAttempt
{
    readonly Action reward;
    readonly Action<bool> closed;
    public bool RewardGranted {get;private set;}
    public bool Closed {get;private set;}
    public RewardedAdAttempt(Action reward,Action<bool> closed){this.reward=reward;this.closed=closed;}
    public bool Grant()
    {
        if(RewardGranted||Closed)return false;
        RewardGranted=true;reward?.Invoke();return true;
    }
    public void Close(){if(Closed)return;Closed=true;closed?.Invoke(RewardGranted);}
}
