using System.Device;
using System.Device.Gpio;
using System.Diagnostics.CodeAnalysis;

namespace RadioHeadNet.Tests;

/// <summary>
/// GPIO driver for the unit testing
/// </summary>
[ExcludeFromCodeCoverage]
internal class GpioDriverFake : GpioDriver
{
    private class PinState(int pinNumber)
    {
        private PinValue _value = PinValue.Low;

        public event PinChangeEventHandler? Callback;

        public int PinNumber { get; } = pinNumber;

        public PinMode Mode { get; set; }

        public bool IsOpen { get; set; }

        public PinValue Value
        {
            get => _value;
            set
            {
                if (value.Equals(_value))
                {
                    return;
                }

                _value = value;
            }
        }

        public PinEventTypes EventModes { get; set; }

        public bool CallbacksExist => Callback != null;

        public void FireCallback(object sender, PinValueChangedEventArgs pinValueChangedEventArgs)
        {
            Callback?.Invoke(sender, pinValueChangedEventArgs);
        }
    }

    private readonly IList<PinState> _pins = new List<PinState>();

    /// <inheritdoc/>
    protected sealed override int PinCount => 8;

    /// <summary>
    /// Creates a GPIO Driver
    /// </summary>
    internal GpioDriverFake()
    {
        for (var i = 0; i < PinCount; i++)
        {
            _pins.Add(new PinState(i));
        }
    }

    private void ValidatePinNumber(int pinNumber)
    {
        if (pinNumber < 0 || pinNumber >= PinCount)
        {
            throw new ArgumentException($"Pin number can only be between 0 and {PinCount - 1}");
        }
    }

    /// <inheritdoc/>
    protected override int ConvertPinNumberToLogicalNumberingScheme(int pinNumber) => pinNumber;

    /// <inheritdoc/>
    protected override void OpenPin(int pinNumber)
    {
        ValidatePinNumber(pinNumber);

        _pins[pinNumber].IsOpen = true;
    }

    /// <inheritdoc/>
    protected override void ClosePin(int pinNumber)
    {
        ValidatePinNumber(pinNumber);

        _pins[pinNumber].IsOpen = true;
    }

    /// <inheritdoc/>
    protected override void SetPinMode(int pinNumber, PinMode mode)
    {
        ValidatePinNumber(pinNumber);

        _pins[pinNumber].Mode = mode;
    }

    /// <inheritdoc/>
    protected override PinMode GetPinMode(int pinNumber)
    {
        ValidatePinNumber(pinNumber);

        return _pins[pinNumber].Mode;
    }

    /// <inheritdoc/>
    protected override bool IsPinModeSupported(int pinNumber, PinMode mode)
    {
        return true;
    }

    /// <inheritdoc/>
    protected override PinValue Read(int pinNumber)
    {
        ValidatePinNumber(pinNumber);

        if (!_pins[pinNumber].IsOpen)
        {
            throw new InvalidOperationException($"Pin {pinNumber} is not open");
        }

        return _pins[pinNumber].Value;
    }

    /// <inheritdoc/>
    protected override void Toggle(int pinNumber) => Write(pinNumber, !_pins[pinNumber].Value);

    /// <inheritdoc/>
    protected override void Write(int pinNumber, PinValue value)
    {
        ValidatePinNumber(pinNumber);
        if (!_pins[pinNumber].IsOpen)
        {
            throw new InvalidOperationException($"Pin {pinNumber} is not open");
        }

        _pins[pinNumber].Value = value;
        var changeType = value == PinValue.High ? PinEventTypes.Rising : PinEventTypes.Falling;
        _pins[pinNumber].FireCallback(this, new PinValueChangedEventArgs(changeType, pinNumber));
    }

    /// <inheritdoc/>
    protected override WaitForEventResult WaitForEvent(int pinNumber, PinEventTypes eventTypes, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc/>
    protected override void AddCallbackForPinValueChangedEvent(int pinNumber, PinEventTypes eventTypes, PinChangeEventHandler callback)
    {
        _pins[pinNumber].Value = Read(pinNumber);
        _pins[pinNumber].EventModes = _pins[pinNumber].EventModes | eventTypes;
        _pins[pinNumber].Callback += callback;
    }

    /// <inheritdoc/>
    protected override void RemoveCallbackForPinValueChangedEvent(int pinNumber, PinChangeEventHandler callback)
    {
        _pins[pinNumber].Callback -= callback;
        if (!_pins[pinNumber].CallbacksExist)
        {
            _pins[pinNumber].EventModes = PinEventTypes.None;
        }
    }

    /// <inheritdoc />
    public override ComponentInformation QueryComponentInformation()
    {
        var ret = new ComponentInformation(this, "Fake driver for unit testing.");
        return ret;
    }

    public void ExternalChange(int pinNumber, PinValue value)
    {
        ValidatePinNumber(pinNumber);

        var pin = _pins[pinNumber];
        if (!pin.IsOpen ||
            (pin.Mode != PinMode.Input &&
             pin.Mode != PinMode.InputPullDown &&
             pin.Mode != PinMode.InputPullUp))
            return;

        if (pin.Value.Equals(value))
            return;

        pin.Value = value;
        var changeType = value == PinValue.High ? PinEventTypes.Rising : PinEventTypes.Falling;
        pin.FireCallback(this, new PinValueChangedEventArgs(changeType, pin.PinNumber));
    }
}