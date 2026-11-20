using System;
using System.Collections.Generic;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// Belvedere Camp's survivors on the pilot model (the DEV preview; docs/expedition/BELVEDERE-CAMP.md "Survivors"). Owner 2026-11-20:
    /// "For the people in the hub, We can use our player model. Of course, dress the NPC's appropriately". Each post (NativeCamp.Posts)
    /// has a role: an outfit on the model (a clean variant through the zombies' dressing: the pack's pieces, the garment bank's apron, lab
    /// coat, coveralls and scrubs, a human skin, no wear) and a day's loop of pack clips (the Crafting pack's chair, work and idle clips, the
    /// RPG pack's unarmed alert and injured idles), baked by the local tool into the camp's own motion bank (Resources/ChipArenaCampMotion:
    /// local, never committed, never the match's or the intros' bank) and played as keyed poses (NativeIntroPack), with a .45 s ease
    /// between beats and a seated clip's hips lowered onto the bench with its feet kept on the floor. A rescue's arrival brings a cheer or
    /// a wave from the standing civilians in view.
    /// At most six draw skinned, the nearest within 30 m (with 3 m of slack); the rest stay the bank's posed figures.
    /// Presentation only: nothing here is replicated or touches the simulation; pure in time and seed, allocation free per sample.
    /// </summary>
    public static class NativeCampSurvivors
    {
        public const string MotionResource="ChipArenaCampMotion";
        public const int MaxSkinned=6;
        public const float Near=30f,Slack=3f,Ease=.45f;
        /// <summary>The bench's seat top (tools/stages/camp_parts.py bench) and where the Crafting pack's chair clips seat the model's thighs
        /// (their underside as the CampChecks motion probe measures it): a seated clip's hips go down by the difference.</summary>
        public const float BenchSeat=.47f,ChairSeat=.55f;
        public enum Stance:byte{Standing,Seated,Kneeling}
        /// <summary>A beat of a role's loop: a clip for `Seconds` (looped, or held on its last frame past its end) at `Speed`, holding `Prop`
        /// (a bank key, camp/prop/...) in its right hand or none.</summary>
        public readonly struct Beat
        {
            public readonly string Clip,Prop;public readonly float Seconds,Speed;public readonly bool Loop;
            public Beat(string clip,float seconds,bool loop=true,float speed=1,string prop=null){Clip=clip;Seconds=seconds;Loop=loop;Speed=speed;Prop=prop;}
        }
        public sealed class Role
        {
            public string Post,Name;public Stance Stance;public Beat[] Beats;public NativeZombieLooks.Variant Look;
            /// <summary>Worn on the head the whole time (camp/prop/headset), or null.</summary>
            public string Head;
            /// <summary>Whether a rescue brings a cheer or a wave (the standing civilians; never a sentry at his post).</summary>
            public bool Cheers;
            /// <summary>The loop's length (s).</summary>
            public float Length;
        }

        // ---- the clips (the bank's names: the RPG pack's own clip names, the Crafting pack's file names) ----
        public const string Idle="Crafter@Idle",Bored="Crafter@Idle-Bored1",Use="Crafter@Item-Use",Hammer="Crafter@Item-Hammer-Table",Chisel="Crafter@Item-Chisel",
            KneelIdle="Crafter@Item-Kneel-Idle",KneelHammer="Crafter@Item-Kneel-Hammer",ChairIdle="Crafter@Chair-Idle",ChairTalk="Crafter@Chair-Talk1",ChairDrink="Crafter@Chair-Drink",
            ChairEat="Crafter@Chair-Eat",Cheer1="Crafter@Cheer1",Cheer2="Crafter@Cheer2",Wave="Crafter@Wave1",Alert="Unarmed-Idle-Alert1",Watch="Unarmed-Idle",
            Injured="Unarmed-Idle-Injured1",Stunned="Unarmed-Stunned";
        /// <summary>Every clip a role plays (the bake's list must hold them; CampChecks).</summary>
        public static IEnumerable<string> Clips(){var seen=new HashSet<string>(StringComparer.Ordinal);foreach(var r in Roles)foreach(var b in r.Beats)if(seen.Add(b.Clip))yield return b.Clip;foreach(var c in new[]{Cheer1,Cheer2,Wave})if(seen.Add(c))yield return c;}
        /// <summary>The props (camp bank keys; "l:" in a beat: held in the left fist).</summary>
        public const string Mug="camp/prop/mug",Bowl="l:camp/prop/bowl",HammerProp="camp/prop/hammer",Headset="camp/prop/headset";
        /// <summary>A prop's seat in its fist: its offset (m) and turn (degrees) in the binding's weapon frame (the game wrist's turn at the
        /// visual fist; a sword's grip runs along its +z).</summary>
        public readonly struct Seat{public readonly float X,Y,Z,Pitch,Yaw,Roll;public Seat(float x,float y,float z,float pitch=0,float yaw=0,float roll=0){X=x;Y=y;Z=z;Pitch=pitch;Yaw=yaw;Roll=roll;}}
        public static Seat Hold(string prop,bool left)
        {
            switch(prop)
            {
                case "camp/prop/mug":return new Seat(0,-.035f,.02f,0,0,0);
                case "camp/prop/bowl":return new Seat(0,-.05f,.02f,0,0,0);
                case "camp/prop/hammer":return new Seat(0,0,.03f,0,0,0);
                default:return new Seat(0,0,0);
            }
        }
        /// <summary>The headset on the head at rest: its centre this far above the head bone and forward of it (the skull's middle).</summary>
        public const float HeadsetUp=.085f,HeadsetForward=.015f;

        // ---- the outfits: the zombies' variant record, clean (no grime, tears, rot or blood), a human skin, no milky eyes ----
        private static float[] Body(float type,float weight,float muscle,float belly,float chest,float neck,float curvy,float butt,float waist,float maturity,float sharp=float.NaN,float round=float.NaN)
            =>new[]{type,weight,muscle,belly,chest,neck,curvy,butt,waist,maturity,sharp,round};
        private const int Keep=-1;
        private static NativeZombieLooks.Variant Dress(string id,bool female,string hair,string upper,string lower,string hands,string feet,int[] colours,int skin,float[] shapes,
            string[] garments=null,int[] garmentColours=null,string[] gear=null)
            =>new NativeZombieLooks.Variant{Id="camp-"+id,Kind=-1,Female=female,Pieces=new[]{hair,upper,lower,hands,feet},Colours=colours,Skin=skin,Eyes=-1,Shapes=shapes,
                Build=new NativeZombieLooks.Build(),Decay=Array.Empty<string>(),Garments=garments??Array.Empty<string>(),GarmentColours=garmentColours??Array.Empty<int>(),Gear=gear??Array.Empty<string>(),Upright=1};
        private static Role R(string post,string name,Stance stance,NativeZombieLooks.Variant look,Beat[] beats,string head=null,bool cheers=false)
        {
            float length=0;foreach(var b in beats)length+=b.Seconds;
            return new Role{Post=post,Name=name,Stance=stance,Look=look,Beats=beats,Head=head,Cheers=cheers,Length=length};
        }
        private static Beat B(string clip,float seconds,bool loop=true,string prop=null)=>new Beat(clip,seconds,loop,1,prop);
        public static readonly Role[] Roles=
        {
            // the mess ring: three round the brazier on the benches, a fourth (the attendant) when the pool is cleared
            R("fire-0","a regular",Stance.Seated,Dress("regular",false,"z-beanie|wild","z-hoodie-vest|hoodie","z-cargo-pants|baggy-pants","fingerless","work-boots",
                new[]{0x7A3B2E,0x3F4A5A,0x4A4A3C,Keep,0x3B2E24},0xD8A07A,Body(50,25,15,15,10,30,0,0,0,40)),
                new[]{B(ChairTalk,4f,false),B(ChairIdle,6f),B(ChairTalk,4f,false),B(ChairIdle,5f)}),
            R("fire-1","the cook",Stance.Seated,Dress("cook",true,"tied-back","z-work-shirt|tee","z-dress-pants|khakis","bare","lace-ups",
                new[]{0x2A1A12,0xD8D2C4,0x3C3A36,Keep,0x2A221C},0xAC7250,Body(-10,30,5,20,35,15,30,30,10,45),new[]{NativeZombieLooks.ApronGarment},new[]{0xC9C2B0}),
                new[]{B(ChairIdle,5f,true,Mug),B(ChairDrink,2.66f,true,Mug),B(ChairIdle,4f,true,Mug),B(ChairDrink,1.33f,true,Mug),B(ChairTalk,4f,false,Mug)}),
            R("fire-2","an old hand",Stance.Seated,Dress("elder",false,"side-swept","sweater","khakis","bare","lace-ups",
                new[]{0x9A9894,0x5A6B4A,0x5A4E3C,Keep,0x2E2620},0xE6B796,Body(45,35,5,35,10,35,0,0,10,80)),
                new[]{B(ChairEat,4.5f,true,Bowl),B(ChairIdle,6f),B(ChairTalk,4f,false)}),
            R("quartermaster","the Quartermaster",Stance.Standing,Dress("quartermaster",false,"z-newsboy|side-swept","z-work-shirt|tee","z-cargo-pants|baggy-pants","gloves","work-boots",
                new[]{0x4A4036,0x8A7A5A,0x4A4638,0x3A2E22,0x3B2E24},0xC48A62,Body(55,45,20,40,15,40,0,0,10,60),new[]{NativeZombieLooks.ApronGarment},new[]{0x5E3D25}),
                new[]{B(Hammer,4f,true,HammerProp),B(Bored,2.67f,false),B(Use,2.66f),B(Idle,3f)},cheers:true),
            R("doc","Doc",Stance.Standing,Dress("doc",true,"tied-back","z-work-shirt|tee","z-dress-pants|khakis","bare","z-loafers|lace-ups",
                new[]{0x2A1F18,0x9FB3C8,0x44464C,Keep,0x2A2522},0x91593C,Body(-10,10,5,5,30,10,25,20,0,50),new[]{NativeZombieLooks.CoatGarment},new[]{0xE4E4DC},new[]{"faceling-lanyard"}),
                new[]{B(Use,2.66f),B(Idle,4f),B(Bored,2.67f,false),B(Idle,3f)},cheers:true),
            R("radio","the radio operator",Stance.Seated,Dress("radio",false,"spiky","z-cardigan|sweater","khakis","bare","sneakers",
                new[]{0x1C1814,0x6B5A48,0x4A4A40,Keep,0x2A2A2E},0xF0CDB4,Body(45,15,10,15,10,25,0,0,0,30)),
                new[]{B(ChairIdle,8f),B(ChairTalk,4f,false),B(ChairIdle,5f)},head:Headset),
            // the lookout's cabin and the four sentry towers: on watch (the RPG pack's unarmed idle and its alert glance)
            R("lookout-0","a lookout",Stance.Standing,Dress("lookout-a",true,"z-beanie|tied-back","z-utility-jacket|hard-jacket","z-cargo-pants|baggy-pants","gloves","work-boots",
                new[]{0x3A4A3A,0x4A5A3A,0x3A3A34,0x222222,0x2E2620},0xE6B796,Body(-10,15,20,5,30,15,20,20,0,35)),
                new[]{B(Watch,5f),B(Alert,1.33f,false),B(Watch,3.4f)}),
            R("lookout-1","a lookout",Stance.Standing,Dress("lookout-b",false,"z-beanie|wild","z-utility-jacket|hard-jacket","z-cargo-pants|baggy-pants","gloves","work-boots",
                new[]{0x6A5A3A,0x5A5236,0x3A3A34,0x222222,0x2E2620},0x774530,Body(50,20,25,10,15,30,0,0,0,35)),
                new[]{B(Watch,3.4f),B(Alert,1.33f,false),B(Watch,5f)}),
            R("sentry-n","a sentry",Stance.Standing,Dress("sentry-n",false,"z-military-cap|side-swept","z-utility-jacket|hard-jacket","z-cargo-pants|baggy-pants","gloves","work-boots",
                new[]{0x3A4436,0x4A5A3A,0x3E3E36,0x1E1E1E,0x2A2420},0xD8A07A,Body(55,15,35,5,15,35,0,0,0,30)),
                new[]{B(Watch,6.7f),B(Alert,1.33f,false)}),
            R("sentry-e","a sentry",Stance.Standing,Dress("sentry-e",true,"z-beanie|tied-back","hard-jacket","z-cargo-pants|baggy-pants","gloves","work-boots",
                new[]{0x222630,0x2C3442,0x3E3E36,0x1E1E1E,0x2A2420},0x5C3523,Body(-10,10,25,0,30,15,20,25,0,30)),
                new[]{B(Watch,5f),B(Alert,1.33f,false),B(Watch,1.7f)}),
            R("sentry-s","a sentry",Stance.Standing,Dress("sentry-s",false,"z-beanie|spiky","z-utility-jacket|hard-jacket","z-cargo-pants|baggy-pants","gloves","work-boots",
                new[]{0x5A2A22,0x5A5236,0x3E3E36,0x1E1E1E,0x2A2420},0xF0CDB4,Body(50,25,20,15,15,30,0,0,0,40)),
                new[]{B(Watch,3.4f),B(Alert,1.33f,false),B(Watch,3.4f)}),
            R("sentry-w","a sentry",Stance.Standing,Dress("sentry-w",false,"z-military-cap|wild","hard-jacket","z-cargo-pants|baggy-pants","gloves","work-boots",
                new[]{0x3A4436,0x30342C,0x3E3E36,0x1E1E1E,0x2A2420},0x91593C,Body(55,10,35,0,15,35,0,0,0,25)),
                new[]{B(Watch,8f),B(Alert,1.33f,false)}),
            // Sparks keeps the REVIVE-O-MATIC going, on one knee at its works
            R("sparks","Sparks, the mechanic",Stance.Kneeling,Dress("sparks",false,"z-ball-cap|spiky","tee","baggy-pants","gloves","work-boots",
                new[]{0xB03A2A,0x6E6A60,0x3A4A6A,0x2A2620,0x2E2620},0xE6B796,Body(50,20,30,10,15,30,0,0,0,30),new[]{NativeZombieLooks.CoverallsGarment},new[]{0x3E5A7A}),
                new[]{B(KneelIdle,3.2f,true,HammerProp),B(KneelHammer,3f,true,HammerProp),B(KneelIdle,1.6f,true,HammerProp)}),
            // the rescued, one per cleared instance
            R("nurse","a nurse",Stance.Standing,Dress("nurse",true,"tied-back","z-scrubs-top|tee","z-scrub-pants|baggy-pants","bare","sneakers",
                new[]{0x3A2A1C,0x3E8C86,0x3E8C86,Keep,0xE2E2E2},0xD8A07A,Body(-10,15,5,5,30,10,25,20,0,35),new[]{NativeZombieLooks.ScrubsTop,NativeZombieLooks.ScrubsPants},new[]{0x3E8C86,0x3E8C86}),
                new[]{B(Idle,4f),B(Bored,2.67f,false),B(Idle,3f)},cheers:true),
            R("clerk","a ticket clerk",Stance.Seated,Dress("clerk",false,"side-swept","z-sweater-vest|sweater","z-dress-pants|khakis","bare","z-loafers|lace-ups",
                new[]{0x3A2A1C,0x5A4A3A,0x2C2E35,Keep,0x1E1A18},0xC48A62,Body(45,20,5,20,10,25,0,0,5,55)),
                new[]{B(ChairIdle,6f),B(ChairTalk,4f,false)}),
            R("welder","a welder",Stance.Standing,Dress("welder",false,"z-beanie|spiky","z-work-shirt|tee","z-cargo-pants|baggy-pants","gloves","work-boots",
                new[]{0x2A2A2A,0x6B6E70,0x3E3A32,0x3A2E22,0x2E2620},0x774530,Body(55,30,35,20,20,40,0,0,10,45),new[]{NativeZombieLooks.ApronGarment},new[]{0x4E3220}),
                new[]{B(Chisel,3.34f),B(Hammer,4f,true,HammerProp),B(Idle,3f)},cheers:true),
            R("office","an office worker",Stance.Standing,Dress("office",true,"side-swept","z-blouse|tee","z-dress-pants|khakis","bare","z-flats|sneakers",
                new[]{0x3A2A1C,0xD0D6DE,0x2C2E35,Keep,0x1E1A18},0xF6DFCF,Body(-10,5,0,0,30,5,25,20,0,30)),
                new[]{B(Injured,5f),B(Stunned,2f),B(Injured,3.34f)}),
            R("attendant","a pool attendant",Stance.Seated,Dress("attendant",false,"wild","z-short-shirt|tee","z-shorts|cuffed-shorts","bare","z-flip-flops|sneakers",
                new[]{0x2A1A10,0xC83A2A,0x2A3A6A,Keep,0x2A2A2A},0xAC7250,Body(50,10,30,0,15,25,0,0,0,25)),
                new[]{B(ChairIdle,7f),B(ChairTalk,4f,false)}),
        };
        public static Role Of(string post){foreach(var r in Roles)if(r.Post==post)return r;return null;}

        // ---- sampling ----
        /// <summary>Scratch for Sample (one per caller; main thread).</summary>
        public sealed class Scratch
        {
            public readonly CapturedRig.Pose A=new CapturedRig.Pose(),B=new CapturedRig.Pose();
            public readonly Quaternion[] Joints=new Quaternion[CapturedRig.Bones.Length];
        }
        /// <summary>Where role `r` is in its loop at `time` (seconds; `seed` turns each survivor's loop to its own phase): the beat, its time
        /// in, the previous beat and how far the ease into this beat has come (1: none).</summary>
        public static void Where(Role r,double time,uint seed,out int beat,out float into,out int previous,out float ease)
        {
            double phase=(seed%997)/997.0*r.Length;double c=(time+phase)%r.Length;if(c<0)c+=r.Length;
            beat=0;double start=0;
            for(int i=0;i<r.Beats.Length;i++){if(c<start+r.Beats[i].Seconds||i==r.Beats.Length-1){beat=i;break;}start+=r.Beats[i].Seconds;}
            into=(float)(c-start);previous=(beat+r.Beats.Length-1)%r.Beats.Length;ease=r.Beats.Length<2?1:Smooth(into/Ease);
        }
        private static float Smooth(float u){u=u<0?0:u>1?1:u;return u*u*(3-2*u);}
        /// <summary>
        /// Role `r`'s pose at `time` into `pose` (rig frame, World() run) from `bank`; false without the bank or its clips (the figure stays
        /// the bank's posed one). A reaction (`react`: Cheer1, Cheer2 or Wave, since `reactAt`, its whole length) takes over, eased in and
        /// out. `prop`: the beat's right-hand prop (or null).
        /// </summary>
        public static bool Sample(Role r,NativeIntroPack.Bank bank,double time,uint seed,CapturedRig.Pose pose,Scratch s,out string prop,string react=null,double reactAt=double.NaN)
        {
            prop=null;if(bank==null||r==null)return false;
            Where(r,time,seed,out int beat,out float into,out int previous,out float ease);
            var now=r.Beats[beat];if(!Clip(bank,now,into,r.Stance,s.A,s.Joints))return false;prop=now.Prop;
            if(ease<.9999f)
            {
                var was=r.Beats[previous];
                if(Clip(bank,was,was.Seconds+into,r.Stance,s.B,s.Joints)){NativeDeployEject.Blend(s.B,s.A,ease,pose);if(into<Ease*.5f)prop=was.Prop;}else Copy(s.A,pose);
            }
            else Copy(s.A,pose);
            // a rescue's cheer or wave over the loop: eased in over .3 s, out over its last .5 s
            if(react!=null&&!double.IsNaN(reactAt)&&bank.TryGet(react,out var rc))
            {
                float u=(float)(time-reactAt);
                if(u>=0&&u<rc.Length)
                {
                    float w=Smooth(u/.3f)*Smooth((rc.Length-u)/.5f);
                    if(w>0&&Clip(bank,new Beat(react,rc.Length,false),u,Stance.Standing,s.B,s.Joints)){Copy(pose,s.A);NativeDeployEject.Blend(s.A,s.B,w,pose);if(w>.5f)prop=null;}
                }
            }
            return true;
        }
        /// <summary>A reaction for a survivor's arrival (`seed`): Cheer1, Cheer2 or the wave.</summary>
        public static string Reaction(uint seed)=>(seed>>3)%3==0?Wave:(seed>>3)%3==1?Cheer1:Cheer2;
        // One beat's clip at `t` seconds into it into `into`: the keyed joints, the keyed body on the clip's pelvis; a seated clip's hips lowered
        // onto the bench (BenchSeat), its feet kept where the clip has them (the keyed legs re-solved as hinges, LegRatio shorter than drawn).
        private static bool Clip(NativeIntroPack.Bank bank,in Beat b,float t,Stance stance,CapturedRig.Pose into,Quaternion[] joints)
        {
            if(!bank.TryGet(b.Clip,out var c))return false;
            into.Reset();c.Sample(t*b.Speed,b.Loop,joints,out var body);
            for(int j=1;j<joints.Length;j++)into.Rotation[j]=joints[j];
            into.Position[CapturedRig.Body]=CapturedRig.Bones[CapturedRig.Body].Position+body;into.World();
            if(stance==Stance.Seated&&b.Clip.StartsWith("Crafter@Chair-",StringComparison.Ordinal))Lower(into,(BenchSeat-ChairSeat)/Math.Max(.1f,bank.Skeleton.LegRatio));
            return true;
        }
        /// <summary>The keyed body moved down by `drop` (rig metres; negative lowers), the legs re-solved to keep both feet (and their turns)
        /// where they were.</summary>
        public static void Lower(CapturedRig.Pose p,float drop)
        {
            p.World();
            Span<Vector3> at=stackalloc Vector3[2];Span<Quaternion> turn=stackalloc Quaternion[2];
            for(int side=0;side<2;side++){int foot=side==0?CapturedRig.LFoot:CapturedRig.RFoot;at[side]=p.WorldPosition[foot];turn[side]=p.WorldRotation[foot];}
            p.Position[CapturedRig.Body]+=new Vector3(0,drop,0);p.World();
            for(int side=0;side<2;side++)
            {
                bool left=side==0;int hip=left?CapturedRig.LHip:CapturedRig.RHip,knee=hip+1,foot=hip+2;
                var f=Vector3.Transform(Vector3.UnitZ,turn[side]);NativeEntrancePose.HingedLeg(p,left,at[side],(float)Math.Atan2(f.X,f.Z));
                p.World();p.Rotation[foot]=Quaternion.Normalize(Quaternion.Inverse(p.WorldRotation[knee])*turn[side]);p.World();
            }
        }
        private static void Copy(CapturedRig.Pose a,CapturedRig.Pose o){for(int i=0;i<CapturedRig.Bones.Length;i++){o.Position[i]=a.Position[i];o.Rotation[i]=a.Rotation[i];}o.World();}

        // ---- who draws skinned ----
        /// <summary>
        /// The posts drawn on the model this frame: of `at` (each post's world point, NaN x for one not shown), the nearest `MaxSkinned`
        /// within Near of the eye, a post already drawn (`drawn`, in and out) kept until past Near+Slack or until a newcomer is nearer by more
        /// than the slack. Pure; `drawn` holds post indices, -1 for none.
        /// </summary>
        public static void Pick(Vector3 eye,ReadOnlySpan<Vector3> at,int[] drawn)
        {
            Span<float> d=stackalloc float[at.Length];for(int i=0;i<at.Length;i++)d[i]=float.IsNaN(at[i].X)?float.MaxValue:Vector3.Distance(eye,at[i]+new Vector3(0,1,0));
            // drop the far ones
            for(int k=0;k<drawn.Length;k++){int i=drawn[k];if(i>=0&&(i>=at.Length||d[i]>Near+Slack))drawn[k]=-1;}
            // take the nearest free one while a slot is free or it beats the farthest drawn by more than the slack
            for(int round=0;round<at.Length;round++)
            {
                int best=-1;for(int i=0;i<at.Length;i++){if(d[i]>Near||Array.IndexOf(drawn,i)>=0)continue;if(best<0||d[i]<d[best]||d[i]==d[best]&&i<best)best=i;}
                if(best<0)break;
                int free=Array.IndexOf(drawn,-1);
                if(free>=0){drawn[free]=best;continue;}
                int far=0;for(int k=1;k<drawn.Length;k++)if(d[drawn[k]]>d[drawn[far]])far=k;
                if(d[best]+Slack<d[drawn[far]]){drawn[far]=best;continue;}
                break;
            }
        }
    }
}
