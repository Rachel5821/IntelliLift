using System;

namespace Project
{
    public partial class Call
    {
        public DateTime ReleaseTime { get; set; }
        public int StartFloor { get; set; }
        public int DestinationFloor { get; set; }

        public double WaitCost { get; set; } = 1.0;
        public double TravelCost { get; set; } = 15.0; 

       
    }
}