using System;
using System.Collections.Generic;
using System.Linq;
using CampusCommons.LibraryCharacter;
using UnityEditor;
using UnityEngine;

namespace CampusCommons.LibraryCharacter.Editor
{
    public static class SchoolGridBuilder
    {
        const string Path="Assets/LibraryCharacter/Generated/SchoolGrid.asset";
        const int MinX=920,MaxX=1080,MinZ=-110,MaxZ=110;
        const float Step=.5f;
        static readonly int[] DX={0,1,0,-1},DZ={1,0,-1,0};
        static int Width=>MaxX-MinX+1;
        static int Id(int x,int z)=>x<MinX || x>MaxX || z<MinZ || z>MaxZ?-1:(z-MinZ)*Width+x-MinX;
        static Vector3 Position(int id)=>new Vector3((id%Width+MinX)*Step,0,(id/Width+MinZ)*Step);
        static bool Walkable(int id,sbyte[] cache)
        {
            if(cache[id]!=0)return cache[id]>0;
            Vector3 p=Position(id);
            bool floor=Physics.Raycast(p+Vector3.up*1.8f,Vector3.down,out RaycastHit hit,2.8f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore) && Mathf.Abs(hit.point.y)<.3f;
            bool clear=floor && !Physics.CheckCapsule(p+Vector3.up*.36f,p+Vector3.up*1.48f,.29f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
            cache[id]=(sbyte)(clear?1:-1);return clear;
        }
        static bool ClearBetween(int a,int b)
        {
            var from=Position(a);var delta=Position(b)-from;
            return !Physics.CapsuleCast(from+Vector3.up*.36f,from+Vector3.up*1.48f,.25f,delta.normalized,out RaycastHit hit,delta.magnitude,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
        }
        public static SchoolGrid Build(Vector3 spawn)
        {
            Physics.SyncTransforms();int height=MaxZ-MinZ+1,total=Width*height;
            var map=AssetDatabase.LoadAssetAtPath<SchoolGrid>(Path);if(!map){map=ScriptableObject.CreateInstance<SchoolGrid>();AssetDatabase.CreateAsset(map,Path);}
            map.step=Step;map.minX=MinX;map.minZ=MinZ;map.width=Width;map.height=height;map.links=new byte[total];
            var cache=new sbyte[total];var distance=Enumerable.Repeat(-1,total).ToArray();var queue=new Queue<int>();
            int start=Id(Mathf.RoundToInt(spawn.x/Step),Mathf.RoundToInt(spawn.z/Step));
            if(start<0 || !Walkable(start,cache))throw new Exception("School spawn is not walkable on navigation grid");
            distance[start]=0;queue.Enqueue(start);int reachable=0;
            while(queue.Count>0)
            {
                int current=queue.Dequeue();reachable++;int x=current%Width+MinX,z=current/Width+MinZ;
                for(int d=0;d<4;d++)
                {
                    int next=Id(x+DX[d],z+DZ[d]);if(next<0 || !Walkable(next,cache))continue;
                    int opposite=(d+2)%4;
                    if((map.links[next]&(1<<opposite))==0 && !ClearBetween(current,next))continue;
                    map.links[current]|=(byte)(1<<d);map.links[next]|=(byte)(1<<opposite);
                    if(distance[next]<0){distance[next]=distance[current]+1;queue.Enqueue(next);}
                }
            }
            if(reachable<1000)throw new Exception("School walkable area unexpectedly small: "+reachable);
            var candidates=Enumerable.Range(0,total).Where(i=>distance[i]>=10 && map.links[i]!=0).ToArray();
            var selected=new List<int>();
            // Include one nearby sheet, then cover the full map with well-spaced locations.
            selected.Add(candidates.OrderBy(i=>Mathf.Abs(distance[i]*Step-6f)).First());
            while(selected.Count<20)
            {
                int best=-1;float bestScore=float.NegativeInfinity;
                foreach(int id in candidates)
                {
                    if(selected.Contains(id))continue;Vector3 p=Position(id);float nearest=float.PositiveInfinity;
                    foreach(int other in selected)nearest=Mathf.Min(nearest,(p-Position(other)).sqrMagnitude);
                    float score=nearest-.0001f*distance[id];
                    if(score>bestScore){bestScore=score;best=id;}
                }
                if(best<0 || bestScore<9)throw new Exception("Could not place twenty spaced sheets");
                selected.Add(best);
            }
            map.sheetLocations=selected.OrderBy(i=>distance[i]).Select(Position).ToArray();
            EditorUtility.SetDirty(map);AssetDatabase.SaveAssets();
            Debug.Log("SCHOOL_GRID_OK reachable="+reachable+" sheet locations="+map.sheetLocations.Length+" max_path_m="+(distance.Max()*Step));
            return map;
        }
    }
}
