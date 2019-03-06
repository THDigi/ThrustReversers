using System;
using System.Collections.Generic;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Game.Entities;
using Sandbox.Game.Lights;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Components;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRage.Utils;
using VRageMath;

using BlendType = VRageRender.MyBillboard.BlendTypeEnum;

namespace Digi.ThrustReversers
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_Thrust), false,
                                 "LargeBlockLargeAtmosphericThrust",
                                 "LargeBlockSmallAtmosphericThrust",
                                 "SmallBlockLargeAtmosphericThrust",
                                 "SmallBlockSmallAtmosphericThrust")]
    public class ThrustBlock : MyGameLogicComponent
    {
        const float JET_FLAME_SCALE_MUL = 1.75f;
        const BlendType BLEND_TYPE = BlendType.SDR;

        public ThrustReverserBlock Reverser;

        MyThrust thrust;
        MyLight light;
        MyLight lightJet;
        float thurstLerpColor;
        float thrustLerpLength;
        float thrustLerpThick;

        float length;
        float thickness;
        float trailOffset;
        float pointOffset;
        float lightOffset;
        float lightJetOffset;
        float coneOffset;
        float coneHeight;
        float coneRadius;
        float pointScaleMul;

        float maxViewDistSq;

        List<FlameInfo> flames;

        struct FlameInfo
        {
            public readonly Vector3 LocalFrom;
            public readonly Vector3 LocalTo;
            public readonly float Radius;
            public readonly Vector3 Direction;
            public readonly float Height;

            public FlameInfo(Vector3 localFrom, Vector3 localTo, float radius)
            {
                LocalFrom = localFrom;
                LocalTo = localTo;
                Radius = radius;

                Direction = (LocalTo - LocalFrom);
                Height = Direction.Normalize();
            }
        }

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            NeedsUpdate = MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
        }

        public override void UpdateOnceBeforeFrame()
        {
            try
            {
                if(ThrustReversersMod.Instance == null || !ThrustReversersMod.Instance.IsPlayer)
                    return;

                thrust = (MyThrust)Entity;
                var grid = thrust.CubeGrid;

                if(grid?.Physics == null || !grid.Physics.Enabled)
                    return;

                ThrustReversersMod.Instance.ThrustLogicDraw.Add(this);

                switch(thrust.BlockDefinition.Id.SubtypeName)
                {
                    case "LargeBlockLargeAtmosphericThrust":
                        maxViewDistSq = 1200;
                        length = 5f;
                        thickness = 1.6f;
                        trailOffset = -3f;
                        lightOffset = 0.75f;
                        lightJetOffset = 1.75f;
                        pointOffset = 0f;
                        pointScaleMul = 1.3f;
                        coneOffset = -3.5f;
                        coneHeight = 18f;
                        coneRadius = 2.65f;
                        break;
                    case "LargeBlockSmallAtmosphericThrust":
                        maxViewDistSq = 800;
                        length = 3f;
                        thickness = 0.5f;
                        trailOffset = -1f;
                        lightOffset = 0f;
                        lightJetOffset = 1.75f;
                        pointOffset = 0f;
                        pointScaleMul = 1.4f;
                        coneOffset = -1.75f;
                        coneHeight = 10f;
                        coneRadius = 0.8f;
                        break;
                    case "SmallBlockLargeAtmosphericThrust":
                        maxViewDistSq = 500;
                        length = 1f;
                        thickness = 0.325f;
                        trailOffset = -0.5f;
                        lightOffset = 0.75f;
                        lightJetOffset = 1.75f;
                        pointOffset = 0.325f;
                        pointScaleMul = 1.1f;
                        coneOffset = 0f;
                        coneHeight = 2f;
                        coneRadius = 0.4225f;
                        break;
                    case "SmallBlockSmallAtmosphericThrust":
                        maxViewDistSq = 300;
                        length = 0.75f;
                        thickness = 0.1f;
                        trailOffset = -0.5f;
                        lightOffset = 0.75f;
                        lightJetOffset = 1.5f;
                        pointOffset = 0.15f;
                        pointScaleMul = 1.4f;
                        coneOffset = -0.05f;
                        coneHeight = 1.2f;
                        coneRadius = 0.15f;
                        break;
                }

                maxViewDistSq *= maxViewDistSq;

                light = MyLights.AddLight();
                light.Start("ThrustLight");
                light.LightOn = false;

                lightJet = MyLights.AddLight();
                lightJet.Start("ThrustJetLight");
                lightJet.LightOn = false;

                GetFlameInfo();
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        private void GetFlameInfo()
        {
            // HACK copied from MyThrust's ThrustDamageAsync(), UpdateThrustFlame(), GetDamageCapsuleLine()
            var thrustDef = thrust.BlockDefinition;
            var thrustLength = /* CurrentStrength * */ 10f * /* MyUtils.GetRandomFloat(0.6f, 1f) * */ thrustDef.FlameLengthScale;

            flames = new List<FlameInfo>();

            var dummies = ThrustReversersMod.Instance.Dummies;
            dummies.Clear();

            Entity.Model.GetDummies(dummies);

            foreach(var dummy in dummies.Values)
            {
                if(dummy.Name.StartsWith("thruster_flame", StringComparison.InvariantCultureIgnoreCase))
                {
                    var startPosition = dummy.Matrix.Translation;
                    var direction = Vector3.Normalize(dummy.Matrix.Forward);
                    var radius = Math.Max(dummy.Matrix.Scale.X, dummy.Matrix.Scale.Y) * 0.5f;
                    var length = thrustLength * radius * thrustDef.FlameDamageLengthScale - radius;
                    var endPosition = startPosition + direction * length;

                    flames.Add(new FlameInfo(startPosition, endPosition, radius));
                }
            }

            dummies.Clear();
        }

        public override void Close()
        {
            Reverser = null;

            if(light != null)
            {
                MyLights.RemoveLight(light);
                light = null;
            }

            if(lightJet != null)
            {
                MyLights.RemoveLight(lightJet);
                lightJet = null;
            }
        }

        public bool Draw()
        {
            if(thrust.Closed)
                return false; // remove from list

            var cm = MyAPIGateway.Session.Camera.WorldMatrix;

            if(thrust.IsWorking && Vector3D.DistanceSquared(cm.Translation, thrust.WorldMatrix.Translation) <= maxViewDistSq)
            {
                float ClosedMultiplier = 1f;

                if(Reverser != null)
                {
                    if(Reverser.Closed)
                    {
                        Reverser = null;
                    }
                    else
                    {
                        ClosedMultiplier = 1 - Reverser.ReflectedThrust;
                    }
                }

                var paused = MyParticlesManager.Paused;
                var def = thrust.BlockDefinition;
                var amount = Math.Min(thrust.CurrentStrength * 2, 1);

                if(!paused)
                {
                    var lengthTarget = Math.Max(amount, 0.25f) * length * 3f * MyUtils.GetRandomFloat(0.9f, 1.1f) * ClosedMultiplier;
                    thrustLerpLength = lengthTarget; // MathHelper.Lerp(thrustLerpLength, lengthTarget, 0.3f);

                    var thickTarget = thickness * MyUtils.GetRandomFloat(0.9f, 1.1f);
                    thrustLerpThick = thickTarget; // MathHelper.Lerp(thrustLerpThick, thickTarget, 0.2f);

                    thurstLerpColor = MathHelper.Lerp(thurstLerpColor, amount, 0.25f);
                }

                //var trailColor = Vector4.Lerp(new Vector4(1f, 0.3f, 0f, 1f) * 0.25f, new Vector4(0.5f, 0.6f, 1f, 1f) * 0.75f, thurstLerpColor);
                var trailColor = Vector4.Lerp(new Vector4(0.5f, 0.6f, 1f, 1f) * 0.25f, new Vector4(1f, 0.3f, 0f, 1f), thurstLerpColor);
                var insideColor = trailColor; // Vector4.Lerp(new Vector4(1f, 0.3f, 0f, 1f) * 0.5f, new Vector4(1f, 1f, 0.9f, 1f), thurstLerpColor);
                var lightColor = insideColor; // Vector4.Lerp(new Vector4(1f, 0.3f, 0f, 1f), new Vector4(1f, 0.5f, 0.25f, 1f), thurstLerpColor);

                var m = thrust.WorldMatrix;
                var gridScale = thrust.CubeGrid.GridScale;
                var material = ThrustReversersMod.Instance.THRUST_MATERIAL;

                for(int i = 0; i < flames.Count; ++i)
                {
                    var flame = flames[i];

                    Vector3D direction = Vector3D.TransformNormal((Vector3D)flame.Direction, m);
                    Vector3D position = Vector3D.Transform((Vector3D)flame.LocalFrom, m);

                    var trailPos = position + (direction * trailOffset);

                    var dirToCam = Vector3.Normalize(cm.Translation - position);
                    var dot = Vector3.Dot(dirToCam, direction);

                    float trailAlpha = 0.5f;
                    float pointsAlpha = 2f;

                    const float TRAIL_ANGLE_LIMIT = 0.95f;
                    const float POINTS_ANGLE_START = 0.9f;
                    const float POINTS_ANGLE_END = 0.35f;

                    if(dot > TRAIL_ANGLE_LIMIT)
                        trailAlpha *= 1 - ((dot - TRAIL_ANGLE_LIMIT) / (1 - TRAIL_ANGLE_LIMIT));

                    if(dot < POINTS_ANGLE_END)
                        pointsAlpha = 0;
                    else if(dot < POINTS_ANGLE_START)
                        pointsAlpha *= (dot - POINTS_ANGLE_END) / (POINTS_ANGLE_START - POINTS_ANGLE_END);

                    if(trailAlpha > 0)
                        MyTransparentGeometry.AddLineBillboard(material, trailColor * trailAlpha, trailPos, direction, thrustLerpLength, JET_FLAME_SCALE_MUL * thrustLerpThick, blendType: BLEND_TYPE);

                    if(pointsAlpha > 0)
                        MyTransparentGeometry.AddBillboardOriented(ThrustReversersMod.Instance.POINT_MATERIAL, trailColor * pointsAlpha, position + (direction * pointOffset), m.Left, m.Up, (thickness * pointScaleMul), blendType: BLEND_TYPE);

                    var coneHeightFinal = coneHeight * (paused ? 1 : MyUtils.GetRandomFloat(0.9f, 1.1f));
                    var coneMatrix = MatrixD.CreateWorld(position + (direction * (coneOffset + coneHeightFinal)), -direction, m.Up);
                    var coneColor = new Color(insideColor);

                    DrawTransparentCone(ref coneMatrix, coneRadius, coneHeightFinal, ref coneColor, 16, ThrustReversersMod.Instance.CONE_MATERIAL, blendType: BLEND_TYPE);
                }

                // TODO reflected flames
                //if(ClosedMultiplier < 1 && amount > 0)
                //{
                //    float reflectedTrailOffset = -0.25f;
                //    float reflectedTrailHeight = 1.5f;
                //
                //    var door = (MyAdvancedDoor)Reverser.Entity;
                //
                //    foreach(var part in door.Subparts.Values)
                //    {
                //        var pm = part.WorldMatrix;
                //
                //        MyTransparentGeometry.AddBillboardOriented(ThrustReversersMod.Instance.THRUST_MATERIAL, trailColor * (1 - ClosedMultiplier), pm.Translation + (pm.Forward * reflectedTrailOffset) + pm.Up * (reflectedTrailHeight * 0.5), pm.Left, pm.Down, 0.5f, reflectedTrailHeight);
                //    }
                //}

                light.LightOn = true;
                light.Position = m.Translation + (m.Forward * lightOffset);
                light.Color = lightColor;
                light.Intensity = 100 * Math.Max(trailColor.W * 2, 0.5f);
                light.Falloff = 3f;
                light.Range = thrustLerpThick * 2f;
                light.UpdateLight();

                lightJet.LightOn = true;
                lightJet.Position = m.Translation + (m.Forward * lightJetOffset);
                lightJet.Color = trailColor;
                lightJet.Falloff = 1f;
                lightJet.Range = thrustLerpLength * 0.75f;
                lightJet.Intensity = 10 * trailColor.W;
                lightJet.UpdateLight();
            }
            else
            {
                if(light.LightOn)
                {
                    light.LightOn = false;
                    light.UpdateLight();
                }

                if(lightJet.LightOn)
                {
                    lightJet.LightOn = false;
                    lightJet.UpdateLight();
                }
            }

            return true; // keep in list
        }

        // HACK copied to add blendType.
        private static void DrawTransparentCone(ref MatrixD worldMatrix, float radius, float height, ref Color color, int wireDivideRatio, MyStringId faceMaterial, int customViewProjectionMatrix = -1, BlendType blendType = BlendType.Standard)
        {
            DrawTransparentCone(worldMatrix.Translation, worldMatrix.Forward * height, worldMatrix.Up * radius, color, wireDivideRatio, faceMaterial, customViewProjectionMatrix, blendType);
        }

        private static void DrawTransparentCone(Vector3D apexPosition, Vector3 directionVector, Vector3 baseVector, Color color, int wireDivideRatio, MyStringId material, int customViewProjectionMatrix = -1, BlendType blendType = BlendType.Standard)
        {
            Vector3 axis = directionVector;
            axis.Normalize();
            float num = (float)(MathHelperD.TwoPi / (double)wireDivideRatio);

            for(int i = 0; i < wireDivideRatio; i++)
            {
                float angle = (float)i * num;
                float angle2 = (float)(i + 1) * num;
                Vector3D point = apexPosition + directionVector + Vector3.Transform(baseVector, Matrix.CreateFromAxisAngle(axis, angle));
                Vector3D point2 = apexPosition + directionVector + Vector3.Transform(baseVector, Matrix.CreateFromAxisAngle(axis, angle2));
                MyQuadD myQuadD;
                myQuadD.Point0 = point;
                myQuadD.Point1 = point2;
                myQuadD.Point2 = apexPosition;
                myQuadD.Point3 = apexPosition;
                MyTransparentGeometry.AddQuad(material, ref myQuadD, color, ref Vector3D.Zero, -1, blendType, null);
            }
        }
    }
}
