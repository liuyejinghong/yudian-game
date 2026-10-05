using System;

namespace Yudian.Construction;

/// <summary>
/// level-job-r1 GLM包B：连续有效施工计时器。
/// 未完成时 eligible=false 立即清零（连续有效施工，中断不保留进度）；
/// eligible=true 累计并封顶 requiredSeconds；完成后任何合法 Advance 保持完成。
/// 新任务使用新实例；本类型不持有任务/权限/世界状态，也不提供重置。
/// </summary>
public sealed class WorkMeter
{
    private readonly double _requiredSeconds;
    private double _elapsedSeconds;

    public WorkMeter(double requiredSeconds)
    {
        if (!double.IsFinite(requiredSeconds) || requiredSeconds <= 0 || requiredSeconds > 3600)
        {
            throw new ArgumentException(
                $"requiredSeconds must be finite in (0, 3600], got {requiredSeconds}.",
                nameof(requiredSeconds));
        }

        _requiredSeconds = requiredSeconds;
    }

    public double ElapsedSeconds => _elapsedSeconds;

    /// <summary>进度比例，恒在 [0, 1]；完成时恰为 1。</summary>
    public double Fraction => _elapsedSeconds / _requiredSeconds;

    public bool IsComplete => _elapsedSeconds >= _requiredSeconds;

    public void Advance(double deltaSeconds, bool eligible)
    {
        if (!double.IsFinite(deltaSeconds) || deltaSeconds < 0 || deltaSeconds > 3600)
        {
            throw new ArgumentException(
                $"deltaSeconds must be finite in [0, 3600], got {deltaSeconds}.",
                nameof(deltaSeconds));
        }

        if (IsComplete)
        {
            return;
        }

        if (!eligible)
        {
            _elapsedSeconds = 0;
            return;
        }

        _elapsedSeconds = Math.Min(_elapsedSeconds + deltaSeconds, _requiredSeconds);
    }
}
