using MiyaIsland.Services;
namespace MiyaIsland.Core.Tests;
public class StartupGreetingQueueTests
{
    private sealed class Clock:TimeProvider {public DateTimeOffset Now=DateTimeOffset.Parse("2026-10-04T00:00:00Z");public override DateTimeOffset GetUtcNow()=>Now;public void Advance(double seconds)=>Now=Now.AddSeconds(seconds);}
    [Fact] public void BlockedGreetingWaitsAndAppearsAfterOneFreeSecond()
    {var clock=new Clock();var queue=new StartupGreetingQueue(clock);queue.Start();clock.Advance(1.5);Assert.False(queue.Poll(true));clock.Advance(20);Assert.True(queue.Pending);Assert.False(queue.Poll(false));clock.Advance(.9);Assert.False(queue.Poll(false));clock.Advance(.1);Assert.True(queue.Poll(false));Assert.False(queue.Pending);Assert.False(queue.Poll(false));}
    [Fact] public void ReturningReminderRestartsAvailabilityDelay()
    {var clock=new Clock();var queue=new StartupGreetingQueue(clock);queue.Start();clock.Advance(2);Assert.False(queue.Poll(false));clock.Advance(.8);Assert.False(queue.Poll(true));clock.Advance(1);Assert.False(queue.Poll(false));clock.Advance(1);Assert.True(queue.Poll(false));}
    [Fact] public void ThreeMinuteDeadlineAbandonsPendingGreeting()
    {var clock=new Clock();var queue=new StartupGreetingQueue(clock);queue.Start();clock.Advance(180);Assert.False(queue.Poll(false));Assert.False(queue.Pending);}
}
