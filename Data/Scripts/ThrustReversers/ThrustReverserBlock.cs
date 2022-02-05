using System;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRageMath;

namespace Digi.ThrustReversers
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_AdvancedDoor), false,
                                 "LargeShip_LargeAtmosphericThrustReverse",
                                 "LargeShip_SmallAtmosphericThrustReverse",
                                 "SmallShip_LargeAtmosphericThrustReverse",
                                 "SmallShip_SmallAtmosphericThrustReverse")]
    public class ThrustReverserBlock : MyGameLogicComponent
    {
        public float ReflectedThrust;

        MyAdvancedDoor block;
        MyAdvancedDoorDefinition def;
        MyThrust linkedThruster;
        byte linkSkip = 127; // link ASAP

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            block = (MyAdvancedDoor)Entity;
            def = (MyAdvancedDoorDefinition)block.BlockDefinition;
            NeedsUpdate = MyEntityUpdateEnum.EACH_FRAME;
        }

        public override void Close()
        {
            linkedThruster = null;
        }

        public override void UpdateBeforeSimulation()
        {
            try
            {
                if(!block.IsFunctional) // only works if it's at full integrity, power status is irelevant
                    return;

                MyCubeGrid grid = block.CubeGrid;

                if(grid.Physics == null || !grid.Physics.Enabled)
                    return;

                if(linkedThruster == null)
                {
                    if(++linkSkip >= 60)
                    {
                        linkSkip = 0;
                        Vector3I pos = grid.WorldToGridInteger(block.WorldMatrix.Translation + block.WorldMatrix.Backward * grid.GridSize);
                        IMySlimBlock slim = grid.GetCubeBlock(pos) as IMySlimBlock;
                        MyThrust thrust = slim?.FatBlock as MyThrust;

                        if(thrust != null)
                        {
                            double alignDot = Math.Round(Vector3.Dot(thrust.WorldMatrix.Backward, block.WorldMatrix.Backward), 1);

                            if(alignDot == 1 && ThrustReversersMod.Instance.LinkableThrusters.Contains(thrust.BlockDefinition.Id.SubtypeName))
                            {
                                linkedThruster = thrust;

                                ThrustBlock logic = linkedThruster.GameLogic.GetAs<ThrustBlock>();
                                logic.Reverser = this;
                            }
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

                float closedRatio = (block.FullyClosed ? 1 : (block.FullyOpen ? 0 : (1 - (block.OpenRatio / def.OpeningSequence[0].MaxOpen)))); // HACK OpenRatio fix

                ReflectedThrust = Math.Max(closedRatio - 0.4f, 0) / 0.6f;

                if(ReflectedThrust > 0 && linkedThruster.CurrentStrength > 0)
                {
                    Vector3D force = linkedThruster.WorldMatrix.Forward * linkedThruster.BlockDefinition.ForceMagnitude * linkedThruster.CurrentStrength * 1.75 * ReflectedThrust;
                    Vector3D forceAt = (ThrustReversersMod.Instance.RealisticThrustersInstalled ? linkedThruster.WorldMatrix.Translation : grid.Physics.CenterOfMassWorld); // Realistic Thrusters Mod support
                    grid.Physics.AddForce(MyPhysicsForceType.APPLY_WORLD_FORCE, force, forceAt, null);
                }
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }
    }
}
