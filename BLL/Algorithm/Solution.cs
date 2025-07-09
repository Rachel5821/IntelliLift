using Project;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Project.Algorithm
{
    public class Solution
    {
        private double[] values;

        private ScheduleCollection schedules;
        public double ObjectiveValue { get; private set; }//ערך פונקציית המטרה
        private double[] requestDuals;
        private double[] elevatorDuals;
        public bool isIntegral { get; private set; }
        // שיפור הבנאי לכלול בדיקות תקינות ולבצע העתקה עמוקה של המערכים
        public Solution(double[] values, ScheduleCollection schedules, double objectiveValue, double[] requestDuals = null, double[] elevatorDuals = null)
        {
            if (values == null)
                throw new ArgumentNullException(nameof(values));
            if (schedules == null)
                throw new ArgumentNullException(nameof(schedules));

            this.values = (double[])values.Clone();
            this.schedules = schedules; // בהנחה שהרשימה לא תשתנה מחוץ למחלקה
            ObjectiveValue = objectiveValue;

            if (requestDuals != null)
                this.requestDuals = (double[])requestDuals.Clone();
            if (elevatorDuals != null)
                this.elevatorDuals = (double[])elevatorDuals.Clone();

            isIntegral = true;
            foreach (var value in this.values)
            {
                if (Math.Abs(value - Math.Round(value)) > 1e-6)
                {
                    isIntegral = false;
                    break;
                }
            }
        }

        public double[] GetRequestDuals()
        {
            return requestDuals != null ? (double[])requestDuals.Clone() : null;
        }

        public double[] GetElevatorsDuals()
        {
            return elevatorDuals != null ? (double[])elevatorDuals.Clone() : null;
        }

        public double[] GetValues()
        {
            return (double[])values.Clone();
        }

        public List<Schedule> GetSelectedSchedules()
        {
            List<Schedule> selected = new List<Schedule>();

            if (values == null || values.Length == 0)
                return selected;

            for (int i = 0; i < values.Length; i++)
            {
                if (Math.Abs(values[i] - 1.0) < 1e-6)
                {
                    selected.Add(schedules.GetSchedule(i));
                }
            }

            return selected;
        }

        public bool IsEmpty()
        {
            return values == null || values.Length == 0 || GetSelectedSchedules().Count == 0;
        }

    }
}