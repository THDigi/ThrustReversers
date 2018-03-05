using System;
using System.Collections.Generic;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Components;
using VRage.Utils;
using VRageMath;

namespace Digi.ThrustReversers
{
    [MySessionComponentDescriptor(MyUpdateOrder.AfterSimulation)]
    public class ThrustReversersMod : MySessionComponentBase
    {
        public static ThrustReversersMod Instance = null;

        public bool init = false;
        public bool isPlayer = false;
        public short skip = 0;

        public List<ThrustBlock> thrustLogicDraw = new List<ThrustBlock>();

        private const string MOD_NAME = "Reverse Thrusters";
        public const ulong MOD_WORKSHOP_ID = 570950770;
        private const string MOD_FOLDER = "ReverseThrusters";

        public bool RealisticThrustersInstalled = false;
        public const ulong REALISTIC_THRUSTERS_MOD_ID = 575893643;
        public const string REALISTIC_THRUSTERS_LOCAL = "ImprovedThrusters.dev";

        public readonly MyStringId THRUST_MATERIAL = MyStringId.GetOrCompute("JetThrust");
        public readonly MyStringId POINT_MATERIAL = MyStringId.GetOrCompute("JetThrustPoint");
        public readonly MyStringId CONE_MATERIAL = MyStringId.GetOrCompute("JetCone");

        public HashSet<string> LinkableThrusters = new HashSet<string>()
        {
            "LargeBlockLargeAtmosphericThrust",
            "LargeBlockSmallAtmosphericThrust",
            "SmallBlockLargeAtmosphericThrust",
            "SmallBlockSmallAtmosphericThrust",
        };

        public override void LoadData()
        {
            Instance = this;
            Log.SetUp(MOD_NAME, MOD_WORKSHOP_ID, MOD_FOLDER);

            try
            {
                // add mount points to vanilla thruster's ends for the reverser blocks to be placeable

                SetThrustMountPoints("LargeBlockLargeAtmosphericThrust",
                    new MyObjectBuilder_CubeBlockDefinition.MountPoint() { Side = BlockSideEnum.Front, Start = new Vector2(0f, 1f), End = new Vector2(0.1f, 2f), },
                    new MyObjectBuilder_CubeBlockDefinition.MountPoint() { Side = BlockSideEnum.Front, Start = new Vector2(2.9f, 1f), End = new Vector2(3f, 2f), },
                    new MyObjectBuilder_CubeBlockDefinition.MountPoint() { Side = BlockSideEnum.Front, Start = new Vector2(1, 2.9f), End = new Vector2(2f, 3f), },
                    new MyObjectBuilder_CubeBlockDefinition.MountPoint() { Side = BlockSideEnum.Front, Start = new Vector2(1f, 0.1f), End = new Vector2(2f, 0f), });

                SetThrustMountPoints("SmallBlockLargeAtmosphericThrust",
                    new MyObjectBuilder_CubeBlockDefinition.MountPoint() { Side = BlockSideEnum.Front, Start = new Vector2(0f, 1f), End = new Vector2(0.1f, 2f), },
                    new MyObjectBuilder_CubeBlockDefinition.MountPoint() { Side = BlockSideEnum.Front, Start = new Vector2(2.9f, 1f), End = new Vector2(3f, 2f), },
                    new MyObjectBuilder_CubeBlockDefinition.MountPoint() { Side = BlockSideEnum.Front, Start = new Vector2(1, 2.9f), End = new Vector2(2f, 3f), },
                    new MyObjectBuilder_CubeBlockDefinition.MountPoint() { Side = BlockSideEnum.Front, Start = new Vector2(1f, 0.1f), End = new Vector2(2f, 0f), });

                SetThrustMountPoints("LargeBlockSmallAtmosphericThrust",
                    new MyObjectBuilder_CubeBlockDefinition.MountPoint() { Side = BlockSideEnum.Front, Start = new Vector2(0f, 0.4f), End = new Vector2(0.05f, 0.6f), },
                    new MyObjectBuilder_CubeBlockDefinition.MountPoint() { Side = BlockSideEnum.Front, Start = new Vector2(0.4f, 0.95f), End = new Vector2(0.6f, 1f), },
                    new MyObjectBuilder_CubeBlockDefinition.MountPoint() { Side = BlockSideEnum.Front, Start = new Vector2(0.95f, 0.4f), End = new Vector2(1f, 0.6f), },
                    new MyObjectBuilder_CubeBlockDefinition.MountPoint() { Side = BlockSideEnum.Front, Start = new Vector2(0.4f, 0f), End = new Vector2(0.6f, 0.05f), });

                SetThrustMountPoints("SmallBlockSmallAtmosphericThrust",
                    new MyObjectBuilder_CubeBlockDefinition.MountPoint() { Side = BlockSideEnum.Front, Start = new Vector2(0f, 0.4f), End = new Vector2(0.05f, 0.6f), },
                    new MyObjectBuilder_CubeBlockDefinition.MountPoint() { Side = BlockSideEnum.Front, Start = new Vector2(0.4f, 0.95f), End = new Vector2(0.6f, 1f), },
                    new MyObjectBuilder_CubeBlockDefinition.MountPoint() { Side = BlockSideEnum.Front, Start = new Vector2(0.95f, 0.4f), End = new Vector2(1f, 0.6f), },
                    new MyObjectBuilder_CubeBlockDefinition.MountPoint() { Side = BlockSideEnum.Front, Start = new Vector2(0.4f, 0f), End = new Vector2(0.6f, 0.05f), });
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        public void Init()
        {
            Log.Init();
            init = true;
            isPlayer = !(MyAPIGateway.Session.IsServer && MyAPIGateway.Utilities.IsDedicated);

            var mods = MyAPIGateway.Session.Mods;

            foreach(var mod in mods)
            {
                if(mod.PublishedFileId == REALISTIC_THRUSTERS_MOD_ID || (mod.PublishedFileId == 0 && mod.Name == REALISTIC_THRUSTERS_LOCAL))
                {
                    RealisticThrustersInstalled = true;
                    Log.Info("Realistic Thrusters mod found, will adjust the thrust reversers accordingly.");
                    break;
                }
            }

            MyAPIGateway.Utilities.InvokeOnGameThread(() => SetUpdateOrder(MyUpdateOrder.NoUpdate)); // stop updating this comp
        }

        protected override void UnloadData()
        {
            init = false;
            RealisticThrustersInstalled = false;
            thrustLogicDraw.Clear();

            Log.Close();
            Instance = null;
        }

        public override void UpdateAfterSimulation()
        {
            try
            {
                if(!init)
                {
                    if(MyAPIGateway.Session == null)
                        return;

                    Init();
                }
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        public override void Draw()
        {
            try
            {
                if(!isPlayer)
                    return;

                for(int i = thrustLogicDraw.Count - 1; i >= 0; --i)
                {
                    if(!thrustLogicDraw[i].Draw())
                    {
                        thrustLogicDraw.RemoveAtFast(i);
                    }
                }
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        private void SetThrustMountPoints(string subtypeId, params MyObjectBuilder_CubeBlockDefinition.MountPoint[] addMPObjects)
        {
            MyCubeBlockDefinition def;

            if(MyDefinitionManager.Static.TryGetCubeBlockDefinition(new MyDefinitionId(typeof(MyObjectBuilder_Thrust), MyStringHash.GetOrCompute(subtypeId)), out def))
            {
                // HACK since there's no way to edit the flame properly, this hides it and the mod draws its own
                var thrustDef = (MyThrustDefinition)def;
                thrustDef.FlameFullColor = Vector4.Zero;
                thrustDef.FlameIdleColor = Vector4.Zero;

                var mp = def.MountPoints;
                def.MountPoints = new MyCubeBlockDefinition.MountPoint[mp.Length + addMPObjects.Length];

                for(int i = 0; i < mp.Length; i++)
                {
                    def.MountPoints[i] = mp[i];
                }

                for(int i = 0; i < addMPObjects.Length; ++i)
                {
                    var mpObj = addMPObjects[i];

                    Vector3 start = new Vector3(Vector2.Min(mpObj.Start, mpObj.End) + 0.001f, 0.0004f);
                    Vector3 end = new Vector3(Vector2.Max(mpObj.Start, mpObj.End) - 0.001f, -0.0004f);

                    int side = (int)mpObj.Side;
                    TransformMountPointPosition(ref start, side, def.Size, out start);
                    TransformMountPointPosition(ref end, side, def.Size, out end);

                    Vector3I forward = Vector3I.Forward;
                    Vector3I.TransformNormal(ref forward, ref m_mountPointTransforms[side], out forward);

                    def.MountPoints[mp.Length + i] = new MyCubeBlockDefinition.MountPoint()
                    {
                        Start = start,
                        End = end,
                        Normal = forward,
                        ExclusionMask = mpObj.ExclusionMask,
                        PropertiesMask = mpObj.PropertiesMask,
                        Enabled = mpObj.Enabled,
                        Default = mpObj.Default,
                    };
                }
            }
        }

        // HACK copied from MyCubeBlockDefinition
        #region
        private void TransformMountPointPosition(ref Vector3 position, int wallIndex, Vector3I cubeSize, out Vector3 result)
        {
            Vector3.Transform(ref position, ref m_mountPointTransforms[wallIndex], out result);
            result += m_mountPointWallOffsets[wallIndex] * cubeSize;
        }

        private readonly Matrix[] m_mountPointTransforms = new Matrix[]
        {
            Matrix.CreateFromDir(Vector3.Right, Vector3.Up) * Matrix.CreateScale(1f, 1f, -1f),
            Matrix.CreateFromDir(Vector3.Up, Vector3.Forward) * Matrix.CreateScale(-1f, 1f, 1f),
            Matrix.CreateFromDir(Vector3.Forward, Vector3.Up) * Matrix.CreateScale(-1f, 1f, 1f),
            Matrix.CreateFromDir(Vector3.Left, Vector3.Up) * Matrix.CreateScale(1f, 1f, -1f),
            Matrix.CreateFromDir(Vector3.Down, Vector3.Backward) * Matrix.CreateScale(-1f, 1f, 1f),
            Matrix.CreateFromDir(Vector3.Backward, Vector3.Up) * Matrix.CreateScale(-1f, 1f, 1f)
        };

        private readonly Vector3[] m_mountPointWallOffsets = new Vector3[]
        {
            new Vector3(1f, 0f, 1f),
            new Vector3(0f, 1f, 1f),
            new Vector3(1f, 0f, 0f),
            new Vector3(0f, 0f, 0f),
            new Vector3(0f, 0f, 0f),
            new Vector3(0f, 0f, 1f)
        };
        #endregion
    }
}