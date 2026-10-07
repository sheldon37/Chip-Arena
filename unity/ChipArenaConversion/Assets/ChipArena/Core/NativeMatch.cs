using System;
using System.Collections.Generic;
using System.Linq;

namespace ChipArena.UnityConversion
{
    public enum NativePhase { Lobby, Countdown, Fight, Result, Draft, Match }
    [Serializable] public sealed class NativeSeat
    {
        public int id,team,roundWins,score;public string name,spec="pistol";public bool bot,connected=true,alive=true;
        public readonly Dictionary<string,int> chips=new Dictionary<string,int>();public List<string> offers=new List<string>();
    }
    public sealed class NativeMatch
    {
        public readonly List<NativeSeat> Seats=new List<NativeSeat>();
        public NativePhase Phase { get;private set; }=NativePhase.Lobby;
        public double Time { get;private set; }
        public double PhaseEnd { get;private set; }
        public double FightAt { get;private set; }
        public int Round { get;private set; }=1;
        public int Set { get;private set; }=1;
        public int Winner { get;private set; }
        public int SetWinner { get;private set; }
        public int DraftSeat { get;private set; }
        public string Result { get;private set; }="";
        public string Chosen { get;private set; }
        public double ReadyAt { get;private set; }
        public bool Teams,BothDraft,SkipBotDrafts=true;
        public int RoundsPerSet=2,SetsToWin=5,Choices=6;
        public double SelectionSeconds=18;
        public Func<ContentChip,bool> DraftFilter;
        public Action ResetRound;
        private readonly ConversionContent data;private readonly SourceRandom random;
        private readonly List<int> queue=new List<int>();private int turn;private double chosenAt;
        public NativeMatch(ConversionContent data,uint seed=12345){this.data=data;random=new SourceRandom(seed);}
        public bool SameSide(NativeSeat a,NativeSeat b)=>a!=null&&b!=null&&(a.id==b.id||Teams&&a.team==b.team);
        public NativeSeat Seat(int id)=>Seats.Find(p=>p.id==id);
        public bool Start()
        {
            if(Phase!=NativePhase.Lobby&&Phase!=NativePhase.Match)return false;
            if(Seats.Count<2||Seats.Count>8||Seats.Select(s=>s.id).Distinct().Count()!=Seats.Count)return false;
            if(Seats.Where(p=>p.connected).Select(p=>Teams?p.team:p.id).Distinct().Count()<2)return false;
            RoundsPerSet=Math.Max(1,Math.Min(10,RoundsPerSet));SetsToWin=Math.Max(1,Math.Min(20,SetsToWin));Choices=Math.Max(1,Math.Min(12,Choices));SelectionSeconds=Math.Max(5,Math.Min(120,Math.Round(SelectionSeconds)));
            Round=Set=1;foreach(var p in Seats){data.Weapon(p.spec);p.chips.Clear();p.roundWins=p.score=0;p.offers.Clear();}BeginRound();return true;
        }
        private void BeginRound()
        { Winner=SetWinner=DraftSeat=0;Chosen=null;foreach(var p in Seats)p.alive=p.connected;ResetRound?.Invoke();Phase=NativePhase.Countdown;PhaseEnd=Time+3; }
        private void NextSet()
        { Set++;Round=1;foreach(var p in Seats){p.roundWins=0;p.offers.Clear();}BeginRound(); }
        public void Tick(double dt)
        {
            if(double.IsNaN(dt)||double.IsInfinity(dt)||dt<0||dt>.25)throw new ArgumentOutOfRangeException(nameof(dt));Time+=dt;
            if(Phase==NativePhase.Countdown&&Time>=PhaseEnd){Phase=NativePhase.Fight;FightAt=Time;}
            else if(Phase==NativePhase.Fight)Resolve();
            else if(Phase==NativePhase.Result&&Time>=PhaseEnd)
            {
                if(SetWinner!=0&&Seat(SetWinner).score>=SetsToWin){Phase=NativePhase.Match;PhaseEnd=0;return;}
                if(SetWinner==0){if(Winner!=0)Round++;BeginRound();return;}
                queue.Clear();
                foreach(var p in Seats.Where(p=>p.connected&&(BothDraft||!SameSide(p,Seat(SetWinner)))).OrderBy(p=>SameSide(p,Seat(SetWinner))))
                {p.offers=NativeContentRules.Offers(data,p.spec,p.chips,random.Next,Choices,DraftFilter);if(p.offers.Count>0)queue.Add(p.id);}
                Phase=NativePhase.Draft;turn=0;NextDraft();
            }
            else if(Phase==NativePhase.Draft)
            {
                var p=Seat(DraftSeat);
                if(p==null||!p.connected){turn++;NextDraft();}
                else if(Chosen!=null&&Time>=chosenAt+RevealSeconds(Chosen)){turn++;NextDraft();}
                else if(Chosen==null&&Time>=ReadyAt&&(Time>=PhaseEnd||p.bot&&Time>=ReadyAt+1.4))Choose(p.id,p.offers[0]);
            }
        }
        public void Resolve()
        {
            if(Phase!=NativePhase.Fight)return;var alive=Seats.Where(p=>p.alive&&p.connected).ToArray();
            if(alive.Select(p=>Teams?p.team:p.id).Distinct().Count()>1)return;
            Winner=alive.Length>0?alive[0].id:0;SetWinner=0;
            if(Winner!=0)
            {
                var win=Seat(Winner);foreach(var p in Seats.Where(p=>SameSide(p,win)))p.roundWins++;
                if(win.roundWins>=RoundsPerSet){foreach(var p in Seats.Where(p=>SameSide(p,win)))p.score++;SetWinner=win.id;}
                Result=win.name+(SetWinner!=0?(win.score>=SetsToWin?" wins the match":" wins set "+Set):" wins round "+Round);
            }
            else Result="Simultaneous knockout — replay this round";
            Phase=NativePhase.Result;PhaseEnd=Time+5;
        }
        private static double RevealSeconds(string id)=>id.StartsWith("l44t-")?4.6:id.StartsWith("arcade-")||id.StartsWith("mishima-")?4.8:3.2;
        private void NextDraft()
        {
            while(turn<queue.Count)
            {
                var p=Seat(queue[turn]);if(p==null||!p.connected){turn++;continue;}
                DraftSeat=p.id;Chosen=null;
                ReadyAt=Time+(SkipBotDrafts&&p.bot?0:Math.Max(1.2,(p.offers.Count-1)*.22+(p.offers.Any(id=>id.StartsWith("l44t-")||id.StartsWith("arcade-")||id.StartsWith("mishima-"))?1.6:1)));
                PhaseEnd=ReadyAt+SelectionSeconds;
                if(SkipBotDrafts&&p.bot&&Choose(p.id,p.offers[0])){turn++;continue;}return;
            }
            NextSet();
        }
        public bool Choose(int seat,string id)
        {
            var p=Seat(seat);if(Phase!=NativePhase.Draft||p==null||!p.connected||DraftSeat!=seat||Chosen!=null||Time<ReadyAt||!p.offers.Contains(id))return false;
            if(!NativeContentRules.Eligible(data,p.spec,p.chips).Any(c=>c.id==id))return false;
            p.chips.TryGetValue(id,out int n);p.chips[id]=n+1;Chosen=id;chosenAt=Time;return true;
        }
    }
}
