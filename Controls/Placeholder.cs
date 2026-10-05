using System.Windows;

namespace MiyaIsland.Controls;

// 模板自行根据 Text 和焦点状态显示占位文字，无事件订阅。
public static class Placeholder
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.RegisterAttached(
        "Text", typeof(string), typeof(Placeholder), new FrameworkPropertyMetadata(string.Empty));

    public static string GetText(DependencyObject element) => (string)element.GetValue(TextProperty);

    public static void SetText(DependencyObject element, string value) =>
        element.SetValue(TextProperty, value);
}
