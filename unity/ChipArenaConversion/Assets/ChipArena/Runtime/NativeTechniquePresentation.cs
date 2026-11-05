using UnityEngine;
using NVector=System.Numerics.Vector3;

namespace ChipArena.UnityConversion
{
    // The weapon techniques in the arena (Claude; docs/WEAPON-TECHNIQUES.md). The effects are Core/NativeTechniqueFx, drawn from the record every
    // peer holds (NativeTechniques.Source: the Dev Sandbox's preview until the rules replicate it). The solid things are models: the planted
    // shield, the thrown greatsword and the sentry (the pilot's own machine gun on a tripod). Three hooks: TechniqueBegin before the rigs are posed
    // (the preview's clock, the weapon out of the hand), RenderTechnique at the end of Render (the models) and TechniqueFx in RenderGlow (the
    // particles). Presentation only: nothing here reaches the authority, the network frame, hit timing, hitboxes, damage or movement.
    public sealed partial class ArenaPresentation
    {
        /// <summary>The Dev Sandbox's Preview Techniques: the local pilot's weapon's technique on a loop. Off everywhere else.</summary>
        public bool TechniquePreviewOn;
        /// <summary>The preview shows only this technique (null: the weapon's in turn).</summary>
        public string TechniquePreviewOnly;
        private float TechniqueShiftOf(int id)=>techniqueShift.TryGetValue(id,out float y)?y:0;
        private readonly NativeTechniquePreview techniquePreview=new NativeTechniquePreview();
        private readonly NativeTechniqueFx techniqueFx=new NativeTechniqueFx();
        private TechSink techSink;

        /// <summary>Start of Render: the preview's records for this frame, and whose weapon has left the hand.</summary>
        private void TechniqueBegin(Snapshot s,float time)
        {
            Actor me=null;if(s?.Actors!=null)foreach(var a in s.Actors)if(a!=null&&a.Id==LocalId){me=a;break;}
            if(!TechniquePreviewOn||me==null||!me.Alive)
            {
                if(techniquePreview.Any)techniquePreview.Clear();if(ReferenceEquals(NativeTechniques.Source,techniquePreview))NativeTechniques.Source=null;
            }
            else
            {
                NativeTechniques.Source=techniquePreview;var feet=visualPositions.TryGetValue(me.Id,out var v)?v:Vec(me.Position);
                Actor nearest=null;float best=float.MaxValue;foreach(var o in s.Actors)if(o!=null&&o!=me&&o.Alive){float dist=(Vec(o.Position)-feet).sqrMagnitude;if(dist<best){best=dist;nearest=o;}}
                techniquePreview.TargetId=nearest?.Id??0;if(nearest!=null){var at=Vec(nearest.Position);techniquePreview.TargetAt=new NVector(at.x,at.y,at.z);}
                techniquePreview.Step(me,new NVector(feet.x,feet.y-TechniqueShiftOf(me.Id),feet.z),me.Yaw*Mathf.Deg2Rad,time,TechniquePreviewOnly);
            }
            if(NativeTechniques.Source==null||s?.Actors==null)return;
            foreach(var a in s.Actors)if(a!=null&&NativeTechniques.WeaponAway(NativeTechniques.Of(a),time))a.WeaponAway=true;
        }
        /// <summary>End of Render: the techniques' models.</summary>
        private void RenderTechnique(Snapshot s,float time)
        {
            if(s?.Actors==null||NativeTechniques.Source==null||sourceModels==null)return;
            foreach(var a in s.Actors)
            {
                var k=NativeTechniques.Of(a);
                if(k?.Kind==NativeTechniques.Bipod&&time>=k.At&&time<=k.Until+.25f){BipodModel(k,time);continue;}
                if(k?.Kind==null||time<k.Release||time>k.Until+.25f)continue;
                switch(k.Kind)
                {
                    case NativeTechniques.Bastion:if(time<k.Until)PlantedShield(a,k,time);break;
                    case NativeTechniques.AnchorThrow:if(time<k.Until)ThrownBlade(a,k,time);break;
                    case NativeTechniques.SentryDrop:SentryModel(a,k,time);break;
                    case NativeTechniques.Tripwire:TripwireModel(k,time);break;
                    case NativeTechniques.ShieldSurf:if(time<k.Until)SurfShield(a,k,time);break;
                    case NativeTechniques.IronVow:if(time<k.Until)PlantedBlade(a,k,time);break;
                    case NativeTechniques.SonarDart:SonarModel(k,time);break;
                    case NativeTechniques.LightningRod:RodModel(k,time);break;
                    case NativeTechniques.AmmoCrate:CrateModel(k,time);break;
                }
            }
        }
        private static Vector3 U(V3 v)=>new Vector3(v.x,v.y,v.z);
        private static Vector3 Flat(V3 v){var f=new Vector3(v.x,0,v.z);return f.sqrMagnitude>1e-5f?f.normalized:Vector3.forward;}
        private void QueueHeld(Actor a,Matrix4x4 m,float opacity=1)
        {
            string held=SkinnedHeld(a);if(held==null)return;sourceModels.Queue(held,m,b=>m,null,0,null,accent:AccentTint(a.AccentColor),opacity:opacity);
        }
        // The thrown or planted weapon: the pilot's own model of it where it wears the model (moved out of the hand, which draws none now), else the shared one.
        private void WeaponAt(Actor a,Matrix4x4 m)
        {
            if(rigs.TryGetValue(a.Id,out var rig)&&VisualWeaponDrawn(rig,m))return;QueueHeld(a,m);
        }
        // The shield bitten into the floor, leaning a little into its facing, punched down as it lands.
        private void PlantedShield(Actor a,NativeTechnique k,float time)
        {
            float age=time-k.Release,drop=Mathf.Clamp01(age/.08f);var f=Flat(k.Dir);var at=U(k.Point)+Vector3.up*(.5f+(1-drop)*.5f);
            WeaponAt(a,Matrix4x4.TRS(at,Quaternion.LookRotation(f,Vector3.up),Vector3.one*1.3f));
        }
        // The greatsword turning end over end along its line; stuck, standing out of the floor where it bit; flying back.
        private void ThrownBlade(Actor a,NativeTechnique k,float time)
        {
            var chest=rigs.TryGetValue(a.Id,out var rig)?rig.Bones[CapturedRig.Chest].position:Vec(a.Position);var at=NativeTechniques.BladeAt(k,time,new NVector(chest.x,chest.y,chest.z),out float flight);var pos=new Vector3(at.X,at.Y,at.Z);
            var dir=Flat(k.Dir);var side=Vector3.Cross(Vector3.up,dir).normalized;bool stuck=time>=k.Arm&&(k.Recast(time)||time<k.Until-NativeTechniques.BladeReturn);Quaternion q;float grip;
            if(stuck){float settle=Mathf.Exp(-(time-k.Arm)*14)*Mathf.Sin((time-k.Arm)*46)*.09f;q=Quaternion.LookRotation((-Vector3.up+dir*.28f+side*settle).normalized,dir);grip=1.28f;pos=U(k.Point);pos-=(q*Vector3.forward)*grip;}
            else{q=Quaternion.AngleAxis(time*1500,side)*Quaternion.LookRotation(dir,Vector3.up);grip=.8f;pos-=(q*Vector3.forward)*grip;}
            WeaponAt(a,Matrix4x4.TRS(pos,q,Vector3.one));
        }
        // The pilot's machine gun on a little tripod: the legs fold out as it arms, the gun swings to its aim and kicks with each round.
        private void SentryModel(Actor a,NativeTechnique k,float time)
        {
            float age=time-k.Release,arm=Mathf.Clamp01(age/Mathf.Max(.05f,k.Arm-k.Release)),open=arm*arm*(3-2*arm),gone=Mathf.Clamp01((time-k.Until)/.25f);var f=Flat(k.Dir);var feet=U(k.Point);
            float armed=time-k.Arm;var aim=armed>0?(U(k.To)-(feet+Vector3.up*.62f)).normalized:(f+Vector3.down*.25f*(1-open)).normalized;aim=Vector3.Slerp(f,aim,Mathf.Clamp01(armed/.3f));
            float kick=0;if(armed>=0&&time<k.Until){float since=armed%NativeTechniqueFx.SentryInterval;kick=Mathf.Exp(-since*26)*.045f;}
            float h=Mathf.Lerp(.2f,.82f,open)-gone*.3f;var head=feet+Vector3.up*h;var q=Quaternion.LookRotation(aim,Vector3.up);var m=Matrix4x4.TRS(head-aim*(.2f+kick),q,Vector3.one*1.05f);if(gone<1)QueueHeld(a,m);
            // The tripod: three legs and a hub, in cubes on the shared solid layer.
            var color=new Color(.09f,.1f,.12f);var edge=new Color(.2f,.22f,.25f);
            for(int i=0;i<3;i++)
            {
                float ang=(i*120+30)*Mathf.Deg2Rad;var d=f*Mathf.Cos(ang)+Vector3.Cross(Vector3.up,f)*Mathf.Sin(ang);var foot=feet+d*Mathf.Lerp(.06f,.55f,open)+Vector3.up*.01f;var top=head-Vector3.up*.05f;var mid=(foot+top)*.5f;var along=top-foot;
                sourceModels.QueuePiece(WeaponShape.Cube,false,Matrix4x4.TRS(mid,Quaternion.LookRotation(along.normalized,d),new Vector3(.036f,.036f,along.magnitude)),color);
                sourceModels.QueuePiece(WeaponShape.Cube,false,Matrix4x4.TRS(foot+Vector3.up*.012f,Quaternion.LookRotation(d,Vector3.up),new Vector3(.05f,.024f,.09f)),edge);
            }
            sourceModels.QueuePiece(WeaponShape.Cube,false,Matrix4x4.TRS(head-Vector3.up*.05f,q,new Vector3(.1f,.07f,.14f)),edge);
        }
        // The two daggers the wire is strung between.
        private void TripwireModel(NativeTechnique k,float time)
        {
            float fly=Mathf.Max(.05f,(k.Arm-k.Release)*.62f);if(time<k.Release+fly||k.Until-time<0)return;var a0=U(k.From);var a1=U(k.Point);var d=(a1-a0).normalized;if(d==Vector3.zero)return;var steel=new Color(.42f,.45f,.5f);var grip=new Color(.07f,.07f,.09f);
            void Stake(Vector3 at,Vector3 along){var q=Quaternion.LookRotation(along,Vector3.up);sourceModels.QueuePiece(WeaponShape.Cube,false,Matrix4x4.TRS(at,q,new Vector3(.018f,.04f,.26f)),steel);sourceModels.QueuePiece(WeaponShape.Cube,false,Matrix4x4.TRS(at-along*.18f,q,new Vector3(.03f,.03f,.13f)),grip);}
            Stake(a1,d);Stake(a0,-d);
        }
        // The pilot's own shield flat under its feet, nose up a little, rocking as it rides.
        private void SurfShield(Actor a,NativeTechnique k,float time)
        {
            if(!visualPositions.TryGetValue(a.Id,out var feet))return;var f=Flat(new V3(k.Dir.x,0,k.Dir.z));float age=time-k.Release,rock=Mathf.Sin(age*9)*2.2f,set=Mathf.Clamp01(age/.1f);
            var q=Quaternion.AngleAxis(rock,f)*Quaternion.LookRotation((Vector3.up+f*.16f).normalized,f);WeaponAt(a,Matrix4x4.TRS(feet+Vector3.down*.04f+Vector3.up*.02f,q,Vector3.one*Mathf.Lerp(1,1.3f,set)));
        }
        // The greatsword standing in the floor before the pilot, ringing as it lands.
        private void PlantedBlade(Actor a,NativeTechnique k,float time)
        {
            float age=time-k.Release,settle=Mathf.Exp(-age*14)*Mathf.Sin(age*46)*.09f;var f=Flat(k.Dir);var side=Vector3.Cross(Vector3.up,f).normalized;var q=Quaternion.LookRotation((-Vector3.up+f*.12f+side*settle).normalized,f);var pos=U(k.Point)-(q*Vector3.forward)*1.06f;WeaponAt(a,Matrix4x4.TRS(pos,q,Vector3.one));
        }
        // The dart in the wall: a sliver and two fins.
        private void SonarModel(NativeTechnique k,float time)
        {
            if(time<k.Release+Mathf.Max(.05f,(k.Arm-k.Release)*.5f)||time>k.Until)return;var normal=U(k.Dir);if(normal==Vector3.zero)normal=Vector3.back;normal.Normalize();var q=Quaternion.LookRotation(normal,Mathf.Abs(normal.y)>.9f?Vector3.forward:Vector3.up);var at=U(k.Point)+normal*.09f;var body=new Color(.08f,.1f,.11f);var fin=new Color(.16f,.3f,.24f);
            sourceModels.QueuePiece(WeaponShape.Cube,false,Matrix4x4.TRS(at,q,new Vector3(.028f,.028f,.2f)),body);sourceModels.QueuePiece(WeaponShape.Cube,false,Matrix4x4.TRS(at+normal*.08f,q,new Vector3(.1f,.012f,.05f)),fin);sourceModels.QueuePiece(WeaponShape.Cube,false,Matrix4x4.TRS(at+normal*.08f,q,new Vector3(.012f,.1f,.05f)),fin);
        }
        // Bipod's tripod: three legs in two sections, a collar and a foot plate each, a hub, and the ammunition box at its side.
        private void BipodModel(NativeTechnique k,float time)
        {
            float ext=NativeTechniqueFx.BipodOut(k,time);if(ext<=.002f)return;var f=Flat(k.Dir);var along=Quaternion.LookRotation(f,Vector3.up);var thick=new Color(.2f,.21f,.25f);var thin=new Color(.42f,.44f,.5f);var dark=new Color(.1f,.1f,.12f);var hub=Vector3.zero;
            for(int i=0;i<3;i++)
            {
                NativeTechniqueFx.BipodLeg(k,time,i,out var t0,out var t1);hub=new Vector3(t0.X,t0.Y,t0.Z);var foot=new Vector3(t1.X,t1.Y,t1.Z);var line=foot-hub;float len=line.magnitude;if(len<.02f)continue;var d=line/len;var q=Quaternion.LookRotation(d,Mathf.Abs(d.y)>.98f?f:Vector3.up);
                sourceModels.QueuePiece(WeaponShape.Cube,false,Matrix4x4.TRS(hub+d*(len*.28f),q,new Vector3(.06f,.06f,len*.56f)),thick);sourceModels.QueuePiece(WeaponShape.Cube,false,Matrix4x4.TRS(hub+d*(len*.76f),q,new Vector3(.034f,.034f,len*.48f)),thin);
                sourceModels.QueuePiece(WeaponShape.Cube,false,Matrix4x4.TRS(hub+d*(len*.55f),q,new Vector3(.085f,.085f,.07f)),dark);sourceModels.QueuePiece(WeaponShape.Cube,false,Matrix4x4.TRS(foot+Vector3.up*.015f,along,new Vector3(.13f,.03f,.17f)*ext),dark);
            }
            sourceModels.QueuePiece(WeaponShape.Cube,false,Matrix4x4.TRS(hub,along,new Vector3(.15f,.13f,.26f)),dark);
            var b=NativeTechniqueFx.BipodBox(k);var box=new Vector3(b.X,b.Y,b.Z);float h=NativeTechniqueFx.BipodBoxHigh*2;sourceModels.QueuePiece(WeaponShape.Cube,false,Matrix4x4.TRS(box,along,new Vector3(.26f,h,.4f)*ext),new Color(.13f,.18f,.09f));sourceModels.QueuePiece(WeaponShape.Cube,false,Matrix4x4.TRS(box+Vector3.up*(h*.1f),along,new Vector3(.27f,h*.2f,.41f)*ext),new Color(.95f,.68f,.12f));
        }
        // Ammo Crate: an olive steel box on skids with steel corners and a black-and-yellow band, its lid swinging up on two rows of brass. It darkens as it is shot.
        private void CrateModel(NativeTechnique k,float time)
        {
            if(!NativeTechniqueFx.CrateAt(k,time,out var m,out float size,out float lid))return;var c=new Vector3(m.X,m.Y,m.Z);float ang=NativeTechniqueFx.CrateAngle(k),hp=Mathf.Clamp01(k.Arm),hx=NativeTechniqueFx.CrateHalfX,hy=NativeTechniqueFx.CrateHalfY,hz=NativeTechniqueFx.CrateHalfZ;
            var q=Quaternion.LookRotation(new Vector3(Mathf.Sin(ang),0,-Mathf.Cos(ang)),Vector3.up);float wear=Mathf.Lerp(.45f,1,hp);var olive=new Color(.13f,.18f,.09f)*wear;var oliveLid=new Color(.17f,.23f,.11f)*wear;var steel=new Color(.42f,.45f,.5f)*wear;var gold=new Color(.95f,.68f,.12f)*wear;var dark=new Color(.05f,.05f,.06f);
            void Piece(Vector3 at,Vector3 scale,Color tint)=>sourceModels.QueuePiece(WeaponShape.Cube,false,Matrix4x4.TRS(c+q*(at*size),q,scale*size),tint);
            Piece(new Vector3(0,-.02f,0),new Vector3(hx*2-.04f,hy*2-.06f,hz*2-.04f),olive);Piece(new Vector3(0,hy-.03f,0),new Vector3(hx*2-.16f,.03f,hz*2-.16f),dark);
            Piece(new Vector3(0,-.06f,0),new Vector3(hx*2-.015f,.14f,hz*2-.015f),gold);for(int i=-3;i<=3;i++)Piece(new Vector3(i*.18f,-.06f,0),new Vector3(.075f,.145f,hz*2-.005f),dark);
            for(int sx=-1;sx<=1;sx+=2)for(int sz=-1;sz<=1;sz+=2)Piece(new Vector3(sx*(hx-.03f),0,sz*(hz-.03f)),new Vector3(.075f,hy*2,.075f),steel);
            for(int sz=-1;sz<=1;sz+=2)Piece(new Vector3(0,-hy+.03f,sz*(hz-.1f)),new Vector3(hx*2+.04f,.07f,.1f),dark);
            float rise=Mathf.Clamp01(lid/1.3f);
            if(rise>.02f)for(int row=-1;row<=1;row+=2)for(int i=-3;i<=3;i++){float h=.2f*rise;var at=new Vector3(i*.16f,hy+h*.5f-.03f,row*.14f);Piece(at,new Vector3(.08f,h,.08f),new Color(.86f,.62f,.2f));Piece(at+Vector3.up*(h*.5f+.025f*rise),new Vector3(.06f,.06f*rise,.06f),new Color(.8f,.34f,.16f));}
            var hinge=new Vector3(0,hy,-hz);var turn=q*Quaternion.AngleAxis(-lid*Mathf.Rad2Deg,Vector3.right);Vector3 Lid(float along,float up)=>c+q*(hinge*size)+turn*(new Vector3(0,up,along)*size);
            sourceModels.QueuePiece(WeaponShape.Cube,false,Matrix4x4.TRS(Lid(hz,.03f),turn,new Vector3(hx*2,.07f,hz*2)*size),oliveLid);sourceModels.QueuePiece(WeaponShape.Cube,false,Matrix4x4.TRS(Lid(hz*2-.09f,.035f),turn,new Vector3(hx*2+.005f,.075f,.12f)*size),gold);
        }
        // The rod: a pole on a three-footed base with insulator bands, driven down as it lands, falling away as it ends.
        private void RodModel(NativeTechnique k,float time)
        {
            if(time<k.Arm)return;float age=time-k.Arm,drive=1-Mathf.Clamp01(age/.08f),gone=Mathf.Clamp01((time-k.Until)/.25f);if(gone>=1)return;var foot=U(k.Point)+Vector3.up*(drive*.5f-gone*.6f);var pole=new Color(.1f,.1f,.14f);var band=new Color(.3f,.26f,.5f);float h=NativeTechniqueFx.RodHeight;var q=Quaternion.identity;
            sourceModels.QueuePiece(WeaponShape.Cube,false,Matrix4x4.TRS(foot+Vector3.up*(h*.5f),q,new Vector3(.05f,h,.05f)),pole);
            for(int i=0;i<3;i++){float y=h*(.55f+.16f*i),w=.2f-.045f*i;sourceModels.QueuePiece(WeaponShape.Cube,false,Matrix4x4.TRS(foot+Vector3.up*y,q,new Vector3(w,.024f,w)),band);}
            for(int i=0;i<3;i++){float ang=i*120*Mathf.Deg2Rad;var d=new Vector3(Mathf.Cos(ang),0,Mathf.Sin(ang));var low=foot+d*.34f+Vector3.up*.012f;var high=foot+Vector3.up*.3f;var along=high-low;sourceModels.QueuePiece(WeaponShape.Cube,false,Matrix4x4.TRS((low+high)*.5f,Quaternion.LookRotation(along.normalized,d),new Vector3(.03f,.03f,along.magnitude)),pole);}
        }
        // Where the rig stands this frame: Grave Walk's body goes down through the floor, a surfer rides a little high. The shift is kept out of the smoothing.
        private readonly System.Collections.Generic.Dictionary<int,float> techniqueShift=new System.Collections.Generic.Dictionary<int,float>();
        private void TechniqueUnshift(int id,ref Vector3 old){if(techniqueShift.TryGetValue(id,out float y)&&y!=0)old.y-=y;}
        private Vector3 TechniquePlace(Actor a,Vector3 p,float time)
        {
            float y=0;var k=NativeTechniques.Source!=null?NativeTechniques.Of(a):null;
            if(k?.Kind==NativeTechniques.GraveWalk)y=-NativeTechniqueFx.GraveDepth(k,time);else if(k?.Kind==NativeTechniques.ShieldSurf&&time>=k.Release&&time<k.Until&&!k.Recast(time))y=.1f*Mathf.Clamp01((time-k.Release)/.1f);
            techniqueShift[a.Id]=y;p.y+=y;return p;
        }
        /// <summary>RenderGlow, after the melee kit's: every technique's particles.</summary>
        private void TechniqueFx(float now)
        {
            BombFx(now);ReviveMachineFx();
            if(NativeTechniques.Source==null||kitScene==null||vfx==null)return;var draw=WeaponFxDraw;if(draw==null)return;
            techSink??=new TechSink();techSink.Bind(vfx,draw,FirstPerson&&!ScopeActive&&ViewCamera!=null);
            techniqueFx.Draw(techSink,kitScene,kitClock,Mathf.Clamp01(WeaponEffectsQuality),LowGraphics);
        }
        private void ClearTechnique(){techniquePreview.Clear();if(ReferenceEquals(NativeTechniques.Source,techniquePreview))NativeTechniques.Source=null;}

        private sealed class TechSink:ITechniqueFxSink
        {
            private NativeVfxDraw vfx;private NativeWeaponFxDraw draw;private bool viewOk;
            public void Bind(NativeVfxDraw v,NativeWeaponFxDraw d,bool view){vfx=v;draw=d;viewOk=view;}
            private static Vector3 U(NVector v)=>new Vector3(v.X,v.Y,v.Z);private static Color C(NVector c,float a=0)=>new Color(c.X,c.Y,c.Z,a);
            public void Glow(NVector at,NVector color,float coverage,float size,int profile,float u,float seed)=>vfx.Glow(U(at),C(color,coverage),size,profile,u,seed);
            public void Streak(NVector head,NVector velocity,NVector color,float width,float minLength,float blur,float taper,float u,float seed)=>vfx.Streak(U(head),U(velocity),C(color),width,minLength,blur,taper,u,seed);
            public void Smoke(NVector at,NVector albedo,float opacity,float size,float u,float seed,float floor)=>vfx.Smoke(U(at),C(albedo),opacity,size,u,seed,floor);
            public void Sprite(bool view,NVector at,NVector color,float size,int profile){if(view&&!viewOk)return;draw.Glow(view,U(at),C(color),size,profile);}
            public void Line(bool view,NVector tail,NVector head,NVector color,float width,bool shard){if(view&&!viewOk)return;draw.Axis(view,U(tail),U(head),C(color),width,shard?NativeWeaponFxDraw.ShardMode:NativeWeaponFxDraw.StreakMode,0);}
            public void Band(bool view,NVector a,NVector b,NVector c,NVector d,float ua,float ub,NVector color)
            {if(view&&!viewOk)return;draw.Quad(view,U(a),U(b),U(c),U(d),new Vector2(ua,-1),new Vector2(ub,-1),new Vector2(ub,1),new Vector2(ua,1),C(color),NativeWeaponFxDraw.Ribbon,0);}
            public void Fire(NVector at,NVector tint,float opacity,float size,float u,float seed,float floor)=>vfx.Fireball(U(at),C(tint),opacity,size,u,seed,floor);
            public void Water(NVector at,NVector lit,int shade,float opacity,float size,NVector segment,float u,float seed,float floor)=>vfx.Water(U(at),C(lit),shade,opacity,size,U(segment),u,seed,floor);
            public void Drop(NVector head,NVector segment,NVector color,float coverage,float width,float u,float seed)=>vfx.Drop(U(head),U(segment),C(color,coverage),width,u,seed);
            public void Tongue(bool view,NVector tail,NVector head,NVector color,float width){if(view&&!viewOk)return;draw.Axis(view,U(tail),U(head),C(color),width,NativeWeaponFxDraw.FlameMode,0);}
        }
    }
}
