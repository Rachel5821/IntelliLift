using System;
using System.Collections.Generic;
using System.Linq;
using Project;

namespace Project.Algorithm
{
    public class PricingProblem
    {
        private readonly ProblemInstance instance;
        private readonly int elevatorIndex;
        private readonly double[] requestDuals; 
        private readonly double elevatorDual; 
        private readonly int maxSchedules; 
        private readonly Elevator elevator;
        private readonly List<Request> unassignedRequests;

        private List<Request> assignedRequests = new List<Request>();
        private List<Request> forbiddenRequests = new List<Request>();

        public PricingProblem(ProblemInstance instance, int elevatorIndex, double[] requestDuals, double elevatorDual, int maxSchedules)
        {
            this.instance = instance;
            this.elevatorIndex = elevatorIndex;
            this.requestDuals = requestDuals;
            this.elevatorDual = elevatorDual;
            this.maxSchedules = maxSchedules;
            elevator = instance.GetElevators()[elevatorIndex];
            unassignedRequests = instance.GetUnassignedRequests();
        }

        // תוספת: פונקציות להגדרת בקשות משויכות ואסורות
        public void SetAssignedRequests(List<Request> requests)
        {
            assignedRequests.Clear();
            if (requests != null)
            {
                assignedRequests.AddRange(requests);
            }
        }

        public void SetForbiddenRequests(List<Request> requests)
        {
            forbiddenRequests.Clear();
            if (requests != null)
            {
                forbiddenRequests.AddRange(requests);
            }
        }
        private List<PricingNode> CreateRootNodes()
        {
            // סינון הבקשות המותרות לפי אילוצי branching
            List<Request> allowedRequests = unassignedRequests
                .Where(r => !forbiddenRequests.Contains(r))
                .ToList();

            // הוספת בקשות משויכות (תמיד צריך לכלול אותן)
            foreach (var req in assignedRequests)
            {
                if (!allowedRequests.Contains(req))
                {
                    allowedRequests.Add(req);
                }
            }

            List<PricingNode> rootNodes = new List<PricingNode>();
            List<int> possibleFloors = GetPossibleNextFloors(allowedRequests);

            foreach (int floor in possibleFloors)
            {
                Schedule schedule = new Schedule(elevatorIndex);
                Stop initialStop = new Stop
                {
                    Floor = floor,
                    ArrivalTime = (float)((float)elevator.CurrentTime + CalculateTravelTime(elevator.CurrentFloor, floor)),
                    Direction = DetermineInitialDirection(elevator.CurrentFloor, floor)
                };
                schedule.AddStop(initialStop);

                PricingNode rootNode = new PricingNode(
                    currentFloor: floor,
                    currentTime: elevator.CurrentTime + CalculateTravelTime(elevator.CurrentFloor, floor),
                    servedRequests: new HashSet<Request>(),
                    unServedRequests: new List<Request>(allowedRequests),
                    currentSchedule: schedule,
                    currentLoad: elevator.LoadedCalls.Count
                );
                rootNodes.Add(rootNode);
            }

            return rootNodes;
        }

        // הוספת פונקציה לקביעת כיוון ראשוני
        private Direction DetermineInitialDirection(int currentFloor, int nextFloor)
        {
            if (currentFloor < nextFloor) return Direction.Up;
            if (currentFloor > nextFloor) return Direction.Down;
            return elevator.CurrentDirection;
        }
        public List<Schedule> GenerateSchedulesWithNegativeReducedCost()
        {
            List<Schedule> resultSchedules = new List<Schedule>();
            PriorityQueue<PricingNode, double> queue = new PriorityQueue<PricingNode, double>();
            double threshold = -1.0e-6; // סף לסינון עלות מופחתת

            // יצירת צמתי שורש
            List<PricingNode> rootNodes = CreateRootNodes();

            // הוספת צמתי השורש לתור העדיפות
            foreach (var rootNode in rootNodes)
            {
                double lowerBound = CalculateLowerBound(rootNode);
                if (lowerBound < threshold)
                {
                    queue.Enqueue(rootNode, lowerBound);
                }
            }

            // מקרה מיוחד: המעלית חייבת לשרת בקשות מסוימות
            if (queue.Count == 0 && assignedRequests.Count > 0)
            {
                // יצירת לוח זמנים שמשרת רק את הבקשות המשויכות
                Schedule mandatorySchedule = CreateScheduleForMandatoryRequests();
                if (mandatorySchedule != null)
                {
                    resultSchedules.Add(mandatorySchedule);
                    return resultSchedules;
                }
            }

            // Branch & Bound רגיל
            while (queue.Count > 0 && resultSchedules.Count < maxSchedules)
            {
                PricingNode node = queue.Dequeue();

                if (node.IsLast())
                {
                    Schedule schedule = node.GetSchedule();
                    double reducedCost = CalculateReducedCost(schedule);

                    if (reducedCost < threshold)
                    {
                        resultSchedules.Add(schedule);
                        threshold = Math.Max(threshold, resultSchedules.Min(s => CalculateReducedCost(s)));
                    }

                    continue;
                }

                List<PricingNode> children = node.Branch();
                foreach (var child in children)
                {
                    double lowerBound = CalculateLowerBound(child);
                    if (lowerBound < threshold)
                    {
                        queue.Enqueue(child, lowerBound);
                    }
                }
            }

            // אם לא נמצאו לוחות זמנים, החזר לוח ריק
            if (resultSchedules.Count == 0)
            {
                Schedule emptySchedule = new Schedule(elevatorIndex);
                Stop initialStop = new Stop
                {
                    Floor = elevator.CurrentFloor,
                    ArrivalTime = 0,
                    Direction = elevator.CurrentDirection
                };
                emptySchedule.AddStop(initialStop);
                resultSchedules.Add(emptySchedule);
            }

            return resultSchedules;
        }

        // פונקציה ליצירת לוח זמנים עבור בקשות שחייבים לשרת
        private Schedule CreateScheduleForMandatoryRequests()
        {
            if (assignedRequests.Count == 0)
                return null;

            Schedule schedule = new Schedule(elevatorIndex);
            Stop initialStop = new Stop
            {
                Floor = elevator.CurrentFloor,
                ArrivalTime = 0,
                Direction = elevator.CurrentDirection
            };
            schedule.AddStop(initialStop);

            // שירות כל בקשה בנפרד - פשוט אבל לא אופטימלי
            foreach (var request in assignedRequests)
            {
                // עצירת איסוף
                float pickupTime = initialStop.ArrivalTime + (float)CalculateTravelTime(elevator.CurrentFloor, request.StartFloor);
                Stop pickupStop = new Stop
                {
                    Floor = request.StartFloor,
                    ArrivalTime = pickupTime,
                    Direction = request.StartFloor < request.DestinationFloor ? Direction.Up : Direction.Down
                };
                pickupStop.AddPickup(request);
                schedule.AddStop(pickupStop);

                // עצירת הורדה
                float dropTime = pickupTime + (float)CalculateTravelTime(request.StartFloor, request.DestinationFloor) + (float)instance.GetstopTime();
                Stop dropStop = new Stop
                {
                    Floor = request.DestinationFloor,
                    ArrivalTime = dropTime,
                    Direction = Direction.Idle
                };
                foreach (var call in request.Calls)
                {
                    dropStop.AddDrop(call);
                }
                schedule.AddStop(dropStop);

                schedule.ServedRequests.Add(request);
            }

            // חישוב עלות ועלות מופחתת
            double totalCost = 0; // צריך לחשב את העלות האמיתית
            foreach (var stop in schedule.Stops)
            {
                foreach (var pickup in stop.Pickups)
                {
                    foreach (var call in pickup.Calls)
                    {
                        double waitTime = stop.ArrivalTime - (float)call.ReleaseTime.ToOADate();
                        totalCost += call.WaitCost * Math.Max(0, waitTime);
                    }
                }
            }
            schedule.TotalCost = (float)totalCost;

            return schedule;
        }



        private bool IsFeasible(PricingNode node)
        {
            // צומת היא סופית אם:
            // 1. אין בקשות משויכות שלא נאספו
            // 2. אין הורדות ממתינות
            // 3. כל הבקשות המשויכות נכללות

            bool allAssignedIncluded = true;
            foreach (var request in assignedRequests)
            {
                if (!node.ServedRequests.Contains(request))
                {
                    allAssignedIncluded = false;
                    break;
                }
            }

            return allAssignedIncluded &&
                   node.ServedRequests.Count == unassignedRequests.Count &&
                   node.currentLoad == 0;
        }

        private double CalculateReducedCost(Schedule schedule)
        {
            double cost = schedule.TotalCost;
            double dualSum = 0;

            // חישוב ∑r∈Ru\S pr
            foreach (var request in schedule.ServedRequests)
            {
                int requestIndex = unassignedRequests.IndexOf(request);
                if (requestIndex >= 0 && requestIndex < requestDuals.Length)
                {
                    dualSum += requestDuals[requestIndex];
                }
            }

            // c̃(S) = c(S) - ∑r∈Ru\S pr - pe
            return cost - dualSum - elevatorDual;
        }


        private Direction DetermineDirection(int fromFloor, int toFloor)
        {
            if (fromFloor < toFloor) return Direction.Up;
            if (fromFloor > toFloor) return Direction.Down;
            return Direction.Idle;
        }

        private double CalculateEarliestPickupTime(PricingNode node, Request request)
        {
            int startFloor = request.StartFloor;
            int currentFloor = node.CurrentFloor;
            double currentTime = node.currentTime;

            // חישוב זמן נסיעה ישיר
            double travelTime = CalculateTravelTime(currentFloor, startFloor);
            double directPickupTime = currentTime + travelTime;

            // במערכת IA, יש לשמור על כיוון הנסיעה הנוכחי
            Direction currentDirection = node.CurrentSchedule.Stops.Last().Direction;
            Direction requestDirection = startFloor < request.DestinationFloor ? Direction.Up : Direction.Down;

            // אם יש נוסעים במעלית והכיוון הפוך, צריך קודם להוריד אותם
            if (node.currentLoad > 0 && currentDirection != Direction.Idle && currentDirection != requestDirection)
            {
                // חישוב זמן להוריד את כל הנוסעים הנוכחיים
                double dropTime = CalculateTimeToDropCurrentPassengers(node);
                return Math.Max(directPickupTime, dropTime);
            }

            return directPickupTime;
        }

        private double CalculateTimeToDropCurrentPassengers(PricingNode node)
        {
            // בפשטות, נניח שכל הנוסעים הנוכחיים מורדים בקומה הבאה
            double currentTime = node.currentTime;
            double travelTime = CalculateTravelTime(node.CurrentFloor, node.CurrentFloor + (int)node.CurrentSchedule.Stops.Last().Direction);
            return currentTime + travelTime + instance.stopTime;
        }

        private double CalculateEarliestDropTime(PricingNode node, Request request, double pickupTime)
        {
            int startFloor = request.StartFloor;
            int destFloor = request.DestinationFloor;

            // זמן הנסיעה מקומת האיסוף לקומת היעד
            double travelTime = CalculateTravelTime(startFloor, destFloor);

            // זמן עצירה לאיסוף + זמן נסיעה + זמן עצירה להורדה
            return pickupTime + instance.stopTime + travelTime + instance.stopTime;
        }

        // פונקציית עזר לבדיקה אם השגיאה היא בגלל חישוב לא נכון
        public void DiagnoseAndPrintLowerBounds()
        {
            Console.WriteLine("=== אבחון חסמים תחתונים ===");

            // סינון הבקשות המותרות
            List<Request> allowedRequests = unassignedRequests
                .Where(r => !forbiddenRequests.Contains(r))
                .ToList();

            // יצירת צמתי שורש
            List<PricingNode> rootNodes = CreateRootNodes();

            foreach (var node in rootNodes)
            {
                Console.WriteLine($"צומת שורש בקומה {node.CurrentFloor}:");

                // חישוב חסם תחתון
                double lowerBound = CalculateLowerBound(node);
                Console.WriteLine($"  חסם תחתון: {lowerBound}");

                // בדיקה ספציפית לכל בקשה
                foreach (var request in node.UnServedRequests)
                {
                    // הדפסת פרטי הבקשה
                    Console.WriteLine($"  בקשה {request.Id} (מ-{request.StartFloor} ל-{request.DestinationFloor}):");

                    // חישוב זמני איסוף והורדה
                    double pickupTime = CalculateEarliestPickupTime(node, request);
                    double dropTime = CalculateEarliestDropTime(node, request, pickupTime);

                    Console.WriteLine($"    זמן איסוף מוקדם ביותר: {pickupTime}");
                    Console.WriteLine($"    זמן הורדה מוקדם ביותר: {dropTime}");

                    // חישוב עלויות
                    double waitCost = 0;
                    double travelCost = 0;

                    foreach (var call in request.Calls)
                    {
                        double callWaitTime = Math.Max(0, pickupTime - call.ReleaseTime.ToOADate());
                        double callTravelTime = dropTime - pickupTime;

                        waitCost += call.WaitCost * callWaitTime;
                        travelCost += call.TravelCost * callTravelTime;

                        Console.WriteLine($"    קריאה: זמן המתנה = {callWaitTime}, זמן נסיעה = {callTravelTime}");
                        Console.WriteLine($"    עלויות: המתנה = {waitCost}, נסיעה = {travelCost}");
                    }

                    // חישוב עלות קיבולת
                    double capacityCost = CalculateCapacityPenalty(node, request);
                    Console.WriteLine($"    עלות קיבולת: {capacityCost}");

                    // חישוב עלות כוללת וערך דואלי
                    double totalCost = waitCost + travelCost + capacityCost;
                    int requestIndex = unassignedRequests.IndexOf(request);
                    double requestDual = requestIndex >= 0 && requestIndex < requestDuals.Length ?
                        requestDuals[requestIndex] : 0;

                    Console.WriteLine($"    עלות כוללת: {totalCost}, ערך דואלי: {requestDual}");
                    Console.WriteLine($"    עלות מופחתת: {totalCost - requestDual}");
                }
            }
        }

        private double CalculateTimeAfterOppositeDirectionDrops(PricingNode node, Direction requestDirection)
        {
            float currentTime = 0;

            // לקיחת הזמן מהעצירה האחרונה בלוח
            if (node.CurrentSchedule.Stops.Count > 0)
            {
                currentTime = node.CurrentSchedule.Stops.Last().ArrivalTime;
            }

            int currentFloor = node.CurrentFloor;

            // רשימת קומות עם הורדות בכיוון ההפוך
            List<int> oppositeDropFloors = new List<int>();

            foreach (var stop in node.CurrentSchedule.Stops)
            {
                foreach (int dropFloor in stop.DropFloors)
                {
                    Direction dropDirection = DetermineDirection(currentFloor, dropFloor);

                    if (dropDirection != requestDirection && dropDirection != Direction.Idle)
                    {
                        oppositeDropFloors.Add(dropFloor);
                    }
                }
            }

            // אם אין הורדות בכיוון ההפוך, מחזירים את הזמן הנוכחי
            if (oppositeDropFloors.Count == 0)
            {
                return currentTime;
            }

            // מיון הקומות לפי סדר העצירה בהתאם לכיוון ההפוך לבקשה
            oppositeDropFloors.Sort((a, b) =>
                requestDirection == Direction.Up ? b.CompareTo(a) : a.CompareTo(b));

            // חישוב הזמן לאחר ביצוע כל ההורדות
            double totalTime = currentTime;
            int lastFloor = currentFloor;

            foreach (int floor in oppositeDropFloors)
            {
                // זמן נסיעה לקומה
                totalTime += CalculateTravelTime(lastFloor, floor);

                // זמן עצירה
                totalTime += Constant.StopTime;

                lastFloor = floor;
            }

            return totalTime;
        }



        private double CalculateCapacityPenalty(PricingNode node, Request request)
        {
            int cabinCapacity = elevator.Capacity;
            int currentLoad = node.currentLoad;
            int requestCalls = request.Calls.Count;

            // במערכת IA, בקשה מתווספת לעומס הנוכחי
            if (currentLoad + requestCalls > cabinCapacity)
            {
                return instance.capacityPenalty * (currentLoad + requestCalls - cabinCapacity);
            }

            return 0;
        }
        private double CalculateLowerBound(PricingNode node)
        {
            // חלק 1: החסם התחתון לעלות המופחתת של הבקשות שכבר נאספו
            double servedCost = node.CurrentSchedule.TotalCost;
            double servedDualSum = 0;

            foreach (var request in node.ServedRequests)
            {
                int requestIndex = unassignedRequests.IndexOf(request);
                if (requestIndex >= 0)
                {
                    servedDualSum += requestDuals[requestIndex];
                }
            }

            // חלק 2: הערכת עלויות נוספות לבקשות שטרם שויכו
            double additionalCost = 0;

            foreach (var request in node.UnServedRequests)
            {
                // חישוב הזמן המוקדם ביותר לאיסוף ולהורדה
                double pickupTime = CalculateEarliestPickupTime(node, request);
                double dropTime = CalculateEarliestDropTime(node, request, pickupTime);

                // חישוב עלויות המתנה ונסיעה
                double requestCost = 0;
                foreach (var call in request.Calls)
                {
                    double waitTime = Math.Max(0, pickupTime - call.ReleaseTime.ToOADate());
                    double travelTime = dropTime - pickupTime;

                    requestCost += call.WaitCost * waitTime + call.TravelCost * travelTime;
                }

                // חישוב עלויות חריגה מקיבולת
                requestCost += CalculateCapacityPenalty(node, request);

                // אם העלות גדולה מהערך הדואלי, אין טעם להוסיף את הבקשה
                int requestIndex = unassignedRequests.IndexOf(request);
                double requestDual = requestIndex >= 0 ? requestDuals[requestIndex] : 0;

                if (requestDual <= requestCost)
                {
                    // אין טעם להוסיף את הבקשה כי תמיד תגדיל את העלות המופחתת
                    continue;
                }

                additionalCost += requestCost - requestDual;
            }

            // החסם התחתון הסופי
            return servedCost - servedDualSum + additionalCost - elevatorDual;
        }

        private double EstimateRequestCost(Request request, int currentFloor, double currentTime)
        {
            // הערכת עלות מינימלית לבקשה (מבוסס על t+(r) ו-t-(c) מסעיף 3.2)
            double travelToPickup = CalculateTravelTime(currentFloor, request.StartFloor);
            double pickupTime = currentTime + travelToPickup + Constant.StopTime;

            double cost = 0;
            foreach (var call in request.Calls)
            {
                double waitTime = Math.Max(0, pickupTime - call.ReleaseTime.ToOADate());
                double travelToDest = CalculateTravelTime(request.StartFloor, request.DestinationFloor);
                double dropTime = pickupTime + travelToDest + Constant.StopTime;

                cost += call.WaitCost * waitTime + call.TravelCost * travelToDest;
            }

            // עלות קיבולת
            if (elevator.LoadedCalls.Count + request.Calls.Count > elevator.Capacity)
            {
                cost += Constant.CapacityPenalty * (elevator.LoadedCalls.Count + request.Calls.Count - elevator.Capacity);
            }

            return cost;
        }

        private double CalculateTravelTime(int fromFloor, int toFloor)
        {
            int distance = Math.Abs(toFloor - fromFloor);
            if (distance == 0) return 0;
            return Constant.ElevatorStartupTime + distance * Constant.DrivePerFloorTime;
        }
        private double CalculateTotalCost(Schedule schedule)
        {
            double totalCost = 0;

            // עלויות המתנה ונסיעה
            foreach (var stop in schedule.Stops)
            {
                // חישוב עלויות המתנה
                foreach (var request in stop.Pickups)
                {
                    foreach (var call in request.Calls)
                    {
                        double waitTime = Math.Max(0, stop.ArrivalTime - (float)call.ReleaseTime.ToOADate());
                        totalCost += call.WaitCost * waitTime;
                    }
                }

                // חישוב עלויות נסיעה
                foreach (var call in stop.Drops)
                {
                    // מציאת זמן האיסוף של הקריאה
                    double pickupTime = 0;
                    foreach (var prevStop in schedule.Stops)
                    {
                        if (prevStop == stop) break;

                        foreach (var request in prevStop.Pickups)
                        {
                            if (request.Calls.Contains(call))
                            {
                                pickupTime = prevStop.ArrivalTime;
                                break;
                            }
                        }

                        if (pickupTime > 0) break;
                    }

                    double travelTime = stop.ArrivalTime - pickupTime;
                    totalCost += call.TravelCost * travelTime;
                }
            }

            // עלויות קיבולת
            for (int i = 0; i < schedule.Stops.Count; i++)
            {
                Stop stop = schedule.Stops[i];
                int load = stop.Current.Count;

                if (load > elevator.Capacity)
                {
                    totalCost += instance.capacityPenalty * (load - elevator.Capacity);
                }
            }

            return totalCost;
        }
        private List<int> GetPossibleNextFloors(List<Request> allowedRequests)
        {
            HashSet<int> floors = new HashSet<int>();

            // קומות התחלה של בקשות
            foreach (var request in allowedRequests)
            {
                floors.Add(request.StartFloor);
            }

            // קומות ייעד של בקשות (עבור הורדות)
            foreach (var request in allowedRequests)
            {
                floors.Add(request.DestinationFloor);
            }

            // קומות הורדה של קריאות שכבר בתוך המעלית
            foreach (var call in elevator.LoadedCalls)
            {
                floors.Add(call.DestinationFloor);
            }

            // מיון הקומות לפי מרחק מהקומה הנוכחית
            return floors.OrderBy(f => Math.Abs(f - elevator.CurrentFloor)).ToList();
        }


    }
}