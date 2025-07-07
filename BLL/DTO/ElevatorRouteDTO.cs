using System.Collections.Generic;

namespace Project.DTO
{
    public class ElevatorRouteDTO
    {
        public int ElevatorId { get; set; }
        public List<ElevatorStopDTO> Stops { get; set; } = new List<ElevatorStopDTO>();
    }
}