using System.Collections.Generic; 

namespace Project.DTO 
{
    public class ElevatorSolveRequestBody
    {
        public int? NumElevators { get; set; }
        public int? NumFloors { get; set; }

        public List<ElevatorStateInput>? Elevators { get; set; }

        public List<ElevatorRequestInput> Requests { get; set; } = new List<ElevatorRequestInput>();
    }
}