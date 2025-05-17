using System;

namespace Project
{
    public partial class Call
    {
        public DateTime ReleaseTime { get; set; }
        public int StartFloor { get; set; }
        public int DestinationFloor { get; set; }

        public double WaitCost { get; set; } = 1.0;  // מקדם עלות להמתנה
        public double TravelCost { get; set; } = 1.0; //מקדם עלות לזמן נסיעה

        //public Direction GetDirection()
        //{
        //    if (DestinationFloor > StartFloor) return Direction.Up;
        //    if (DestinationFloor < StartFloor) return Direction.Down;
        //    return Direction.Idle;
        //}
        //public float calculate_waiting_cost(Call call,float PickupTime)
        //{
        //    TimeSpan waitingTime = DateTime.FromOADate(PickupTime)-ReleaseTime;
        //    return (float)(WaitCost * waitingTime.TotalSeconds);
        //}

        //public float CalculaeTravelTime(Call call,float ArrivalTime)
        //{
        //    TimeSpan TravelTime = DateTime.FromOADate(ArrivalTime)-call.ReleaseTime;
        //    return (float)(TravelCost * TravelTime.TotalSeconds);
        //}

    }
}