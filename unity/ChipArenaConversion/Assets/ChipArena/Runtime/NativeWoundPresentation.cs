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
                if(w.destroyed||time-w.at>.3f||time<w.at-.05f||!sprayedWounds.Add(actor.Id+":"+w.id+"@"+w.at))continue;var mark=sourceModels.WoundMark(w);if(!mark.HasValue)continue;var a=mark.Value;var bone=rig.Bones[a.bone];Vector3 origin=bone.TransformPoint(a.point),normal=bone.TransformDirection(a.normal),along=bone.TransformDirection(a.direction);bool chips=w.kind=="bruise",embers=w.kind=="scorch";int baseCount=w.kind=="cavity"?14:w.kind=="cut"?10:chips||embers?5:7;int count=Math.Max(2,(int)Math.Round(baseCount*(.6+.6*Mathf.Clamp01(w.severity))*(ForceCompatibilityRendering?.5f:1)));uint seed=NativeWoundCarve.Seed(w.id.ToString(System.Globalization.CultureInfo.InvariantCulture));
                for(int n=0;n<count;n++)
                {float H(int k)=>(float)NativeWoundCarve.Hash(seed,n,k);float R(int k)=>H(k)-.5f;var d=woundDrops[woundCursor++%woundDrops.Length]??(woundDrops[(woundCursor-1)%woundDrops.Length]=new WoundDrop());d.Position=origin;d.Landed=false;d.Ground=actor.Position.y;float speed=embers?.8f+H(3):1.6f+2.4f*H(4);d.Velocity=normal*speed+along*(.6f*speed*H(5))+new Vector3(R(0)*1.6f,(embers?1.2f:.9f)+R(1),R(2)*1.6f);d.Max=d.Life=embers?.5f+.4f*H(6):chips?.9f:1.4f+.8f*H(6);d.Size=chips?.022f+.02f*H(7):.016f+.022f*H(7);ColorUtility.TryParseHtmlString(actor.BodyColor,out var body);d.Color=chips?body.linear*(.7f+.4f*H(8)):NativeArena.ColorOf(embers?(H(8)<.5f?0xff7a2a:0x2a2420):(H(8)<.6f?0xa8141f:0x780a14)).linear;}
            }
        }
        private void RenderWoundSpray(float time)
        {
            float dt=woundClock<0?0:Mathf.Clamp(time-woundClock,0,.05f);woundClock=time;for(int i=0;i<woundDrops.Length;i++){var d=woundDrops[i];if(d==null||d.Life<=0)continue;d.Life-=dt;if(!d.Landed){d.Velocity.y-=15*dt;d.Position+=d.Velocity*dt;if(d.Position.y<=d.Ground+.006f){d.Position.y=d.Ground+.006f;d.Landed=true;}}float fade=Mathf.Clamp01(d.Life/Math.Min(.5f,d.Max));Vector3 size=d.Landed?new Vector3(d.Size*2.2f*fade,.004f,d.Size*2*fade):new Vector3(d.Size,d.Size*(1+Math.Min(2.5f,d.Velocity.magnitude*.15f)),d.Size)*fade;sourceModels.WoundCube(Matrix4x4.TRS(d.Position,Quaternion.Euler(0,i*1.7f*Mathf.Rad2Deg,0),size),d.Color);}
        }
        private void ClearWoundSpray(){Array.Clear(woundDrops,0,woundDrops.Length);sprayedWounds.Clear();woundClock=-1;woundCursor=0;}
        private void FatalWound(NativeDeath death,NativeCorpse corpse,float age,float alpha)
        {
            int part=death.wound?.destroyed==true?death.wound.part:corpse.Style=="dev-gibber"?5:-1;if(part<0)return;int parent=CapturedRig.Bones[part].Parent;while(parent>=0&&!corpse.Parts.ContainsKey(parent))parent=CapturedRig.Bones[parent].Parent;if(parent<0)return;
            Vector3 joint=corpse.Bind[part].GetColumn(3);Vector3 local=corpse.Bind[parent].inverse.MultiplyPoint3x4(joint);var transform=corpse.Bone(parent);Vector3 stump=transform.MultiplyPoint3x4(local);Quaternion rotation=transform.rotation;float radius=part==5?.14f:part==2?.2f:.085f;
            for(int i=0;i<9;i++){Vector3 offset=new Vector3((i%3-1)*radius*.55f,0,(i/3-1)*radius*.55f);sourceModels.WoundCube(Matrix4x4.TRS(stump+rotation*offset,rotation,new Vector3(radius*.55f,.035f,radius*.55f)),NativeArena.ColorOf(i==4?0xe8dfcd:i%2==0?0x780a14:0xa8141f).linear,alpha);}
            // A bounded cosmetic spray. The authority wound alone chooses sever/crush; none of these cubes are colliders.
            bool crush=death.wound?.kind=="crush";float stop=corpse.Style=="dev-gibber"?3.6f:crush?.8f:1.2f;int limit=corpse.Style=="dev-gibber"?55:30;
            for(int i=0;i<limit;i++){float born=i/(float)limit*stop,t=age-born;if(t<0||t>1.1f)continue;uint seed=(uint)death.seed;float H(int k)=>(float)NativeWoundCarve.Hash(seed,i,k);Vector3 velocity=rotation*Vector3.up*(2+H(0)*3)+new Vector3((H(1)-.5f)*3,1.8f+H(2)*2,(H(3)-.5f)*3);Vector3 position=stump+velocity*t+Vector3.down*(7.5f*t*t);position.y=Math.Max(death.position.y-.85f+.006f,position.y);float size=(.025f+.035f*H(4))*.6f;sourceModels.WoundCube(Matrix4x4.TRS(position,Quaternion.identity,new Vector3(size,position.y<=death.position.y-.83f?.006f:size*2,size)),NativeArena.ColorOf(i%3==0?0x750516:0xc91428).linear,alpha);}
        }
    }
}
