using System;
using System.Numerics;
namespace ChipArena.UnityConversion
{
    // The chip install on the draft stage (owner 2026-11-18: "we need to improve the animation when the player model "installs"
    // the chip. Please review, improve drastically, and update."; presentation only). The pick's follow-through on the PvP chip
    // draft, the Siren / Ward Zero personal pick and the Deathmatch's chip picks: the pilot answers the click at once, the other
    // cards break apart, the chosen card flies at the pilot and bursts above the tray into the tier's 3D cartridge, the camera
    // pushes in from the portrait to the visor and the wrist, the left hand takes the cartridge by its edge, the right forearm
    // comes up as one checks a watch with its device's lid swinging open, the hand squares the cartridge over the bay and presses
    // it down: the clack (a short hold of the body on the pose clock, PoseAge), the empty shell bursts out of the fingers, the
    // tier's core glows in the bay, the lid slams over it and the light runs up the arm into the chest core and the visor; the
    // pilot rises into the power and looks into the lens while the details unfold beside him. Every tier plays the same beats
    // and times, scaled by its level (Profile). This is the timeline, the clocks, the curve kit, the tier profile, the stage
    // device's states, the text and the sound schedule as pure functions of the pick's age (seconds after the authoritative pick:
    // `a`, the stage's smoothed clock) and the pose age (`p`, equal to `a` but through the clack's hold). Engine-free and
    // allocation-free; nothing here reaches the rules, the network or a save (the install cue's moment, NativeDraftPlant.SoundAt,
    // the PvE follow-through's cap and the Expedition's hold are unchanged).
    public static class NativeInstallShot
    {
        // ---- the keys (seconds after the pick; Clock: the 2D layer, the camera and the seat's impact run on `a`, the body, the
        // hands, the device, the cartridge and the surge on `p`) ----
        /// <summary>The clack: the cartridge seats exactly on the install cue's key (NativeDraftPlant.SoundAt, unchanged), so the
        /// install route, the DEV install sounds and the Bombadier's arena install effects keep their moment.</summary>
        public const float Seat=NativeDraftPlant.SoundAt;
        public const float FlyStart=.08f,ReachStart=.06f,PushStart=.15f,PushEnd=1.05f,RiseStart=.18f,RiseEnd=.84f,RiseSettled=.92f,Burst=.44f,BootStart=.40f,BootEnd=.52f,
            LidOpen=.52f,LidOpenEnd=.72f,LidSettled=.78f,PinchStart=.52f,Catch=.58f,PinchSettled=.64f,AbsorbEnd=.76f,GazeToBay=.80f,CarryEnd=.98f,AlignEnd=1.06f,ThumbOff=1.08f,
            PressShaped=1.11f,Contact=1.12f,Release=1.15f,ReleaseEnd=1.19f,ShutStart=1.20f,ShutEnd=1.24f,ShutSettled=1.30f,SurgeStart=1.25f,SurgeEnd=1.43f,CoreAt=1.43f,VisorAt=1.47f,
            PowerStart=1.40f,PowerEnd=1.75f,PanStart=1.50f,PanEnd=2.35f,DetailsAt=1.60f,LookStart=1.90f,LookEnd=2.40f,HoldStart=1.90f,HoldEnd=2.55f,ReadAt=2.6f,
            ShortHold=1.55f,BloomAt=1.80f,ShortEnd=1.95f,ShardsGone=1.55f,TurnSeconds=3.2f;
        /// <summary>The hold's slow share (the body runs at a tenth of its pace through the hold) and the catch-up after it (s).</summary>
        public const float HoldRate=.1f,CatchUp=.12f;
        /// <summary>The settle of a turn that holds `turn` seconds after the pick (3.2 s: 2.55-3.05).</summary>
        public static float SettleStart(float turn)=>Math.Max(HoldEnd,turn-.65f);
        public static float SettleEnd(float turn)=>Math.Max(HoldEnd+.5f,turn-.15f);
        /// <summary>The PvP turn's hold after the pick (NativeMatch: RevealSeconds) for chip `id` (the screen's copy: 3.2 s, l44t 4.6, the
        /// bomb, arcade and Mishima chips 4.8, the dog, glaive, Katana and Fangs chips 5.2).</summary>
        public static float Turn(string id)=>id==null?TurnSeconds:NativeBombChips.Is(id)?4.8f:NativeDogChips.Is(id)?(float)NativeDogChips.RevealSeconds:NativeGlaiveChips.Is(id)?(float)NativeGlaiveChips.RevealSeconds
            :NativeKatanaChips.Is(id)?(float)NativeKatanaChips.RevealSeconds:NativeFangsChips.Is(id)?(float)NativeFangsChips.RevealSeconds:id.StartsWith("l44t-",StringComparison.Ordinal)?4.6f:id.StartsWith("arcade-",StringComparison.Ordinal)||id.StartsWith("mishima-",StringComparison.Ordinal)?4.8f:TurnSeconds;

        // ---- the curve kit (closed form: any frame rate, scrubbable) ----
        public static float Smooth(float t)=>MathUtil.Smooth(t);
        public static float Ramp(float t,float a,float b)=>MathUtil.Smooth((t-a)/(b-a));
        /// <summary>A cubic ease in and out (the camera's push).</summary>
        public static float Cubic(float t){t=MathUtil.Clamp(t,0,1);return t<.5f?4*t*t*t:1-4*(1-t)*(1-t)*(1-t);}
        public static float EaseOut(float t){t=MathUtil.Clamp(t,0,1);float i=1-t;return 1-i*i*i;}
        public static float EaseIn(float t){t=MathUtil.Clamp(t,0,1);return t*t*t;}
        /// <summary>An arrival with overshoot `s` (the back ease).</summary>
        public static float EaseOutBack(float t,float s=1.70158f){t=MathUtil.Clamp(t,0,1)-1;return 1+(s+1)*t*t*t+s*t*t;}
        /// <summary>A hump 0 -> 1 -> 0 over [0, 1] (sin).</summary>
        public static float Bump(float t)=>t<=0||t>=1?0:(float)Math.Sin(Math.PI*t);
        /// <summary>The under-damped response to a kick at t = 0: e^(-zeta w t) sin(w sqrt(1 - zeta^2) t), normalised to a peak of 1; 0
        /// before the kick. The absorb, the give, the camera's kick, the lid's bounces and the nods.</summary>
        public static float Kick(float t,float omega,float zeta)
        {
            if(!(t>0))return 0;float wd=omega*(float)Math.Sqrt(Math.Max(1e-4f,1-zeta*zeta));
            // The first peak: tan(wd t) = wd / (zeta w).
            float tp=(float)Math.Atan2(wd,zeta*omega)/wd,peak=(float)(Math.Exp(-zeta*omega*tp)*Math.Sin(wd*tp));
            return (float)(Math.Exp(-zeta*omega*t)*Math.Sin(wd*t))/Math.Max(1e-4f,peak);
        }
        /// <summary>Cubic Hermite between two points with their tangents (a path's keys with matched velocities).</summary>
        public static Vector3 Hermite(Vector3 p0,Vector3 m0,Vector3 p1,Vector3 m1,float t)
        {
            t=MathUtil.Clamp(t,0,1);float t2=t*t,t3=t2*t;return p0*(2*t3-3*t2+1)+m0*(t3-2*t2+t)+p1*(-2*t3+3*t2)+m1*(t3-t2);
        }

        // ---- the clocks ----
        /// <summary>The pose clock: `a` but through the clack's hold, where the body runs at HoldRate for `hold` seconds, then catches
        /// up over CatchUp (zero-sum: every key after Seat + hold + CatchUp lands where it would have). Reduced effects: no hold.</summary>
        public static float PoseAge(float a,float hold,bool reduced)
        {
            if(reduced||!(hold>0)||!(a>=Seat))return a;
            if(a<Seat+hold)return Seat+HoldRate*(a-Seat);
            if(a<Seat+hold+CatchUp){float u=(a-Seat-hold)/CatchUp;return a-(1-HoldRate)*hold*(1-Smooth(u));}
            return a;
        }
        /// <summary>The pose clock's rate (dp/da): the stage's springs step by it.</summary>
        public static float PoseRate(float a,float hold,bool reduced)
        {
            if(reduced||!(hold>0)||!(a>=Seat))return 1;
            if(a<Seat+hold)return HoldRate;
            if(a<Seat+hold+CatchUp){float u=(a-Seat-hold)/CatchUp;return 1+(1-HoldRate)*hold/CatchUp*6*u*(1-u);}
            return 1;
        }

        // ---- the tier ----
        /// <summary>
        /// A chip's install level and extras (NativeChipCartridge.Look: its rank and crest), so every tier, a future one too, gets its
        /// install: Level (L) scales the flash, the light, the give and the stage tint; Grade 1-6 gates the extras (rare and up: the
        /// core's ignite ring and a second packet; epic and up: the orbit ring circling the forearm, motes; legendary and up: the gems'
        /// sparks, the line-up's dim, the nova, the double corridor front and the Raise; mythic: the prism shards). The kits (rank 0)
        /// take epic's extras with their crest's flourish; the boss relics and the rainbow pick mythic's.
        /// </summary>
        public readonly struct Profile
        {
            public readonly string Tier;public readonly float Level,Flash,Hold,Kick,Tint,AccentMix,Visor;public readonly int Sparks,Rings,Grade,Rank;
            public readonly NativeChipCartridge.Crest Crest;public readonly bool Boss,Rainbow,Kit,Ability,Wild;public readonly NativeChipCartridge.Look Look;
            public Profile(string tier,string chip)
            {
                Look=NativeChipCartridge.Of(NativeChipCartridge.Known(tier)?tier:"common");Tier=Look.Tier;Rank=Look.Rank;Crest=Look.Crest;
                Boss=NativeBossChips.IsTier(tier);Rainbow=tier==NativeChipCartridge.Rainbow;Kit=!Boss&&!Rainbow&&Look.Rank==0;
                Ability=chip!=null&&chip.StartsWith(NativeAbilityChips.Prefix,StringComparison.Ordinal);Wild=chip!=null&&chip.StartsWith(NativeWildChips.Prefix,StringComparison.Ordinal);
                int r=Boss||Rainbow?6:Kit?4:Math.Max(1,Math.Min(6,Look.Rank));Grade=r;
                // common .. mythic, the kits between epic and legendary, the boss relics above mythic.
                float[] level={.55f,.65f,.75f,.85f,1f,1.1f},flash={.16f,.19f,.23f,.27f,.32f,.34f},hold={.04f,.045f,.055f,.065f,.08f,.09f},kick={1f,1.1f,1.2f,1.3f,1.4f,1.5f},tint={.2f,.32f,.45f,.58f,.7f,.75f},visor={.4f,.45f,.7f,.75f,1f,1f};
                int[] sparks={6,8,10,12,14,16},rings={1,1,2,2,3,3};int i=r-1;
                Level=Kit?.95f:Boss?1.15f:level[i];Flash=Kit?.30f:flash[i];Hold=Boss?.1f:Kit?.075f:hold[i];Kick=Kit?1.35f:kick[i];Tint=Boss?.7f:Kit?.65f:tint[i];Visor=Kit?.9f:visor[i];
                Sparks=Kit?13:sparks[i];Rings=Kit?2:rings[i];AccentMix=!Kit&&!Boss&&!Rainbow&&r<=2?.5f:0;
            }
            /// <summary>The chip's light (sRGB hex): the core's colour.</summary>
            public int Color=>Look.Core;
        }
        public static Profile Of(string tier,string chip)=>new Profile(tier,chip);
        // The profile last made, kept so a frame's draw makes none (Profile's arrays are the only allocation).
        private static string lastTier,lastChip;private static Profile last;
        public static Profile Cached(string tier,string chip)
        {
            if(lastTier!=null&&string.Equals(tier,lastTier,StringComparison.Ordinal)&&string.Equals(chip,lastChip,StringComparison.Ordinal))return last;
            last=new Profile(tier,chip);lastTier=tier??"";lastChip=chip;return last;
        }

        // ---- the hero hold (one per pick, the same on every client) ----
        public const int ClenchNod=0,Raise=1;
        /// <summary>The hold the pilot strikes from HoldStart: the Raise for legendary and up and, by hash, for rare and epic; else the
        /// clench and nod.</summary>
        public static int Hero(int seat,string chip,int set,in Profile pr)
        {
            if(pr.Grade>=5)return Raise;if(pr.Grade<3)return ClenchNod;
            uint h=2166136261;unchecked{h=(h^(uint)seat)*16777619;h=(h^(uint)set)*16777619;if(chip!=null)foreach(char c in chip)h=(h^c)*16777619;}
            return (h>>7)%3==0?Raise:ClenchNod;
        }

        // ---- the stage's device (NativePilotGear's bay, lid and lens on the install's clock; the arena's stays NativePilotGear.Device) ----
        /// <summary>The device this frame: Lid 0 shut .. 1 open (past 1 in its overshoot, a little open in its bounce), the strips', the
        /// bay's and the lens's levels, whether the bay and the lens take the chip's colour (else the accent).</summary>
        public struct DeviceState{public float Lid,Strips,Bay,Lens;public bool BayChip,LensChip;}
        public const float LidOvershoot=6f/NativePilotGear.LidOpenDegrees;
        /// <summary>The device at pose age `p` for a turn of `turn` s (`shortCut`: the PvE / Deathmatch follow-through, which ends on the
        /// power pose).</summary>
        public static DeviceState Device(float p,float turn,bool shortCut,in Profile pr)
        {
            var d=new DeviceState{Strips=1,Lens=.6f};if(!(p>=0))return d;
            // The strips boot from .40: a flicker (0, .4, .1, 1.4) to the selecting glow; at the surge they flare three times and decay.
            if(p<BootStart)d.Strips=1;else if(p<BootEnd){float u=(p-BootStart)/(BootEnd-BootStart);d.Strips=u<.25f?0:u<.5f?.4f:u<.75f?.1f:1.4f;}else d.Strips=1.5f;
            if(p>=SurgeStart)d.Strips=1.5f+1.5f*3*(float)Math.Exp(-(p-SurgeStart)/.3f);
            // The lid: open .52-.72 (a motorised ease out, its 6 degree overshoot settling by .78), shut 1.20-1.24 with a 6 degree
            // bounce settled by 1.30; under the settle it stays shut.
            if(p>=LidOpen&&p<ShutStart)
            {
                float u=(p-LidOpen)/(LidOpenEnd-LidOpen);d.Lid=p<LidOpenEnd?EaseOut(u)*(1+LidOvershoot):1+LidOvershoot*(1-Ramp(p,LidOpenEnd,LidSettled));
            }
            else if(p>=ShutStart&&p<ShutEnd)d.Lid=1-EaseIn((p-ShutStart)/(ShutEnd-ShutStart));
            else if(p>=ShutEnd)d.Lid=LidOvershoot*Math.Max(0,Bump((p-ShutEnd)/(ShutSettled-ShutEnd)));
            // The bay: its ready glow pulsing in the accent from the lid's opening, faster (5 to 12 Hz) as the cartridge comes in; the
            // chip's colour from the latch, bright and decaying.
            if(p>=LidOpen&&p<Contact){float hz=PulsePhase(p);d.Bay=.55f+.3f*(float)Math.Sin(2*Math.PI*hz);}
            else if(p>=Contact){float u=MathUtil.Clamp((p-Contact)/.7f,0,1);d.BayChip=true;d.Bay=1.2f+2.2f*(1-u)*(1-u);}
            // The lens: the accent until the lid shuts, then the chip's colour (2.2 decaying to .9 over .5 s, held activated), fading
            // with the settle.
            if(p>=ShutStart)
            {
                float k=MathUtil.Clamp((p-ShutStart)/.5f,0,1),fade=shortCut?0:Ramp(p,SettleStart(turn),SettleEnd(turn));
                d.LensChip=true;d.Lens=(2.2f-1.3f*Smooth(k))*(1-fade)+.6f*fade;
            }
            return d;
        }
        // The bay pulse's phase (cycles) at p: 5 Hz from the lid's opening, rising evenly to 12 Hz from the catch to the contact.
        private static float PulsePhase(float p)
        {
            float before=Math.Min(p,Catch)-LidOpen;if(p<=Catch)return 5*before;float T=Contact-Catch,t=Math.Min(p,Contact)-Catch;return 5*(Catch-LidOpen)+5*t+7*t*t/(2*T);
        }

        // ---- the text ----
        /// <summary>The heading's install words: "Upgrade secured" typed over .2 s from the clack (its letters shown), "Installing"
        /// before it. `chars` the letters of "Upgrade secured" shown (-1: the installing title).</summary>
        public static int Secured(float a,int length)=>!(a>=Seat)?-1:Math.Min(length,(int)Math.Ceiling(length*MathUtil.Clamp((a-Seat)/.2f,0,1)));
        /// <summary>The turn pill's stamp at the clack: 1.15 back to 1 over .15 s.</summary>
        public static float Stamp(float a)=>!(a>=Seat)?1:1+.15f*(1-Smooth((a-Seat)/.15f));
        /// <summary>Whether the chip reads installed (the pill, the nameplate, the follow-through's heading).</summary>
        public static bool Installed(float a)=>a>=Seat;

        // ---- the readout beside the pilot (PvP) ----
        /// <summary>Whether the details unfold beside the pilot (the default; false: the centred card from DetailsAt, as before).</summary>
        public const bool DetailsBeside=true;
        /// <summary>The readout block's centre (fraction of the screen width) for the screen's aspect, and the pilot's centre the camera
        /// pans to.</summary>
        public static float ReadoutX(float aspect)=>aspect>=2.2f?.70f:aspect<1.29f?.72f:aspect<1.5f?.71f:.69f;
        public static float PilotX(float aspect)=>aspect>=2.2f?.40f:aspect<1.5f?.32f:.36f;
        /// <summary>The block's unfold (0..1) from a line of light over DetailsAt..+.2, and its leader line's draw (0..1).</summary>
        public static float Unfold(float a)=>EaseOut((a-DetailsAt)/.2f);
        public static float Leader(float a)=>Smooth((a-DetailsAt-.02f)/.15f);

        // ---- the short cut (the PvE pick, the Deathmatch's picks): the bloom back to the live view ----
        /// <summary>The bloom's coverage (0..1 of its reach) and alpha at age `a`: from BloomAt it grows from the device to about 70% of the
        /// screen at alpha .35 by ShortEnd; then (`after` seconds past the end, the live view under it) it fades to 0 over .12 s.</summary>
        public static void Bloom(float a,float after,bool reduced,out float reach,out float alpha)
        {
            if(after>=0){reach=1;alpha=(reduced?.3f:.35f)*(1-MathUtil.Clamp(after/(reduced?.1f:.12f),0,1));return;}
            float u=Smooth((a-BloomAt)/(ShortEnd-BloomAt));reach=u;alpha=reduced?0:.35f*u;
        }

        // ---- the tray (2D) ----
        /// <summary>When card `i` of a tray starts to break (nearest first: `distance` its grid distance from the chosen card) and how
        /// long a break lasts.</summary>
        public static float ShatterAt(int distance)=>.04f+.045f*Math.Max(1,distance);
        public const float ShatterSpan=.70f;
        /// <summary>When the last card of an `n`-card tray has gone.</summary>
        public static float TrayGone(int n)=>ShatterAt(Math.Max(1,n-1))+ShatterSpan;
        /// <summary>The chosen card's flight to the catch (FlyStart .. Catch, the 3D cartridge taking over at Burst, about a fifth of the way
        /// short: its last 8-12 cm flown in 3D): the share of the way (eased in and out, a little linear) and the arc's lift (a share of the
        /// screen height).</summary>
        public static float Fly(float a){float u=MathUtil.Clamp((a-FlyStart)/(Catch-FlyStart),0,1);return .6f*Smooth(u)+.4f*u;}
        public const float FlyArc=.06f,FlyBank=-9;
        public static float FlyLift(float a)=>FlyArc*Bump(Fly(a));
        /// <summary>The card's screen point at `a` from its slot `from` toward the catch `to` (fractions: x from the left, y from the top).</summary>
        public static Vector2 CardAt(Vector2 from,Vector2 to,float a){float u=Fly(a);var at=from+(to-from)*u;at.Y-=FlyLift(a);return at;}
        /// <summary>The card's scale at `a`: from 1 to `end` (the cartridge's projected width over the card's at the burst).</summary>
        public static float CardScale(float a,float end){float u=Smooth((a-FlyStart)/(Burst-FlyStart));return 1+(end-1)*u;}

        // ---- the sounds (existing bank keys layered now; the bespoke install keys replace their layers once generated) ----
        // Body: 0 every drafter, 1 a pilot (the model or voxel body, not the Siren), 2 the Hot Dog, 3 the Siren drafter. Pose: on the
        // pose clock (else `a`). Grade: the least grade it plays for. Rank: its rate climbs with the rank (the charge whine). Bespoke:
        // the generated key that replaces this layer (its first layer plays it; the rest go quiet while it has clips).
        public struct Cue{public float At;public string Key,Bespoke,Fallback;public float Rate,Gain;public int Grade,Body;public bool Pose,Rank,Lead;}
        private static Cue C(float at,string key,float rate,float gain,string bespoke=null,bool lead=false,int grade=0,int body=0,bool pose=false,bool rank=false,string fallback=null)=>
            new Cue{At=at,Key=key,Rate=rate,Gain=gain,Bespoke=bespoke,Lead=lead,Grade=grade,Body=body,Pose=pose,Rank=rank,Fallback=fallback};
        /// <summary>The install's cues after the pick (the click's own ui.draft.pick plays on the click; the install route at Seat is
        /// NativeInstallCue's). Every viewer of the stage hears them, once a pick.</summary>
        public static readonly Cue[] Cues={
            C(.03f,"turret.lock",1.4f,.2f,"ui.install.lock",true),
            C(.10f,"gun.flyby",.8f,.22f),
            C(Burst,"impact.glass",1.4f,.2f),
            C(LidOpen,"gun.reload.start",1.25f,.35f,"ui.install.lid",true,pose:true),C(LidOpen,"turret.servo",1.6f,.2f,"ui.install.lid",pose:true),C(LidOpenEnd,"ui.click",1.3f,.25f,"ui.install.lid",pose:true),
            C(Catch,"ui.draft.hover",1.3f,.5f,"ui.install.catch",true,pose:true),C(Catch,"turret.lock",1.1f,.3f,"ui.install.catch",pose:true),
            C(.88f,"turret.spinup",1.2f,.18f,rank:true,pose:true),
            C(CarryEnd,"chip.mine.arm",1.4f,.18f,pose:true),C(1.02f,"chip.mine.arm",1.7f,.18f,pose:true),
            C(Seat,"gun.reload.end",1.1f,.45f,"ui.install.seat",true),C(Seat,"impact.metal",.8f,.3f,"ui.install.seat"),
            C(Seat,"boing",1,.5f,body:2),
            C(Release,"impact.glass",1.2f,.25f),
            C(ShutStart,"chip.bomb.attach",1.2f,.3f,pose:true),
            C(SurgeStart,"turret.spinup",1.6f,.22f,"ui.install.surge",true),C(SurgeStart,"gulp",1,.4f,body:2),
            C(CoreAt,"chip.heal",1,.3f),C(CoreAt,"hit.confirm",1,.35f,grade:3),C(CoreAt,"impact.energy",1,.3f,grade:4),
            C(VisorAt,"chip.critical",1,.3f,grade:5),
            C(DetailsAt,"ui.toggle",.9f,.2f),
            C(Catch,"siren.claw",1,.4f,body:3),C(Seat,"siren.claw",.85f,.45f,body:3),C(1.34f,"siren.laugh",1,.25f,body:3),C(1.40f,"siren.vents",1,.3f,body:3),
        };
        /// <summary>The short cut's bloom back to play.</summary>
        public static readonly Cue Out=C(BloomAt,"chip.teleport",1,.25f,"ui.install.out",true);
        /// <summary>The shatter's glass, at the first three breaks only (at most three voices).</summary>
        public static readonly Cue Shatter=C(0,"break.glass",1.6f,.12f,"ui.install.shatter",true);
        /// <summary>The bespoke keys (NativeSoundKeys.All; prompts in docs/audio/sound-requests-install.json).</summary>
        public static readonly string[] Bespoke={"ui.install.lock","ui.install.shatter","ui.install.lid","ui.install.catch","ui.install.seat","ui.install.surge","ui.install.out"};
        /// <summary>Whether cue `c` plays for a drafter of body `body` (1 pilot, 2 Hot Dog, 3 Siren) and `grade`.</summary>
        public static bool Plays(in Cue c,int body,int grade)=>grade>=c.Grade&&(c.Body==0?body!=3||c.Key!="turret.spinup"&&c.Key!="gun.reload.start"&&c.Key!="turret.servo"&&c.Key!="ui.click"&&c.Key!="chip.bomb.attach"&&c.Key!="chip.mine.arm"
            :c.Body==1?body==1||body==2:c.Body==body);
        /// <summary>A cue's playback rate: the charge whine climbs with the rank (rank 0 counts as 4).</summary>
        public static float Rate(in Cue c,in Profile pr)=>c.Rank?1.2f+.1f*(pr.Rank>0?pr.Rank:4):c.Rate;
        /// <summary>The boss sets' seat motif sound (NativeBossChips' order: Siren, 6:47, Converter, THE HUM, THE DEEP END, NOCTIEL).</summary>
        public static readonly string[] BossSeat={"siren.vents","boss.station.chime","boss.ironworks.hurt.clang","boss.hum.pulse","boss.deepend.ripple","boss.noctiel.feather"};
    }
}
