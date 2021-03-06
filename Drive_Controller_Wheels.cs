﻿#region Header
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Globalization;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame;
using VRageMath;

namespace SpaceEngineersScripting
{
	public class Wrapper
	{
		static void Main()
		{

		}
	}


	class Program : MyGridProgram
	{
		#endregion
		#region CodeEditor
		//Configuration
		//--------------------
		const string
			nameController = "Cockpit";

		const uint
			commandsMax = 10;   //maximum number of commands to read each run

		static readonly Gear[]  //sorted in order of maximum speed ascending
			gears = new Gear[] {
				//        name, speed,  power
				new Gear ("1",  20.0f,  80.0f),
				new Gear ("2",  40.0f,  40.0f),
				new Gear ("3",  80.0f,  20.0f),
				new Gear ("4", 120.0f,  1600.0f/120.0f),
			};


		//Definitions
		//--------------------

		//Opcodes for use as arguments
		//-commands may be issued directly
		const string
			command_Initialise = "Init";   //args: <none>

		//Bus ids
		const string    //precalculated: 8 characters long
			busId_GearAutoToggle =  "GearAuto", //Switch between automatic/manual gearbox
			busId_GearUp =          "GearUp  ", //Change up one gear
			busId_GearDown =        "GearDown"; //Change down one gear

		//Utility definitions
		const float
			kmph_p_mps = (60 * 60) / 1000.0f;

		static readonly uint
			gearMax = (uint)gears.Length -1;

		static Vector3D GridToWorld(Vector3I gridVector, IMyCubeGrid grid){
			//convert a grid vector for the specified grid into a world direction vector
			return Vector3D.Subtract (
				grid.GridIntegerToWorld(gridVector),
				grid.GridIntegerToWorld(Vector3I.Zero) );
		}

		static Vector3D Project(Vector3D a, Vector3D b){
			return Vector3D.Multiply( b, Vector3D.Dot(a,b)/Vector3D.Dot(b,b) );
		}


		//Internal Types
		//--------------------

		//API definitions

		public struct MotorSuspension
		{	//IMyMotorSuspension API wrapper
			//PROPERTIES:
			///public static void SetSteering(IMyMotorSuspension motorSuspension, bool steering){
			///	motorSuspension.SetValue<bool>("Steering", steering);
			///}
			public static void SetMaxSteerAngle(IMyMotorSuspension motorSuspension, float maxSteerAngle){
				motorSuspension.SetValue<float>("MaxSteerAngle", maxSteerAngle);
			}
			public static void SetSteerSpeed(IMyMotorSuspension motorSuspension, float steerSpeed){
				motorSuspension.SetValue<float>("SteerSpeed", steerSpeed);
			}
			public static void SetSteerReturnSpeed(IMyMotorSuspension motorSuspension, float steerReturnSpeed){
				motorSuspension.SetValue<float>("SteerReturnSpeed", steerReturnSpeed);
			}
			///public static void SetInvertSteering(IMyMotorSuspension motorSuspension, bool invertSteering){
			///	motorSuspension.SetValue<bool>("InvertSteering", invertSteering);
			///}
			///public static void SetPropulsion(IMyMotorSuspension motorSuspension, bool propulsion){
			///	motorSuspension.SetValue<bool>("Propulsion", propulsion);
			///}
			///public static void SetInvertPropulsion(IMyMotorSuspension motorSuspension, bool invertPropulsion){
			///	motorSuspension.SetValue<bool>("InvertPropulsion", invertPropulsion);
			///}
			public static void SetPower(IMyMotorSuspension motorSuspension, float power){
				motorSuspension.SetValue<float>("Power", power);
			}
			///public static void SetFriction(IMyMotorSuspension motorSuspension, float friction){
			///	motorSuspension.SetValue<float>("Friction", friction);
			///}
			///public static void SetDamping(IMyMotorSuspension motorSuspension, float damping){
			///	motorSuspension.SetValue<float>("Damping", damping);
			///}
			public static void SetStrength(IMyMotorSuspension motorSuspension, float strength){
				motorSuspension.SetValue<float>("Strength", strength);
			}
			///public static void SetHeight(IMyMotorSuspension motorSuspension, float height){
			///	motorSuspension.SetValue<float>("Height", height);
			///}
			///public static void SetTravel(IMyMotorSuspension motorSuspension, float travel){
			///	motorSuspension.SetValue<float>("Travel", travel);
			///}
			public static float GetSpeedLimit(IMyMotorSuspension motorSuspension){  //in kmph
				return motorSuspension.GetValue<float>("Speed Limit");
			}
			public static void SetSpeedLimit(IMyMotorSuspension motorSuspension, float speedLimit){ //in kmph
				motorSuspension.SetValue<float>("Speed Limit", speedLimit);
			}
		}
		public struct ShipController
		{	//IMyShipController API wrapper
			//PROPERTIES:
			///public static void SetControlThrusters(IMyShipController shipController, bool controlThrusters){
			///	shipController.SetValue<bool>("ControlThrusters", controlThrusters);
			///}
			///public static void SetControlWheels(IMyShipController shipController, bool controlWheels){
			///	shipController.SetValue<bool>("ControlWheels", controlWheels);
			///}
			public static void SetHandBrake(IMyShipController shipController, bool handBrake){
				shipController.SetValue<bool>("HandBrake", handBrake);
			}
			///public static void SetDampenersOverride(IMyShipController shipController, bool dampenersOverride){
			///	shipController.SetValue<bool>("DampenersOverride", dampenersOverride);
			///}
			///public static bool GetMainCockpit(IMyShipController shipController){
			///	return shipController.GetValue<bool>("MainCockpit");
			///}
			///public static void SetMainCockpit(IMyShipController shipController, bool mainCockpit){
			///	shipController.SetValue<bool>("MainCockpit", mainCockpit);
			///}
			///public static bool GetHorizonIndicator(IMyShipController shipController){
			///	return shipController.GetValue<bool>("HorizonIndicator");
			///}
			///public static void SetHorizonIndicator(IMyShipController shipController, bool horizonIndicator){
			///	shipController.SetValue<bool>("HorizonIndicator", horizonIndicator);
			///}
		}
		public struct WheelControls
		{   //IMyShipController controls interpreter
			public readonly bool
				///moveForward, moveBackward,
				///turnLeft, turnRight,
				brake;

			public WheelControls(IMyShipController controller)
			{
				Vector3
					move = controller.MoveIndicator;

				///moveForward = move.Z < 0;
				///moveBackward = move.Z > 0;
				///turnLeft = move.X < 0;
				///turnRight = move.X > 0;
				brake = move.Y > 0;
			}
		}

		public struct Gear
		{
			public readonly string
				name;
			public readonly float
				speedLimitMax;
			public readonly float
				power;

			public Gear(string name, float speedLimitMax, float power)
			{
				this.name = name;
				this.speedLimitMax = speedLimitMax;
				this.power = power;
			}
		}

		/// <summary>
		/// A bus specialising in transfer of temporary records to a single reader.
		/// </summary>
		public struct CommandBus
		{
			//The bus stores a series of records as a string, each ended by the terminator
			//	*It is not checked that records do not contain the terminator
			//	 so make sure that you do not corrupt it by using this character

			//The Command Bus stores only one Record Type:
			//-Temporary: appended on write (duplicates allowed), destructive read (FIFO)
			//e.g. use temporary records to issue commands / directional data transfer

			//Records have an Id allowing basic discrimination
			//(e.g. source and/or destination and/or data as needed)

			//Additionally, records all have a Data Type to allow for basic type checking.
			//(new types may be easily added, so long as they can be encoded/decoded
			// from a string, and ensured not to contain the record terminator)

			//Temporary records are appended to the end of the store.
			//Interpreting the read records is down to the application.

			//FORMAT
			//<Record> ::= <RecordBody><recordTerminator>
			//<RecordBody> ::= <RecordTemporary>
			//<RecordTemporary> ::= <id><Data>
			//<id> ::= <string-lengthId>
			//<Data> ::= <DataInt> | <DataFloat> | <DataString>
			//<DataInt> ::= <dataTypeInt><int>
			//<DataFloat> ::= <dataTypeFloat><float>
			//<DataString> ::= <dataTypeFloat><string>

			//e.g. Temporary string record 'Speed' = "reset"
			//Speed__Sreset\n


			//Configuration
			public const char
				recordTerminator = '\n';//'\x1E'; //Record separator

			public const char
				dataTypeInt = 'I',
				dataTypeFloat = 'F',
				dataTypeString = 'S';

			public const int
				lengthId = 8;

			//The source of the storage
			public IMyTerminalBlock
				bus;

			//Internal storage interface
			private string Store{
				get { return bus.CustomData; }
				set { bus.CustomData = value; }
			}


			//Internal Implementation
			private string
				store;
			private int
				readPos;


			//PUBLIC INTERFACE

			public CommandBus(IMyTerminalBlock bus){
				this.bus = bus;

				store = null;
				readPos = 0;
			}

			/// <summary>
			/// Utility function to pad a string into an identifier of the required length.
			/// It is NOT checked that the given id not already too long, and hence invalid.
			/// </summary>
			/// <returns>The valid identifier based on <paramref name="id"/>.</returns>
			public static string ExtendId(string id){
				return id.PadRight(lengthId, ' ');
			}

			/// <summary>
			/// Begin the read process.
			/// Caches the store and prepares for optimised reads.
			/// </summary>
			public void BeginRead(){
				store = Store;
				readPos = 0;
			}

			/// <summary>
			/// Reads the next record in the cached store.
			/// </summary>
			/// <returns><c>true</c>, if a record was found; <c>false</c> if were no records to read.</returns>
			public bool ReadNext(out string id, out char dataType, out string data){
				if (readPos < store.Length)
				{
					id = store.Substring (readPos, lengthId);
					readPos += lengthId;

					dataType = store[readPos++];

					int dataPos = readPos;
					while (store[readPos++] != recordTerminator) {};
					data = store.Substring (dataPos, readPos -dataPos -1);

					return true;
				} else {
					//end of storage; no record to return
					id = null;
					dataType = '\0';
					data = null;
					return false;
				}
			}

			/// <summary>
			/// End the read process.
			/// Saves the cached store.
			/// </summary>
			public void EndRead(){
				//no work required if nothing was read
				if (readPos > 0) {
					Store = store.Remove(0, readPos);
					readPos = 0;
				}
				store = null;
			}

		}

		public struct Status
		{
			//program data not persistent across restarts
			public bool
				initialised;
			public bool
				modified;

			//command data persistent across restarts
			public uint
				gear;
			public bool
				gearsAutomatic;

			//configuration constants
			private const char
				delimiter = ';';

			//Operations

			public void Initialise(){   //data setup
				gear = 0;
				gearsAutomatic = true;
			}

			public string Store()
			{
				return
					gear.ToString()
					+delimiter +gearsAutomatic.ToString();
			}

			public bool TryRestore(string storage)
			{
				string[] elements = storage.Split(delimiter);
				return
					(elements.Length == 2)
					&& uint.TryParse(elements[0], out gear)
					&& bool.TryParse(elements[1], out gearsAutomatic);
			}
		}


		//Global variables
		//--------------------
		Status
			status;

		IMyShipController
			controller;
		CommandBus
			busCommand;

		Vector3I
			shipForward;
		List<IMyTerminalBlock>
			temp = new List<IMyTerminalBlock>();


		//Program
		//--------------------

		public Program()
		{
			Echo ("Restarted.");

			//script has been reloaded
			//-may be first time running
			//-world may have been reloaded (or script recompiled)
			if (Storage == null) {
				//use default values
				status.Initialise();
			} else {
				//attempt to restore saved values
				//  -otherwise use defaults
				Echo ("restoring saved state...");
				if ( !status.TryRestore(Storage) ){
					Echo ("restoration failed.");
					status.Initialise();
				}
			}
			//We are not initialised after restart
			//-attempt to initialise now to reduce load at run-time
			status.initialised = false;
			status.modified = false;
			Initialise();
		}

		public void Save()
		{
			Storage = status.Store();
		}


		public void Main(string argument)
		{
			//First ensure the system is able to process commands
			//-if necessary, perform first time setup
			//-if necessary or requested, initialise the system
			if ( !status.initialised || argument == command_Initialise) {
				//if we cannot initialise, end here
				if ( !Initialise() )
					return;
			}
			if (argument == command_Initialise) {
				Echo ("resetting.");
				status.Initialise ();
			}
			else if ( !Validate() ) {
				//if saved state is not valid, try re-initialising
				//if we cannot initialise, end here
				if ( !Initialise() )
					return;
			}

			//Read any control commands
			busCommand.BeginRead();
			ReadCommands();
			busCommand.EndRead();

			//Perform main processing
			Update();

			//Save status back if required
			if (status.modified) {
				Save();
				status.modified = false;
			}
		}


		private void ReadCommands()
		{
			string
				id,	data;
			char
				dataType;

			//Read commands until there are none left, or hit max
			for (int i=0; i<commandsMax; i++) {
				if (busCommand.ReadNext (out id, out dataType, out data)) {
					switch (dataType)
					{
						case CommandBus.dataTypeString:
							if (id == busId_GearUp) {
								Echo ("Received command: Gear up.");
								if (data != "") {
									Echo ("WARNING: Unexpected command data \"" + data + "\"");
								}
								Cmd_GearUp ();

							} else if (id == busId_GearDown) {
								Echo ("Received command: Gear down.");
								if (data != "") {
									Echo ("WARNING: Unexpected command data \"" + data + "\"");
								}
								Cmd_GearDown ();

							} else if (id == busId_GearAutoToggle) {
								Echo ("Received command: Switch gearbox mode.");
								if ( data != "" ) {
									Echo ("WARNING: Unexpected command data \"" +data +"\"");
								}
								Cmd_GearAutoToggle();

							} else {
								break;
							}
							continue;

						default:
							break;
					}
					//If we exited without 'continue', the command was unrecognised.
					Echo ("WARNING: Unrecognised command \"" +id +"\" type '" +dataType +"'");
				} else {
					Echo ("All commands read.");
					return;
				}
			}

			Echo ("Not reading further commands.");
		}


		private void Update()
		{
			{   //Apply handbrake if controller is not in use
				if (!controller.IsUnderControl && !controller.HandBrake) {
					ShipController.SetHandBrake(controller, true);
				}
			}

			MyShipVelocities
				vWorld = controller.GetShipVelocities();
			Vector3D
				worldForward = GridToWorld(shipForward, controller.CubeGrid),
				vWorldForward = Project(vWorld.LinearVelocity, worldForward);
			float
				speedForward = (float)(vWorldForward.Length() * kmph_p_mps),
				vForward = speedForward * Math.Sign(Vector3D.Dot(vWorldForward, worldForward));
			WheelControls
				controls = new WheelControls(controller);

			//Manage automatic gearbox
			if (status.gearsAutomatic) {
				//check which gear we should be in
				//-compare to vForward to limit to lowest gear for reversing
				if ((status.gear < gearMax) && (vForward >= 0.9f*gears[status.gear].speedLimitMax)) {
					status.gear++;
					status.modified = true;
				} else if ((status.gear > 0) && (vForward < 0.9f*gears[status.gear-1].speedLimitMax)) {
					status.gear--;
					status.modified = true;
				}
			}

			//Pre-calculate traction control values
			float
				speedLimitMax = gears[status.gear].speedLimitMax,
				speedLimit = MyMath.Clamp (speedForward+1.0f, 1.0f, speedLimitMax);
			float
				power = gears[status.gear].power;
			bool
				speedUnsafe = (speedForward >= speedLimitMax*1.1f),
				brake = controls.brake || speedUnsafe;

			//Check each suspension unit
			//-only control suspension units on our grid
			//-check that we have access to the suspension unit
			GridTerminalSystem.GetBlocksOfType<IMyMotorSuspension>(temp);
			int count = 0;
			for (int i=0; i<temp.Count; i++) {
				IMyMotorSuspension suspension = (IMyMotorSuspension)temp[i];

				if (suspension.CubeGrid != controller.CubeGrid)
					continue;

				count++;
				//Check if we can actually use the suspension unit
				if ( ValidateBlock (suspension, callbackRequired: false) ) {

					//Apply traction control
					//-limit wheel speed based on vehicle's forward speed
					//-apply brakes while speed is unsafe
					MotorSuspension.SetSpeedLimit(suspension, speedLimit);
					if (!controller.HandBrake)
						suspension.Brake = brake;

					//Apply gear ratio
					if (suspension.Power != power) {
						MotorSuspension.SetPower(suspension, power);
					}

				}
			} //end for

			//Output status
			Echo ("vF : " +speedForward.ToString("F1") +" kmph");
			Echo ("gear : " +gears[status.gear].name );
			Echo ("mode : " + (status.gearsAutomatic ? "Auto" : "Manual") );
			//Echo(temp.Count.ToString() +" wheels found.");
			//Echo(count.ToString() +" processed.");
		}


		private void Cmd_GearAutoToggle()
		{
			status.gearsAutomatic = !status.gearsAutomatic;
			status.modified = true;
		}

		private void Cmd_GearUp()
		{
			//manually changing gears transitions to manual gearbox
			if (status.gearsAutomatic) {
				status.gearsAutomatic = false;
				status.modified = true;
			}
			if (status.gear < gearMax) {
				status.gear ++;
				status.modified = true;
			}
		}

		private void Cmd_GearDown()
		{
			//manually changing gears transitions to manual gearbox
			if (status.gearsAutomatic) {
				status.gearsAutomatic = false;
				status.modified = true;
			}
			if (status.gear > 0) {
				status.gear --;
				status.modified = true;
			}
		}


		private bool FindBlock<BlockType>(out BlockType block, string nameBlock, ref List<IMyTerminalBlock> temp)
			where BlockType : class, IMyTerminalBlock
		{
			block = null;
			GridTerminalSystem.GetBlocksOfType<BlockType> (temp);
			for (int i=0; i<temp.Count; i++){
				if (temp[i].CustomName == nameBlock) {
					if (block == null) {
						block = (BlockType)temp[i];
					} else {
						Echo ("ERROR: duplicate name \"" +nameBlock +"\"");
						return false;
					}
				}
			}
			//verify that the block was found
			if (block == null) {
				Echo ("ERROR: block not found \"" +nameBlock +"\"");
				return false;
			}
			return true;
		}

		private bool ValidateBlock(IMyTerminalBlock block, bool callbackRequired=true)
		{
			//check for block deletion?

			//check that we have required permissions to control the block
			if ( ! Me.HasPlayerAccess(block.OwnerId) ) {
				Echo ("ERROR: no permissions for \"" +block.CustomName +"\"");
				return false;
			}

			//check that the block has required permissions to make callbacks
			if ( callbackRequired && !block.HasPlayerAccess(Me.OwnerId) ) {
				Echo ("ERROR: no permissions on \"" +block.CustomName +"\"");
				return false;
			}

			//check that block is functional
			if (!block.IsFunctional) {
				Echo ("ERROR: non-functional block \"" +block.CustomName +"\"");
				return false;
			}

			return true;
		}


		private bool Initialise()
		{
			status.initialised = false;
			Echo ("initialising...");

			//Find Controller and verify that it is operable
			if ( !( FindBlock<IMyShipController>(out controller, nameController, ref temp)
			    && ValidateBlock(controller, callbackRequired:true) ))
				return false;

			//Set up command bus
			busCommand = new CommandBus(Me);

			//Initialise ship data
			shipForward = Base6Directions.GetIntVector(controller.Orientation.Forward);

			status.initialised = true;
			Echo ("Initialisation completed with no errors.");
			return true;
		}


		private bool Validate(){
			bool valid =
				ValidateBlock(controller, callbackRequired:true);

			if ( !valid ) {
				Echo ("Validation of saved blocks failed.");
			}
			return valid;
		}
		#endregion
		#region footer
	}
}
#endregion