using System;
using System.Collections.Generic;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    public struct NativeStaffPart
    {
        public Vector3 Position,Size;public Quaternion Rotation;public int Color;
        public NativeStaffPart(Vector3 position,Vector3 size,int color,Quaternion rotation)
        {Position=position;Size=size;Color=color;Rotation=rotation;}
    }
    public static class NativeStaffGeometry
    {
        // weapon-visuals.ts shaft/head proportions; the gem is a voxel diamond in this native mesh set.
        public static readonly NativeStaffPart[][] Parts={Build(1),Build(2),Build(3),Build(4)};
        private static NativeStaffPart[] Build(int element)
        {
            var parts=new List<NativeStaffPart>();
            void Box(int color,float x,float y,float z,float sx,float sy,float sz,float ry=0,float rz=0)
            {parts.Add(new NativeStaffPart(new Vector3(x,y,z),new Vector3(sx,sy,sz),color,CapturedRig.Euler(0,ry,rz)));}
            Box(3,0,0,.13f,.065f,.065f,.94f);Box(6,0,0,-.355f,.085f,.085f,.05f);
            Box(3,0,0,0,.082f,.082f,.17f);Box(3,0,0,.40f,.08f,.08f,.11f);
            Box(6,0,0,.53f,.15f,.14f,.08f);Box(6+element,0,0,.72f,.20f,.20f,.28f,0,(float)Math.PI/4);
            if(element==2)
            {foreach(float x in new[]{-.19f,.19f})Box(5,x,0,.725f,.035f,.035f,.32f);Box(5,0,0,.885f,.415f,.035f,.035f);Box(8,0,0,.57f,.26f,.03f,.03f);}
            else if(element==3)
            {foreach(float x in new[]{-1f,1f})Box(5,x*.09f,0,.70f,.035f,.035f,.32f,x*.32f);foreach(float z in new[]{.24f,.30f,.36f})Box(6,0,0,z-.05f,.095f,.095f,.022f);}
            else if(element==4)
            {Box(5,-.13f,0,.72f,.04f,.04f,.30f);Box(5,-.045f,0,.885f,.21f,.04f,.04f);Box(5,.045f,0,.835f,.04f,.04f,.10f);Box(10,0,0,.58f,.20f,.03f,.03f);}
            else
            {for(int n=0;n<3;n++){float a=n*(float)Math.PI*2/3;Box(5,(float)Math.Cos(a)*.17f,(float)Math.Sin(a)*.17f,.61f,.06f,.06f,.29f);}Box(6,0,0,.585f,.19f,.19f,.04f);}
            return parts.ToArray();
        }
    }
}
