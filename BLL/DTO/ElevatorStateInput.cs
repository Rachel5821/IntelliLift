using System.ComponentModel.DataAnnotations; 
using Project; 

namespace Project.DTO 
{
    public class ElevatorStateInput
    {
        public int Id { get; set; }

        public int Capacity { get; set; } = 8;
        public int CurrentFloor { get; set; } = 1;
        public Direction CurrentDirection { get; set; } = Direction.Idle; 
        public double CurrentTime { get; set; } = 0;
    }
}