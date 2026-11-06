using System;
using System.Collections.Generic;
using UnityEngine;

namespace CampusCommons.LibraryCharacter
{
    /// <summary>Precomputed, collision-checked walking connections for the school.</summary>
    public sealed class SchoolGrid : ScriptableObject
    {
        public float step=.5f;
        public int minX,minZ,width,height;
        public byte[] links;
        public Vector3[] sheetLocations;
        public Vector3 exitLocation;

        int[] visited,previous,queue;
        int stamp;
        static readonly int[] DX={0,1,0,-1};
        static readonly int[] DZ={1,0,-1,0};

        int Index(int x,int z)
        {
            int localX=x-minX,localZ=z-minZ;
            return localX<0 || localX>=width || localZ<0 || localZ>=height ? -1 : localZ*width+localX;
        }
        public Vector3 Position(int index)
        {
            return new Vector3((index%width+minX)*step,0,(index/width+minZ)*step);
        }
        public int NearestCell(Vector3 point)
        {
            if(links==null || links.Length!=width*height)return -1;
            int cx=Mathf.RoundToInt(point.x/step),cz=Mathf.RoundToInt(point.z/step),best=-1;
            float distance=float.PositiveInfinity;
            for(int radius=0;radius<=6;radius++)
            {
                for(int dz=-radius;dz<=radius;dz++)for(int dx=-radius;dx<=radius;dx++)
                {
                    if(Mathf.Max(Mathf.Abs(dx),Mathf.Abs(dz))!=radius)continue;
                    int id=Index(cx+dx,cz+dz);if(id<0 || links[id]==0)continue;
                    float d=(Position(id)-new Vector3(point.x,0,point.z)).sqrMagnitude;
                    if(d<distance){distance=d;best=id;}
                }
                if(best>=0)return best;
            }
            return -1;
        }
        public bool FindPath(Vector3 from,Vector3 to,List<Vector3> path)
        {
            return FindPath(from,to,path,null);
        }
        // The stored grid describes the school with its doors removed. At runtime
        // callers can reject connections obstructed by an open door leaf.
        public bool FindPath(Vector3 from,Vector3 to,List<Vector3> path,Func<Vector3,Vector3,bool> canTraverse)
        {
            path.Clear();int start=NearestCell(from),goal=NearestCell(to);
            if(start<0 || goal<0)return false;
            int size=links.Length;
            if(queue==null || queue.Length!=size){queue=new int[size];previous=new int[size];visited=new int[size];stamp=0;}
            if(stamp==int.MaxValue){Array.Clear(visited,0,visited.Length);stamp=0;}
            stamp++;int head=0,tail=0;queue[tail++]=start;visited[start]=stamp;previous[start]=-1;
            while(head<tail && visited[goal]!=stamp)
            {
                int current=queue[head++],x=current%width+minX,z=current/width+minZ;
                for(int d=0;d<4;d++)
                {
                    if((links[current]&(1<<d))==0)continue;
                    int next=Index(x+DX[d],z+DZ[d]);if(next<0 || visited[next]==stamp)continue;
                    if(canTraverse!=null && !canTraverse(Position(current),Position(next)))continue;
                    visited[next]=stamp;previous[next]=current;queue[tail++]=next;
                }
            }
            if(visited[goal]!=stamp)return false;
            for(int at=goal;at!=start && at>=0;at=previous[at])path.Add(Position(at));
            path.Reverse();return true;
        }
    }
}
