using System.Windows.Media.Animation;

namespace MiyaIsland.Controls;

/// <summary>
/// 欠阻尼弹簧缓动：在 t = 1 时基本静止，默认参数约有 2% 的过冲。
/// 用于小岛展开，让变形有一点“弹性”但不晃。
/// </summary>
public sealed class SpringEase : IEasingFunction
{
    public double DampingRatio { get; init; } = 0.78;
    public double AngularFrequency { get; init; } = 7.05;

    public double Ease(double normalizedTime)
    {
        if (normalizedTime <= 0) return 0;
        if (normalizedTime >= 1) return 1;

        var zeta = DampingRatio;
        var omega = AngularFrequency;
        var damped = omega * Math.Sqrt(1 - zeta * zeta);
        var decay = Math.Exp(-zeta * omega * normalizedTime);
        return 1 - decay * (Math.Cos(damped * normalizedTime) + zeta * omega / damped * Math.Sin(damped * normalizedTime));
    }
}
