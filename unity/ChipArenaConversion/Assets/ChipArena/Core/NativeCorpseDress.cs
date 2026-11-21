using System;
namespace ChipArena.UnityConversion
{
    /// <summary>
    /// What a dead pilot's model corpse starts from, drops and keeps wearing (owner 2026-11-14: "Yes, fix them", on the three corpse gaps
    /// found with the helmet's loss at death; Runtime NativeCorpseDressPresentation.cs). Pure; presentation only: no rule, damage, timing
    /// or networking reads it.
    /// - The pose: a death seen as it happens starts from the body as it was last drawn (its pack clips, stride and hand fits), so the
    ///   corpse's first frame shows the pose the frame before it showed (it used to snap to the game rig's pose). A death first seen late
    ///   (a guest's first sight of it, a body rebuilt after the killcam's reset) or by a pilot whose body was not drawn (the local pilot in
    ///   first person, the voxel bodies) has no drawn body of that moment and starts from the game rig's pose, as before.
    /// - The weapon: what the hand held drops loose and settles with the body, the model, skin and modules it was drawn with (a cut
    ///   body's with the half its hand is on, a cut-off hand's flying with it); what the game puts away or wears as part of the body stays
    ///   hidden (Of).
    /// - The gear: the chest core, the wrist device and the back emblem stay on the body, each on the side of a cut its bone went to,
    ///   their light going out (DeadGlow).
    /// </summary>
    public static class NativeCorpseDress
    {
        // ---- the pose ----
        /// <summary>The oldest death (s, on the clock the corpse is made on) that starts from the drawn pose, and how many frames before
        /// the corpse's the body may last have been drawn.</summary>
        public const float DrawnAge=.25f;public const int DrawnFrames=2;
        /// <summary>Whether a corpse made `age` s after its death may start from the drawn pose (one seen as it happens).</summary>
        public static bool FromDrawn(float age)=>age>=0&&age<DrawnAge;
        /// <summary>Whether a body last posed in frame `posed` shows the death's moment in frame `frame` (drawn this frame or just before).</summary>
        public static bool Drawn(int posed,int frame)=>posed<=frame&&posed>=frame-DrawnFrames;

        // ---- the weapon ----
        /// <summary>What happens to a held weapon at death: it drops; it is part of the body (fists, gauntlets); the game puts it away
        /// (hammerspace); it is ammunition drawn by the moment; or the class holds none.</summary>
        public enum Held{Drops,Body,PutAway,Ammo,None}
        /// <summary>
        /// The held weapon of `spec` at death. Drops: the guns and their series, the sword, greatsword, daggers, shield, bow, the four staffs
        /// and L33t's, the Katana (drawn), the Glaive's ring (in hand), and so the DEV Greatsword and DEV Katana (they play those). Kept off:
        /// the fighters' fists and the Cyber Lord's gauntlets (worn: part of the body); the Top Dog's mallet (put away, hammerspace) and the
        /// Fangs' paired daggers (kept with the body, the coordinator's call); the Bombadier's TNT (lit and thrown by the moment: a loose
        /// bundle would read as a live bomb); the Siren (no weapon).
        /// </summary>
        public static Held Of(string spec)
        {
            switch(spec)
            {
                case null:case "":case "siren":return Held.None;
                case "emperor":case "ansatsuken":case CyberLordSpec.Id:return Held.Body;
                case NativeTopDog.Spec:case NativeFangs.Spec:return Held.PutAway;
                case NativeBombs.Spec:return Held.Ammo;
                default:return Held.Drops;
            }
        }
        public static bool Drops(string spec)=>Of(spec)==Held.Drops;
        /// <summary>The hand the dropped weapon leaves: the staffs', L33t's and the bow's left (NativeLobbyPoses.HeldBone), else the right.</summary>
        public static int Hand(string spec)=>NativeLobbyPoses.HeldBone(spec);
        /// <summary>Whether the other hand holds a copy that stays in it (the daggers' second blade, gripped as the hand falls).</summary>
        public static bool OffHand(string spec)=>spec=="daggers";
        /// <summary>The dropped weapon's box (m, each side) round the model it carries: its size, never thinner than BoxLeast (a blade's
        /// edge would tunnel through a floor) nor longer than BoxMost.</summary>
        public const float BoxLeast=.04f,BoxMost=1.8f;
        /// <summary>How fast (m/s) the dropped weapon eases out of a floor or wall its model started in (a blade held by a wall, a muzzle held
        /// low), rather than jumping out at PhysX's own 10 m/s. (The first-frame drop filmed on 2026-11-14 was not this: a probe showed the weapon
        /// starting where it was drawn; the local pilot's corpse in that capture caught up several physics steps per frame.)</summary>
        public const float SeatEase=1f;
        public static float Box(float size)=>MathUtil.Finite(size)?MathUtil.Clamp(size,BoxLeast,BoxMost):BoxLeast;

        // ---- the gear ----
        /// <summary>How long the gear keeps its light after the death (s), how long it takes to fail, the faint glow it keeps after, and its
        /// stutter (the slot length and the most of them that drop out at the end).</summary>
        public const float GlowHold=.15f,GlowOut=1.1f,GlowLeft=.06f,StutterSlot=1/14f,StutterMost=.55f,StutterLow=.2f;
        /// <summary>The core's, emblem's and device's light `since` s after the death (1: as alive): held a moment, then failing in
        /// stutters (more as it fails) to GlowLeft, seeded per pilot. Never above 1 or below GlowLeft.</summary>
        public static float DeadGlow(float since,int seed)
        {
            if(!MathUtil.Finite(since)||since<GlowHold)return 1;
            float t=(since-GlowHold)/GlowOut;if(t>=1)return GlowLeft;
            float left=1-MathUtil.Smooth(t);
            bool off=Hash(seed,(int)Math.Floor(since/StutterSlot))<StutterMost*t;
            return GlowLeft+(1-GlowLeft)*left*(off?StutterLow:1);
        }
        private static float Hash(int seed,int k){unchecked{uint x=(uint)seed*0x9e3779b1u^(uint)k*0x85ebca6bu;x^=x>>15;x*=0x2c1b3c6du;x^=x>>12;x*=0x297a2d39u;x^=x>>15;return (x&0xffffff)/16777216f;}}
        /// <summary>The device on a dead wrist: its lid shut, no chip, the bay dark, the strips and lens at the dead glow.</summary>
        public static NativePilotGear.DeviceState Device(float glow)
        {
            var d=NativePilotGear.Device(0,float.NaN,float.NaN,0);float g=MathUtil.Finite(glow)?MathUtil.Clamp(glow,0,1):1;
            d.Strips*=g;d.Lens*=g;d.Bay=0;d.Chip=false;d.BayChip=false;d.LensChip=false;return d;
        }
    }
}
