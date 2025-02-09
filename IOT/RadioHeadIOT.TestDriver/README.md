The test driver searches for input parameters in the follwoing order:
1. Command line arguments
1. in appsettings.json file
1. the defaults hard coded in the app

The following parameters are supported:
- -- host             Host Device
  - values are Ftx232H | RPi
  - default is Ftx232H
- --select <pin-num>  Device Select Pin:
   - default is 5
- --reset <pin-num>   Reset Pin
   - default is 6
- intr <pin-num>      Interrupt Pin
   - default is 7
- freq <frequency>    Frequency
   - default: 915.0
- level <power-level  PowerLevel
   - default: 20