using System;
using System.Collections.Generic;
using DBEntities.Model;

namespace BLL
{
    public class Column
    {
        public List<Request> ServedRequests { get; set; } = new List<Request>();
        public List<int> Floors { get; set; } = new List<int>();
        public double Cost { get; set; } = 0;
        public double ReducedCost { get; set; } = 0;
        public Elevator AssignedElevator { get; set; } = null;
    }
}
