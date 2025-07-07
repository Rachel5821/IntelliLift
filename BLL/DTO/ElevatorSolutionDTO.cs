using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace Project.DTO
{
    public class ElevatorSolutionDTO
    {
        public List<RequestAssignmentDTO> Assignments { get; set; } = new List<RequestAssignmentDTO>();
        public List<ElevatorRouteDTO> Routes { get; set; } = new List<ElevatorRouteDTO>();
        public string Message { get; set; }
    }
}