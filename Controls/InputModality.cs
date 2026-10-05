using System.Windows;
using System.Windows.Input;
namespace MiyaIsland.Controls;

/// <summary>Focus remains available to controls; its visual is shown only after keyboard navigation.</summary>
public sealed class InputModality : DependencyObject
{
    public static InputModality State {get;}=new();
    public static readonly DependencyProperty KeyboardNavigationProperty=DependencyProperty.Register(nameof(KeyboardNavigation),typeof(bool),typeof(InputModality),new PropertyMetadata(false));
    public bool KeyboardNavigation {get=>(bool)GetValue(KeyboardNavigationProperty);private set=>SetValue(KeyboardNavigationProperty,value);}
    private static bool _initialized;
    public static void Initialize()
    {
        if(_initialized)return;_initialized=true;
        InputManager.Current.PreProcessInput+=(_,e)=>
        {
            if(e.StagingItem.Input is MouseButtonEventArgs {ButtonState:MouseButtonState.Pressed})State.KeyboardNavigation=false;
            else if(e.StagingItem.Input is KeyEventArgs {IsDown:true} key && key.Key is Key.Tab or Key.Left or Key.Right or Key.Up or Key.Down)State.KeyboardNavigation=true;
        };
    }
}
