using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Project;

namespace Project
{
    public partial class Dispatch
    {
        public Dictionary<int, Schedule> Schedules { get; set; } = new Dictionary<int, Schedule>();
        public float TotalCost { get; set; }

        public void AddSchedule(int elevatorId, Schedule schedule)
        {
            Schedules[elevatorId] = schedule;
        }

        public float CalculateTotalCost()
        {
            TotalCost = Schedules.Values.Sum(schedule => schedule.TotalCost);
            return TotalCost;
        }

        public bool IsValid(List<Request> unassignedRequests)
        {
            

            return true;
        }
    }
}
