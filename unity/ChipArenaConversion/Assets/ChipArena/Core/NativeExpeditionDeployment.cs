using System;
using System.Collections.Generic;
using System.Linq;

namespace ChipArena.UnityConversion
{
    // Pre-run authority: no active PvE match exists until the host deploys.
    [Serializable] public sealed class ExpeditionInstanceTheme
    {
        public string id, theme;
    }
    [Serializable] public sealed class ExpeditionDistrict
    {
        public string instance, theme, side;
        public int x, z;
        public ExpeditionDistrict Copy()=>(ExpeditionDistrict)MemberwiseClone();
    }
    [Serializable] public sealed class ExpeditionWorldCell
    {
        public int x,z; public bool road,sanctuary;
        // [neutral, chosen instance 0, chosen instance 1, chosen instance 2], normalized to 100.
        public int[] influences=Array.Empty<int>();
        public ExpeditionWorldCell Copy()
        {
            var c=(ExpeditionWorldCell)MemberwiseClone();
            c.influences=(int[])influences.Clone();
            return c;
        }
    }
    [Serializable] public sealed class ExpeditionDeploymentState
    {
        public int schema=1,revision,instanceRolls,mutatorRolls;
        public string station,host; public uint seed,worldSeed;
        public bool worldRolled,deployed;
        public string[] instances=Array.Empty<string>(),mutators=Array.Empty<string>();
        public ExpeditionDistrict[] districts=Array.Empty<ExpeditionDistrict>();
        public ExpeditionWorldCell[] cells=Array.Empty<ExpeditionWorldCell>();
        public ExpeditionDeploymentState Copy()
        {
            var s=(ExpeditionDeploymentState)MemberwiseClone();
            s.instances=(string[])instances.Clone();s.mutators=(string[])mutators.Clone();
            s.districts=districts.Select(d=>d.Copy()).ToArray();
            s.cells=cells.Select(c=>c.Copy()).ToArray();
            return s;
        }
    }
    public sealed class NativeExpeditionDeployment
    {
        public const int InstanceCount=3, GridWidth=25, GridHeight=19, CellMetres=10;
        private const int HalfX=GridWidth/2,HalfZ=GridHeight/2;
        private readonly ExpeditionDeploymentState state;
        public NativeExpeditionDeployment(string station,string host,uint seed)
        {
            if(!ExpeditionValidation.Id(station)||!ExpeditionValidation.Id(host))
                throw new ArgumentException("Deployment station and host need stable identifiers.");
            state=new ExpeditionDeploymentState{station=station,host=host,seed=seed};
        }
        public ExpeditionDeploymentState Snapshot()=>state.Copy();
        public bool Ready=>state.worldRolled&&!state.deployed;
        public bool Deployed=>state.deployed;
        private bool Authorized(string account)=>account==state.host&&!state.deployed;
        private static bool InstanceCatalog(ExpeditionInstanceTheme[] items)=>
            items!=null&&items.Length>=InstanceCount&&items.Length<=64&&
            items.All(x=>x!=null&&ExpeditionValidation.Id(x.id)&&ExpeditionValidation.Id(x.theme))&&
            items.Select(x=>x.id).Distinct(StringComparer.Ordinal).Count()==items.Length;
        private static bool MutatorCatalog(string[] ids)=>
            ids!=null&&ids.Length>0&&ids.Length<=128&&ids.All(ExpeditionValidation.Id)&&
            ids.Distinct(StringComparer.Ordinal).Count()==ids.Length;

        // Deterministic across desktop and Unity, independent of System.Random and wall clocks.
        private static uint Mix(uint x)
        {
            unchecked {x^=x>>16;x*=0x7feb352du;x^=x>>15;x*=0x846ca68bu;x^=x>>16;}
            return x==0?0x9e3779b9u:x;
        }
        private struct Roller
        {
            private uint x;
            public Roller(uint seed){x=Mix(seed);}
            private uint Next()
            {
                unchecked {x^=x<<13;x^=x>>17;x^=x<<5;}
                return x;
            }
            public int Index(int n)=>n<1?throw new ArgumentOutOfRangeException(nameof(n)):
                (int)(((ulong)Next()*(uint)n)>>32);
        }
        private static void Shuffle<T>(T[] items,ref Roller rng)
        {
            for(int i=items.Length-1;i>0;i--)
            {
                int j=rng.Index(i+1);T temp=items[i];items[i]=items[j];items[j]=temp;
            }
        }

        // First machine: select three fixed interiors and generate the outdoor layout blueprint.
        public bool RollInstances(string account,ExpeditionInstanceTheme[] catalog)
        {
            if(!Authorized(account)||!InstanceCatalog(catalog)||state.instanceRolls==int.MaxValue)return false;
            int next=state.instanceRolls+1;
            uint worldSeed=Mix(state.seed^unchecked((uint)next*0x9e3779b9u)^0x38c31fffu);
            var rng=new Roller(worldSeed);
            var choices=catalog.OrderBy(x=>x.id,StringComparer.Ordinal).ToArray();
            Shuffle(choices,ref rng);
            var districts=new ExpeditionDistrict[InstanceCount];
            for(int i=0;i<InstanceCount;i++)
            {
                string side=i==0?"east":i==1?"west":"south";
                int offset=rng.Index(5)-2;
                districts[i]=new ExpeditionDistrict{instance=choices[i].id,theme=choices[i].theme,side=side,
                    x=side=="east"?11:side=="west"?-11:offset,z=side=="south"?-8:offset};
            }
            state.instances=districts.Select(x=>x.instance).ToArray();
            state.districts=districts;
            state.cells=Generate(districts,worldSeed);
            state.worldSeed=worldSeed;state.worldRolled=true;state.instanceRolls=next;state.revision++;
            return true;
        }
        // Second machine: optional independent draw of at most three vetted mutator IDs.
        // Gameplay effects and rewards are intentionally NOT applied by this foundation.
        public bool RollMutators(string account,string[] catalog)
        {
            if(!Authorized(account)||!state.worldRolled||!MutatorCatalog(catalog)||state.mutatorRolls==int.MaxValue)return false;
            int next=state.mutatorRolls+1;
            var rng=new Roller(Mix(state.seed^unchecked((uint)next*0x85ebca6bu)^0xc2b2ae35u));
            var choices=catalog.OrderBy(x=>x,StringComparer.Ordinal).ToArray();
            Shuffle(choices,ref rng);
            state.mutators=choices.Take(Math.Min(3,choices.Length)).ToArray();
            state.mutatorRolls=next;state.revision++;
            return true;
        }
        public bool SkipMutators(string account)
        {
            if(!Authorized(account)||!state.worldRolled)return false;
            if(state.mutators.Length>0){state.mutators=Array.Empty<string>();state.revision++;}
            return true;
        }
        // partyReady is verified by the future host room adapter, never taken from the guest.
        public bool Deploy(string account,bool partyReady)
        {
            if(!Authorized(account)||!state.worldRolled||!partyReady)return false;
            state.deployed=true;state.revision++;return true;
        }

        private static int CellIndex(int x,int z)=>(x+HalfX)*GridHeight+(z+HalfZ);
        private static bool Inside(int x,int z)=>x>=-HalfX&&x<=HalfX&&z>=-HalfZ&&z<=HalfZ;
        private static void Route(bool[] roads,int tx,int tz,ref Roller rng,ref int x,ref int z)
        {
            while(x!=tx||z!=tz)
            {
                bool stepX=x!=tx&&(z==tz||rng.Index(2)==0);
                if(stepX)x+=Math.Sign(tx-x);else z+=Math.Sign(tz-z);
                if(Inside(x,z))roads[CellIndex(x,z)]=true;
            }
        }
        private static ExpeditionWorldCell[] Generate(ExpeditionDistrict[] districts,uint seed)
        {
            var rng=new Roller(Mix(seed^0x721ce3a9u));
            var roads=new bool[GridWidth*GridHeight];
            for(int x=-2;x<=2;x++)for(int z=-2;z<=2;z++)roads[CellIndex(x,z)]=true;
            foreach(var district in districts)
            {
                int x=0,z=0;
                int midX=district.side=="east"?5:district.side=="west"?-5:rng.Index(7)-3;
                int midZ=district.side=="south"?-4:rng.Index(7)-3;
                Route(roads,midX,midZ,ref rng,ref x,ref z);
                Route(roads,district.x,district.z,ref rng,ref x,ref z);
            }
            // Procedural one-cell branches can only attach to already connected trunk cells.
            var trunk=Enumerable.Range(0,roads.Length).Where(i=>roads[i]).ToArray();
            foreach(int index in trunk)
            {
                if(rng.Index(5)!=0)continue;
                int x=index/GridHeight-HalfX,z=index%GridHeight-HalfZ,d=rng.Index(4);
                x+=d==0?1:d==1?-1:0;z+=d==2?1:d==3?-1:0;
                if(Inside(x,z))roads[CellIndex(x,z)]=true;
            }
            var cells=new ExpeditionWorldCell[roads.Length];
            for(int x=-HalfX;x<=HalfX;x++)for(int z=-HalfZ;z<=HalfZ;z++)
            {
                int idx=CellIndex(x,z);
                var cell=new ExpeditionWorldCell{x=x,z=z,road=roads[idx],sanctuary=Math.Abs(x)<=2&&Math.Abs(z)<=2};
                double radius=Math.Sqrt(x*x+z*z)*CellMetres;
                var scores=new double[4];
                scores[0]=.12+1.5*Math.Max(0,1-radius/65);
                for(int i=0;i<InstanceCount;i++)
                {
                    double dx=(x-districts[i].x)*CellMetres,dz=(z-districts[i].z)*CellMetres;
                    scores[i+1]=1/(1+(dx*dx+dz*dz)/2500);
                }
                double sum=scores.Sum();var shares=new int[4];var frac=new double[4];int used=0;
                for(int i=0;i<4;i++)
                {
                    double p=scores[i]*100/sum;shares[i]=(int)Math.Floor(p);
                    frac[i]=p-shares[i];used+=shares[i];
                }
                for(int k=used;k<100;k++)
                {
                    int best=0;for(int i=1;i<4;i++)if(frac[i]>frac[best])best=i;
                    shares[best]++;frac[best]=-1;
                }
                if(cell.sanctuary)shares=new[]{100,0,0,0};
                cell.influences=shares;cells[idx]=cell;
            }
            return cells;
        }
    }
}
