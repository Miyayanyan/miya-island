using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
namespace MiyaIsland;
public partial class MainWindow
{
    private readonly DispatcherTimer _autoCollapseDelay=new(){Interval=TimeSpan.FromMilliseconds(350)};
    private readonly HashSet<ContextMenu> _openMenus=[];
    private bool _windowDeactivated;
    private void InitializeAutoCollapse()
    {
        _autoCollapseDelay.Tick+=(_,_)=>{_autoCollapseDelay.Stop();if(CanAutoCollapse())Collapse();};
        Deactivated+=(_,_)=>{_windowDeactivated=true;QueueAutoCollapse();};
        Activated+=(_,_)=>{_windowDeactivated=false;_autoCollapseDelay.Stop();};
        IslandHost.LostKeyboardFocus+=(_,_)=>QueueAutoCollapse();
        IslandHost.PreviewKeyDown+=(_,e)=>
        {if(e.Key!=Key.Escape)return;Keyboard.ClearFocus();FocusManager.SetFocusedElement(this,null);QueueAutoCollapse();e.Handled=true;};
        TasksPanel.PopupActivityChanged+=QueueAutoCollapse;
        foreach(var combo in new[]{RestStyleCombo,BreathingSpeedCombo,FontFamilyCombo})
        {combo.DropDownOpened+=(_,_)=>_autoCollapseDelay.Stop();combo.DropDownClosed+=(_,_)=>QueueAutoCollapse();}
        IslandHost.ContextMenuOpening+=(_,_)=>_autoCollapseDelay.Stop();
        IslandHost.ContextMenuClosing+=(_,_)=>QueueAutoCollapse();
    }
    private bool CanAutoCollapse()=>_appearance.AutoCollapse && _expanded && !IslandHost.IsMouseOver &&
        (!IslandHost.IsKeyboardFocusWithin || _windowDeactivated) && !TasksPanel.IsEditorOpen && !TasksPanel.HasOpenMenu &&
        !RestStyleCombo.IsDropDownOpen && !BreathingSpeedCombo.IsDropDownOpen && !FontFamilyCombo.IsDropDownOpen &&
        !_glassMenuOpen && _openMenus.Count==0 && _activeReminder is null && !_greetingVisible && _compactPress is null && !_compactDragging &&
        IslandHost.ContextMenu?.IsOpen!=true;
    private void QueueAutoCollapse(){_autoCollapseDelay.Stop();_autoCollapseDelay.Start();}
    private void TrackAutoCollapseMenu(ContextMenu menu)
    {
        menu.Opened+=(_,_)=>{_openMenus.Add(menu);_autoCollapseDelay.Stop();};
        menu.Closed+=(_,_)=>{_openMenus.Remove(menu);QueueAutoCollapse();};
    }
}
