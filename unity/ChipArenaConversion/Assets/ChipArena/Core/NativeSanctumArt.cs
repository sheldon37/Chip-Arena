using System;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The Hollow Sanctum's art poses (docs/arenas/STAGE-ART.md): where each modelled rig part of the stage bank
    /// (tools/stages/sanctum_props.py; its frames are listed there) stands this frame, read only from Core NativeSanctumRig's state
    /// after its Build (the very functions the simulation decides hits with), so the models move exactly as the box rig did:
    /// the chandeliers on their chains (swaying, slipping, falling, lying as wrecks with their candles strewn, hoisted again), the
    /// censer on its rope and its pull rope, the bell and its clapper, the crypt gates' grilles and lanterns, the holy fire's
    /// baskets by their heat and the banners stirring. Pure and allocation free per frame.
    /// </summary>
    public static class NativeSanctumArt
    {
        /// <summary>The rig props by slot (the bank's "rig/&lt;name&gt;/&lt;tier&gt;").</summary>
        public static readonly string[] Slots={"chandelier","chandelier-wreck","chain","censer","pull-rope","bell","clapper",
            "chandelier-candles-0","chandelier-candles-1","chandelier-candles-2","chandelier-candles-3",
            "wreck-candles-0","wreck-candles-1","wreck-candles-2","wreck-candles-3","floor-candles-0","floor-candles-1","floor-candles-2","floor-candles-3",
            "gate-sanctum-gate-w","gate-sanctum-gate-e","lanterns-sanctum-gate-w","lanterns-sanctum-gate-e",
            "fire-sanctum-fire-n-0","fire-sanctum-fire-n-1","fire-sanctum-fire-s-0","fire-sanctum-fire-s-1","banner-red","banner-blue"};
        public const int Chandelier=0,Wreck=1,Chain=2,Censer=3,PullRope=4,Bell=5,Clapper=6,Candles=7,WreckCandles=11,FloorCandles=15,Gate=19,Lanterns=21,Fire=23,BannerRed=27,BannerBlue=28;
        /// <summary>The chain's ring over the hub (local y in the chandelier's frame), where the chain starts.</summary>
        public const double ChainFoot=NativeSanctumLooks.CrownHeight+.7;
        /// <summary>The pull rope's cleat on the south bridge (the rig's).</summary>
        public const double CleatY=5.05,CleatZ=-7.02;

        public static void Pose(NativeSanctumRig rig,MapHazard[] hazards,double render,NativeStageArtPose into)
        {
            into.Clear();if(rig==null)return;
            // the chandeliers
            for(int k=0;k<NativeSanctumRig.Chandeliers;k++)
            {
                int o=k*5;double x=rig.Ring[o],y=rig.Ring[o+1]-.12,z=rig.Ring[o+2],sway=rig.Ring[o+3];int phase=(int)rig.Ring[o+4];double anchor=NativeSanctumRig.Anchor;
                if(x==0&&z==0&&y<=0)continue;
                if(phase!=3)
                {
                    var q=FactoryQ.Axis(0,0,1,sway);q.Rotate(0,y-anchor,0,out double ox,out double oy,out double oz);
                    into.Add(Chandelier,x+ox,anchor+oy,z+oz,q);into.Add(Candles+k,x+ox,anchor+oy,z+oz,q);
                    if(phase==0||phase==1||phase==4)ChainUp(into,x,anchor,z,q,y+ChainFoot-anchor);
                }
                else
                {
                    var tilt=NativeSanctumRig.DownTilt(k);tilt.Rotate(0,-.3,0,out double ox,out double oy,out double oz);
                    into.Add(Wreck,x+ox,y+.3+oy,z+oz,tilt);into.Add(WreckCandles+k,x+ox,y+.3+oy,z+oz,tilt);into.Add(FloorCandles+k,x,y,z,FactoryQ.Identity);
                }
                if(phase==2||phase==3)
                {
                    // the snapped chain whips up toward the anchor (the rig's tail)
                    double hub=y+(phase==3?.6:NativeSanctumLooks.CrownHeight),age=rig.Age[k];
                    double tail=Math.Min(anchor-.5,hub+.4+Math.Max(0,6-age*5));ChainSpan(into,x,anchor,z,x+Math.Sin(age*9)*.12,tail,z);
                }
            }
            // the censer on its rope, its pull rope while it is held
            foreach(var h in hazards??Array.Empty<MapHazard>())
            {
                if(h==null||h.kind!=NativeFactoryHazards.SawRail)continue;
                double a=rig.CenserAngle,L=rig.CenserRope,py=NativeSanctumLooks.CenserPivotY;double cy=py-L*Math.Cos(a),cz=Math.Sin(a)*L;
                double glow=rig.CenserGlow;into.Add(Censer,rig.CenserX,cy,cz,FactoryQ.Axis(1,0,0,-a),1,glow,glow,glow);
                if(rig.CenserHeld)into.Span(PullRope,rig.CenserX,rig.CenserY+.6,rig.CenserZ,0,CleatY,CleatZ);
                break;
            }
            // the bell and its clapper (it lags the swing)
            into.Add(Bell,NativeSanctumLooks.BellX,NativeSanctumLooks.BellY,NativeSanctumLooks.BellZ,FactoryQ.Axis(1,0,0,rig.Bell));
            into.Add(Clapper,NativeSanctumLooks.BellX,NativeSanctumLooks.BellY,NativeSanctumLooks.BellZ,FactoryQ.Axis(1,0,0,rig.Bell*.55));
            // the crypt gates: each grille at its closure, the lanterns in their colour; the holy fire by its heat
            int gate=0,fire=0;
            foreach(var h in hazards??Array.Empty<MapHazard>())
            {
                if(h==null)continue;
                if(h.kind==NativeWardZeroHazards.Shutter&&gate<2&&gate<rig.GateCount)
                {
                    double bottom=h.center.y-h.size.y/2,tall=h.size.y,y0=bottom+(1-rig.GateClosure[gate])*tall;into.Add(Gate+gate,h.center.x,y0,h.center.z,FactoryQ.Identity);
                    int lamp=rig.GateLamp[gate];Lin(lamp,out double r,out double gg,out double b);into.Add(Lanterns+gate,0,0,0,FactoryQ.Identity,1,r,gg,b);gate++;
                }
                else if(h.kind==NativeWardZeroHazards.Burner&&fire<rig.FireCount)
                {
                    double heat=rig.FireHeat[fire];double k=.15+1.6*heat*heat;int zones=Math.Min(2,h.zones?.Length??0);
                    for(int zi=0;zi<zones&&fire*2+zi<4;zi++)into.Add(Fire+fire*2+zi,0,0,0,FactoryQ.Identity,1,k,k*(.55+.45*heat),k*(.25+.75*heat*heat));
                    fire++;
                }
            }
            // the banners on the arcade piers' nave faces: crimson west, blue east
            foreach(int s in Sides)foreach(double px in NativeSanctumRig.BannerPiers)foreach(int sz in Sides)
            {
                double x=s*px,zc=sz*NativeSanctumRig.BannerZ-sz*NativeSanctumRig.BannerReach;
                // the model hangs across z on the +z side's pier; the -z side's turns half round to face the nave the same way
                var q=FactoryQ.Axis(0,0,1,NativeSanctumRig.BannerSway(render,px,sz));
                into.Add(s<0?BannerRed:BannerBlue,x,NativeSanctumRig.BannerTop,zc,q);
            }
        }
        private static readonly int[] Sides={-1,1};
        // Chain from the hub's ring up to the anchor in the chandelier's swayed frame: whole metres from the foot, the last scaled.
        private static void ChainUp(NativeStageArtPose into,double x,double anchor,double z,in FactoryQ q,double foot)
        {
            double len=-foot;if(len<=.01)return;int n=(int)Math.Ceiling(len-1e-6);double piece=len/n;
            for(int i=0;i<n;i++){q.Rotate(0,foot+i*piece,0,out double ox,out double oy,out double oz);into.Add(Chain,x+ox,anchor+oy,z+oz,q,piece);}
        }
        private static void ChainSpan(NativeStageArtPose into,double x0,double y0,double z0,double x1,double y1,double z1)
        {
            double dx=x1-x0,dy=y1-y0,dz=z1-z0,len=Math.Sqrt(dx*dx+dy*dy+dz*dz);if(len<.05)return;int n=(int)Math.Ceiling(len-1e-6);
            for(int i=0;i<n;i++){double t0=(double)i/n,t1=(double)(i+1)/n;into.Span(Chain,x1-dx*t1,y1-dy*t1,z1-dz*t1,x1-dx*t0,y1-dy*t0,z1-dz*t0);}
        }
        // An sRGB hex to linear 0..1 (the lanterns' tint multiplies a white glow).
        private static void Lin(int hex,out double r,out double g,out double b){r=L((hex>>16)&255);g=L((hex>>8)&255);b=L(hex&255);}
        private static double L(int c){double v=c/255.0;return v<=.04045?v/12.92:Math.Pow((v+.055)/1.055,2.4);}
    }
}
