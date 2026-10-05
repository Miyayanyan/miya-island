namespace MiyaIsland.Services;

/// <summary>Wait for one uninterrupted second of availability, with a three minute launch deadline.</summary>
public sealed class StartupGreetingQueue(TimeProvider? clock=null)
{
    private readonly TimeProvider _clock=clock??TimeProvider.System;
    private DateTimeOffset _deadline,_initial;
    private DateTimeOffset? _availableSince;
    public bool Pending {get;private set;}
    public void Start()
    {var now=_clock.GetUtcNow();_deadline=now.AddMinutes(3);_initial=now.AddSeconds(1.5);_availableSince=null;Pending=true;}
    public bool Poll(bool blocked)
    {
        if(!Pending)return false;
        var now=_clock.GetUtcNow();if(now>=_deadline){Pending=false;return false;}
        if(blocked){_availableSince=null;return false;}
        _availableSince??=now;
        if(now<_initial || now-_availableSince.Value<TimeSpan.FromSeconds(1))return false;
        Pending=false;return true;
    }
}
