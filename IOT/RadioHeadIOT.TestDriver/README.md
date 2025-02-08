The test driver searches for input parameters in the follwoing order:
1. Command line arguments
1. in appsettings.json file
1. the defaults hard coded in the app

The following parameters are supported:
- HostDevice
  - values are Ftx232H | RPi
  - default is Ftx232H
- DeviceSelectPin:
   - default is 5
- ResetPin
   - default is 6
- InterruptPin
   - default is 7
- Frequency
   - default: 915.0
- PowerLevel
   - default: 20