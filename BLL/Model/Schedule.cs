using Project.Algorithm;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Project
{
    public partial class Schedule
    {
        public int Id { get; set; }
        public int ElevatorIndex { get; set; }
        // public Elevator Elevator { get; set; }
        public List<Stop> Stops { get; set; } = new List<Stop>();
        public float TotalCost { get; set; }
        public float CapacityPenaltyCost { get; set; }
        public List<Request> ServedRequests { get; set; } = new List<Request>();

        // Basic constructor
        public Schedule(int elevatorIndex)
        {
            ElevatorIndex = elevatorIndex;
            Stops = new List<Stop>();
            ServedRequests = new List<Request>();
        }


        // Copy constructor - creates a deep copy
        public Schedule(Schedule source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            // Copy simple properties
            Id = source.Id;
            ElevatorIndex = source.ElevatorIndex;
            TotalCost = source.TotalCost;
            CapacityPenaltyCost = source.CapacityPenaltyCost;

            // Deep copy of Stops
            Stops = source.Stops.Select(stop => new Stop
            {
                Floor = stop.Floor,
                ArrivalTime = stop.ArrivalTime,
                DropFloors = new HashSet<int>(stop.DropFloors)
            }).ToList();

            // Deep copy of ServedRequests (assuming Request is immutable or has its own copy mechanism)
            ServedRequests = new List<Request>(source.ServedRequests);
        }


        public void AddStop(Stop stop)
        {
            Stops.Add(stop);
        }

        public bool IsFeasible()
        {
            return true;
        }

        public float CalculateCost(float capacityPenalty)
        {
            return 1;
        }

        public bool ServedRequestsContains(int requestId)
        {
            foreach (var request in ServedRequests)
            {
                if (request.Id == requestId)
                {
                    return true;
                }
            }
            return false;
        }
        private Schedule ConvertScheduleFromColumn(Column column)
        {
            // המרת עמודה לסקדיול
            Schedule schedule = new Schedule(column.AssignedElevator.Id);
            schedule.ServedRequests = new List<Request>(column.ServedRequests);
            schedule.TotalCost = (float)column.Cost;

            // יצירת עצירות לפי רשימת הקומות
            int currentFloor = column.AssignedElevator.CurrentFloor;
            float currentTime = 0;

            for (int i = 0; i < column.Floors.Count; i++)
            {
                int floor = column.Floors[i];

                // חישוב זמן נסיעה
                if (i > 0)
                {
                    float travelTime = (float)Constant.CalculateTravelTime(currentFloor, floor);
                    currentTime += travelTime;
                }

                // יצירת עצירה
                Direction direction = Direction.Idle;
                if (i < column.Floors.Count - 1)
                {
                    int nextFloor = column.Floors[i + 1];
                    direction = nextFloor > floor ? Direction.Up : (nextFloor < floor ? Direction.Down : Direction.Idle);
                }

                Stop stop = new Stop
                {
                    Floor = floor,
                    ArrivalTime = currentTime,
                    Direction = direction
                };

                // הוספת מידע על איסוף והורדה
                foreach (var request in column.ServedRequests)
                {
                    if (floor == request.StartFloor)
                    {
                        stop.AddPickup(request);
                    }
                    else if (floor == request.DestinationFloor)
                    {
                        foreach (var call in request.Calls)
                        {
                            stop.AddDrop(call);
                        }
                    }
                }

                schedule.AddStop(stop);

                // עדכון קומה נוכחית וזמן
                currentFloor = floor;

                // אם יש עצירה (איסוף או הורדה), הוסף זמן עצירה
                if (stop.Pickups.Count > 0 || stop.Drops.Count > 0)
                {
                    currentTime += (float)Project.Constant.StopTime;
                }
            }

            return schedule;
        }
    }
}