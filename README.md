# SE_Drive_Wheels
Space Engineers: Controller for wheel drive augmentation.

> This project is configured to be built within a separate IDE to allow for error checking, code completion etc..
> Only the section in `#region CodeEditor` should be copied into the Space Engineers editor. This region has been automatically extracted into the corresponding `txt` file.

##Description
A lightweight, real-time script to improve the handling and versatility of wheeled vehicles.

To be effective, this script should be run at the full physics simulation speed of 60Hz.
This may be achieved by setting a Timer Block to "Trigger [self]" and "Run [program]".

### Automatic Handbrake
Automatically applies the handbrake whenever the vehicle is not under control.

### Traction Control
Eliminates wheel-spin, reducing twitchiness and increasing grip.
Manages brakes to enforce maximum speed. 

### Gearbox
Configurable gears allow speed and power to be controlled.
+ Automatic mode will select gears based on vehicle speed. (The lowest gear also applies when reversing.)
+ Manual mode allows the gear to be chosen.

To use manual gears, the command bus must be used.
This is because a program called at the full physics speed may get a chance to process any additional runs, and so any arguments passed are lost.
The [Commands][link-Commands] script should be run with arguments matching the `busId` values in the `Definitions` section, reproduced below, to place the commands in the command bus for the [Controller][link-Main] script to read.

[link-Main]:./Drive_Controller_Wheels.txt
[link-Commands]:./Drive_Controller_Wheels_Commands.txt

| Argument      | Effect        |
| ------------- | ------------- |
| `GearAuto`    | Switch between automatic and manual gearbox modes
| `GearUp`      | Change up one gear (and use manual gearbox)
| `GearDown`    | Change down one gear (and use manual gearbox)

##Known Issues
+ The Traction Control overriding the wheel brakes prevents the normal `brake` key binding from working. As a workaround, you may still use the handbrake.

##Hardware
| Block(s)      | number        | Configurable  |
| ------------- | ------------- | ------------- |
| Ship Controller | single        | by name constant
| Wheel Suspensions | [all]         | no*
| Text Panel    | single        | by name constant
*but limited by the algorithm to those on the same grid as the Ship Controller

##Configuration
+ `nameController`: the name of the Ship Controller used to identify the controlled grid, and manage the handbrake
+ `nameBusCommand`: the name of the Text/LCD Panel used as a buffer for Gearbox commands
+ `commandsMax` : the maximum number of commands that will be read from the command bus each time the script is executed
+ `gears` : the gears that the vehicle may use - configurable Speed (kmph) and Power (%)

##Standard Blocks
+ `MotorSuspension`: IMyMotorSuspension API wrapper for setting Wheel Suspension values
+ `ShipController`: IMyShipController API wrapper for setting Ship Controller values
+ `CommandBus` : [Data Bus](https://github.com/darkthing41/SE_DataBus) cut down and optimised for passing commands
+ `FindBlock()`: find blocks during setup
+ `ValidateBlock()`: check that found blocks are usable
+ Status/Initialise/Validate framework
