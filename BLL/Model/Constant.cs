using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Project
{
    public static class Constant
    {
        public static double StopTime = 2.0;         
        public static double LoadTime = 1.0;           
        public static double DirectionChangeTime = 1.5;
        public static double ElevatorStartupTime = 1.0;
        public static double DrivePerFloorTime = 1.5; 
        public static double CapacityPenalty = 100.0; 

        public static double CalculateTravelTime(int fromFloor, int toFloor)
        {
            int distance = Math.Abs(toFloor - fromFloor);
            if (distance == 0) return 0;
            return ElevatorStartupTime + distance * DrivePerFloorTime;
        }
    }
}
