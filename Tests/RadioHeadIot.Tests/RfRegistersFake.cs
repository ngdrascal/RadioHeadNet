using System.Device.Gpio;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;

namespace RadioHeadIot.Tests;

[ExcludeFromCodeCoverage]
internal class RfRegistersFake
{
    private class Register
    {
        public byte Value { get; set; }
        public int ReadCount { get; set; }
        public int WriteCount { get; set; }
        public Action<int>? OnReadAction { get; set; }
        public Action<int>? AfterReadAction { get; set; }
        public Action<int>? AfterWriteAction { get; set; }
    }

    private readonly GpioPin _chipSelectPin;

    private enum States { Waiting, Ready, Reading, Writing }

    private readonly ILogger _regLogger;
    private readonly ILogger _stateLogger;

    private readonly Register[] _registers = new Register[byte.MaxValue];

    private byte _registerIndex;

    private States _internalState;
    private States State
    {
        get => _internalState;
        set
        {
            _stateLogger.LogDebug("state: {0}->{1}", _internalState, value);
            _internalState = value;
        }
    }

    public RfRegistersFake(byte[] initialRegValues, GpioPin chipSelectPin, ILoggerFactory loggerFactory)
    {
        _chipSelectPin = chipSelectPin;
        _regLogger = loggerFactory.CreateLogger(nameof(Rf69RegistersFake) + ".registers");
        _stateLogger = loggerFactory.CreateLogger(nameof(Rf69RegistersFake) + ".states");

        _chipSelectPin.ValueChanged += (_, args) =>
        {
            if (args.ChangeType == PinEventTypes.Falling)
            {
                State = States.Ready;
            }
            else if (args.ChangeType == PinEventTypes.Rising)
            {
                State = States.Waiting;
            }
        };

        for (var i = 0; i < _registers.Length; i++)
        {
            _registers[i] = new Register();
        }

        for (var i = 0; i < initialRegValues.Length; i++)
        {
            _registers[i].Value = initialRegValues[i];
        }
    }

    public byte Read()
    {
        byte result = 0;

        if (State != States.Reading)
        {
            return result;
        }

        var targetReg = _registers[_registerIndex];

        targetReg.OnReadAction?.Invoke(targetReg.ReadCount);

        result = targetReg.Value;

        _regLogger.LogDebug("reg[{0}] == {1}", _registerIndex.ToString("X2"), result.ToString("X2"));

        targetReg.ReadCount++;

        targetReg.AfterReadAction?.Invoke(targetReg.ReadCount);

        // if targeting the FIFO register, do not auto-increment the register index
        // NOTE: the register index can roll over from 255 to 0
        if (_registerIndex != 0)
            _registerIndex++;

        return result;
    }

    public void Write(byte value)
    {
        if (State == States.Ready)
        {
            State = (value & 0x80) == 0x80 ? States.Writing : States.Reading;
            _registerIndex = (byte)(value & 0x7F);
        }
        else if (State == States.Writing)
        {
            _regLogger.LogDebug("reg[{0}] <- {1}", _registerIndex.ToString("X2"), value.ToString("X2"));

            var targetReg = _registers[_registerIndex];
            targetReg.Value = value;
            targetReg.WriteCount++;

            targetReg.AfterWriteAction?.Invoke(targetReg.WriteCount);

            // if targeting the FIFO register, do not auto-increment the register index
            // NOTE: the register index can roll over from 255 to 0
            if (_registerIndex != 0)
            {
                _registerIndex++;
            }
        }
        else if (State == States.Reading)
        {
            // do nothing
        }
    }

    public byte Peek(byte regIndex)
    {
        return _registers[regIndex].Value;
    }

    public void Poke(byte regIndex, byte value)
    {
        _registers[regIndex].Value = value;
    }

    public void DoOnRead(byte regIndex, Action<int> action)
    {
        _registers[regIndex].OnReadAction = action;
    }

    public void DoAfterRead(byte regIndex, Action<int> action)
    {
        _registers[regIndex].AfterReadAction = action;
    }

    public void DoAfterWrite(byte regIndex, Action<int> action)
    {
        _registers[regIndex].AfterWriteAction = action;
    }

    public int ReadCount(byte regIndex)
    {
        return _registers[regIndex].ReadCount;
    }

    public int WriteCount(byte regIndex)
    {
        return _registers[regIndex].WriteCount;
    }
}
