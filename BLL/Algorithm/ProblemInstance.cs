using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;
using Project;

namespace Project.Algorithm
{
    public class ProblemInstance
    {
        public int numElevators { get; private set; }
        public int numFloors { get; set; }
        public double stopTime { get; set; }
        private double loadTime { get; set; }
        private double drivePerFloorTime { get; set; }
        public double capacityPenalty { get; set; } = 1000;
        private List<Elevator> elevators { get; set; } = new List<Elevator>();
        private List<Request> unassignedRequests { get; set; } = new List<Request>();

        public ProblemInstance(int numElevators, int numFloors, double stopTime, double loadTime,
                          double drivePerFloorTime, double capacityPenalty = 1000)
        {
            this.numElevators = numElevators;
            this.numFloors = numFloors;
            this.stopTime = stopTime;
            this.loadTime = loadTime;
            this.drivePerFloorTime = drivePerFloorTime;
            this.capacityPenalty = capacityPenalty;
        }

        public void AddElevator(Elevator elevator)
        {
            elevators.Add(elevator);
        }

        public void AddRequest(Request request)
        {
            unassignedRequests.Add(request);
        }

        public List<Request> GetUnassignedRequests()
        {
            return new List<Request>(unassignedRequests);
        }

        public double GetstopTime()
        {
            return stopTime;
        }

        public List<Elevator> GetElevators()
        {
            return new List<Elevator>(elevators);
        }

        public void RemoveRequest(Request request)
        {
            unassignedRequests.Remove(request);
        }

        public void RemoveRequestAt(int index)
        {
            if (index >= 0 && index < unassignedRequests.Count)
            {
                unassignedRequests.RemoveAt(index);
            }
        }

        public Request GetRequestAt(int index)
        {
            if (index >= 0 && index < unassignedRequests.Count)
            {
                return unassignedRequests[index];
            }
            throw new IndexOutOfRangeException($"Index {index} is out of range for unassigned requests");
        }
    }
}