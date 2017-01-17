#region Header
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

namespace SpaceEngineersScripting_Support
{
	class Program : MyGridProgram
	{
		#endregion
		#region CodeEditor
		//Configuration
		//--------------------

		const string
			nameBusCommand = "Bus Drive.Command";


		//Definitions
		//--------------------

		//Opcodes for use as arguments
		//-commands may be issued directly
		const string
			command_Initialise = "Init";   //args: <none>


		//Internal Types
		//--------------------

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
			public IMyTextPanel
				bus;

			//Internal storage interface
			private void Append(string value){
				bus.WritePrivateText(value, true);
			}


			//Internal Implementation

			/// <summary>
			/// AppendTemporary archetype; append raw string as a temporary record.
			/// </summary>
			private void AppendTemporaryData(ref string id, char dataType, ref string data){
				Append(id +dataType +data +recordTerminator);
			}


			//PUBLIC INTERFACE

			public CommandBus(IMyTextPanel bus){
				this.bus = bus;
			}

			/// <summary>
			/// Utility function to pad a string into an identifier of the required length.
			/// It is NOT checked that the given id not already too long, and hence invalid.
			/// </summary>
			/// <returns>The valid identifier based on <paramref name="id"/>.</returns>
			public static string ExtendId(string id){
				return id.PadRight(lengthId, ' ');
			}


			public void AppendTemporaryInt(string id, int value){
				string data = value.ToString ();
				AppendTemporaryData(ref id, dataTypeInt, ref data);
			}

			public void AppendTemporaryFloat(string id, float value){
				string data = value.ToString ("R");
				AppendTemporaryData(ref id, dataTypeFloat, ref data);
			}

			public void AppendTemporaryString(string id, string value){
				AppendTemporaryData(ref id, dataTypeString, ref value);
			}

		}


		//Global variables
		//--------------------
		bool
			initialised;

		CommandBus
			busCommand;


		//Program
		//--------------------

		public Program()
		{
			Echo ("Restarted.");

			//We are not initialised after restart
			//-attempt to initialise now to reduce load at run-time
			initialised = false;
			Initialise();
		}


		public void Main(string argument)
		{
			//First ensure the system is able to process commands
			//-if necessary, perform first time setup
			//-if necessary or requested, initialise the system
			if ( !initialised || argument == command_Initialise) {
				//if we cannot initialise, end here
				if ( !Initialise() )
					return;
			}
			else if ( !Validate() ) {
				//if saved state is not valid, try re-initialising
				//if we cannot initialise, end here
				if ( !Initialise() )
					return;
			}

			//Check that the argument is not a command for us
			if (argument == command_Initialise)
				return;

			//Otherwise send argument as command to drive controller
			//-write command to the command bus
			if (argument.Length > CommandBus.lengthId) {
				Echo ("ERROR: bus id too long \"" +argument +"\"");
			} else {
				busCommand.AppendTemporaryString(CommandBus.ExtendId(argument), "");
				Echo ("Command Bus updated: " +argument);
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
			initialised = false;
			Echo ("initialising...");

			var temp = new List<IMyTerminalBlock>();

			//Discover command bus
			{
				IMyTextPanel textPanel;
				if ( !( FindBlock<IMyTextPanel>(out textPanel, nameBusCommand, ref temp)
				    && ValidateBlock(textPanel, callbackRequired:false) ))
					return false;
				else
					busCommand = new CommandBus(textPanel);
			}

			initialised = true;
			Echo ("Initialisation completed with no errors.");
			return true;
		}


		private bool Validate(){
			bool valid =
				ValidateBlock(busCommand.bus, callbackRequired:false);

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