using UnityEngine;
using UnityEngine.Rendering;
using NVector=System.Numerics.Vector3;
using NQuaternion=System.Numerics.Quaternion;

namespace ChipArena.UnityConversion
{
    // Burn flames, poison bubbles, slow droplets / shackle cuffs and the Orbital Mark reticle (client/status-effects.ts)
    // and team bands (client/team-identity.ts). Cosmetic only: the flags come from the pilots' statuses, the clock is the
    // match clock, and nothing here writes the simulation. Shapes and motion are NativeStatusVisuals (source frame); this
    // adapter mirrors x once. Buffers are allocated on first use, so a rendered frame allocates nothing.
    public sealed partial class ArenaPresentation
    {
        private sealed class StatusBatch
        {
            public readonly Matrix4x4[] Matrices;public int Count;public Mesh Mesh;public Material Material;
            public StatusBatch(int capacity){Matrices=new Matrix4x4[capacity];}
            public void Add(Matrix4x4 matrix){if(Count<Matrices.Length)Matrices[Count++]=matrix;}
        }
        private const int StatusSurfaces=9;
        private readonly StatusBatch[] statusWorld=new StatusBatch[StatusSurfaces],statusView=new StatusBatch[StatusSurfaces],teamBands=new StatusBatch[8],teamArmbands=new StatusBatch[8];
        private readonly Material[] statusMaterials=new Material[StatusSurfaces+8];private readonly Mesh[] statusMeshes=new Mesh[6];private Matrix4x4 armbandLocal;
        private readonly NativeStatusVisuals.Piece[] statusPieces=new NativeStatusVisuals.Piece[NativeStatusVisuals.MaxPieces];
        private readonly NativeMinion[] statusAdds=new NativeMinion[NativeStatusVisuals.AddRigs];private readonly float[] statusAddDistance=new float[NativeStatusVisuals.AddRigs];private readonly byte[] statusAddFlags=new byte[NativeStatusVisuals.AddRigs];
        private readonly Matrix4x4[] bandLocal=new Matrix4x4[4];private Matrix4x4 coreLocal,rimLocal,glintLocal;private bool statusReady,statusFailed;
        // Team for the draft stage's pilot (draft-stage.ts:164 teamArmor), or -1; set by the draft screen each frame.
        public int DraftBandTeam=-1;

        // renderer.ts: statusEffects.update after the frame's rigs are posed; quality is the effects setting (1, or .3 reduced).
        public void RenderStatus(NativeArena arena,Camera camera,float quality,bool view=true)
        {
            if(arena==null||camera==null||!StatusReady())return;
            float time=arena.Time;double now=arena.Match.Time;bool fight=arena.Match.Phase==NativePhase.Fight,teams=arena.Match.Teams;
            Vector3 eye=camera.transform.position;Quaternion look=camera.transform.rotation;
            // The same camera in the source frame: mirrored, and turned about y because three cameras look down -z.
            var sourceEye=new NVector(-eye.x,eye.y,eye.z);var sourceLook=new NQuaternion(look.x,-look.y,-look.z,look.w)*new NQuaternion(0,1,0,0);
            foreach(var p in arena.Pilots)
            {
                if(!p.Seat.connected||p.Hp<=0)continue;bool own=FirstPerson&&p.Seat.id==LocalId;rigs.TryGetValue(p.Seat.id,out var rig);
                if(!own&&rig!=null&&SpawnShowsFor(p.Seat.id,CapturedRig.Chest))Bands(rig,NativeStatusVisuals.PilotBand(teams,arena.Match.Cooperative,p.Seat.AuthorityBoss,p.Seat.team,p.Seat.id==arena.LocalId));
                if(own||!fight)continue;
                Vector3 at=rig!=null?rig.Root.transform.position:p.Body.position;var status=PilotStatus(arena,p,now,quality);
                status.Root=new NVector(-at.x,at.y,at.z);status.Camera=sourceEye;status.CameraRotation=sourceLook;QueueStatus(status,time,Matrix4x4.Translate(at),statusWorld);
            }
            // pve/horde-view.ts:116-123: burning, poisoned or slowed adds, nearest to the camera first, at most AddRigs.
            int adds=0;
            foreach(var m in arena.Minions)
            {
                var add=m.Pve;if(add==null||m.Hp<=0||add.Travelling||time<add.ActiveAt)continue;byte flags=0;
                foreach(var pair in m.Dots){if(pair.Value.Until<now)continue;if(pair.Key.EndsWith(":fire",System.StringComparison.Ordinal))flags|=1;else if(pair.Key.EndsWith(":poison",System.StringComparison.Ordinal))flags|=2;}
                if(m.SlowUntil>time)flags|=4;if(flags==0)continue;
                Vector3 at=m.Body.transform.position;float distance=new Vector2(at.x-eye.x,at.z-eye.z).magnitude;
                if(adds==statusAdds.Length&&distance>=statusAddDistance[adds-1])continue;
                int slot=adds<statusAdds.Length?adds++:adds-1;while(slot>0&&statusAddDistance[slot-1]>distance){statusAdds[slot]=statusAdds[slot-1];statusAddDistance[slot]=statusAddDistance[slot-1];statusAddFlags[slot]=statusAddFlags[slot-1];slot--;}
                statusAdds[slot]=m;statusAddDistance[slot]=distance;statusAddFlags[slot]=flags;
            }
            for(int i=0;i<adds;i++)
            {
                var m=statusAdds[i];statusAdds[i]=null;Vector3 at=m.Body.transform.position;at.y=NativeStatusVisuals.AddRootHeight(at.y,m.Pve.Crawl);byte flags=statusAddFlags[i];
                // Adds pass their slow as {factor .6, until t+1}: droplets, never cuffs.
                var status=new NativeStatusVisuals.Rig{Active=true,Quality=quality,Fire=(flags&1)!=0,Poison=(flags&2)!=0,Slowed=(flags&4)!=0,SlowUntil=time+1,Root=new NVector(-at.x,at.y,at.z),Camera=sourceEye,CameraRotation=sourceLook};
                QueueStatus(status,time,Matrix4x4.Translate(at),statusWorld);
            }
            FlushStatus(statusWorld,0,null);FlushStatus(teamBands,0,null);FlushStatus(teamArmbands,0,null);
            // The hands rig (StatusRig(true)) rides the view model: three flames and three bubbles, no slow or mark parts.
            if(!view||!FirstPerson||!fight||viewRig==null||ViewCamera==null||ScopeActive)return;
            NativePilot self=null;foreach(var p in arena.Pilots)if(p.Seat.id==LocalId){self=p;break;}
            if(self==null||self.Hp<=0)return;var hands=PilotStatus(arena,self,now,quality);hands.FirstPerson=true;
            QueueStatus(hands,time,viewRig.Root.transform.localToWorldMatrix,statusView);FlushStatus(statusView,10,ViewCamera);
        }
        // Team bands on a single presentation rig (draft stage, entrance), drawn at once.
        private void TeamBands(CapturedAnimation.Player rig,int team){if(rig==null||team<0||!StatusReady())return;Bands(rig,team);FlushStatus(teamBands,0,null);FlushStatus(teamArmbands,0,null);}
        private void Bands(CapturedAnimation.Player rig,int team)
        {
            if(team<0||team>=teamBands.Length)return;
            // On the player's model: a band round each upper arm (the voxel bands' boxes sit at the voxel body's joints).
            var body=VisualPosedBody(rig);if(body!=null){var ring=teamArmbands[team];ring.Add(VisualLimbFrame(body,rig,CapturedRig.LArm)*armbandLocal);ring.Add(VisualLimbFrame(body,rig,CapturedRig.RArm)*armbandLocal);return;}
            var batch=teamBands[team];for(int i=0;i<bandLocal.Length;i++)batch.Add(rig.Bones[NativeStatusVisuals.BandBones[i]].localToWorldMatrix*bandLocal[i]);
        }
        private static NativeStatusVisuals.Rig PilotStatus(NativeArena arena,NativePilot p,double now,float quality)
        {
            // simulation.ts:539 a status shows while any of its DoTs has time left; ability-system.ts:165 the slow; the
            // Orbital Mark zone attached to this pilot (gun-abilities.ts:30: born, lock after `duration`, strike at `fuse`).
            var status=new NativeStatusVisuals.Rig{Active=true,Quality=quality};float time=arena.Time;
            foreach(var pair in p.Dots){if(pair.Value.Until<=now)continue;if(pair.Key.EndsWith(":fire",System.StringComparison.Ordinal))status.Fire=true;else if(pair.Key.EndsWith(":poison",System.StringComparison.Ordinal))status.Poison=true;}
            if(p.SlowEnd>time&&p.SlowFactor<1){status.Slowed=true;status.SlowUntil=p.SlowEnd;status.Shackled=NativeStatusVisuals.Shackled(p.SlowFactor);}
            foreach(var zone in arena.AbilityZones)
                if(zone.Attached==p&&zone.Definition!=null&&zone.Definition.id=="orbital-mark")
                {status.Marked=true;status.MarkUntil=zone.Until;status.MarkBorn=zone.Born;status.MarkLock=zone.LockAt>0?zone.LockAt:zone.Until;break;}
            return status;
        }
        private void QueueStatus(in NativeStatusVisuals.Rig rig,float time,Matrix4x4 parent,StatusBatch[] target)
        {
            int count=NativeStatusVisuals.Build(rig,time,statusPieces);
            for(int i=0;i<count;i++)
            {
                var p=statusPieces[i];var m=parent*Matrix4x4.TRS(new Vector3(-p.Position.X,p.Position.Y,p.Position.Z),new Quaternion(p.Rotation.X,-p.Rotation.Y,-p.Rotation.Z,p.Rotation.W),new Vector3(p.Scale.X,p.Scale.Y,p.Scale.Z));
                switch(p.Kind)
                {
                    case NativeStatusVisuals.Kind.Flame:target[(int)NativeStatusVisuals.Surface.Fire].Add(m);target[(int)NativeStatusVisuals.Surface.Core].Add(m*coreLocal);break;
                    case NativeStatusVisuals.Kind.Ember:target[(int)NativeStatusVisuals.Surface.Ember].Add(m);break;
                    case NativeStatusVisuals.Kind.Bubble:
                        // A popping bubble keeps only its rim (status-effects.ts:50).
                        if(!p.Popping)target[(int)NativeStatusVisuals.Surface.Bubble].Add(m);target[(int)NativeStatusVisuals.Surface.Rim].Add(m*rimLocal);if(!p.Popping)target[(int)NativeStatusVisuals.Surface.Glint].Add(m*glintLocal);break;
                    case NativeStatusVisuals.Kind.Slow:target[(int)NativeStatusVisuals.Surface.Slow].Add(m);break;
                    case NativeStatusVisuals.Kind.Shackle:target[(int)NativeStatusVisuals.Surface.Shackle].Add(m);break;
                    default:target[(int)NativeStatusVisuals.Surface.Mark].Add(m);break;
                }
            }
        }
        private void FlushStatus(StatusBatch[] list,int layer,Camera camera)
        {
            foreach(var b in list)
            {
                if(b==null||b.Count==0)continue;bool drawn=false;
                if(UsesInstancing)
                {
                    try{Graphics.DrawMeshInstanced(b.Mesh,0,b.Material,b.Matrices,b.Count,null,ShadowCastingMode.Off,false,layer,camera);drawn=true;}
                    catch(System.InvalidOperationException e){UseCompatibility(e.Message);}
                    catch(System.ArgumentException e){UseCompatibility(e.Message);}
                }
                if(!drawn)for(int i=0;i<b.Count;i++)Graphics.DrawMesh(b.Mesh,b.Matrices[i],b.Material,layer,camera,0,null,ShadowCastingMode.Off,false);
                b.Count=0;
            }
        }
        private bool StatusReady()
        {
            if(statusReady||statusFailed)return statusReady;if(cube==null)return false;
            var shader=Resources.Load<Shader>("NativeStatusUnlit");
            if(shader==null||!shader.isSupported){statusFailed=true;Debug.LogWarning("NativeStatusUnlit shader is unavailable; status effects and team bands are not drawn");return false;}
            var flame=new NVector[NativeStatusVisuals.FlameOutline.Length/2];for(int i=0;i<flame.Length;i++)flame[i]=new NVector(NativeStatusVisuals.FlameOutline[i*2],NativeStatusVisuals.FlameOutline[i*2+1],0);
            statusMeshes[0]=StatusMesh("Status flame",flame,NativeStatusVisuals.FlameTriangles);
            statusMeshes[1]=StatusMesh("Status ember",NativeStatusVisuals.OctahedronPoints,NativeStatusVisuals.OctahedronTriangles);
            NativeStatusVisuals.Sphere(8,6,out var bubble,out var bubbleTriangles);statusMeshes[2]=StatusMesh("Status bubble",bubble,bubbleTriangles);
            NativeStatusVisuals.Ring(.88f,1,16,out var ring,out var ringTriangles);statusMeshes[3]=StatusMesh("Status bubble rim",ring,ringTriangles);
            NativeStatusVisuals.Sphere(5,3,out var glint,out var glintTriangles);statusMeshes[4]=StatusMesh("Status bubble glint",glint,glintTriangles);
            NativeStatusVisuals.Armband(14,out var band,out var bandTriangles);statusMeshes[5]=StatusMesh("Team armband",band,bandTriangles);
            for(int s=0;s<StatusSurfaces;s++)
            {
                // Translucent layers draw back to front within a part as three's per-object sort does: flame then core; body, rim, glint.
                var style=NativeStatusVisuals.Surfaces[s];int order=s==(int)NativeStatusVisuals.Surface.Core||s==(int)NativeStatusVisuals.Surface.Rim?1:s==(int)NativeStatusVisuals.Surface.Glint?2:0;
                var material=StatusMaterial(shader,style.Color,style.Opacity,style.DoubleSide,order);statusMaterials[s]=material;
                var mesh=s<=(int)NativeStatusVisuals.Surface.Core?statusMeshes[0]:s<=(int)NativeStatusVisuals.Surface.Glint?statusMeshes[s-1]:cube;
                statusWorld[s]=new StatusBatch(256){Mesh=mesh,Material=material};statusView[s]=new StatusBatch(16){Mesh=mesh,Material=material};
            }
            for(int t=0;t<teamBands.Length;t++){var material=StatusMaterial(shader,NativeStatusVisuals.TeamColors[t],1,false);statusMaterials[StatusSurfaces+t]=material;teamBands[t]=new StatusBatch(64){Mesh=cube,Material=material};teamArmbands[t]=new StatusBatch(32){Mesh=statusMeshes[5],Material=material};}
            Vector3 Mirror(NVector v)=>new Vector3(-v.X,v.Y,v.Z);Vector3 Size(NVector v)=>new Vector3(v.X,v.Y,v.Z);
            coreLocal=Matrix4x4.TRS(Mirror(NativeStatusVisuals.CorePosition),Quaternion.identity,Size(NativeStatusVisuals.CoreScale));rimLocal=Matrix4x4.Translate(Mirror(NativeStatusVisuals.RimPosition));
            glintLocal=Matrix4x4.TRS(Mirror(NativeStatusVisuals.GlintPosition),Quaternion.identity,Size(NativeStatusVisuals.GlintScale));
            for(int i=0;i<bandLocal.Length;i++)bandLocal[i]=Matrix4x4.TRS(Mirror(NativeStatusVisuals.BandPositions[i]),Quaternion.identity,Size(NativeStatusVisuals.BandSizes[i]));
            armbandLocal=Matrix4x4.TRS(new Vector3(0,-NativeStatusVisuals.ArmbandDrop,0),Quaternion.identity,new Vector3(NativeStatusVisuals.ArmbandRadius,NativeStatusVisuals.ArmbandHeight,NativeStatusVisuals.ArmbandRadius));
            return statusReady=true;
        }
        private static Material StatusMaterial(Shader shader,int hex,float opacity,bool doubleSide,int order=0)
        {
            bool clear=opacity<1;var color=NativeArena.ColorOf(hex);color.a=opacity;
            var material=new Material(shader){name="Status "+hex.ToString("x6"),enableInstancing=true,renderQueue=(clear?(int)RenderQueue.Transparent:(int)RenderQueue.Geometry)+order};
            material.SetColor("_Color",color);material.SetFloat("_SrcBlend",(float)(clear?BlendMode.SrcAlpha:BlendMode.One));material.SetFloat("_DstBlend",(float)(clear?BlendMode.OneMinusSrcAlpha:BlendMode.Zero));
            material.SetFloat("_ZWrite",clear?0:1);material.SetFloat("_Cull",(float)(doubleSide?CullMode.Off:CullMode.Back));return material;
        }
        private static Mesh StatusMesh(string name,NVector[] points,int[] triangles)
        {
            // Source frame to Unity: mirror x and reverse the winding, so the source's front faces stay front faces.
            var vertices=new Vector3[points.Length];for(int i=0;i<points.Length;i++)vertices[i]=new Vector3(-points[i].X,points[i].Y,points[i].Z);
            var order=new int[triangles.Length];for(int i=0;i<triangles.Length;i+=3){order[i]=triangles[i];order[i+1]=triangles[i+2];order[i+2]=triangles[i+1];}
            var mesh=new Mesh{name=name};mesh.vertices=vertices;mesh.triangles=order;mesh.RecalculateBounds();mesh.UploadMeshData(true);return mesh;
        }
        private void DisposeStatus()
        {
            foreach(var m in statusMaterials)if(m!=null)CapturedAnimation.Release(m);foreach(var m in statusMeshes)if(m!=null)CapturedAnimation.Release(m);
            System.Array.Clear(statusMaterials,0,statusMaterials.Length);System.Array.Clear(statusMeshes,0,statusMeshes.Length);statusReady=false;
        }
    }
}
