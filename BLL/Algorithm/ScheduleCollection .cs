using Project;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Project.Algorithm
{
    public class ScheduleCollection
    {
        private List<Schedule> schedules;

        public ScheduleCollection()
        {
            schedules = new List<Schedule>();
        }

        public ScheduleCollection(ScheduleCollection other)
        {
            schedules = new List<Schedule>();
            foreach (var schedule in other.schedules)
            {
                schedules.Add(new Schedule(schedule));
            }
        }

        public void Add(Schedule schedule)
        {
            schedules.Add(schedule);
        }

        public Schedule GetSchedule(int index)
        {
            if (index < 0 || index >= schedules.Count)
                throw new IndexOutOfRangeException("Index is out of range");
            return schedules[index];
        }

        public List<Schedule> GetAllSchedules()
        {
            return new List<Schedule>(schedules);
        }

        public int Count => schedules.Count;

        public bool IsEmpty()
        {
            return schedules.Count == 0;
        }
        public int RemoveAll(Predicate<Schedule> match)
        {
            return schedules.RemoveAll(match);
        }
    }
}
