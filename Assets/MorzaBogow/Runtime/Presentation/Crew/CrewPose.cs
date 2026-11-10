using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace MorzaBogow.Presentation
{
    /// <summary>What a man is doing; each is a pose of the rig as a function of its phase (CrewPose).</summary>
    public enum CrewAction
    {
        StandIdle, SitIdle, Row, RowStanding, Steer, SteerSeated, Watch, Haul, Bail, Brace, Jolt,
        BowReady, BowDraw, BowLoose, JavelinReady, JavelinThrow, Crank, Load, Aim,
        HookThrow, HaulLine, Climb, Fight, Guard, CutLine, Fall, Overboard, Kneel, LieWounded, SitWounded, Walk, Carry, HoldOar,
    }

    /// <summary>One man to pose this frame: where his station is, what he does and where his hands must be.</summary>
    public struct FigureInput
    {
        /// <summary>The station's frame in the world (ship's model times the station's place and facing), unscaled.</summary>
        public float4x4 Frame;
        public float Height, Girth;
        public int Action;
        /// <summary>Phase of a cycle (rowing, hauling) or progress of a one-off (a throw, a fall), 0 to 1.</summary>
        public float Phase;
        /// <summary>How strongly (bracing, the size of a jolt, the pull of a stroke), 0 to 1.</summary>
        public float Strength;
        /// <summary>Where he looks or aims, in the station's frame (unit vector); zero for straight ahead.</summary>
        public float3 Aim;
        /// <summary>Hands that hold something (bit 1 left, bit 2 right) and where, in the station's frame.</summary>
        public int Hands;
        public float3 HandLeft, HandRight;
        /// <summary>Feet that stand on something other than the station's floor (bit 1 left, bit 2 right), in the station's frame.</summary>
        public int Feet;
        public float3 FootLeft, FootRight;
        /// <summary>Seated men sit at the station's origin (their hips), standing men stand on it.</summary>
        public bool Seated;
        /// <summary>His weapon or tool: 0 standing at his station (Rest), 1 in his right hand, 2 slung on his back, 3 in his left hand.</summary>
        public int WeaponMode;
        /// <summary>His shield: 0 on his back, 1 on his left forearm.</summary>
        public int ShieldMode;
        /// <summary>Where his weapon stands when he does not hold it, in the station's frame.</summary>
        public float3 Rest;
    }

    /// <summary>
    /// The poses of the crews' rig (CrewBody): for each action the turn of every joint as a function of its phase,
    /// then two-bone reaching for hands that hold an oar's handle, a tiller, a winch or a rail and for feet that step on a
    /// rail, then the world matrix of every bone. Pure maths on blittable data, run for all men at once as a Burst job.
    /// Angles: a positive turn about X bends the spine forward and swings a hanging limb backward; arms swing forward with
    /// a negative turn; a knee bends with a positive one.
    /// </summary>
    public static class CrewPose
    {
        public const int Bones = CrewBody.Bones;

        static quaternion X(float degrees) => quaternion.RotateX(math.radians(degrees));
        static quaternion Y(float degrees) => quaternion.RotateY(math.radians(degrees));
        static quaternion Z(float degrees) => quaternion.RotateZ(math.radians(degrees));
        static quaternion XYZ(float x, float y, float z) => math.mul(Y(y), math.mul(X(x), Z(z)));
        static float Wave(float phase) => math.sin(phase * math.PI * 2f);
        static float Smooth(float a, float b, float x) { var t = math.saturate((x - a) / (b - a)); return t * t * (3f - 2f * t); }

        /// <summary>The local turn of each bone and the pelvis's offset from the station origin, for an action at a phase.</summary>
        public static void Evaluate(in FigureInput figure, ref FixedPose pose)
        {
            for (var i = 0; i < Bones; i++) pose[i] = quaternion.identity;
            pose.Pelvis = float3.zero;
            var p = figure.Phase;
            var s = figure.Strength;
            var aimYaw = math.lengthsq(figure.Aim) > 1e-6f ? math.degrees(math.atan2(figure.Aim.x, figure.Aim.z)) : 0f;
            var aimPitch = math.lengthsq(figure.Aim) > 1e-6f ? math.degrees(math.asin(math.clamp(figure.Aim.y, -1f, 1f))) : 0f;
            switch ((CrewAction)figure.Action)
            {
                case CrewAction.StandIdle:
                {
                    // A slow shift of weight from foot to foot, a few seconds long; never a gesture for show.
                    var sway = Wave(p) * 1.5f;
                    pose[(int)Bone.Pelvis] = Z(sway);
                    pose[(int)Bone.Spine] = Z(-sway * 0.6f);
                    pose[(int)Bone.UpperArmL] = Z(-4f);
                    pose[(int)Bone.UpperArmR] = Z(4f);
                    pose[(int)Bone.ForeArmL] = X(-8f);
                    pose[(int)Bone.ForeArmR] = X(-8f);
                    pose[(int)Bone.Head] = Y(aimYaw * 0.5f);
                    break;
                }
                case CrewAction.SitIdle:
                case CrewAction.SitWounded:
                {
                    // Sitting on the deck with knees drawn up, forearms on them; the wounded slumped.
                    var slump = figure.Action == (int)CrewAction.SitWounded ? 18f : 6f;
                    Seat(ref pose, 70f, 110f);
                    pose[(int)Bone.Spine] = X(slump + Wave(p) * 1f);
                    pose[(int)Bone.Chest] = X(slump * 0.5f);
                    pose[(int)Bone.UpperArmL] = XYZ(-35f, 0f, -6f);
                    pose[(int)Bone.UpperArmR] = XYZ(-35f, 0f, 6f);
                    pose[(int)Bone.ForeArmL] = X(-60f);
                    pose[(int)Bone.ForeArmR] = X(-60f);
                    pose[(int)Bone.Head] = XYZ(slump * 0.6f, aimYaw * 0.4f, 0f);
                    break;
                }
                case CrewAction.Row:
                case CrewAction.HoldOar:
                {
                    // Facing aft on a low seat, feet braced on the deck: forward to the catch, back through the drive.
                    // The lean follows the oar's own phase; the hands are put on the handle by reaching (below).
                    RowingStroke.Sample(p, out var sweep, out _);
                    var lean = figure.Action == (int)CrewAction.HoldOar ? 0f : -sweep;
                    Seat(ref pose, 72f + lean * 8f, 70f - lean * 10f);
                    pose[(int)Bone.Spine] = X(10f + lean * 16f * (0.6f + 0.4f * s));
                    pose[(int)Bone.Chest] = X(4f + lean * 8f);
                    pose[(int)Bone.Head] = X(-6f - lean * 10f);
                    break;
                }
                case CrewAction.RowStanding:
                {
                    RowingStroke.Sample(p, out var sweep, out _);
                    var lean = -sweep;
                    pose[(int)Bone.ThighL] = X(-14f - lean * 6f);
                    pose[(int)Bone.ThighR] = X(10f);
                    pose[(int)Bone.ShinL] = X(18f + lean * 6f);
                    pose[(int)Bone.ShinR] = X(4f);
                    pose.Pelvis = new float3(0f, -0.04f, 0f);
                    pose[(int)Bone.Spine] = X(14f + lean * 14f);
                    pose[(int)Bone.Chest] = X(6f + lean * 6f);
                    break;
                }
                case CrewAction.Steer:
                {
                    // Standing between the tillers, a hand on each; leaning into the helm as it goes over.
                    pose[(int)Bone.Pelvis] = Y(s * 6f);
                    pose[(int)Bone.Spine] = XYZ(4f, s * 4f, -s * 3f);
                    pose[(int)Bone.ThighL] = X(-4f);
                    pose[(int)Bone.ThighR] = X(5f);
                    pose[(int)Bone.ShinL] = X(6f);
                    pose[(int)Bone.Head] = Y(aimYaw * 0.6f);
                    break;
                }
                case CrewAction.SteerSeated:
                    Seat(ref pose, 80f, 85f);
                    pose[(int)Bone.Spine] = XYZ(6f, s * 6f, 0f);
                    pose[(int)Bone.Head] = Y(aimYaw * 0.6f);
                    break;
                case CrewAction.Watch:
                {
                    // Standing at the bow, head and shoulders turned toward what he watches; a hand on the rail.
                    var turn = math.clamp(aimYaw, -100f, 100f);
                    pose[(int)Bone.Pelvis] = Y(turn * 0.25f);
                    pose[(int)Bone.Spine] = Y(turn * 0.25f);
                    pose[(int)Bone.Chest] = XYZ(-2f, turn * 0.2f, 0f);
                    pose[(int)Bone.Head] = XYZ(-aimPitch * 0.5f, turn * 0.3f, 0f);
                    pose[(int)Bone.UpperArmL] = XYZ(-20f, 0f, -10f);
                    pose[(int)Bone.ForeArmL] = X(-35f);
                    pose[(int)Bone.ThighR] = X(6f);
                    break;
                }
                case CrewAction.Haul:
                case CrewAction.HaulLine:
                {
                    // Hand over hand on a line above him (brails) or before him (a grapple's line): reach, grip, pull back.
                    var pull = Smooth(0f, 0.5f, p) - Smooth(0.5f, 1f, p);
                    var high = figure.Action == (int)CrewAction.Haul;
                    pose[(int)Bone.Spine] = X(high ? -6f + pull * 10f : 18f - pull * 22f);
                    pose[(int)Bone.ThighL] = X(-18f);
                    pose[(int)Bone.ShinL] = X(20f);
                    pose[(int)Bone.ThighR] = X(12f);
                    pose[(int)Bone.UpperArmL] = X(high ? -150f + pull * 60f : -70f + pull * 50f);
                    pose[(int)Bone.UpperArmR] = X(high ? -120f + pull * 50f : -55f + pull * 40f);
                    pose[(int)Bone.ForeArmL] = X(-20f - pull * 50f);
                    pose[(int)Bone.ForeArmR] = X(-30f - pull * 50f);
                    pose.Pelvis = new float3(0f, -0.03f, -pull * 0.06f);
                    break;
                }
                case CrewAction.Bail:
                {
                    // Bent to the bilge, scooping and throwing water over the side.
                    var swing = Smooth(0.2f, 0.7f, p) - Smooth(0.75f, 1f, p);
                    pose[(int)Bone.Pelvis] = X(25f);
                    pose[(int)Bone.Spine] = XYZ(25f - swing * 30f, swing * 30f, 0f);
                    pose[(int)Bone.ThighL] = X(-55f);
                    pose[(int)Bone.ThighR] = X(-45f);
                    pose[(int)Bone.ShinL] = X(70f);
                    pose[(int)Bone.ShinR] = X(60f);
                    pose.Pelvis = new float3(0f, -0.22f, -0.08f);
                    pose[(int)Bone.UpperArmR] = X(-40f - swing * 80f);
                    pose[(int)Bone.UpperArmL] = X(-50f - swing * 60f);
                    pose[(int)Bone.ForeArmR] = X(-30f);
                    pose[(int)Bone.ForeArmL] = X(-40f);
                    break;
                }
                case CrewAction.Brace:
                {
                    // Before a ram's blow: knees bent, weight low and back, a hand to the rail.
                    var low = s;
                    pose.Pelvis = new float3(0f, -0.14f * low, -0.05f * low);
                    pose[(int)Bone.ThighL] = X(-35f * low);
                    pose[(int)Bone.ThighR] = X(-25f * low);
                    pose[(int)Bone.ShinL] = X(55f * low);
                    pose[(int)Bone.ShinR] = X(45f * low);
                    pose[(int)Bone.Spine] = X(20f * low);
                    pose[(int)Bone.UpperArmL] = XYZ(-30f * low, 0f, -25f * low);
                    pose[(int)Bone.UpperArmR] = XYZ(-30f * low, 0f, 25f * low);
                    pose[(int)Bone.ForeArmL] = X(-25f);
                    pose[(int)Bone.ForeArmR] = X(-25f);
                    break;
                }
                case CrewAction.Jolt:
                {
                    // Thrown forward by a blow and catching himself, as hard as the blow was.
                    var hit = math.sin(math.saturate(p) * math.PI) * s;
                    pose.Pelvis = new float3(0f, -0.08f * hit, 0.12f * hit);
                    pose[(int)Bone.Spine] = X(30f * hit);
                    pose[(int)Bone.Chest] = X(12f * hit);
                    pose[(int)Bone.ThighL] = X(-30f * hit);
                    pose[(int)Bone.ShinL] = X(40f * hit);
                    pose[(int)Bone.UpperArmL] = X(-60f * hit);
                    pose[(int)Bone.UpperArmR] = X(-70f * hit);
                    break;
                }
                case CrewAction.BowReady:
                case CrewAction.BowDraw:
                case CrewAction.BowLoose:
                {
                    // Side-on to the mark: the bow arm out toward it, the string hand drawn to the chin, loosed, recovered.
                    var drawn = figure.Action == (int)CrewAction.BowReady ? 0f : figure.Action == (int)CrewAction.BowDraw ? Smooth(0f, 1f, p) : 1f - Smooth(0.15f, 1f, p);
                    var raise = figure.Action == (int)CrewAction.BowReady ? 0.25f : 1f;
                    var turn = aimYaw;
                    pose[(int)Bone.Pelvis] = Y(turn - 70f * raise);
                    pose[(int)Bone.Spine] = Y(30f * raise);
                    pose[(int)Bone.Chest] = XYZ(-aimPitch * 0.4f * raise, 25f * raise, 0f);
                    pose[(int)Bone.Head] = XYZ(-aimPitch * 0.5f, 15f * raise, 0f);
                    pose[(int)Bone.UpperArmL] = XYZ(-80f * raise - aimPitch * 0.6f, 0f, -10f);
                    pose[(int)Bone.ForeArmL] = X(-5f);
                    pose[(int)Bone.UpperArmR] = XYZ(-75f * raise, 0f, 30f * drawn);
                    pose[(int)Bone.ForeArmR] = X(-30f - 110f * drawn);
                    pose[(int)Bone.ThighL] = X(-8f);
                    pose[(int)Bone.ThighR] = X(8f);
                    break;
                }
                case CrewAction.JavelinReady:
                case CrewAction.JavelinThrow:
                {
                    // Javelin over the shoulder; the throw: wind back, step and hurl, follow through, recover.
                    var windup = figure.Action == (int)CrewAction.JavelinReady ? 0.2f : Smooth(0f, 0.45f, p);
                    var hurl = figure.Action == (int)CrewAction.JavelinReady ? 0f : Smooth(0.45f, 0.6f, p) * (1f - Smooth(0.75f, 1f, p));
                    pose[(int)Bone.Pelvis] = Y(aimYaw - 35f * windup + 45f * hurl);
                    pose[(int)Bone.Spine] = XYZ(-10f * windup + 25f * hurl, -20f * windup + 15f * hurl, 0f);
                    pose[(int)Bone.UpperArmR] = XYZ(-150f + 40f * windup - 90f * hurl, 0f, 15f);
                    pose[(int)Bone.ForeArmR] = X(-60f * (1f - hurl));
                    pose[(int)Bone.UpperArmL] = XYZ(-60f, 0f, -15f);
                    pose[(int)Bone.ForeArmL] = X(-50f);
                    pose[(int)Bone.ThighL] = X(-20f * hurl - 6f);
                    pose[(int)Bone.ThighR] = X(15f * windup);
                    pose[(int)Bone.ShinL] = X(10f);
                    break;
                }
                case CrewAction.Crank:
                {
                    // At a winch: legs braced, both hands on the handle going round (hands by reaching).
                    pose.Pelvis = new float3(0f, -0.06f, 0f);
                    pose[(int)Bone.ThighL] = X(-25f);
                    pose[(int)Bone.ThighR] = X(10f);
                    pose[(int)Bone.ShinL] = X(30f);
                    pose[(int)Bone.ShinR] = X(15f);
                    pose[(int)Bone.Spine] = X(18f + Wave(p) * 8f);
                    break;
                }
                case CrewAction.Load:
                case CrewAction.Carry:
                {
                    // Carrying a bolt or a stone held low in both arms, setting it in place.
                    var place = figure.Action == (int)CrewAction.Load ? Smooth(0.3f, 0.7f, p) * (1f - Smooth(0.8f, 1f, p)) : 0f;
                    pose[(int)Bone.Spine] = X(10f + place * 30f);
                    pose[(int)Bone.UpperArmL] = XYZ(-35f - place * 30f, 0f, -8f);
                    pose[(int)Bone.UpperArmR] = XYZ(-35f - place * 30f, 0f, 8f);
                    pose[(int)Bone.ForeArmL] = X(-70f);
                    pose[(int)Bone.ForeArmR] = X(-70f);
                    pose[(int)Bone.ThighL] = X(-place * 30f);
                    pose[(int)Bone.ShinL] = X(place * 40f);
                    pose.Pelvis = new float3(0f, -0.06f * place, 0f);
                    break;
                }
                case CrewAction.Aim:
                {
                    // Behind the machine, sighting along it, a hand on the trigger.
                    pose[(int)Bone.Spine] = X(25f);
                    pose[(int)Bone.Chest] = X(10f);
                    pose[(int)Bone.Head] = X(-25f);
                    pose[(int)Bone.ThighL] = X(-30f);
                    pose[(int)Bone.ShinL] = X(35f);
                    pose[(int)Bone.ThighR] = X(10f);
                    pose.Pelvis = new float3(0f, -0.07f, 0f);
                    break;
                }
                case CrewAction.HookThrow:
                {
                    // A grapnel swung once round and thrown across.
                    var swing = Smooth(0f, 0.55f, p);
                    var throwOut = Smooth(0.55f, 0.75f, p);
                    pose[(int)Bone.Pelvis] = Y(aimYaw - 30f * swing + 40f * throwOut);
                    pose[(int)Bone.UpperArmR] = XYZ(-60f - 100f * swing + 90f * throwOut, 0f, 20f);
                    pose[(int)Bone.ForeArmR] = X(-20f);
                    pose[(int)Bone.UpperArmL] = XYZ(-40f, 0f, -10f);
                    pose[(int)Bone.ForeArmL] = X(-60f);
                    pose[(int)Bone.ThighL] = X(-15f * throwOut);
                    break;
                }
                case CrewAction.Climb:
                {
                    // Over the rails: a hand on each rail, one foot up on the rail and over (hands and feet by reaching).
                    var up = math.sin(math.saturate(p) * math.PI);
                    pose.Pelvis = new float3(0f, up * 0.25f, 0f);
                    pose[(int)Bone.Spine] = X(25f * up);
                    pose[(int)Bone.ThighL] = X(-70f * Smooth(0f, 0.5f, p) + 50f * Smooth(0.5f, 1f, p));
                    pose[(int)Bone.ShinL] = X(80f * up);
                    pose[(int)Bone.ThighR] = X(-50f * Smooth(0.4f, 0.9f, p));
                    pose[(int)Bone.ShinR] = X(70f * Smooth(0.3f, 0.8f, p));
                    break;
                }
                case CrewAction.Fight:
                case CrewAction.Guard:
                {
                    // Spear or sword over the shield: thrust and recover in turn with the man opposite; guard covers.
                    var thrust = figure.Action == (int)CrewAction.Fight ? math.max(0f, math.sin(p * math.PI * 2f)) : 0f;
                    var cover = figure.Action == (int)CrewAction.Guard ? 0.7f + 0.3f * Wave(p) : 0.4f;
                    pose.Pelvis = new float3(0f, -0.06f, 0.18f * thrust);
                    pose[(int)Bone.Pelvis] = Y(aimYaw - 20f);
                    pose[(int)Bone.Spine] = XYZ(12f + 10f * thrust, 15f * thrust, 0f);
                    pose[(int)Bone.ThighL] = X(-30f - 15f * thrust);
                    pose[(int)Bone.ShinL] = X(35f);
                    pose[(int)Bone.ThighR] = X(18f);
                    pose[(int)Bone.ShinR] = X(10f);
                    pose[(int)Bone.UpperArmL] = XYZ(-60f * cover, 0f, -20f);
                    pose[(int)Bone.ForeArmL] = X(-80f * cover);
                    pose[(int)Bone.UpperArmR] = XYZ(-50f - 50f * thrust, 0f, 25f - 15f * thrust);
                    pose[(int)Bone.ForeArmR] = X(-90f + 70f * thrust);
                    break;
                }
                case CrewAction.CutLine:
                {
                    // Hacking at a grapple's line on the rail.
                    var chop = Smooth(0.3f, 0.5f, p) * (1f - Smooth(0.6f, 1f, p));
                    pose[(int)Bone.Spine] = X(15f + 25f * chop);
                    pose[(int)Bone.UpperArmR] = XYZ(-150f + 110f * chop, 0f, 10f);
                    pose[(int)Bone.ForeArmR] = X(-40f + 30f * chop);
                    pose[(int)Bone.UpperArmL] = XYZ(-50f, 0f, -15f);
                    pose[(int)Bone.ForeArmL] = X(-40f);
                    pose[(int)Bone.ThighL] = X(-25f);
                    pose[(int)Bone.ShinL] = X(25f);
                    break;
                }
                case CrewAction.Fall:
                {
                    // Struck down: knees give, he crumples to the deck and lies.
                    var t = math.saturate(p);
                    var down = Smooth(0f, 0.7f, t);
                    pose.Pelvis = new float3(0f, -0.75f * down, -0.25f * down);
                    pose[(int)Bone.Pelvis] = X(-80f * Smooth(0.3f, 1f, t));
                    pose[(int)Bone.Spine] = X(15f * down);
                    pose[(int)Bone.ThighL] = X(-40f * down);
                    pose[(int)Bone.ShinL] = X(60f * down);
                    pose[(int)Bone.ThighR] = X(-10f * down);
                    pose[(int)Bone.UpperArmL] = XYZ(-30f * down, 0f, -40f * down);
                    pose[(int)Bone.UpperArmR] = XYZ(-50f * down, 0f, 30f * down);
                    pose[(int)Bone.Head] = XYZ(20f * down, 30f * down, 0f);
                    break;
                }
                case CrewAction.Overboard:
                {
                    // Over the side: a step onto the rail and a fall outward; the station's frame carries him off (director).
                    var t = math.saturate(p);
                    pose[(int)Bone.Pelvis] = Z(-70f * Smooth(0.2f, 0.8f, t));
                    pose[(int)Bone.UpperArmL] = XYZ(-120f * t, 0f, -60f * t);
                    pose[(int)Bone.UpperArmR] = XYZ(-110f * t, 0f, 70f * t);
                    pose[(int)Bone.ThighL] = X(-60f * t);
                    pose[(int)Bone.ShinL] = X(70f * t);
                    break;
                }
                case CrewAction.Kneel:
                    // Yielding: down on a knee, hands empty and open.
                    pose.Pelvis = new float3(0f, -0.42f, 0f);
                    pose[(int)Bone.ThighL] = X(-80f);
                    pose[(int)Bone.ShinL] = X(85f);
                    pose[(int)Bone.ThighR] = X(5f);
                    pose[(int)Bone.ShinR] = X(95f);
                    pose[(int)Bone.FootR] = X(40f);
                    pose[(int)Bone.Spine] = X(15f);
                    pose[(int)Bone.Head] = X(25f);
                    pose[(int)Bone.UpperArmL] = XYZ(-40f, 0f, -20f);
                    pose[(int)Bone.UpperArmR] = XYZ(-40f, 0f, 20f);
                    pose[(int)Bone.ForeArmL] = X(-30f);
                    pose[(int)Bone.ForeArmR] = X(-30f);
                    break;
                case CrewAction.LieWounded:
                    // Lying on his back along the deck, a knee up.
                    pose.Pelvis = new float3(0f, -0.78f, 0f);
                    pose[(int)Bone.Pelvis] = X(-88f);
                    pose[(int)Bone.ThighL] = X(-40f);
                    pose[(int)Bone.ShinL] = X(70f);
                    pose[(int)Bone.UpperArmR] = XYZ(-20f, 0f, 40f);
                    pose[(int)Bone.ForeArmR] = X(-80f);
                    pose[(int)Bone.Head] = X(15f + Wave(p) * 3f);
                    break;
                case CrewAction.Walk:
                {
                    // A walking step at the pace the director gives; arms swing against the legs.
                    var w = Wave(p);
                    var lift = math.max(0f, -math.cos(p * math.PI * 2f));
                    pose.Pelvis = new float3(0f, -0.02f + math.abs(w) * 0.025f, 0f);
                    pose[(int)Bone.ThighL] = X(-25f * w);
                    pose[(int)Bone.ThighR] = X(25f * w);
                    pose[(int)Bone.ShinL] = X(10f + 35f * math.max(0f, w));
                    pose[(int)Bone.ShinR] = X(10f + 35f * math.max(0f, -w));
                    pose[(int)Bone.UpperArmL] = X(18f * w);
                    pose[(int)Bone.UpperArmR] = X(-18f * w);
                    pose[(int)Bone.ForeArmL] = X(-15f);
                    pose[(int)Bone.ForeArmR] = X(-15f);
                    pose[(int)Bone.Spine] = Y(-6f * w + lift * 0f);
                    break;
                }
            }
        }

        /// <summary>A seated man: thighs forward, shins down to his feet; the pelvis at the station's origin.</summary>
        static void Seat(ref FixedPose pose, float hip, float knee)
        {
            pose[(int)Bone.ThighL] = XYZ(-hip, 0f, -4f);
            pose[(int)Bone.ThighR] = XYZ(-hip, 0f, 4f);
            pose[(int)Bone.ShinL] = X(knee);
            pose[(int)Bone.ShinR] = X(knee);
            pose[(int)Bone.FootL] = X(-(knee - hip) * 0.5f);
            pose[(int)Bone.FootR] = X(-(knee - hip) * 0.5f);
        }

        // Forward kinematics and reaching ----------------------------------------------------------------------------
        /// <summary>
        /// Every bone's world matrix for a man: his station's frame, the pose's turns down the hierarchy at his height, then
        /// his hands and feet put where they must be by two-bone reaching, which bends the elbow and knee the way the pose
        /// bent them.
        /// </summary>
        public static void Solve(in FigureInput figure, NativeSlice<float4x4> bones)
        {
            var pose = new FixedPose();
            Evaluate(figure, ref pose);
            var scale = figure.Height / CrewBody.ReferenceHeight;
            var world = new FixedMatrices();
            for (var i = 0; i < Bones; i++)
            {
                var parent = Parent(i);
                var offset = Offset(i) * scale;
                if (i == 0)
                {
                    // Seated men sit on the station's origin (the top of the seat); standing ones stand on it.
                    var root = figure.Seated ? new float3(0f, 0.1f * scale, 0f) : new float3(0f, offset.y, 0f);
                    world[i] = math.mul(figure.Frame, float4x4.TRS(root + pose.Pelvis, pose[i], new float3(1f)));
                }
                else world[i] = math.mul(world[parent], float4x4.TRS(offset, pose[i], new float3(1f)));
            }
            if ((figure.Hands & 1) != 0) Reach(ref world, (int)Bone.UpperArmL, math.transform(figure.Frame, figure.HandLeft), scale, CrewBody.UpperArm, CrewBody.ForeArm + CrewBody.Palm, -1f);
            if ((figure.Hands & 2) != 0) Reach(ref world, (int)Bone.UpperArmR, math.transform(figure.Frame, figure.HandRight), scale, CrewBody.UpperArm, CrewBody.ForeArm + CrewBody.Palm, 1f);
            if ((figure.Feet & 1) != 0) Reach(ref world, (int)Bone.ThighL, math.transform(figure.Frame, figure.FootLeft) + new float3(0f, CrewBody.AnkleHeight * scale, 0f), scale, CrewBody.Thigh, CrewBody.Shank, -1f, true);
            if ((figure.Feet & 2) != 0) Reach(ref world, (int)Bone.ThighR, math.transform(figure.Frame, figure.FootRight) + new float3(0f, CrewBody.AnkleHeight * scale, 0f), scale, CrewBody.Thigh, CrewBody.Shank, 1f, true);
            for (var i = 0; i < Bones; i++) bones[i] = world[i];
            if (bones.Length < CrewRenderer.Matrices) return;

            // Sockets: what he holds rides in his fist (its length along the hand's front), else it is slung on his back
            // or stands at his station; his shield on the left forearm or on his back.
            var grip = new float3(0f, -0.085f * scale, 0f);
            var held = quaternion.RotateX(math.radians(90f));
            var back = math.mul(world[(int)Bone.Chest], float4x4.TRS(new float3(0f, 0.16f * scale, -0.17f * scale), math.mul(quaternion.RotateY(math.PI), quaternion.RotateZ(math.radians(35f))), new float3(1f)));
            bones[CrewRenderer.Weapon] = figure.WeaponMode switch
            {
                1 => math.mul(world[(int)Bone.HandR], float4x4.TRS(grip, held, new float3(1f))),
                3 => math.mul(world[(int)Bone.HandL], float4x4.TRS(grip, held, new float3(1f))),
                2 => back,
                _ => math.mul(figure.Frame, float4x4.TRS(figure.Rest, quaternion.identity, new float3(1f))),
            };
            bones[CrewRenderer.ShieldSocket] = figure.ShieldMode == 1
                ? math.mul(world[(int)Bone.ForeArmL], float4x4.TRS(new float3(-0.06f * scale, -0.13f * scale, 0f), math.mul(quaternion.RotateX(math.radians(90f)), quaternion.RotateZ(math.radians(-90f))), new float3(1f)))
                : math.mul(world[(int)Bone.Chest], float4x4.TRS(new float3(0f, 0.12f * scale, -0.2f * scale), quaternion.RotateY(math.PI), new float3(1f)));
            bones[CrewRenderer.Extra] = math.mul(world[(int)Bone.Chest], float4x4.TRS(new float3(0.09f * scale, 0.12f * scale, -0.16f * scale), quaternion.RotateZ(math.radians(-15f)), new float3(1f)));
        }

        /// <summary>
        /// Puts the end of a two-bone limb (upper arm and forearm with the palm, or thigh and shank) on a target: the
        /// elbow or knee bends in the plane its pose already bent it in, so arms stay arms and knees bend forward.
        /// Out of reach, the limb stretches toward the target and stops; it never stretches longer than it is.
        /// </summary>
        static void Reach(ref FixedMatrices world, int upper, float3 target, float scale, float lengthA, float lengthB, float side, bool leg = false)
        {
            var middle = upper + 1;
            var end = upper + 2;
            var a = lengthA * scale;
            var b = lengthB * scale;
            var root = world[upper].c3.xyz;
            var joint = world[middle].c3.xyz;
            var toTarget = target - root;
            var distance = math.clamp(math.length(toTarget), math.abs(a - b) + 0.01f, a + b - 0.002f);
            var direction = math.normalizesafe(toTarget, new float3(0f, -1f, 0f));
            // The bend plane: from where the joint already is, away from the line to the target.
            var bend = joint - root - direction * math.dot(joint - root, direction);
            if (math.lengthsq(bend) < 1e-6f)
            {
                var forward = world[upper].c2.xyz;
                bend = leg ? forward : -forward + new float3(0f, 0f, 0f);
            }
            bend = math.normalizesafe(bend, new float3(0f, 0f, 1f));
            var cosA = math.clamp((a * a + distance * distance - b * b) / (2f * a * distance), -1f, 1f);
            var sinA = math.sqrt(1f - cosA * cosA);
            var elbow = root + direction * (a * cosA) + bend * (a * sinA);
            var hand = root + direction * distance;
            var hint = world[upper].c2.xyz;
            world[upper] = Segment(root, elbow, hint, scale, world[upper]);
            world[middle] = Segment(elbow, hand, hint, scale, world[middle]);
            // The hand or foot keeps its turn relative to the forearm or shank, at the end.
            var endLocal = math.mul(math.inverse(world[middle]), world[end]);
            endLocal.c3 = new float4(0f, -(leg ? CrewBody.Shank : CrewBody.ForeArm) * scale, 0f, 1f);
            world[end] = math.mul(world[middle], endLocal);
        }

        /// <summary>A segment hanging from its joint (local -Y) to a point, its front kept toward a hint.</summary>
        static float4x4 Segment(float3 from, float3 to, float3 hint, float scale, float4x4 previous)
        {
            var down = math.normalizesafe(to - from, new float3(0f, -1f, 0f));
            var up = -down;
            var front = hint - up * math.dot(hint, up);
            if (math.lengthsq(front) < 1e-6f) front = previous.c0.xyz;
            var rotation = quaternion.LookRotationSafe(math.normalizesafe(front), up);
            return float4x4.TRS(from, rotation, new float3(1f));
        }

        static int Parent(int bone) => bone switch { 0 => -1, 1 => 0, 2 => 1, 3 => 2, 4 => 3, 5 => 2, 6 => 5, 7 => 6, 8 => 2, 9 => 8, 10 => 9, 11 => 0, 12 => 11, 13 => 12, 14 => 0, 15 => 14, _ => 15 };

        static float3 Offset(int bone) => bone switch
        {
            0 => new float3(0f, 0.900f, 0f),
            1 => new float3(0f, 0.075f, -0.01f),
            2 => new float3(0f, 0.155f, 0.005f),
            3 => new float3(0f, 0.245f, 0.005f),
            4 => new float3(0f, 0.075f, 0.01f),
            5 => new float3(-0.185f, 0.21f, -0.01f),
            6 => new float3(0f, -0.309f, 0f),
            7 => new float3(0f, -0.242f, 0f),
            8 => new float3(0.185f, 0.21f, -0.01f),
            9 => new float3(0f, -0.309f, 0f),
            10 => new float3(0f, -0.242f, 0f),
            11 => new float3(-0.085f, -0.02f, 0f),
            12 => new float3(0f, -0.407f, 0f),
            13 => new float3(0f, -0.408f, 0f),
            14 => new float3(0.085f, -0.02f, 0f),
            15 => new float3(0f, -0.407f, 0f),
            _ => new float3(0f, -0.408f, 0f),
        };
    }

    /// <summary>Seventeen local turns and the pelvis's offset, without allocation (Burst).</summary>
    public struct FixedPose
    {
        quaternion r0, r1, r2, r3, r4, r5, r6, r7, r8, r9, r10, r11, r12, r13, r14, r15, r16;
        public float3 Pelvis;

        public quaternion this[int i]
        {
            get => i switch { 0 => r0, 1 => r1, 2 => r2, 3 => r3, 4 => r4, 5 => r5, 6 => r6, 7 => r7, 8 => r8, 9 => r9, 10 => r10, 11 => r11, 12 => r12, 13 => r13, 14 => r14, 15 => r15, _ => r16 };
            set
            {
                switch (i)
                {
                    case 0: r0 = value; break;
                    case 1: r1 = value; break;
                    case 2: r2 = value; break;
                    case 3: r3 = value; break;
                    case 4: r4 = value; break;
                    case 5: r5 = value; break;
                    case 6: r6 = value; break;
                    case 7: r7 = value; break;
                    case 8: r8 = value; break;
                    case 9: r9 = value; break;
                    case 10: r10 = value; break;
                    case 11: r11 = value; break;
                    case 12: r12 = value; break;
                    case 13: r13 = value; break;
                    case 14: r14 = value; break;
                    case 15: r15 = value; break;
                    default: r16 = value; break;
                }
            }
        }
    }

    /// <summary>Seventeen matrices without allocation (Burst).</summary>
    public struct FixedMatrices
    {
        float4x4 m0, m1, m2, m3, m4, m5, m6, m7, m8, m9, m10, m11, m12, m13, m14, m15, m16;

        public float4x4 this[int i]
        {
            get => i switch { 0 => m0, 1 => m1, 2 => m2, 3 => m3, 4 => m4, 5 => m5, 6 => m6, 7 => m7, 8 => m8, 9 => m9, 10 => m10, 11 => m11, 12 => m12, 13 => m13, 14 => m14, 15 => m15, _ => m16 };
            set
            {
                switch (i)
                {
                    case 0: m0 = value; break;
                    case 1: m1 = value; break;
                    case 2: m2 = value; break;
                    case 3: m3 = value; break;
                    case 4: m4 = value; break;
                    case 5: m5 = value; break;
                    case 6: m6 = value; break;
                    case 7: m7 = value; break;
                    case 8: m8 = value; break;
                    case 9: m9 = value; break;
                    case 10: m10 = value; break;
                    case 11: m11 = value; break;
                    case 12: m12 = value; break;
                    case 13: m13 = value; break;
                    case 14: m14 = value; break;
                    case 15: m15 = value; break;
                    default: m16 = value; break;
                }
            }
        }
    }

    /// <summary>Poses every man of the frame at once on worker threads, compiled by Burst.</summary>
    [BurstCompile(CompileSynchronously = true)]
    public struct CrewPoseJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<FigureInput> Figures;
        [NativeDisableParallelForRestriction] public NativeArray<float4x4> Bones;

        public void Execute(int index) => CrewPose.Solve(Figures[index], Bones.Slice(index * CrewRenderer.Matrices, CrewRenderer.Matrices));
    }
}
