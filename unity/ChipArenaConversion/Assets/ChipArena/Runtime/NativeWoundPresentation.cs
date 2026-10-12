using System;
using System.Collections.Generic;
using UnityEngine;
namespace ChipArena.UnityConversion
{
    public sealed partial class ArenaPresentation
    {
        private sealed class WoundDrop {public Vector3 Position,Velocity;public Color Color;public float Life,Max,Size,Ground;public bool Landed;}
        private readonly WoundDrop[] woundDrops=new WoundDrop[160];private int woundCursor;private float woundClock=-1;private readonly HashSet<string> sprayedWounds=new HashSet<string>();
        private void LivingWounds(CapturedAnimation.Player rig,Actor actor,float time)
        {
            if(actor.SourceSpec=="siren"||actor.Wounds==null||FirstPerson&&actor.Id==LocalId)return;
            foreach(var w in actor.Wounds)
            {
                if(w.destroyed||time-w.at>.3f||time<w.at-.05f||!sprayedWounds.Add(actor.Id+":"+w.id+"@"+w.at))continue;var mark=sourceModels.WoundMark(w);if(!mark.HasValue)continue;var a=mark.Value;var bone=rig.Bones[a.bone];Vector3 origin=bone.TransformPoint(a.point),normal=bone.TransformDirection(a.normal),along=bone.TransformDirection(a.direction);bool chips=w.kind=="bruise",embers=w.kind=="scorch";
                // Blood (cuts, punctures, cavities; a bruise a little) flies, lands and stays (Core/NativeBlood); the bruise's
                // shell chips and the burn's embers keep their short-lived cubes below.
                if(!embers)Bleed(actor.Id,w,origin,normal,along,time);if(!chips&&!embers)continue;int baseCount=chips||embers?5:7;int count=Math.Max(2,(int)Math.Round(baseCount*(.6+.6*Mathf.Clamp01(w.severity))*(ForceCompatibilityRendering?.5f:1)));uint seed=NativeWoundCarve.Seed(w.id.ToString(System.Globalization.CultureInfo.InvariantCulture));
                for(int n=0;n<count;n++)
                {float H(int k)=>(float)NativeWoundCarve.Hash(seed,n,k);float R(int k)=>H(k)-.5f;var d=woundDrops[woundCursor++%woundDrops.Length]??(woundDrops[(woundCursor-1)%woundDrops.Length]=new WoundDrop());d.Position=origin;d.Landed=false;d.Ground=actor.Position.y;float speed=embers?.8f+H(3):1.6f+2.4f*H(4);d.Velocity=normal*speed+along*(.6f*speed*H(5))+new Vector3(R(0)*1.6f,(embers?1.2f:.9f)+R(1),R(2)*1.6f);d.Max=d.Life=embers?.5f+.4f*H(6):chips?.9f:1.4f+.8f*H(6);d.Size=chips?.022f+.02f*H(7):.016f+.022f*H(7);ColorUtility.TryParseHtmlString(actor.BodyColor,out var body);d.Color=chips?body.linear*(.7f+.4f*H(8)):NativeArena.ColorOf(embers?(H(8)<.5f?0xff7a2a:0x2a2420):(H(8)<.6f?0xa8141f:0x780a14)).linear;}
            }
        }
        private void RenderWoundSpray(float time)
        {
            float dt=woundClock<0?0:Mathf.Clamp(time-woundClock,0,.05f);woundClock=time;for(int i=0;i<woundDrops.Length;i++){var d=woundDrops[i];if(d==null||d.Life<=0)continue;d.Life-=dt;if(!d.Landed){d.Velocity.y-=15*dt;d.Position+=d.Velocity*dt;if(d.Position.y<=d.Ground+.006f){d.Position.y=d.Ground+.006f;d.Landed=true;}}float fade=Mathf.Clamp01(d.Life/Math.Min(.5f,d.Max));Vector3 size=d.Landed?new Vector3(d.Size*2.2f*fade,.004f,d.Size*2*fade):new Vector3(d.Size,d.Size*(1+Math.Min(2.5f,d.Velocity.magnitude*.15f)),d.Size)*fade;sourceModels.WoundCube(Matrix4x4.TRS(d.Position,Quaternion.Euler(0,i*1.7f*Mathf.Rad2Deg,0),size),d.Color);}
        }
        private void ClearWoundSpray(){Array.Clear(woundDrops,0,woundDrops.Length);sprayedWounds.Clear();woundClock=-1;woundCursor=0;}
        // C18: the fatal wound's stump caps, blood fountain and crush fragments are NativeKillEffectPresentation.cs (KillWound,
        // KillSpray), ported from body-trauma.ts woundCap / CrushDebris and gibber-vfx.ts.
    }
}
