using System;

namespace Yudian.Construction;

/// <summary>连续作业计时；未完成时中断清零，完成后保持结果。</summary>
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
