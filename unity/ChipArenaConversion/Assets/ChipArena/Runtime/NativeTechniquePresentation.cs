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
                techniquePreview.Step(me,new NVector(feet.x,feet.y,feet.z),me.Yaw*Mathf.Deg2Rad,time);
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
                var k=NativeTechniques.Of(a);if(k?.Kind==null||time<k.Release||time>k.Until+.25f)continue;
                switch(k.Kind)
                {
                    case NativeTechniques.Bastion:if(time<k.Until)PlantedShield(a,k,time);break;
                    case NativeTechniques.AnchorThrow:if(time<k.Until)ThrownBlade(a,k,time);break;
                    case NativeTechniques.SentryDrop:SentryModel(a,k,time);break;
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
        /// <summary>RenderGlow, after the melee kit's: every technique's particles.</summary>
        private void TechniqueFx(float now)
        {
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
