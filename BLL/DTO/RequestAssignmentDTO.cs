using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Project.DTO
{
    public class RequestAssignmentDTO
    {
        public int RequestId { get; set; }
        public int ElevatorId { get; set; }
        public int StartFloor { get; set; }
        public int DestinationFloor { get; set; }
    }
}