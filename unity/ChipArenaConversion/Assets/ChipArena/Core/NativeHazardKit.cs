using System;
using System.Collections.Generic;

namespace ChipArena.UnityConversion
{
    /// <summary>What starts a kit piece (docs/HAZARD-KIT.md "Triggers"). `mode`: "always" (active for good), "schedule" (a
    /// cycle from `first` + `offset` every `period` s), "event" (the named authority events in `events`; `radius` &gt; 0 keeps
    /// only events within that many metres of the piece, `hazard` only events of that stage hazard), "manual" (an adapter or
    /// the DEV preview fires it). `hazard` also makes the piece follow that stage hazard's own phase (warn = arming, live =
    /// active, clear = cooldown). Durations: `warn` (arming), `active`, `cooldown`; 0 takes the kind's default.</summary>
    [Serializable] public sealed class KitTrigger
    {
        public string mode,hazard;public string[] events;public float first,period,offset,warn,active,cooldown,radius;
    }
    /// <summary>The piece's bank keys (null: the kind's and look's own) and its mix: a gain trim, the full-volume radius and
    /// the radius it falls silent at (m; 0: the kind's).</summary>
    [Serializable] public sealed class KitSounds
    {
        public string loop,pulse,start,stop;public float gain,inner,outer;
    }
    /// <summary>
    /// One hazard-kit piece as a map authors it (docs/HAZARD-KIT.md "Record"). JsonUtility shape: vectors {x,y,z}, every
    /// field optional. `kind` (<see cref="NativeHazardKit.Kinds"/>), `id` (unique on the map), `variant` (a saw's floor-slot,
    /// wall, ceiling-drop, rail or pendulum), `look` (alarms: biohazard, lockdown, evacuation; turrets: security, industrial;
    /// saws: industrial, surgical), `mount` (floor, wall, ceiling), `text` (a sign's words, A-Z and spaces). `pos` and `rot`
    /// (degrees: pitch, yaw, roll; local +z faces out of the wall, +y up) place it, in the frame of block `anchor` when one
    /// is named. `size` scales it (0: 1). `a`/`b`: a saw's path ends (or a pendulum's pivot and rest) in the same frame.
    /// Params: `range` (a turret's reach, an alarm's light), `arc` (degrees: a turret's sweep, a pendulum's swing), `rate`
    /// (a klaxon's blast period, a turret's rounds per second), `radius` (a saw's blade), `speed` (a saw's travel m/s),
    /// `length` (a pendulum's arm). `music`: "free" (default) or "beat" (lock pulses, flashes and turns to the current
    /// track's beat map when it has one); `react`: the music dips and swells when it trips.
    /// </summary>
    [Serializable] public sealed class KitRecord
    {
        public string id,kind,variant,look,mount,anchor,music,text;public V3 pos,rot,size,a,b;
        public float range,arc,rate,radius,speed,length,gain;public bool react;public KitTrigger trigger;public KitSounds sounds;
    }
    /// <summary>Presentation-only kit placements (Resources/HazardKit.json): by map id or by map look (every map with that look).</summary>
    [Serializable] public sealed class KitPlacement {public string map,look,note;public KitRecord[] kit;}
    [Serializable] public sealed class KitSet {public int schema;public string note;public KitPlacement[] placements;}

    /// <summary>A piece's behaviour state (docs/HAZARD-KIT.md "States"): idle, arming (the warning), active, cooldown, disabled
    /// (powered off or hacked) and broken (destroyed).</summary>
    public enum NativeKitState : byte {Idle,Arming,Active,Cooldown,Disabled,Broken}
    /// <summary>How hard lights may flash (Settings · Alarm lights; Reduced effects forces at least Reduced): Full (crisp flashes,
    /// at most 3 a second), Reduced (soft pulses at most 2 a second, less contrast), Calm (no flashes: a slow glow).</summary>
    public enum NativeKitFlash : byte {Full,Reduced,Calm}

    /// <summary>
    /// The reusable hazard kit's catalogue, defaults and validation (docs/HAZARD-KIT.md). Pure data rules; the states are
    /// Core NativeHazardKitClock, the looks NativeHazardKitBuild, the sound NativeHazardKitSound. Nothing here decides a hit:
    /// alarms are presentation; turret and saw authority is Astra's (the contract in the doc).
    /// </summary>
    public static class NativeHazardKit
    {
        public const int Schema=1,MaxPieces=96;
        public const string Beacon="alarm-beacon",Strobe="alarm-strobe",Klaxon="alarm-klaxon",Sign="alarm-sign",Turret="turret",Saw="saw";
        public static readonly string[] Kinds={Beacon,Strobe,Klaxon,Sign,Turret,Saw};
        public static readonly string[] Mounts={"wall","ceiling","floor"};
        public const string Biohazard="biohazard",Lockdown="lockdown",Evacuation="evacuation";
        public static readonly string[] AlarmLooks={Biohazard,Lockdown,Evacuation},TurretLooks={"security","industrial"},SawLooks={"industrial","surgical"};
        public const string SawFloor="floor-slot",SawWall="wall",SawDrop="ceiling-drop",SawRail="rail",SawPendulum="pendulum";
        public static readonly string[] SawVariants={SawFloor,SawWall,SawDrop,SawRail,SawPendulum};
        public static readonly string[] Modes={"always","schedule","event","manual"};
        /// <summary>The authority events a record may name (docs/HAZARD-KIT.md "Triggers"), all read from replicated state.</summary>
        public static readonly string[] Events={"round-start","wave-start","breach","boss-reveal","boss-phase","lockdown","door-seal","dev-trigger","victory","defeat"};
        public static bool Alarm(string kind)=>kind==Beacon||kind==Strobe||kind==Klaxon||kind==Sign;
        public static bool Known(string kind)=>Array.IndexOf(Kinds,kind)>=0;

        /// <summary>A look's colours (sRGB): the lamp, the lamp's off tint, the strobe flash, the sign's face and its words.</summary>
        public struct Palette {public int Lamp,Off,Flash,Face,Ink,Stripe;public string Text,Loop;}
        public static Palette Look(string look)
        {
            switch(look)
            {
                case Lockdown:return new Palette{Lamp=0xFFA31A,Off=0x3A2408,Flash=0xFFD27A,Face=0x2A1C08,Ink=0xFFB52E,Stripe=0xF4B321,Text="LOCKDOWN",Loop="alarm.lockdown"};
                case Evacuation:return new Palette{Lamp=0x2DFF8E,Off=0x0B2A18,Flash=0xF2FFF6,Face=0x0A2416,Ink=0x5BFFA8,Stripe=0x2DFF8E,Text="EVACUATE",Loop="alarm.evacuation"};
                default:return new Palette{Lamp=0xFF2A22,Off=0x3A0A08,Flash=0xFFD8D0,Face=0x2A0606,Ink=0xFF4436,Stripe=0xF4B321,Text="BIOHAZARD",Loop="alarm.biohazard"};
            }
        }
        /// <summary>The kind's default timings (s): arming, active, cooldown.</summary>
        public static void Timing(string kind,out float warn,out float active,out float cooldown)
        {
            switch(kind)
            {
                case Klaxon:warn=.35f;active=3.4f;cooldown=.8f;return;
                case Sign:warn=.8f;active=10;cooldown=2.5f;return;
                case Turret:warn=.8f;active=3;cooldown=1.2f;return;
                case Saw:warn=1.2f;active=4;cooldown=1.6f;return;
                default:warn=.6f;active=8;cooldown=2.5f;return;
            }
        }
        public static float Warn(KitRecord r){Timing(r.kind,out float w,out _,out _);return r.trigger!=null&&r.trigger.warn>0?r.trigger.warn:w;}
        public static float Live(KitRecord r){Timing(r.kind,out _,out float a,out _);return r.trigger!=null&&r.trigger.active>0?r.trigger.active:a;}
        public static float Cool(KitRecord r){Timing(r.kind,out _,out _,out float c);return r.trigger!=null&&r.trigger.cooldown>0?r.trigger.cooldown:c;}
        public static string Mode(KitRecord r)=>r.trigger==null||string.IsNullOrEmpty(r.trigger.mode)?"event":r.trigger.mode;
        public static bool BeatLocked(KitRecord r)=>r!=null&&r.music=="beat";
        public static float Scale(KitRecord r)=>r.size.x>0?r.size.x:r.kind==Turret?1.4f:1;

        /// <summary>Throws on records the presentation (and the contract) cannot run as written: ids, kinds, looks, mounts,
        /// variants, triggers, events, timings and geometry.</summary>
        public static void Validate(KitRecord[] kit,string where="kit")
        {
            if(kit==null)return;if(kit.Length>MaxPieces)throw new InvalidOperationException(where+": more than "+MaxPieces+" pieces");var ids=new HashSet<string>(StringComparer.Ordinal);
            foreach(var r in kit)
            {
                void Need(bool ok,string what){if(!ok)throw new InvalidOperationException(where+" "+r?.id+": "+what);}
                Need(r!=null&&!string.IsNullOrEmpty(r.id)&&ids.Add(r.id),"missing or duplicate id");Need(Known(r.kind),"unknown kind "+r.kind);
                Need(NativeMapRules.Finite(r.pos)&&NativeMapRules.Finite(r.rot)&&NativeMapRules.Finite(r.size)&&NativeMapRules.Finite(r.a)&&NativeMapRules.Finite(r.b),"geometry");
                Need(r.mount==null||Array.IndexOf(Mounts,r.mount)>=0,"unknown mount "+r.mount);Need(r.music==null||r.music=="free"||r.music=="beat","music must be free or beat");
                if(Alarm(r.kind))Need(r.look==null||Array.IndexOf(AlarmLooks,r.look)>=0,"unknown alarm look "+r.look);
                if(r.kind==Turret)Need((r.look==null||Array.IndexOf(TurretLooks,r.look)>=0)&&r.range>=0&&r.range<=60&&r.arc>=0&&r.arc<=360&&r.rate>=0&&r.rate<=30,"turret look, range, arc or rate");
                if(r.kind==Saw)Need(Array.IndexOf(SawVariants,r.variant)>=0&&(r.look==null||Array.IndexOf(SawLooks,r.look)>=0)&&r.radius>=0&&r.radius<=2.5f&&r.speed>=0&&r.speed<=20,"saw variant, look, radius or speed");
                if(r.kind==Sign)Need(r.text==null||r.text.Length<=14&&Letters(r.text),"sign text: up to 14 of A-Z and spaces");
                Need(r.size.x>=0&&r.size.x<=4&&r.gain>=0&&r.gain<=4,"size or gain");
                var t=r.trigger;if(t==null)continue;
                Need(t.mode==null||Array.IndexOf(Modes,t.mode)>=0,"unknown trigger mode "+t.mode);
                Need(MathUtil.Finite(t.first)&&MathUtil.Finite(t.period)&&MathUtil.Finite(t.offset)&&t.warn>=0&&t.active>=0&&t.cooldown>=0&&t.radius>=0&&t.warn<=30&&t.active<=600&&t.cooldown<=60,"trigger timing");
                if(t.mode=="schedule")Need(t.period>0&&t.first>=0&&Warn(r)+Live(r)+Cool(r)<=t.period+1e-4,"schedule: period must hold warn + active + cooldown");
                if(t.events!=null)foreach(var e in t.events)Need(Array.IndexOf(Events,e)>=0,"unknown event "+e);
                if(t.mode=="event")Need(t.events!=null&&t.events.Length>0||!string.IsNullOrEmpty(t.hazard),"an event trigger names events or a hazard");
            }
        }
        public static bool Letters(string s){foreach(char c in s)if(!(c>='A'&&c<='Z'||c==' '))return false;return true;}
        /// <summary>The records placed on map `mapId` with look `look` (both the map's and the look's placements, in file order).</summary>
        public static KitRecord[] For(KitSet set,string mapId,string look)
        {
            if(set?.placements==null)return Array.Empty<KitRecord>();var list=new List<KitRecord>();
            foreach(var p in set.placements)if(p?.kit!=null&&(p.map!=null&&p.map==mapId||p.look!=null&&p.look==look))list.AddRange(p.kit);
            return list.ToArray();
        }
        /// <summary>Validates every placement of a set (schema, unique ids per map, each record).</summary>
        public static void Validate(KitSet set)
        {
            if(set==null||set.schema!=Schema||set.placements==null)throw new InvalidOperationException("Hazard kit: schema must be "+Schema+" with placements");
            foreach(var p in set.placements){if(p==null||(p.map==null)==(p.look==null))throw new InvalidOperationException("Hazard kit: each placement names a map or a look");Validate(p.kit,"Hazard kit "+(p.map??p.look));}
        }
        /// <summary>A record's world frame: block `anchor` (centre, rotation) when it has one, then its own pos and rot.</summary>
        public static void Frame(KitRecord r,V3 anchorPos,FactoryQ anchorRot,out V3 at,out FactoryQ rot)
        {
            const double D=Math.PI/180;anchorRot.Rotate(r.pos.x,r.pos.y,r.pos.z,out double x,out double y,out double z);at=new V3((float)(anchorPos.x+x),(float)(anchorPos.y+y),(float)(anchorPos.z+z));
            rot=anchorRot*FactoryQ.Axis(0,1,0,r.rot.y*D)*FactoryQ.Axis(1,0,0,r.rot.x*D)*FactoryQ.Axis(0,0,1,r.rot.z*D);
        }
        /// <summary>A point of the record's frame (a saw's path end) in the world.</summary>
        public static V3 Point(V3 local,V3 anchorPos,FactoryQ anchorRot){anchorRot.Rotate(local.x,local.y,local.z,out double x,out double y,out double z);return new V3((float)(anchorPos.x+x),(float)(anchorPos.y+y),(float)(anchorPos.z+z));}

        /// <summary>
        /// The DEV preview gallery's pieces (presentation only): a row of test bays `spacing` m apart, `ahead` m in front of
        /// `origin` facing it (yaw radians, the viewer's facing), each piece on its own small wall, ceiling or floor plate
        /// (<see cref="NativeHazardKitBuild.Bays"/> draws them). Manual triggers, fired by the gallery clock.
        /// </summary>
        public static KitRecord[] Gallery(V3 origin,double yaw,float ahead=7.5f,float spacing=3.2f)
        {
            var list=new List<KitRecord>();
            void Add(KitRecord r){r.trigger=r.trigger??new KitTrigger{mode="manual"};list.Add(r);}
            Add(new KitRecord{id="g-beacon",kind=Beacon,look=Lockdown,mount="wall",music="beat",react=true});
            Add(new KitRecord{id="g-strobe",kind=Strobe,look=Biohazard,mount="wall",music="beat"});
            Add(new KitRecord{id="g-klaxon",kind=Klaxon,look=Biohazard,mount="wall",music="beat"});
            Add(new KitRecord{id="g-sign-bio",kind=Sign,look=Biohazard,mount="wall"});
            Add(new KitRecord{id="g-sign-lock",kind=Sign,look=Lockdown,mount="wall"});
            Add(new KitRecord{id="g-sign-evac",kind=Sign,look=Evacuation,mount="wall",music="beat"});
            Add(new KitRecord{id="g-beacon-evac",kind=Beacon,look=Evacuation,mount="ceiling",music="beat"});
            Add(new KitRecord{id="g-turret-floor",kind=Turret,mount="floor",look="security",range=18,arc=110,rate=10});
            Add(new KitRecord{id="g-turret-ceiling",kind=Turret,mount="ceiling",look="security",range=18,arc=140,rate=10});
            Add(new KitRecord{id="g-turret-wall",kind=Turret,mount="wall",look="industrial",range=18,arc=100,rate=10});
            foreach(var v in SawVariants)Add(new KitRecord{id="g-saw-"+v,kind=Saw,variant=v,look=v==SawPendulum||v==SawDrop?"surgical":"industrial",radius=.55f,speed=2.2f,length=2.1f,arc=55,trigger=new KitTrigger{mode="manual"}});
            // In a shallow arc facing the viewer, centred on the facing.
            double fx=Math.Sin(yaw),fz=Math.Cos(yaw),rx=Math.Cos(yaw),rz=-Math.Sin(yaw);int n=list.Count;
            for(int i=0;i<n;i++)
            {
                var r=list[i];double s=(i-(n-1)/2.0)*spacing,bow=Math.Abs(s)*.18;double x=origin.x+fx*(ahead+bow)+rx*s,z=origin.z+fz*(ahead+bow)+rz*s;
                double face=yaw+Math.PI-Math.Atan2(s,ahead+bow)*.6;float deg=(float)(face*180/Math.PI);
                float y=r.mount=="ceiling"?origin.y+3.1f:r.mount=="wall"?origin.y+(r.kind==Sign?2.2f:r.kind==Turret?1.9f:2.4f):origin.y;
                r.pos=new V3((float)x,y,(float)z);r.rot=new V3(0,deg,0);
                if(r.kind==Saw)
                {
                    // Path ends in the bay's own frame (local x across the bay): a slot or wall run, a drop, a rail, a pivot.
                    switch(r.variant)
                    {
                        case SawFloor:r.pos=new V3((float)x,origin.y,(float)z);r.a=new V3(-1.1f,0,0);r.b=new V3(1.1f,0,0);break;
                        case SawWall:r.pos=new V3((float)x,origin.y+1.1f,(float)z);r.a=new V3(-1.1f,0,0);r.b=new V3(1.1f,0,0);break;
                        case SawDrop:r.pos=new V3((float)x,origin.y+3.1f,(float)z);r.a=new V3(0,0,0);r.b=new V3(0,-1.9f,0);break;
                        case SawRail:r.pos=new V3((float)x,origin.y+2.4f,(float)z);r.a=new V3(-1.1f,0,0);r.b=new V3(1.1f,0,0);break;
                        default:r.pos=new V3((float)x,origin.y+3.2f,(float)z);r.length=2.1f;break;
                    }
                }
            }
            return list.ToArray();
        }
    }
}
