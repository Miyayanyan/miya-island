using MiyaIsland.Models;
using MiyaIsland.Services;
namespace MiyaIsland.Core.Tests;
public class TaskRetentionTests
{
    [Fact] public void OnlyOldCompletedNonRecurringTasksArePruned()
    {
        var now=new DateTime(2026,10,4);
        var tasks=new List<TaskItem>{new(){Done=true,CompletedAt=now.AddDays(-31)},new(){Done=true,CompletedAt=now.AddDays(-30)},new(){Done=false,CompletedAt=now.AddDays(-31)},new(){Done=true,CompletedAt=now.AddDays(-31),Remind=new(){Repeat=RepeatKind.Daily}}};
        Assert.Equal(1,TaskRetention.Prune(tasks,now,30));Assert.Equal(3,tasks.Count);
    }
    [Fact] public void ForeverRetainsEverything(){var tasks=new List<TaskItem>{new(){Done=true,CompletedAt=DateTime.MinValue}};Assert.Equal(0,TaskRetention.Prune(tasks,DateTime.Now,0));}
}
