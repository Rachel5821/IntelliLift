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
        private readonly double[] requestDuals; // πρ for ρ ∈ Ru
        private readonly double elevatorDual;   // πe for elevator e
        private readonly int k; // max schedules to find with negative reduced cost
        private readonly Elevator elevator;
        private readonly List<Request> unassignedRequests; // Ru

        // התאמה למערכות IA: הפרדת assigned מ-unassigned
        private List<Request> assignedRequests = new List<Request>(); // R(e) - חייבות להיות בכל schedule
        private List<Request> forbiddenRequests = new List<Request>(); // אסורות עקב אילוצי branching

        public PricingProblem(ProblemInstance instance, int elevatorIndex, double[] requestDuals, double elevatorDual, int k)
        {
            this.instance = instance;
            this.elevatorIndex = elevatorIndex;
            this.requestDuals = requestDuals;
            this.elevatorDual = elevatorDual;
            this.k = k;
            elevator = instance.GetElevators()[elevatorIndex];
            unassignedRequests = instance.GetUnassignedRequests();
        }

        /// <summary>
        /// קביעת בקשות שחייבות להיות משורתות (ההחלטה כבר התקבלה)
        /// במערכת IA: אלו בקשות שכבר נאמר לנוסעים שהמעלית הזו תגיע אליהם
        /// </summary>
        public void SetAssignedRequests(List<Request> requests)
        {
            assignedRequests.Clear();
            if (requests != null)
            {
                assignedRequests.AddRange(requests);
            }
        }

        /// <summary>
        /// קביעת בקשות אסורות (בגלל אילוצי branching במאמר)
        /// </summary>
        public void SetForbiddenRequests(List<Request> requests)
        {
            forbiddenRequests.Clear();
            if (requests != null)
            {
                forbiddenRequests.AddRange(requests);
            }
        }

        /// <summary>
        /// Branch & Bound pricing algorithm as described in Section 3.1
        /// Returns k schedules with negative reduced cost
        /// משתמש ב-PricingNode הקיימת במקום BranchBoundNode
        /// </summary>
        public List<Schedule> GenerateSchedulesWithNegativeReducedCost()
        {
            // Initialize according to the paper
            List<Schedule> M = new List<Schedule>(); // Result set M
            PriorityQueue<PricingNode, double> Q = new PriorityQueue<PricingNode, double>(); // Queue Q for best-first traversal
            double θ = -1.0e6; // Threshold θ (initially −1.0 × 10^6)

            // Start by computing lower bounds on the reduced cost for each root node
            List<PricingNode> rootNodes = CreateRootNodes();

            foreach (var rootNode in rootNodes)
            {
                double lowerBound = CalculateLowerBound(rootNode);
                if (lowerBound < θ)
                {
                    Q.Enqueue(rootNode, lowerBound);
                }
            }

            // Branch & Bound main loop - traverse the search forest in a best-first manner
            while (Q.Count > 0)
            {
                // Select the node v from Q with the smallest lower bound
                PricingNode v = Q.Dequeue();

                // If v is feasible and has reduced cost less than threshold θ
                if (v.IsLast())
                {
                    Schedule schedule = v.GetSchedule();
                    double reducedCost = CalculateReducedCost(schedule);

                    if (reducedCost < θ)
                    {
                        // The corresponding schedule is collected in the result set M
                        M.Add(schedule);

                        // If M has now k schedules, the search is stopped and M returned
                        if (M.Count >= k)
                        {
                            return M;
                        }

                        // θ is set to the minimum reduced cost of a schedule in M
                        θ = M.Min(s => CalculateReducedCost(s));
                    }
                    continue;
                }

                // Branch on v and collect all valid child nodes in the set N
                List<PricingNode> N = v.Branch();

                foreach (var u in N)
                {
                    double uLowerBound = CalculateLowerBound(u);
                    // Each u ∈ N is added to Q provided that its lower bound is less than θ
                    if (uLowerBound < θ)
                    {
                        Q.Enqueue(u, uLowerBound);
                    }
                }
            }

            return M;
        }

        /// <summary>
        /// Create root nodes as described in the paper:
        /// "There is a separate root node r for every floor f where the elevator can still stop at next"
        /// כולל את ה-assigned requests כחובה
        /// </summary>
        private List<PricingNode> CreateRootNodes()
        {
            List<PricingNode> rootNodes = new List<PricingNode>();

            // Fi(e) - set of floors that are admissible for the first stop in a schedule
            HashSet<int> Fi = GetAdmissibleFloorsForFirstStop();

            foreach (int f in Fi)
            {
                // Sr is the schedule corresponding to dropping all loaded calls of elevator e with first stop at floor f
                // + serving all assigned requests
                Schedule Sr = CreateScheduleWithFirstStopAtFloorAndAssignedRequests(f);

                // Create PricingNode with assigned requests already served
                PricingNode rootNode = new PricingNode(
                    currentFloor: Sr.Stops.LastOrDefault()?.Floor ?? f,
                    currentTime: Sr.Stops.LastOrDefault()?.ArrivalTime ?? (float)elevator.CurrentTime,
                    servedRequests: new HashSet<Request>(assignedRequests), // כבר כולל assigned requests
                    unServedRequests: GetAllowedUnassignedRequests(), // רק unassigned requests שמותרות
                    currentSchedule: Sr,
                    currentLoad: CalculateLoadAfterSchedule(Sr)
                );

                rootNodes.Add(rootNode);
            }

            return rootNodes;
        }

        /// <summary>
        /// Create schedule Sr as described: dropping all loaded calls with first stop at floor f
        /// PLUS serving all assigned requests (במערכת IA - התחייבות!)
        /// </summary>
        private Schedule CreateScheduleWithFirstStopAtFloorAndAssignedRequests(int firstFloor)
        {
            Schedule schedule = new Schedule(elevatorIndex);
            float currentTime = (float)elevator.CurrentTime;
            int currentFloor = elevator.CurrentFloor;

            // Travel to first floor if needed
            if (currentFloor != firstFloor)
            {
                double travelTime = CalculateTravelTime(currentFloor, firstFloor);
                currentTime += (float)travelTime;
            }

            // First stop at floor f
            Stop firstStop = new Stop
            {
                Floor = firstFloor,
                ArrivalTime = currentTime,
                Direction = GetFeasibleDirectionForFirstStop()
            };
            schedule.AddStop(firstStop);

            // Add stops for dropping all loaded calls
            currentFloor = firstFloor;
            foreach (var loadedCall in elevator.LoadedCalls)
            {
                double travelTime = CalculateTravelTime(currentFloor, loadedCall.DestinationFloor);
                currentTime += (float)travelTime;

                Stop dropStop = new Stop
                {
                    Floor = loadedCall.DestinationFloor,
                    ArrivalTime = currentTime,
                    Direction = Direction.Idle
                };
                dropStop.AddDrop(loadedCall);
                schedule.AddStop(dropStop);

                currentTime += (float)instance.stopTime;
                currentFloor = loadedCall.DestinationFloor;
            }

            // ***הנקודה המרכזית***: הוספת כל ה-assigned requests כחובה
            // במערכת IA אלו בקשות שכבר נאמר לנוסעים שהמעלית הזו תגיע
            foreach (var assignedRequest in assignedRequests)
            {
                // נסיעה לקומת האיסוף
                double travelTime = CalculateTravelTime(currentFloor, assignedRequest.StartFloor);
                currentTime += (float)travelTime;

                // עצירת איסוף
                Stop pickupStop = new Stop
                {
                    Floor = assignedRequest.StartFloor,
                    ArrivalTime = currentTime,
                    Direction = DetermineDirection(assignedRequest.StartFloor, assignedRequest.DestinationFloor)
                };
                pickupStop.AddPickup(assignedRequest);
                schedule.AddStop(pickupStop);

                currentTime += (float)instance.stopTime;
                currentFloor = assignedRequest.StartFloor;

                // עצירת הורדה
                travelTime = CalculateTravelTime(currentFloor, assignedRequest.DestinationFloor);
                currentTime += (float)travelTime;

                Stop dropStop = new Stop
                {
                    Floor = assignedRequest.DestinationFloor,
                    ArrivalTime = currentTime,
                    Direction = Direction.Idle
                };

                foreach (var call in assignedRequest.Calls)
                {
                    dropStop.AddDrop(call);
                }
                schedule.AddStop(dropStop);

                currentTime += (float)instance.stopTime;
                currentFloor = assignedRequest.DestinationFloor;

                schedule.ServedRequests.Add(assignedRequest);
            }

            schedule.TotalCost = (float)CalculateTotalScheduleCost(schedule);
            return schedule;
        }

        /// <summary>
        /// Lower bound calculation as described in Section 3.2:
        /// "consists of two parts: a lower bound on the reduced cost of requests already picked up 
        /// and a lower bound on the additional reduced cost for serving still unserved requests"
        /// מותאם ל-PricingNode הקיימת
        /// </summary>
        private double CalculateLowerBound(PricingNode v)
        {
            // Part 1: "The reduced cost for the picked up requests are at least c̃(Sv)"
            double servedCost = v.CurrentSchedule.TotalCost;
            double servedDualSum = 0;

            foreach (var request in v.ServedRequests)
            {
                int requestIndex = unassignedRequests.IndexOf(request);
                if (requestIndex >= 0 && requestIndex < requestDuals.Length)
                {
                    servedDualSum += requestDuals[requestIndex];
                }
            }

            // Part 2: Lower bound for additional reduced cost
            double additionalCost = 0;

            foreach (var ρ in v.UnServedRequests)
            {
                // Calculate t+(ρ) - earliest pickup time as described in the paper
                double tPlus = CalculateEarliestPickupTime(v, ρ);

                double requestCost = 0;
                foreach (var c in ρ.Calls)
                {
                    // Calculate t-(c) - earliest drop time
                    double tMinus = CalculateEarliestDropTime(v, ρ, c, tPlus);

                    // Cost calculation as per the paper formula
                    double waitTime = Math.Max(0, tPlus - c.ReleaseTime.ToOADate());
                    double travelTime = tMinus - tPlus;

                    requestCost += c.WaitCost * waitTime + c.TravelCost * travelTime;
                }

                // Add capacity penalty cost
                requestCost += CalculateCapacityPenalty(v, ρ);

                // Dual fixing as described: "If πρ ≤ c̄(ρ) it will never be favorable to serve this request"
                int requestIndex = unassignedRequests.IndexOf(ρ);
                double πρ = requestIndex >= 0 && requestIndex < requestDuals.Length ? requestDuals[requestIndex] : 0;

                if (πρ > requestCost)
                {
                    // Optional request is worth including
                    additionalCost += requestCost - πρ;
                }
            }

            // Return total lower bound
            return servedCost - servedDualSum + additionalCost - elevatorDual;
        }

        /// <summary>
        /// Calculate t+(ρ) as described in the paper
        /// מותאם ל-PricingNode הקיימת
        /// </summary>
        private double CalculateEarliestPickupTime(PricingNode v, Request ρ)
        {
            int fPlus = ρ.StartFloor;

            // השתמש במידע מה-PricingNode
            int currentFloor = v.CurrentFloor;
            double currentTime = v.currentTime;

            // Check if direction of ρ is opposite to current direction
            Direction ρDirection = DetermineDirection(ρ.StartFloor, ρ.DestinationFloor);
            Direction currentDirection = v.CurrentSchedule.Stops.LastOrDefault()?.Direction ?? Direction.Idle;

            if (currentDirection != Direction.Idle && currentDirection != ρDirection)
            {
                // "the elevator has to visit all drop floors before it can pickup ρ"
                return CalculateTimeAfterAllDropFloors(v) + CalculateTravelTime(GetLastDropFloor(v), fPlus);
            }
            else
            {
                // Direct travel to pickup floor
                return currentTime + CalculateTravelTime(currentFloor, fPlus);
            }
        }

        /// <summary>
        /// Calculate t-(c) as described in the paper
        /// מותאם ל-PricingNode הקיימת
        /// </summary>
        private double CalculateEarliestDropTime(PricingNode v, Request ρ, Call c, double tPlus)
        {
            // Following the paper: "let f1, ..., fℓ be the sequence of ℓ floors to be visited before dropping c"
            List<int> floorsSequence = GetFloorsSequenceBeforeDropping(v, ρ, c);

            double time = tPlus + Math.Max(instance.stopTime, ρ.Calls.Count * instance.stopTime);

            // Add travel times between floors
            for (int i = 0; i < floorsSequence.Count - 1; i++)
            {
                time += CalculateTravelTime(floorsSequence[i], floorsSequence[i + 1]);
                if (i < floorsSequence.Count - 2) // Not the last stop
                {
                    time += instance.stopTime;
                }
            }

            return time;
        }

        /// <summary>
        /// Calculate capacity penalty as described in the paper
        /// מותאם ל-PricingNode הקיימת
        /// </summary>
        private double CalculateCapacityPenalty(PricingNode v, Request ρ)
        {
            int currentLoad = v.currentLoad;
            int requestLoad = ρ.Calls.Count;

            if (currentLoad + requestLoad > elevator.Capacity)
            {
                return instance.capacityPenalty * (currentLoad + requestLoad - elevator.Capacity);
            }

            return 0;
        }

        /// <summary>
        /// Calculate reduced cost as defined in the paper: c̃(S) := c(S) - ∑ρ∈Ru∩S πρ - πe
        /// שים לב: רק unassigned requests נכנסות לחישוב הdual sum!
        /// assigned requests לא נכנסות כי הן לא ב-Ru (הן כבר "לא אפשריות לשינוי")
        /// </summary>
        private double CalculateReducedCost(Schedule schedule)
        {
            double cost = schedule.TotalCost;
            double dualSum = 0;

            // ***נקודה קריטית***: רק unassigned requests נכנסות לחישוב!
            // assigned requests לא חלק מ-Ru במאמר
            foreach (var request in schedule.ServedRequests)
            {
                // רק אם זה לא assigned request
                if (!assignedRequests.Contains(request))
                {
                    int requestIndex = unassignedRequests.IndexOf(request);
                    if (requestIndex >= 0 && requestIndex < requestDuals.Length)
                    {
                        dualSum += requestDuals[requestIndex];
                    }
                }
            }

            return cost - dualSum - elevatorDual;
        }

        #region Helper Methods - Implementation Details

        /// <summary>
        /// מחזיר רק unassigned requests שמותרות (לא forbidden)
        /// </summary>
        private List<Request> GetAllowedUnassignedRequests()
        {
            return unassignedRequests
                .Where(r => !forbiddenRequests.Contains(r) && !assignedRequests.Contains(r))
                .ToList();
        }

        /// <summary>
        /// חישוב load אחרי ביצוע schedule
        /// </summary>
        private int CalculateLoadAfterSchedule(Schedule schedule)
        {
            int load = elevator.LoadedCalls.Count;

            foreach (var stop in schedule.Stops)
            {
                load += stop.Pickups.Sum(r => r.Calls.Count);
                load -= stop.Drops.Count;
            }

            return Math.Max(0, load);
        }

        private HashSet<int> GetAdmissibleFloorsForFirstStop()
        {
            HashSet<int> floors = new HashSet<int> { elevator.CurrentFloor };

            foreach (var call in elevator.LoadedCalls)
            {
                floors.Add(call.DestinationFloor);
            }

            return floors;
        }

        private Direction GetFeasibleDirectionForFirstStop()
        {
            return elevator.CurrentDirection;
        }

        private double CalculateTimeAfterAllDropFloors(PricingNode v)
        {
            // פשוט - במימוש מלא צריך לחשב דרך כל ה-drop floors
            var lastStop = v.CurrentSchedule.Stops.LastOrDefault();
            if (lastStop?.DropFloors.Count > 0)
            {
                return lastStop.ArrivalTime + lastStop.DropFloors.Count * instance.stopTime;
            }
            return v.currentTime;
        }

        private int GetLastDropFloor(PricingNode v)
        {
            var lastStop = v.CurrentSchedule.Stops.LastOrDefault();
            return lastStop?.DropFloors.LastOrDefault() ?? v.CurrentFloor;
        }

        private List<int> GetFloorsSequenceBeforeDropping(PricingNode v, Request ρ, Call c)
        {
            return new List<int> { ρ.StartFloor, c.DestinationFloor };
        }

        private double CalculateTravelTime(int fromFloor, int toFloor)
        {
            int distance = Math.Abs(toFloor - fromFloor);
            if (distance == 0) return 0;
            return Constant.ElevatorStartupTime + distance * Constant.DrivePerFloorTime;
        }

        private Direction DetermineDirection(int fromFloor, int toFloor)
        {
            if (fromFloor < toFloor) return Direction.Up;
            if (fromFloor > toFloor) return Direction.Down;
            return Direction.Idle;
        }

        private double CalculateTotalScheduleCost(Schedule schedule)
        {
            double totalCost = 0;

            foreach (var stop in schedule.Stops)
            {
                // Wait costs
                foreach (var request in stop.Pickups)
                {
                    foreach (var call in request.Calls)
                    {
                        double waitTime = Math.Max(0, stop.ArrivalTime - call.ReleaseTime.ToOADate());
                        totalCost += call.WaitCost * waitTime;
                    }
                }

                // Travel costs
                foreach (var call in stop.Drops)
                {
                    double pickupTime = FindPickupTimeForCall(schedule, call);
                    double travelTime = stop.ArrivalTime - pickupTime;
                    totalCost += call.TravelCost * travelTime;
                }

                // Capacity penalty
                int load = CalculateLoadAtStop(schedule, stop);
                if (load > elevator.Capacity)
                {
                    totalCost += instance.capacityPenalty * (load - elevator.Capacity);
                }
            }

            return totalCost;
        }

        private double FindPickupTimeForCall(Schedule schedule, Call call)
        {
            foreach (var stop in schedule.Stops)
            {
                foreach (var request in stop.Pickups)
                {
                    if (request.Calls.Contains(call))
                    {
                        return stop.ArrivalTime;
                    }
                }
            }
            return 0;
        }

        private int CalculateLoadAtStop(Schedule schedule, Stop targetStop)
        {
            int load = elevator.LoadedCalls.Count;

            foreach (var stop in schedule.Stops)
            {
                if (stop == targetStop) break;
                load += stop.Pickups.Sum(r => r.Calls.Count);
                load -= stop.Drops.Count;
            }

            return Math.Max(0, load);
        }

        /// <summary>
        /// פונקציית עזר לאבחון (ניתן להסיר בפרודקשן)
        /// </summary>
        public void DiagnoseSystem()
        {
            Console.WriteLine("=== אבחון Branch & Bound למערכת IA ===");
            Console.WriteLine($"מעלית: {elevatorIndex}");
            Console.WriteLine($"Assigned requests (חובה): {assignedRequests.Count}");
            Console.WriteLine($"Unassigned requests (B&B): {GetAllowedUnassignedRequests().Count}");
            Console.WriteLine($"Forbidden requests: {forbiddenRequests.Count}");
            Console.WriteLine($"Max schedules: {k}");
            Console.WriteLine($"Elevator dual (πe): {elevatorDual}");

            if (assignedRequests.Count > 0)
            {
                Console.WriteLine("Assigned requests (יופיעו בכל schedule):");
                foreach (var req in assignedRequests)
                {
                    Console.WriteLine($"  בקשה {req.Id}: {req.StartFloor} → {req.DestinationFloor}");
                }
            }

            if (requestDuals != null && requestDuals.Length > 0)
            {
                Console.WriteLine("Request duals (πρ) - רק לunassigned:");
                var allowedRequests = GetAllowedUnassignedRequests();
                for (int i = 0; i < Math.Min(requestDuals.Length, allowedRequests.Count); i++)
                {
                    Console.WriteLine($"  בקשה {allowedRequests[i].Id}: πρ = {requestDuals[i]}");
                }
            }
        }

        #endregion
    }
}