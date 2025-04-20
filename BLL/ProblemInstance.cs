using DBEntities.Model;
using ElevatorSimulation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;


namespace BLL
{
    public class ProblemInstance
    {
        private int numElevators; 
        private int numFloors;
        private double stopTime;
        private double loadTime;
        private double DrivePerFloorTime;         
        private double capacityPenalty;
        private List<Elevator> elevators = new List<Elevator>();
        private List<Request> unassignedRequests = new List<Request>();
        public ProblemInstance(int numElevators, double stopTime, double loadTime,
                          double DrivePerFloorTime, double capacityPenalty)
        {
            this.numElevators = numElevators;
            this.stopTime = stopTime;
            this.loadTime = loadTime;
            this.DrivePerFloorTime = DrivePerFloorTime;
            this.capacityPenalty = capacityPenalty;
        }
        //private bool isImmediateAssignment;   
        public int getNumElevators()
        {
            return numElevators;
        }

        public double getTStop()
        {
            return stopTime;
        }

        public double getTLoad()
        {
            return loadTime;
        }

        public double getTDrivePerFloor()
        {
            return DrivePerFloorTime;
        }

        public double getCapacityPenalty()
        {
            return capacityPenalty;
        }
          


    }
}
