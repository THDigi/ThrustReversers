using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Sandbox.Common;
using Sandbox.Common.Components;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Common.ObjectBuilders.VRageData;
using Sandbox.Common.ObjectBuilders.Definitions;
using Sandbox.Definitions;
using Sandbox.Engine;
using Sandbox.Engine.Physics;
using Sandbox.Engine.Multiplayer;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.Game.Gui;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces;
using VRageMath;
using VRage;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRage.Game.Components;
using VRage.Utils;
using Digi.Utils;
using Ingame = Sandbox.ModAPI.Ingame;

namespace Digi.ImprovedThrusters
{
	[MySessionComponentDescriptor(MyUpdateOrder.AfterSimulation)]
	public class ImprovedThrusters : MySessionComponentBase
	{
		public bool init { get; private set; }
		public static short skip = 0;
		
		public static bool realisticThrustersInstalled = false;
		public const ulong realisticThrustersWorkshopId = 575893643;
		public const string realisticThrustersDevName = "ImprovedThrusters.dev";
		
		public void Init()
		{
			Log.Info("Initialized");
			init = true;
			
			var mods = MyAPIGateway.Session.GetCheckpoint("null").Mods;
			
			foreach(var mod in mods)
			{
				if(mod.PublishedFileId == realisticThrustersWorkshopId || (mod.PublishedFileId == 0 && mod.Name == realisticThrustersDevName))
				{
					realisticThrustersInstalled = true;
					Log.Info("Realistic Thrusters mod found, will adjust the thrust reversers accordingly.");
					break;
				}
			}
		}
		
		protected override void UnloadData()
		{
			init = false;
			realisticThrustersInstalled = false;
			
			Log.Info("Mod unloaded");
			Log.Close();
		}
		
		public override void UpdateAfterSimulation()
		{
			if(!init)
			{
				if(MyAPIGateway.Session == null)
					return;
				
				Init();
			}
		}
	}
	
	[MyEntityComponentDescriptor(typeof(MyObjectBuilder_AdvancedDoor),
	                             "LargeShip_LargeAtmosphericThrustReverse",
	                             "LargeShip_SmallAtmosphericThrustReverse",
	                             "SmallShip_LargeAtmosphericThrustReverse")]
	public class ThrustReverse : MyGameLogicComponent
	{
		private MyThrust linkedThruster = null;
		private byte skip = 127; // link ASAP
		
		private static HashSet<string> linkableThrusters = new HashSet<string>()
		{
			"LargeBlockLargeAtmosphericThrust",
			"LargeBlockSmallAtmosphericThrust",
			"SmallBlockLargeAtmosphericThrust",
		};
		
		public override void Init(MyObjectBuilder_EntityBase objectBuilder)
		{
			Entity.NeedsUpdate |= MyEntityUpdateEnum.EACH_FRAME;
		}
		
		public override void UpdateAfterSimulation()
		{
			try
			{
				var door = Entity as MyAdvancedDoor;
				
				if(!door.IsFunctional) // only works if it's at full integrity, power status is irelevant
					return;
				
				var grid = door.CubeGrid;
				
				if(grid.Physics == null || !grid.Physics.Enabled || grid.Physics.IsStatic)
					return;
				
				if(linkedThruster == null)
				{
					if(++skip >= 60)
					{
						skip = 0;
						var pos = grid.WorldToGridInteger(door.WorldMatrix.Translation + door.WorldMatrix.Backward * grid.GridSize);
						var slim = grid.GetCubeBlock(pos) as IMySlimBlock;
						
						if(slim != null && slim.FatBlock is MyThrust)
						{
							var thrust = slim.FatBlock as MyThrust;
							var alignDot = Math.Round(Vector3.Dot(thrust.WorldMatrix.Backward, door.WorldMatrix.Backward), 1);
							
							if(alignDot == 1 && linkableThrusters.Contains(thrust.BlockDefinition.Id.SubtypeName))
								linkedThruster = thrust;
						}
					}
					
					return;
				}
				
				if(linkedThruster.Closed || linkedThruster.MarkedForClose)
				{
					linkedThruster = null;
					return;
				}
				
				if(!linkedThruster.IsWorking)
					return;
				
				var def = door.BlockDefinition as MyAdvancedDoorDefinition;
				float closedRatio = (door.FullyClosed ? 1 : (door.FullyOpen ? 0 : (1 - (door.OpenRatio / def.OpeningSequence[0].MaxOpen)))); // HACK temporary OpenRatio fix
				
				if(closedRatio > 0 && linkedThruster.CurrentStrength > 0)
				{
					var force = linkedThruster.WorldMatrix.Forward * linkedThruster.BlockDefinition.ForceMagnitude * linkedThruster.CurrentStrength * 1.75 * closedRatio;
					var forceAt = (ImprovedThrusters.realisticThrustersInstalled ? linkedThruster.WorldMatrix.Translation : grid.Physics.CenterOfMassWorld); // Realistic Thrusters Mod support
					grid.Physics.AddForce(MyPhysicsForceType.APPLY_WORLD_FORCE, force, forceAt, null);
				}
			}
			catch(Exception e)
			{
				Log.Error(e);
			}
		}
		
		public override MyObjectBuilder_EntityBase GetObjectBuilder(bool copy = false)
		{
			return Entity.GetObjectBuilder(copy);
		}
	}
}