using System;

namespace DBEntities.Model
{
    public partial class Call
    {
        public DateTime ReleaseTime { get; set; }
        public int StartFloor { get; set; }
        public int DestinationFloor { get; set; }

        public Direction GetDirection()
        {
            if (DestinationFloor > StartFloor) return Direction.Up;
            if (DestinationFloor < StartFloor) return Direction.Down;
            return Direction.Idle;
        }
    }
}