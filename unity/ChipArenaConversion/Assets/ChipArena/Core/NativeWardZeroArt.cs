using System;
using System.Collections.Generic;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// Ward Zero's art poses (docs/arenas/STAGE-ART.md, docs/arenas/WARDZERO-ART.md): what of the stage bank ChipArenaStageWardZero
    /// (tools/stages/wardzero_art.py) moves or can leave, drawn as rig props each frame. Both map records ("siren" and the
    /// Expedition's "expedition-ward-zero") share the bank and these poses.
    /// <list type="bullet">
    /// <item>The movers (Core NativeWardZeroKit.Movers): the kit's moving parts in groups, each where the kit's own
    /// <see cref="NativeWardZeroKit.Move"/> puts it this frame: the floor and ceiling vents' grate lids sliding aside, the wall
    /// vents' louvres lifting, the cell gates' bars rising, the doors' leaves parting, the drop hatches' leaves, and every blast
    /// shutter slat coming down out of its lintel (hidden while it is back inside, as the box was). Slots "m0".."mN": movers of
    /// one shape share a model.</item>
    /// <item>The blocks that can leave (<see cref="Posed"/>): the interior wall blocks a Hulk may break out, the breach panels
    /// and the Expedition's exit frame, each its own model in the block's frame (slot "b/&lt;block id&gt;"), drawn where the
    /// block stands with its knock's shudder and left out while it is broken out or its breach is open, exactly as the block
    /// dressing and the kit parts on it are.</item>
    /// </list>
    /// Everything else of the art is static scenery. Presentation only: never sim data, a collider or the network frame. Build
    /// allocates once per map; <see cref="Pose"/> is pure and allocation free.
    /// </summary>
    public sealed class NativeWardZeroArt
    {
        public const string Bank="ChipArenaStageWardZero";
        /// <summary>The bank's rig props by slot (its "rig/&lt;name&gt;/&lt;tier&gt;"): the movers' models, then the posed blocks.</summary>
        /// <summary>How many kept boxes each kit part lies on (NativeStageArt.BoxLayers; 0 for a part the art replaces or one on a
        /// turned block): the presentation draws a kept box that many layers nearer the eye, over NativeStageArt.KitLayers.</summary>
        public static int[] KeptLayers(NativeWardZeroKit kit,ContentBlock[] blocks)
        {
            var parts=kit.Parts;int n=parts.Length;var boxes=new float[n*6];var skip=new bool[n];var layers=new int[n];
            for(int i=0;i<n;i++)
            {
                var p=parts[i];var b=p.Parent>=0&&p.Parent<blocks.Length?blocks[p.Parent]:null;
                if(b==null||i>=kit.Keeps.Length||!kit.Keeps[i]||b.rotation!=null&&Math.Abs(b.rotation.w)<.9999f){skip[i]=true;continue;}
                int o=i*6;boxes[o]=b.pos.x+p.X;boxes[o+1]=b.pos.y+p.Y;boxes[o+2]=b.pos.z+p.Z;boxes[o+3]=p.Hx;boxes[o+4]=p.Hy;boxes[o+5]=p.Hz;
            }
            NativeStageArt.BoxLayers(boxes,n,skip,layers);return layers;
        }
        public string[] Slots=Array.Empty<string>();
        /// <summary>How many slots are movers' models; the posed block i draws slot MoverSlots + i.</summary>
        public int MoverSlots;
        /// <summary>The posed blocks (indices into the map's blocks) and, for a breach panel, its entry (else -1).</summary>
        public int[] Blocks=Array.Empty<int>();public short[] BlockEntry=Array.Empty<short>();
        /// <summary>A mover's first kit part (its drive, entry, hazard and clip are the group's).</summary>
        public int[] MoverPart=Array.Empty<int>();

        /// <summary>Whether the art draws this map block as a rig prop of its own (it can leave: a Hulk breaks interior walls
        /// out, a breach opens its panel; the exit frame stands only in the Expedition's record). Every block
        /// NativeHulkBreach.Eligible names is one (checked).</summary>
        public static bool Posed(ContentBlock b)
        {
            if(b?.id==null||b.role!="static")return false;
            if(b.id.StartsWith("expedition-ward-zero-exit-",StringComparison.Ordinal))return true;
            var s=NativeWardZeroKit.Stem(b.id);if(s==null)return false;
            return s.StartsWith("breach-",StringComparison.Ordinal)||b.id.IndexOf("-wall-",StringComparison.Ordinal)>=0;
        }
        public static string SlotOf(ContentBlock b)=>"b/"+b.id;

        public static NativeWardZeroArt Build(NativeWardZeroKit kit,ContentBlock[] blocks,NativeWardEntry[] entries)
        {
            var a=new NativeWardZeroArt();blocks=blocks??Array.Empty<ContentBlock>();entries=entries??Array.Empty<NativeWardEntry>();
            var slots=new List<string>();a.MoverSlots=kit?.MoverSlots??0;for(int i=0;i<a.MoverSlots;i++)slots.Add("m"+i);
            if(kit!=null){a.MoverPart=new int[kit.Movers.Length];for(int i=0;i<a.MoverPart.Length;i++)a.MoverPart[i]=-1;for(int i=kit.MoverOf.Length-1;i>=0;i--)if(kit.MoverOf[i]>=0)a.MoverPart[kit.MoverOf[i]]=i;}
            var posed=new List<int>();var entry=new List<short>();
            for(int i=0;i<blocks.Length;i++)
            {
                var b=blocks[i];if(!Posed(b))continue;short e=-1;for(int k=0;k<entries.Length;k++)if(entries[k]?.kind=="breach"&&entries[k].block==b.id){e=(short)k;break;}
                posed.Add(i);entry.Add(e);slots.Add(SlotOf(b));
            }
            a.Blocks=posed.ToArray();a.BlockEntry=entry.ToArray();a.Slots=slots.ToArray();return a;
        }

        /// <summary>This frame's draws. states, open, mood: what the kit's Move reads (the hazard states, the entries' open amounts,
        /// the mood); breached by entry and blockOut by block: what is broken out (left undrawn); nudge: three floats a block, its
        /// knock's shudder (world m). Any of them may be null.</summary>
        public void Pose(NativeWardZeroKit kit,ContentBlock[] blocks,NativeHazardState[] states,float[] open,in NativeWardMood mood,bool[] breached,bool[] blockOut,float[] nudge,NativeStageArtPose into)
        {
            into.Clear();if(kit==null||blocks==null)return;var I=FactoryQ.Identity;var movers=kit.Movers;
            for(int g=0;g<movers.Length&&g<MoverPart.Length;g++)
            {
                var m=movers[g];int pi=MoverPart[g];if(pi<0||m.Parent<0||m.Parent>=blocks.Length)continue;
                if(m.Entry>=0&&breached!=null&&m.Entry<breached.Length&&breached[m.Entry])continue;if(blockOut!=null&&m.Parent<blockOut.Length&&blockOut[m.Parent])continue;
                float f=NativeWardZeroKit.Move(kit.Parts[pi],states,open,mood,out bool hidden);if(hidden)continue;var b=blocks[m.Parent];
                double x=m.X+m.Mx*f,y=m.Y+m.My*f,z=m.Z+m.Mz*f;Turn(b,ref x,ref y,ref z);int n=m.Parent*3;
                if(nudge!=null&&n+2<nudge.Length){x+=nudge[n];y+=nudge[n+1];z+=nudge[n+2];}
                into.Add(m.Slot,b.pos.x+x,b.pos.y+y,b.pos.z+z,Rot(b));
            }
            for(int i=0;i<Blocks.Length;i++)
            {
                int bi=Blocks[i];if(bi<0||bi>=blocks.Length)continue;var b=blocks[bi];if(b==null)continue;
                if(blockOut!=null&&bi<blockOut.Length&&blockOut[bi])continue;int e=BlockEntry[i];if(e>=0&&breached!=null&&e<breached.Length&&breached[e])continue;
                double x=b.pos.x,y=b.pos.y,z=b.pos.z;int n=bi*3;if(nudge!=null&&n+2<nudge.Length){x+=nudge[n];y+=nudge[n+1];z+=nudge[n+2];}
                into.Add(MoverSlots+i,x,y,z,Rot(b));
            }
        }
        private static FactoryQ Rot(ContentBlock b)=>b.rotation==null?FactoryQ.Identity:new FactoryQ{X=b.rotation.x,Y=b.rotation.y,Z=b.rotation.z,W=b.rotation.w};
        private static void Turn(ContentBlock b,ref double x,ref double y,ref double z){if(b.rotation==null)return;Rot(b).Rotate(x,y,z,out x,out y,out z);}
    }
}
