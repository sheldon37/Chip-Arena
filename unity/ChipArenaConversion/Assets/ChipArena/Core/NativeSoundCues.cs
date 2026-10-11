using System;

namespace ChipArena.UnityConversion
{
    /// <summary>What the presentation resolves about one fresh event beyond <see cref="NativeEventSounds.Context"/> for the bank:
    /// whether an Explosion's Detail names a weapon spec (a bare weapon blast) and the kill style of a Death.</summary>
    public struct NativeSoundDetail{public bool WeaponDetail;public string KillStyle;}

    /// <summary>One play the bank plan asks for: a bank key (its variation, entry gain, rate and length cap are applied when it
    /// plays) or, with no key, exactly the source route's sound. A banked routed layer keeps its source sound (Clip, Rate,
    /// Positional) for a clip that fails to load. Delay is seconds after the event; AtSource plays at the event's source pilot
    /// instead of the event position.</summary>
    public struct NativeSoundPlay
    {
        public string Clip,Key,Gate;public float Gain,Rate,Delay;public bool Positional,Tone,AtSource;public NativeSoundPriority Priority;
        public static NativeSoundPlay Source(in NativeEventSounds.Sound s)=>new NativeSoundPlay{Clip=s.Clip,Gate=s.Gate,Gain=s.Gain,Rate=s.Rate,Positional=s.Positional,Tone=s.Tone,Priority=NativeSoundPriority.Normal};
    }

    /// <summary>A bank-only layer for an event that is silent today or gains a layer (headshot dink, kill confirm, impacts).</summary>
    public struct NativeSoundExtra{public string Key;public float Gain,Delay;public bool AtSource;}

    /// <summary>
    /// The bank lookup beside the parity router: NativeEventSounds.Route stays the source's logical cue layer (EventFeedbackChecks)
    /// and this maps each routed sound plus its detail (blast style via NativeBlastSim.StyleOf, prop material, weapon spec, kind)
    /// to a bank key. A null key keeps the routed clip. A cover key names the recorded cue a routed layer belongs to: while that
    /// key has clips, the layer is not played (the recording already holds it, e.g. the geyser's synth explosion under its
    /// eruption). Every key is a literal here so Tests/EffectsAudioChecks.cs can match them to NativeSoundKeys.
    /// </summary>
    public static class NativeSoundCues
    {
        public static string Pick(in NativeEventSounds.Context c,in NativeSoundDetail d,in NativeEventSounds.Sound s,int layer,out string cover)
        {
            cover=null;if(s.Tone||s.Clip==null)return null;string clip=s.Clip;
            switch(c.Kind)
            {
                case EventKind.Shot:return Shot(clip);
                // blockHit (Source = blocker): blade deflects, a shield blocker's shield block; the Ansatsuken arcade parry stays.
                case EventKind.Block:return clip=="parry"?(c.SourceSpec=="shield"?"melee.shield.block":"melee.block"):null;
                case EventKind.ReloadStart:return "gun.reload.start";
                case EventKind.ReloadEnd:return "gun.reload.end";
                case EventKind.BurnTick:return "status.burn-tick";
                case EventKind.PoisonTick:return "status.poison-tick";
                // The owner-approved hitmarker stays the fallback of hit.confirm (no clips planned); the proc/add ticks stay.
                case EventKind.Hit:case EventKind.Headshot:return clip=="hurt"?"hit.hurt":clip=="stone"?"impact.debris":clip=="hit"&&s.Gate=="hit"?"hit.confirm":null;
                case EventKind.Impact:return Material(c.Material,false);
                case EventKind.Break:return Material(c.Material,true);
                case EventKind.Death:return clip=="stone"?"death.pilot":clip=="explosion"?"blast.debris":null;
                case EventKind.Explosion:case EventKind.Collapse:return Blast(c,d,clip,layer,out cover);
                case EventKind.BlockPulse:return "blast.nova";
                case EventKind.Arc:return c.Detail=="leet-lock"?"dev.leet.glitch":"blast.static";
                case EventKind.Teleport:return "magic.blink-strike";
                case EventKind.Field:return "blast.static";
                case EventKind.CeilingHit:return "impact.ceiling";
                case EventKind.HazardHit:return clip=="saw-cut"?"hazard.saw":null;
                case EventKind.PveBuy:return "pve.buy";
                case EventKind.PveGib:return "pve.gib";
                case EventKind.PvePhase:if(clip=="darkness")return "siren.reveal";cover="siren.reveal";return null;
                case EventKind.PveRevive:return "pve.revive";
                // Install keeps the DEV chips' arcade/Mishima cues.
                default:return null;
            }
        }
        /// <summary>A routed shot clip (the weapon spec, DEV specs already folded to lightning/fire) to its bank key.</summary>
        public static string Shot(string clip)
        {
            switch(clip)
            {
                case "pistol":return "gun.pistol.shot";case "rifle":return "gun.rifle.shot";case "machinegun":return "gun.machinegun.shot";
                case "fire":return "magic.fire.shot";case "water":return "magic.water.shot";case "lightning":return "magic.lightning.shot";case "darkness":return "magic.darkness.shot";
                default:return null;
            }
        }
        /// <summary>A prop's material (content id, Brickfall's brick/timber/glass too) to its impact or break key.</summary>
        public static string Material(string material,bool breaks)
        {
            switch(NativeStructureFx.Classify(material))
            {
                case NativeStructureMaterial.Timber:return breaks?"break.wood":"impact.wood";
                case NativeStructureMaterial.Metal:return breaks?"break.metal":"impact.metal";
                case NativeStructureMaterial.Glass:return breaks?"break.glass":"impact.glass";
                default:return breaks?"break.stone":"impact.stone";
            }
        }
        /// <summary>The visual blast style (NativeBlastSim.StyleOf, the same as the VFX) to its body key.</summary>
        public static string Style(NativeBlastStyle style)
        {
            switch(style)
            {
                case NativeBlastStyle.Shock:return "blast.shock";case NativeBlastStyle.Implosion:return "blast.implosion";case NativeBlastStyle.Frost:return "blast.frost";
                case NativeBlastStyle.Toxic:return "blast.toxic";case NativeBlastStyle.Void:return "blast.void";case NativeBlastStyle.Nova:return "blast.nova";
                case NativeBlastStyle.Kinetic:return "blast.kinetic";case NativeBlastStyle.Quake:return "blast.quake";case NativeBlastStyle.Meteor:return "blast.meteor";
                case NativeBlastStyle.Mine:return "blast.mine";default:return "blast.fire";
            }
        }
        // BLAST_CUE's layers by fx tag: the ability set pieces take their own cue, the rest the blast style's body.
        private static string Blast(in NativeEventSounds.Context c,in NativeSoundDetail d,string clip,int layer,out string cover)
        {
            cover=null;string tag=c.Detail;
            if(NativeBlastCues.Static(tag,c.Radius))return "blast.static";
            // An unarmed rocket: NativeBlastCues.Classify draws the dud's grey sparks; it clanks instead of exploding.
            if(tag=="rocket"&&c.Radius<.5f&&clip=="explosion")return "gun.rocket.dud";
            switch(tag)
            {
                case "meteor-strike":return clip=="stone"?"blast.debris":"blast.meteor";
                case "geyser-wall":if(clip=="water")return "magic.geyser";cover="magic.geyser";return null;
                case "orbital-mark":if(clip=="lightning")return "blast.shock";cover="blast.shock";return null;
                case "ball-lightning":return clip=="lightning"?"blast.static":"blast.shock";
                case "shackles":if(clip=="darkness")return "magic.shackles";cover="magic.shackles";return null;
                case "flame-lance":return "magic.flame-lance";
                case "tidal-wave":return "magic.tidal-wave";
                case "leet-nuke":if(layer==0){cover="blast.leet-nuke";return null;}return "blast.leet-nuke";
                case "fireworks":return "blast.fireworks";
            }
            return Style(NativeBlastCues.Style(NativeBlastCues.ExplosionTag(tag,c.Radius,d.WeaponDetail,out _)));
        }
        /// <summary>The per-style recipe around a recorded blast body (transient + body + debris tail, or an element accent).
        /// Returns the layers after the body: key, gain relative to the body's routed gain, delay (s).</summary>
        public static int Recipe(string body,NativeSoundExtra[] o)
        {
            int n=0;
            switch(body)
            {
                case "blast.fire":case "blast.mine":case "blast.kinetic":case "blast.meteor":n=X(o,n,"blast.transient",1,0);n=X(o,n,"blast.debris",.4f,.12f);break;
                case "blast.quake":n=X(o,n,"blast.debris",.4f,.15f);break;
                case "blast.frost":n=X(o,n,"status.frost-shatter",.6f,.04f);break;
            }
            return n;
        }
        private static int X(NativeSoundExtra[] o,int n,string key,float gain,float delay=0,bool atSource=false){if(n<o.Length)o[n]=new NativeSoundExtra{Key=key,Gain=gain,Delay=delay,AtSource=atSource};return Math.Min(o.Length,n+1);}
        /// <summary>Bank-only layers for one event: the hooks for sounds that are silent today. Without clips they stay silent.</summary>
        public static int Extras(in NativeEventSounds.Context c,in NativeSoundDetail d,NativeSoundExtra[] o)
        {
            int n=0;
            switch(c.Kind)
            {
                case EventKind.Hit:case EventKind.Headshot:
                    if(!c.TargetPilot||!(c.Amount>NativeEventFeedback.HitFloor))break;
                    if(c.Kind==EventKind.Headshot&&c.SourceMe&&!c.Ally&&!c.TargetMe)n=X(o,n,"hit.headshot",.8f);
                    // Positional weapon impacts: heard by everyone but the victim (who hears hurt); a gun's body hit only by third
                    // parties (the attacker already hears the hitmarker).
                    if(c.Detail=="static")n=X(o,n,"status.shock-zap",.7f);
                    else if(!c.TargetMe&&!NativeEventSounds.Proc(c.Detail)&&c.Detail!="debris"){string k=BodyImpact(c.SourceSpec);if(k!=null&&(k!="hit.body"||!c.SourceMe))n=X(o,n,k,.6f);}
                    break;
                case EventKind.Death:
                    if(c.TargetPilot){if(c.SourceMe&&!c.TargetMe)n=X(o,n,"hit.kill-confirm",.8f);string k=KillStyle(d.KillStyle);if(k!=null)n=X(o,n,k,.8f);}
                    else if(c.Pve)n=X(o,n,"pve.add.death",.6f);
                    break;
                case EventKind.Impact:{string k=SurfaceImpact(c.SourceSpec);if(k!=null)n=X(o,n,k,.5f);break;}
                case EventKind.HazardHit:if(c.Detail=="lava")n=X(o,n,"hazard.lava",.8f);break;
                // Time Out freezes for the event's Amount (1.2 s); the frost shatters when it ends.
                case EventKind.Freeze:n=X(o,n,"status.freeze",.7f);n=X(o,n,"status.frost-shatter",.7f,c.Amount>0?c.Amount:1.2f);break;
                case EventKind.Critical:if(c.SourceMe)n=X(o,n,"chip.critical",.7f);break;
                case EventKind.Heal:if(c.TargetMe)n=X(o,n,"chip.heal",.5f);break;
                case EventKind.PveMoney:if(c.SourceMe)n=X(o,n,"pve.money",.5f);break;
                case EventKind.Vacuum:n=X(o,n,"chip.vacuum",.7f);break;
                case EventKind.Teleport:if(c.Detail!="blink-strike")n=X(o,n,"chip.teleport",.7f);break;
                case EventKind.Split:n=X(o,n,"chip.split",.5f);break;
                case EventKind.Ricochet:n=X(o,n,"gun.ricochet",.55f);break;
                case EventKind.Pong:n=X(o,n,"chip.pong",.6f);break;
                case EventKind.Summon:n=X(o,n,"chip.summon",.7f);break;
                case EventKind.Dodge:n=X(o,n,"hit.dodge",.6f);break;
                case EventKind.Catch:n=X(o,n,"melee.shield.throw",.5f);break;
                case EventKind.Leet:n=X(o,n,"dev.leet.glitch",.7f);break;
                case EventKind.Siren:{string k=Siren(c.Detail,out bool atSource);if(k!=null)n=X(o,n,k,.8f,0,atSource);break;}
                case EventKind.Install:if(c.SourceMe&&!DevInstall(c.Detail))n=X(o,n,"ui.draft.plant",.6f);break;
            }
            return n;
        }
        /// <summary>Chips whose install already has the source's Mishima/arcade cue.</summary>
        public static bool DevInstall(string chip)=>chip!=null&&(chip.StartsWith("mishima-",StringComparison.Ordinal)||chip.StartsWith("arcade-",StringComparison.Ordinal));
        /// <summary>A direct hit's positional impact by the attacker's weapon spec (the fighters' hits play from their state).</summary>
        public static string BodyImpact(string spec)
        {
            switch(spec)
            {
                case "pistol":case "rifle":case "machinegun":return "hit.body";
                case "fire":return "magic.fire.hit";case "water":return "magic.water.hit";case "lightning":case "leet":return "magic.lightning.hit";case "darkness":return "magic.darkness.hit";
                case "sword":return "melee.sword.hit";case "greatsword":return "melee.greatsword.hit";case "daggers":return "melee.daggers.hit";case "shield":return "melee.shield.bash";
                default:return null;
            }
        }
        /// <summary>A magic bolt on a prop: water splashes, the other schools hit as energy (layered on the material clip).</summary>
        public static string SurfaceImpact(string spec)=>spec=="water"?"magic.water.hit":spec=="fire"||spec=="lightning"||spec=="darkness"||spec=="leet"?"impact.energy":null;
        /// <summary>A pilot death's kill style accent (the killer's kill effect, or the death's own style).</summary>
        public static string KillStyle(string style)
        {
            switch(style)
            {
                case "confetti":return "death.style.confetti";
                case "ghost":case "ascension":case "ragdoll-soul":return "death.style.soul";
                case "launch":case "ragdoll":return "death.style.launch";
                case "dev-matrix":return "death.style.matrix";
                case "dev-gibber":case "vent-splat":case "siren-crush":return "death.style.gib";
                case "ragdoll-frost":return "status.frost-shatter";
                case "siren-dissipate":return "siren.dissipate";
                default:return null;
            }
        }
        /// <summary>A Siren move (the Siren event's Detail) to its cue; atSource: heard at the Siren, not at the aim point.</summary>
        public static string Siren(string move,out bool atSource)
        {
            atSource=true;
            switch(move)
            {
                case "swipe":case "rend":return "siren.claw";case "slam":return "siren.slam";case "rush":return "siren.rush";case "laugh":return "siren.laugh";
                case "hellfire":return "siren.hellfire";case "damnation":return "siren.damnation";
                case "eruption":case "rain":atSource=false;return "siren.eruption";
                default:atSource=false;return null;
            }
        }

        // State cues NativeAudio.Render derives (the key; the caller passes today's clip as the fallback).
        public static string Roll(string clip)=>clip=="dash"?"move.roll":null;
        /// <summary>A melee swing (AttackAt) by spec and today's clip; the electric hook (recorded) stays. The Siren's swing is
        /// covered by its per-move cue (<see cref="SwingCover"/>).</summary>
        public static string Swing(string spec,string clip)
        {
            switch(spec)
            {
                case "sword":return "melee.sword.swing";case "greatsword":return "melee.greatsword.swing";case "daggers":return "melee.daggers.swing";
                // The shield's recorded bash is its hit (BodyImpact); its swing is the short melee whoosh.
                case "shield":return "melee.miss";
                case "emperor":return clip=="arcade-punch"?"dev.fist.swing":null;case "ansatsuken":return "dev.fist.swing";
                default:return null;
            }
        }
        public static string SwingCover(string spec)=>spec=="siren"?"siren.claw":null;
        /// <summary>An ability cast release by ability id; other casts keep the spec's shot key. Blink Strike's cast dash is
        /// covered by its teleport cue.</summary>
        public static string Cast(string ability,string spec,out string cover)
        {
            cover=null;
            switch(ability)
            {
                case "frag-grenade":return "gun.frag.throw";case "rocket":return "gun.rocket.launch";case "orbital-mark":return "gun.orbital.mark";
                case "blink-strike":cover="magic.blink-strike";return null;
                case "wolf-spin":return "melee.greatsword.spin";case "dagger-throw":return "melee.daggers.throw";case "shield-throw":return "melee.shield.throw";
                default:return Shot(spec)??Swing(spec,spec);
            }
        }
        /// <summary>A destruction layer's clip (NativeStructureSounds.Plan) to its override key: the 11 synthesized layers one for
        /// one, and the bank clips the plans borrow (the timber and metal landings, the chimney's whoosh). The plans, delays and
        /// the six-voice ledger stay; Tests/StructureFxChecks.cs keeps checking the synthesized fallbacks.</summary>
        public static string Structure(string clip)
        {
            switch(clip)
            {
                case "structure/thud":return "structure.thud";case "structure/crack":return "structure.crack";case "structure/gravel":return "structure.gravel";
                case "structure/rebar":return "structure.rebar";case "structure/groan":return "structure.groan";case "structure/creak":return "structure.creak";
                case "structure/snap":return "structure.snap";case "structure/boom":return "structure.boom";case "structure/rumble":return "structure.rumble";
                case "structure/glass":return "structure.glass";case "structure/debris":return "structure.debris";
                case "wood":return "structure.land.timber";case "metal":return "structure.land.metal";case "dash":return "structure.whoosh";
                default:return null;
            }
        }
        /// <summary>The DEV fighters' punches and kicks (the finisher's heavy hit and the electric hook stay).</summary>
        public static string Fighter(string clip)=>clip=="arcade-punch"||clip=="arcade-kick"?"dev.fist.hit":null;
    }

    /// <summary>
    /// The bank plan for one fresh event: each routed sound becomes its bank key while that key has clips, is dropped while a
    /// covering cue has clips, and is otherwise exactly the routed sound; then the blast recipe around a recorded body and the
    /// bank-only extras. With an empty bank (no LocalAssets) the plan is the route, sound for sound.
    /// </summary>
    public static class NativeSoundPlan
    {
        public const int MaxPlays=6;
        private static readonly NativeSoundExtra[] extras=new NativeSoundExtra[4],recipe=new NativeSoundExtra[3];
        public static int Event(NativeSoundBank bank,in NativeEventSounds.Context c,in NativeSoundDetail d,NativeEventSounds.Sound[] routed,int count,NativeSoundPlay[] o)
        {
            int n=0;string body=null;float bodyGain=0;
            for(int i=0;i<count&&n<o.Length;i++)
            {
                var s=routed[i];string key=NativeSoundCues.Pick(c,d,s,i,out string cover);
                if(key!=null&&bank.Has(key)&&!Uses(o,n,key)){var b=Banked(bank,c,key,s.Gain,0,s.Gate,false);b.Clip=s.Clip;b.Rate=s.Rate;b.Positional=s.Positional;o[n++]=b;if(body==null&&key.StartsWith("blast.",StringComparison.Ordinal)){body=key;bodyGain=s.Gain;}continue;}
                if(key!=null&&bank.Has(key))continue;
                if(cover!=null&&bank.Has(cover))continue;
                o[n++]=NativeSoundPlay.Source(s);
            }
            if(body!=null&&(c.Kind==EventKind.Explosion||c.Kind==EventKind.Collapse))
            {
                int r=NativeSoundCues.Recipe(body,recipe);
                for(int i=0;i<r&&n<o.Length;i++)if(bank.Has(recipe[i].Key)&&!Uses(o,n,recipe[i].Key))o[n++]=Banked(bank,c,recipe[i].Key,bodyGain*recipe[i].Gain,recipe[i].Delay,null,false);
            }
            int x=NativeSoundCues.Extras(c,d,extras);
            for(int i=0;i<x&&n<o.Length;i++)if(bank.Has(extras[i].Key)&&!Uses(o,n,extras[i].Key))o[n++]=Banked(bank,c,extras[i].Key,extras[i].Gain,extras[i].Delay,null,extras[i].AtSource);
            return n;
        }
        private static bool Uses(NativeSoundPlay[] o,int n,string key){for(int i=0;i<n;i++)if(o[i].Key==key)return true;return false;}
        private static NativeSoundPlay Banked(NativeSoundBank bank,in NativeEventSounds.Context c,string key,float gain,float delay,string gate,bool atSource)
        {
            var e=bank.Entry(key);return new NativeSoundPlay{Key=key,Gain=gain,Delay=delay,Gate=gate,Positional=e.Positional,AtSource=atSource,Priority=Priority(e.Priority,c.SourceMe||c.TargetMe)};
        }
        /// <summary>The local pilot's own sounds (and the ones that hit it) are never dropped for a distant one.</summary>
        public static NativeSoundPriority Priority(NativeSoundPriority p,bool mine)=>mine&&p==NativeSoundPriority.Normal?NativeSoundPriority.High:p;
    }

    /// <summary>Field-state cues (presentation-timed from replicated chip fields and ability zones, like the Siren lock beeps).</summary>
    public static class NativeFieldSounds
    {
        public const float MineArm=.6f;
        /// <summary>The loop a live field holds for its life (nearest <see cref="NativeVoicePool.MaxLoops"/> only).</summary>
        public static string Loop(string kind,string fx)
        {
            if(fx=="singularity")return "magic.singularity";if(fx=="ball-lightning")return "magic.ball-lightning";
            switch(kind)
            {
                case "acid":return "field.acid";case "fire":return "field.fire";case "frost":return "field.frost";case "black-hole":return "field.black-hole";
                case "repulsion":return "field.repulsion";case "heal":return "field.heal";case "storm":return "field.storm";default:return null;
            }
        }
        /// <summary>The one-shot a field plays when it is born: the time bubble, a bomb sticking, the meteor whistle, a warning sweep.</summary>
        public static string Born(string kind,string fx)
        {
            if(kind=="time")return "field.time";
            switch(fx)
            {
                case "sticky-bomb":case "demolition-charge":return "chip.bomb.attach";
                case "meteor-strike":case "artillery":return "magic.meteor.incoming";
                case "delayed-gratification":case "implosion-bomb":case "revenge-mail":return "chip.delayed.charge";
                default:return null;
            }
        }
        /// <summary>Born cues timed to end on the detonation: the sweep's rate, or the whistle's start offset.</summary>
        public static bool EndsOnDetonation(string key)=>key=="chip.delayed.charge"||key=="magic.meteor.incoming";
        public static float SweepRate(float clipSeconds,float fuse)=>fuse<=.05f?1:Math.Max(.5f,Math.Min(2,clipSeconds/fuse));
        public static float StartOffset(float clipSeconds,float fuse)=>Math.Max(0,clipSeconds-Math.Max(0,fuse));
        /// <summary>Bombs that count down with beeps (MarkBeeps' accelerating schedule).</summary>
        public static bool Fused(string kind,string fx)=>kind=="bomb"&&(fx=="sticky-bomb"||fx=="demolition-charge"||fx=="bubble-mine");
        public static bool Mine(string kind)=>kind=="mine";
        /// <summary>Fuse beep i of a bomb born at <paramref name="born"/> detonating at <paramref name="until"/>: a .3 s gap shrinking
        /// by .8 to .07 s, the rate rising 1 to 1.6 over the fuse. False past the last beep.</summary>
        public static bool Beep(double born,double until,int i,out double at,out float rate)
        {
            at=born+.1;double gap=.3;rate=1;
            for(int k=0;k<i;k++){at+=gap;gap=Math.Max(.07,gap*.8);}
            if(i>=64||at>=until-.03)return false;
            rate=1+.6f*(float)Math.Max(0,Math.Min(1,(at-born)/Math.Max(.05,until-born)));return true;
        }
    }

    /// <summary>Footsteps, jumps and landings from replicated locomotion state (presentation only).</summary>
    public static class NativeFootsteps
    {
        public const float Stride=1.9f,MinSpeed=1.5f,MaxStep=3,MinAir=.25f;
        public static string Surface(string map)
        {
            switch(map)
            {
                case "foundry":case "freight":case "reactor":case "siren":return "move.step.metal";
                case "moonlit":case "frozen":return "move.step.snow";
                case "outpost":return "move.step.gravel";
                case "citadel":case "forge":case "spikevault":case "lavaworks":return "move.step.stone";
                case "canal":case "sawmill":return "move.step.wood";
                default:return "move.step.concrete";
            }
        }
    }

    /// <summary>The shared UI helpers' sound for a button by its label: back/close/cancel, a primary confirm, else a click.</summary>
    public static class NativeUiSounds
    {
        public const string Click="ui.click",Hover="ui.hover",Back="ui.back",Confirm="ui.confirm",Error="ui.error";
        public static string Button(string label,bool primary)
        {
            string l=(label??"").Trim().ToLowerInvariant();
            if(l=="×"||l=="✕"||l=="x"||l=="←"||l.StartsWith("back",StringComparison.Ordinal)||l.StartsWith("close",StringComparison.Ordinal)||l.StartsWith("cancel",StringComparison.Ordinal)||l.StartsWith("leave",StringComparison.Ordinal)||l.StartsWith("return",StringComparison.Ordinal)||l.StartsWith("exit",StringComparison.Ordinal))return Back;
            return primary?Confirm:Click;
        }
    }
}
