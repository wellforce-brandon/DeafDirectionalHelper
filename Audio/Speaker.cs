using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace DeafDirectionalHelper.Audio;

public class Speaker : INotifyPropertyChanged
{
    // How many time in milliseconds the value will stay at its highest value
    //
    // Why? To highlight the sound direction at their highest, so it gives
    // me this time to read the information in the middle of a fight.
    private const long HighestDelayMs = 1000;

    private readonly object _lock = new();
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();

    private float _value;
    private long _holdUntilMs;

    public float Value
    {
        get { lock (_lock) { return _value; } }
        set
        {
            bool changed = false;
            lock (_lock)
            {
                var now = _stopwatch.ElapsedMilliseconds;

                // If new value is lower and we're still in hold period, ignore it
                if (value < _value && now < _holdUntilMs)
                    return;

                // If new value is higher, extend the hold period
                if (value > _value)
                    _holdUntilMs = now + HighestDelayMs;

                _value = value;
                changed = true;
            }

            if (changed)
                OnPropertyChanged();
        }
    }

    public static implicit operator float(Speaker s) { lock (s._lock) { return s._value; } }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
