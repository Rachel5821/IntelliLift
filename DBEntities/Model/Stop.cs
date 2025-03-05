using System;
using System.Collections.Generic;

namespace DBEntities.Model
{
    public partial class Stop
    {
        public int Floor { get; set; }
        public Direction Direction { get; set; }
        public float ArrivalTime { get; set; }
        public List<Request> Pickups { get; set; } = new List<Request>();
        public List<Call> Drops { get; set; } = new List<Call>();
        public List<Call> Current { get; set; } = new List<Call>();
        public HashSet<int> DropFloors { get; set; } = new HashSet<int>();

        public void AddPickup(Request request)
        {
            Pickups.Add(request);
            foreach (var call in request.Calls)
            {
                Current.Add(call);
                DropFloors.Add(call.DestinationFloor);
            }
        }

        public void AddDrop(Call call)
        {
            Drops.Add(call);
        }

        public float CalculateStopTime(float baseStopTime, float loadTime)
        {
            return 1;
        }
    }
}