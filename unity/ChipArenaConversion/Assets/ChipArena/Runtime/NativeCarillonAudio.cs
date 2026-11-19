using UnityEngine;

namespace ChipArena.UnityConversion
{
    public sealed partial class NativeAudio
    {
        private NativeArena carillonAudioView;
        private double carillonLastTime=-1;
        private int carillonAudioGeneration;
        private readonly int[] carillonPhases=new int[8];
        private void CarillonCues(NativeArena arena,bool menu)
        {
            if(menu||arena?.MapId!=NativeThunderheart.Id||arena.Match.Phase!=NativePhase.Fight){carillonAudioView=null;return;}
            var match=arena.Match;double now=match.Time;
            bool fresh=ReferenceEquals(carillonAudioView,arena)&&carillonAudioGeneration==arena.Generation&&now>=carillonLastTime&&now-carillonLastTime<.5;
            carillonLastTime=now;carillonAudioView=arena;carillonAudioGeneration=arena.Generation;
            int round=NativeStructureFx.RoundIndex(match.Set,match.Round);
            var hs=arena.Map.hazards;
            void Change(int i,int state,string id,string fallback,Vector3 at,float gain,float pitch)
            {
                if(fresh&&state!=carillonPhases[i]&&state!=0&&id!=null)Cue(id,fallback,at,gain,pitch);
                carillonPhases[i]=state;
            }
            for(int i=0;i<2;i++)
            {
                var h=hs[i];var s=NativeWardZeroHazards.At(h,match.Phase,round,match.FightAt,now);
                bool warn=s.Phase==NativeHazardPhase.Warn;
                Change(i,(int)s.Phase,warn?"carillon.toll":"carillon.iris",warn?"metal":"stone",NativeArena.Vec(h.center),warn?.72f:.43f,warn?.42f:.62f);
            }
            for(int i=2;i<=4;i++)
            {
                var h=hs[i];var s=NativeFactoryHazards.State(h,round,NativeFactoryHazards.Clock(match.Phase,match.FightAt,now));
                bool enabled=i==4||arena.CarillonWellFed(h);
                Change(i,enabled?s.Phase+1:0,s.Phase==NativeFactoryHazards.Warning?"carillon.hiss":s.Phase==NativeFactoryHazards.Bursting?"carillon.gust":null,"dash",NativeArena.Vec(h.a),.40f,i==4?.55f:1.15f);
            }
            var grid=NativeCyberCityHazards.Grid(hs[5],round,match.FightAt,now);
            Change(5,(int)grid.Phase,"carillon.charge","lightning",NativeArena.Vec(hs[5].center),.32f,.85f);
            for(int i=0;i<2;i++)
            {
                int side=i==0?-1:1;var pos=new Vector3(side*20,2.78f,side*8);
                float near=ArenaPresentation.CarillonProximity(arena,pos);
                // Hysteresis keeps a pilot on the threshold from retriggering the bell.
                bool ringing=near>(carillonPhases[6+i]==1?.15f:.48f);
                Change(6+i,ringing?1:0,"carillon.sentry","metal",pos,.34f,i==0?1.35f:1.65f);
            }
        }
    }
}
