using DBEntities.Model;
using System;
using System.Collections.Generic;
using System.Linq;


namespace ElevatorSimulation
{
    public partial class Schedule
    {
        public Elevator Elevator { get; set; }
        public List<Stop>Stops { get; set; } = new List<Stop>();
        public float TotalCost { get; set; }
        public float CapacityPenaltyCost { get; set; }
        public List<Request> ServedRequests { get; set; } = new List<Request>();

        public void AddStop(Stop stop)
        {
            Stops.Add(stop);
        }

        public bool IsFeasible()
        {
            return true;
        }

        public float CalculateCost(float capacityPenalty)
        {
            return 1;
        }
    }
}