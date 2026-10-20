using System;
using System.Collections.Generic;

namespace ChipArena.UnityConversion
{
    // The city detail pass's inhabited places (Claude, 2026-10-20; docs/EXPEDITION-HUB.md "The city in detail"):
    //  - the sanctuary: the plaza's walls lived against (supplies stacked at the foot, jerrycans, a MISSING board, a medic cabinet,
    //    a radio with its aerial up the wall, hand-painted signs, lanterns on brackets), tarps pitched off the walls above a Hulk
    //    with bedding under them (flat), washing across a corner, a lookout and a flag on a roof;
    //  - the gates, street side: floodlights on the posts down each street, sandbags along the fence's outer foot, a painted board;
    //  - the join circles: work floods on the door's face either side of its airlock, lighting the circle and its street.
    // The sanctuary's floor keeps its rule (below 2.6 m only flat paint, the fence's, brazier's and walls' own dressing); nothing
    // stands in a gate's threshold or leaves' travel, a slot, the machine, a cache, a door's volumes or a join circle (the
    // keep-outs and ExpeditionDressingChecks); the gates' and doors' logic reads none of it.
    public sealed partial class NativeExpeditionCityKit
    {
        private bool InSanctuary(double x,double z){foreach(var v in area?.sanctuary??Array.Empty<ExpeditionVolume>()){var b=Aabb(v,0);if(x>b[0]&&x<b[1]&&z>b[4]&&z<b[5])return true;}return false;}
        private void Camp()
        {
            var signs=new[]{"SUPPLIES","MEDIC","WATER","RADIO","NO GUNS"};
            foreach(var f in faces)
            {
                if(f.Front==0)continue;double a=double.NaN,c=double.NaN;
                for(double u=f.U0+.25;u<f.U1-.25;u+=.5){At(f,u,0,.6,out double x,out double z);if(!InSanctuary(x,z))continue;if(double.IsNaN(a))a=u;c=u;}
                if(double.IsNaN(a)||c-a<3)continue;string id=blocks[f.Block].id;double len=c-a,mid=(a+c)/2;sanctuaryOk=true;int made=(int)(Hash(id,f.Index,1300)*15);
                // Supplies at the foot: crates and boxes stacked against the wall (in front of the storefront's trim), jerrycans.
                for(double u=a+.4;u<c-.6;u+=1.6+1.2*Hash(id,(int)(u*10),1301))
                {
                    double t=Hash(id,(int)(u*10),1302);if(!Open(f,u-.4,u+.75,0,1.6,.02,false))continue;Begin();
                    if(t<.55){double y=0;for(int k=0;k<2+(int)(Hash(id,(int)(u*10),1303)*3);k++){double bw=.4+.25*Hash(id,k,1304),bh=.28+.18*Hash(id,k,1305);
                        FBox(f,u+(Hash(id,k,1306)-.5)*.2,y+bh/2,.185,bw,bh,.12,With(P(k%2==0?NativeBlockLooks.Timber:NativeBlockLooks.Plain,k%2==0?0x6A5038:Pick(new[]{0x4A5A3A,0x6A6A5A,0x5A4A3A},Hash(id,k,1307)),RoleSanctuary),detail:k>0),(Hash(id,k,1308)-.5)*4);y+=bh;}}
                    else{for(int k=0;k<3+(int)(Hash(id,(int)(u*10),1309)*3);k++)FBox(f,u+k*.22,.18,.185,.18,.36,.12,With(P(NativeBlockLooks.SteelGreen,Pick(new[]{0x8A2A1E,0x3A5A2E,0x6A6440},Hash(id,k,1310)),RoleSanctuary),detail:k>1));}
                    End();
                }
                // On the wall: the board of the missing (paper, photographs, the word), the medic cabinet, the radio, a sign, a lantern.
                double t2=Hash(id,f.Index,1311);
                Begin();
                if(made%3==0){FBox(f,mid,1.75,.16,1.7,1.05,.04,P(NativeBlockLooks.Timber,0x5A4632,RoleSanctuary));
                    for(int k=0;k<12;k++){bool photo=Hash(id,k,1312)<.45;FBox(f,mid-.7+1.4*Hash(id,k,1313),1.35+.75*Hash(id,k,1314),.183+.002*(k%3),photo?.15:.2,photo?.2:.24,.004,With(P(NativeBlockLooks.Plain,photo?0x3A3632:0xE0D8C0,RoleSanctuary),detail:true),(Hash(id,k,1315)-.5)*16);}
                    Text(f,"MISSING",mid,2.35,.185,.05,.006,P(NativeBlockLooks.Plain,0xB82A20,RoleSign));}
                else if(made%3==1){FBox(f,mid,1.6,.17,.62,.62,.12,P(NativeBlockLooks.SteelGreen,0xD8D4CC,RoleSanctuary));FBox(f,mid,1.6,.232,.4,.1,.006,P(NativeBlockLooks.Plain,0xB8221A,RoleSanctuary));FBox(f,mid,1.6,.232,.1,.4,.006,P(NativeBlockLooks.Plain,0xB8221A,RoleSanctuary));}
                else{FBox(f,mid,1.15,.18,.5,.32,.12,P(NativeBlockLooks.SteelGreen,0x3A4232,RoleSanctuary));FBox(f,mid-.1,1.18,.241,.18,.12,.006,With(Glow(0x7DFF8A,.6f,RoleSanctuary),detail:true));FBox(f,mid+.15,(1.3+f.Top-f.Base)/2,.2,.02,f.Top-f.Base-1.3,.02,With(P(NativeBlockLooks.Plain,0x141414,RoleSanctuary),detail:true));}
                string word=signs[made%signs.Length];double cell=.06;FBox(f,mid,3.35,.15,TextWidth(word,cell)+.4,.5,.03,P(NativeBlockLooks.Timber,0x7A6448,RoleSign),(Hash(id,f.Index,1316)-.5)*4);Text(f,word,mid,3.35-2.5*cell,.168,cell,.006,P(NativeBlockLooks.Plain,0xE8E4D8,RoleSign),(Hash(id,f.Index,1316)-.5)*4);
                End();
                // A lantern on a bracket over the supplies (the plaza's corners: warm, a slow flicker).
                {double lu=Hash(id,f.Index,1317)<.5?a+.6:c-.6;Begin();FBox(f,lu,3.52,.25,.05,.05,.5,P(NativeBlockLooks.SteelGreen,0x2A2622,RoleSanctuary));FBox(f,lu,3.3,.5,.2,.32,.2,P(NativeBlockLooks.SteelGreen,0x2A2622,RoleSanctuary));
                    FBox(f,lu,3.27,.5,.14,.2,.14,Glow(0xFFB86A,1.5f,RoleSanctuary,DriveFire,(float)Hash(id,f.Index,1318)));
                    if(End()){At(f,lu,0,.7,out double lx,out double lz);Light(lx,f.Base+3.1,lz,0xFFB86A,.8,7,.6,GlowLampFlicker,Hash(id,f.Index,1318));}}
                // A tarp pitched off the wall over the plaza above a Hulk, its two guy lines; bedding laid flat under it.
                if(len>=4&&Hash(id,f.Index,1319)<.75)
                {
                    double tw=Math.Min(4.2,len-.6),depth=2.4,pitch=12,tu=mid;int hex=Pick(new[]{0x3A5A7A,0x6A6A4A,0x7A4A2A,0x4A6A5A},Hash(id,f.Index,1320));
                    Begin();FBox(f,tu,3.9-Math.Sin(pitch*Math.PI/180)*depth/2,Math.Cos(pitch*Math.PI/180)*depth/2+.05,tw,.03,depth,With(P(NativeBlockLooks.Plain,hex,RoleHigh),fade:true),0,pitch);
                    foreach(double s in new[]{-1.0,1.0})FBox(f,tu+s*(tw/2-.1),3.55,depth+.25,.02,.02,.9,With(P(NativeBlockLooks.Plain,0x8A7A60,RoleHigh),detail:true),0,-35);
                    for(int k=0;k<3;k++){double bu=tu-tw/2+.6+k*(tw-1.2)/2;At(f,bu,0,1.25,out double bx,out double bz);int g=GroundAt(bx,bz);if(g<0)continue;
                        Box(g,bx,.02,bz,f.Axis==0?1.9:.75,.04,f.Axis==0?.75:1.9,With(P(NativeBlockLooks.Plain,Pick(new[]{0x5A3A3A,0x3A4A5A,0x6A5A3A,0x4A4A4A},Hash(id,k,1321)),RoleSanctuary),detail:true));
                        At(f,bu,0,.45,out double px,out double pz);Box(g,px,.03,pz,f.Axis==0?.35:.5,.05,f.Axis==0?.5:.35,With(P(NativeBlockLooks.Plain,0xC8C0B0,RoleSanctuary),detail:true));}
                    End();
                }
                sanctuaryOk=false;
            }
            // A lookout on a roof over the plaza: sandbags round a searchlight, a flag on a pole.
            foreach(var stem in new[]{"bldg-ne-tenement","bldg-sw-tenement"})
            {
                if(!byStem.TryGetValue(stem,out int i))continue;var b=blocks[i];double top=b.pos.y+b.size.y/2,x=b.pos.x+(b.pos.x>0?-b.size.x/2+2:b.size.x/2-2),z=b.pos.z+(b.pos.z>0?-b.size.z/2+1.6:b.size.z/2-1.6);
                if(Overlapping(x,top+1,z,1.2,1,1.2,i))continue;Begin();
                for(int k=0;k<6;k++){double a=k*60*Math.PI/180;Box(i,x+Math.Sin(a)*.9,top+.18,z+Math.Cos(a)*.9,.7,.32,.35,With(P(NativeBlockLooks.Plain,BagHexes[k%BagHexes.Length],RoleRoof),detail:k%2==1),AxisRot(0,1,0,k*60+90));}
                Box(i,x,top+.6,z,.12,1.0,.12,P(NativeBlockLooks.SteelGreen,0x2A2E2A,RoleRoof));Box(i,x,top+1.2,z,.5,.4,.6,P(NativeBlockLooks.SteelGreen,0x2A2E2A,RoleRoof),AxisRot(0,1,0,b.pos.x>0?60:-120));
                Box(i,x,top+3.0,z+.9,.08,6.0,.08,P(NativeBlockLooks.SteelGreen,0x3A3C3E,RoleRoof));Box(i,x,top+5.4,z+1.6,.03,.8,1.3,With(P(NativeBlockLooks.Plain,0xD8D4C8,RoleRoof),fade:false),AxisRot(1,0,0,6));
                Box(i,x,top+5.4,z+1.6,.035,.16,1.3,P(NativeBlockLooks.Plain,0x3AB84A,RoleRoof),AxisRot(1,0,0,6));
                if(End())Light(x,top+1.3,z,0xE8F0FF,.9,12,1.4,GlowFlood);
            }
            GateCamp();JoinFloods();
        }
        /// <summary>Each hub gate's street side: a floodlight on each post down the street, sandbags along the fence's outer foot
        /// beyond the leaves, a painted board beside the opening.</summary>
        private void GateCamp()
        {
            if(hub==null)return;
            foreach(var g in hub.Gates)
            {
                double half=g.Width/2;sanctuaryOk=true;
                foreach(int side in new[]{-1,1})
                {
                    // The fence section on this side of the opening.
                    int sect=-1;NativeExpeditionHubGates.World(g,side*(half+1.2),0,0,out double sx,out _,out double sz);
                    foreach(var fid in g.Fence){if(!byStem.TryGetValue(Stem(fid)??"",out int p))continue;var b=blocks[p];if(Math.Abs(sx-b.pos.x)<=b.size.x/2+.05&&Math.Abs(sz-b.pos.z)<=b.size.z/2+.05){sect=p;break;}}
                    if(sect<0)continue;var fb=blocks[sect];double thick=Math.Min(fb.size.x,fb.size.z),outer=thick/2;var q=AxisRot(0,1,0,g.Yaw);
                    void At3(double u,double y,double d,out double x,out double yy,out double z){NativeExpeditionHubGates.World(g,u,y,d,out x,out yy,out z);}
                    // The flood on the post: an arm out of its street side, the head tipped down the street, its light.
                    Begin();double pu=side*(half+.25);At3(pu,4.15,outer+.25,out double ax,out double ay,out double az);Box(sect,ax,ay,az,.08,.08,.5,P(NativeBlockLooks.SteelGreen,0x2A2E2A,RoleGate),q);
                    At3(pu,4.05,outer+.55,out double hx,out double hy,out double hz);Box(sect,hx,hy,hz,.5,.36,.3,P(NativeBlockLooks.SteelGreen,0x1E2220,RoleGate),Then(q,AxisRot(1,0,0,22)));
                    At3(pu,4.0,outer+.71,out double lx,out double ly,out double lz);Box(sect,lx,ly,lz,.4,.28,.03,Glow(g.Wood?SodiumHex:0xE8F0FF,2.0f,RoleGate),Then(q,AxisRot(1,0,0,22)));
                    if(End()){At3(pu,3.6,outer+2.2,out double fx,out double fy,out double fz);Light(fx,fy,fz,g.Wood?SodiumHex:0xE8F0FF,.9,13,1.6,GlowFlood);}
                    // Sandbags along the outer foot past the leaves (two courses and a top one).
                    for(int c=0;c<3;c++)for(double u=half+.75+(c%2)*.4;u<half+2.9;u+=.82)
                    {
                        At3(side*u,.16+c*.3,outer+.125,out double bx,out double by,out double bz);double s2=Hash(fb.id,c*100+(int)(u*10)+side,1401);
                        Begin();Box(sect,bx,by,bz,.78,.3,.24,With(P(NativeBlockLooks.Plain,BagHexes[(int)(s2*BagHexes.Length)%BagHexes.Length],RoleSanctuary),detail:c>0),Then(q,AxisRot(0,1,0,(s2-.5)*4)));End();
                    }
                    // The painted board on one side.
                    if(side>0)
                    {
                        Begin();At3(side*(half+1.9),2.1,outer+.19,out double bx,out double by,out double bz);Box(sect,bx,by,bz,1.7,.5,.03,P(NativeBlockLooks.Timber,0x6A5438,RoleSign),q);
                        const string word="HOLSTER";double cell=.06,w=TextWidth(word,cell);
                        for(int k=0;k<word.Length;k++)foreach(var gl in GlyphBoxes(word[k])){double cu=side*(half+1.9)-w/2+k*4*cell+(gl[0]+gl[2]*.5)*cell;double cy=2.1-2.5*cell+(5-gl[1]-gl[3]*.5)*cell;
                            // Read from the street: the board's u runs against the gate frame's u there.
                            At3(side*(half+1.9)*2-cu,cy,outer+.21,out double gx,out double gy,out double gz);Box(sect,gx,gy,gz,gl[2]*cell,gl[3]*cell,.006,P(NativeBlockLooks.Plain,0xE8E4D8,RoleSign),q);}
                        lettering.Add(word);End();
                    }
                }
                sanctuaryOk=false;
            }
        }
        /// <summary>Two work floods on each instance door's face, either side of and above its airlock unit (past the station's
        /// canopy there), down on the join circle.</summary>
        private void JoinFloods()
        {
            foreach(var d in area?.doors??Array.Empty<ExpeditionDoorLayout>())
            {
                if(d.closure==null||d.join==null)continue;double a=d.closure.yaw*Math.PI/180;Face f=null;
                // The face the door stands in: its plane just behind the closure, looking out the door's way.
                foreach(double back in new[]{.2,.4,.6}){f=FaceAt(d.closure.x-Math.Sin(a)*back,d.closure.z-Math.Cos(a)*back,Math.Sin(a),Math.Cos(a));if(f!=null)break;}if(f==null)continue;
                double u0=U(f,d.closure.x,d.closure.z);
                foreach(double s in new[]{-1.0,1.0})
                {
                    double u=double.NaN,y=4.75;foreach(double off in new[]{d.closure.sx/2+UnitWing+.7,d.closure.sx/2+UnitWing+1.6,d.closure.sx/2+UnitWing+2.6,d.closure.sx/2+UnitWing+3.6,d.closure.sx/2+UnitWing+4.4}){double cu=u0+s*off;if(Open(f,cu-.35,cu+.35,y-.3,y+.4,.02,false)){u=cu;break;}}if(double.IsNaN(u))continue;
                    Begin();FBox(f,u,y,.3,.08,.08,.6,P(NativeBlockLooks.SteelGreen,0x2A2E2A,RoleLandmark));FBox(f,u,y-.08,.62,.55,.38,.3,P(NativeBlockLooks.SteelGreen,0x1E2220,RoleLandmark),0,28);
                    FBox(f,u,y-.16,.79,.44,.28,.03,Glow(0xE8F0FF,2.0f,RoleLandmark),0,28);
                    if(End()){At(f,u,0,3.0,out double lx,out double lz);Light(lx,f.Base+3.2,lz,0xE8F0FF,.9,12,1.4,GlowFlood);}
                }
            }
        }
    }
}
