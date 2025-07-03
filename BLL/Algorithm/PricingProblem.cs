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
        private readonly double[] requestDuals; // πρ 
        private readonly double elevatorDual;   // πe 
        private readonly int maxSchedules; 
        private readonly Elevator elevator;
        private readonly List<Request> unassignedRequests;

        // Lists for Branch & Price
        private List<Request> assignedRequests = new List<Request>();
        private List<Request> forbiddenRequests = new List<Request>();

        // Pricing of old schedules - המאמר סעיף 3.3
        private static List<Schedule> oldSchedules = new List<Schedule>();
        private bool useOldSchedules = true;

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

        /// <summary>
        /// שמירת לוחות זמנים מהריצה הקודמת - למימוש "pricing of old schedules"
        /// </summary>
        public static void SaveOldSchedules(List<Schedule> schedules)
        {
            oldSchedules.Clear();
            if (schedules != null)
            {
                oldSchedules.AddRange(schedules);
            }
        }

        /// <summary>
        /// הפונקציה הראשית - מימוש EXACTREPLAN מהמאמר
        /// </summary>
        public List<Schedule> GenerateSchedulesWithNegativeReducedCost()
        {
            List<Schedule> resultSchedules = new List<Schedule>();
            double threshold = -1.0e-6; // θ במאמר

            // STEP 1: Pricing of old schedules (סעיף 3.3 במאמר)
            // "If pricing of old schedules is enabled, we keep all schedules from the previous 
            // reoptimization run and check whether any of those has negative reduced cost"
            if (useOldSchedules && TryPricingOldSchedules(resultSchedules, threshold))
            {
                Console.WriteLine($"מצאנו {resultSchedules.Count} לוחות זמנים ישנים עם עלות מופחתת שלילית");
                if (resultSchedules.Count >= maxSchedules)
                {
                    return resultSchedules;
                }
            }

            // STEP 2: מקרה מיוחד - רק בקשה חדשה אחת (מאמר סעיף 3)
            // "We do not invoke the column generation/IP solver if there is only a single unassigned request"
            List<Request> allowedRequests = GetAllowedUnassignedRequests();
            if (allowedRequests.Count == 1)
            {
                Console.WriteLine("רק בקשה חדשה אחת - משתמש בהקצאה אופטימלית");
                Schedule bestSchedule = SolveSingleRequestCaseOptimal(allowedRequests[0]);
                if (bestSchedule != null)
                {
                    double reducedCost = CalculateReducedCost(bestSchedule);
                    if (reducedCost < threshold)
                    {
                        resultSchedules.Add(bestSchedule);
                        SaveScheduleForNextRun(bestSchedule);
                    }
                }
                return resultSchedules;
            }

            // STEP 3: Branch & Bound מלא עם Column Generation
            Console.WriteLine($"מריץ Branch & Bound מלא עם {allowedRequests.Count} בקשות חדשות");
            List<Schedule> branchAndBoundResults = RunBranchAndBound(threshold);

            foreach (var schedule in branchAndBoundResults)
            {
                resultSchedules.Add(schedule);
                SaveScheduleForNextRun(schedule);

                if (resultSchedules.Count >= maxSchedules)
                    break;
            }

            // STEP 4: אם לא מצאנו כלום, יצור מסלול בסיסי
            if (resultSchedules.Count == 0)
            {
                Schedule fallbackSchedule = CreateFallbackSchedule();
                if (fallbackSchedule != null)
                {
                    resultSchedules.Add(fallbackSchedule);
                    SaveScheduleForNextRun(fallbackSchedule);
                }
            }

            return resultSchedules;
        }

        /// <summary>
        /// STEP 1: Pricing of old schedules - מאמר סעיף 3.3
        /// </summary>
        private bool TryPricingOldSchedules(List<Schedule> resultSchedules, double threshold)
        {
            if (!useOldSchedules || oldSchedules.Count == 0)
                return false;

            bool foundSchedules = false;

            foreach (var oldSchedule in oldSchedules)
            {
                // בדוק אם הלוח הישן שייך למעלית הזו
                if (oldSchedule.ElevatorIndex != elevatorIndex)
                    continue;

                // חשב עלות מופחתת עם הערכים הדואליים החדשים
                double reducedCost = CalculateReducedCost(oldSchedule);

                if (reducedCost < threshold)
                {
                    // הלוח הישן עדיין טוב!
                    resultSchedules.Add(oldSchedule);
                    foundSchedules = true;

                    if (resultSchedules.Count >= maxSchedules)
                        break;
                }
            }

            return foundSchedules;
        }

        /// <summary>
        /// STEP 2: פתרון אופטימלי למקרה של בקשה יחידה - כמו במאמר
        /// "compute an optimal schedule serving this request for each elevator and then choosing the elevator/schedule with least additional cost"
        /// </summary>
        private Schedule SolveSingleRequestCaseOptimal(Request singleRequest)
        {
            Schedule baseSchedule = CreateBaseScheduleWithAssignedRequests();

            // נסה להכניס את הבקשה בכל מקום אפשרי במסלול
            List<Schedule> candidates = new List<Schedule>();

            // אופציה 1: הוסף בהתחלה (לפני כל העצירות הקיימות)
            Schedule startOption = InsertRequestAtStart(baseSchedule, singleRequest);
            if (startOption != null)
                candidates.Add(startOption);

            // אופציה 2-N: הוסף אחרי כל עצירה קיימת
            for (int i = 0; i <= baseSchedule.Stops.Count; i++)
            {
                Schedule insertOption = InsertRequestAfterStop(baseSchedule, singleRequest, i);
                if (insertOption != null)
                    candidates.Add(insertOption);
            }

            // בחר את השיטה עם העלות הנמוכה ביותר
            Schedule bestSchedule = null;
            double minCost = double.MaxValue;

            foreach (var candidate in candidates)
            {
                double cost = CalculateTotalCost(candidate);
                if (cost < minCost)
                {
                    minCost = cost;
                    bestSchedule = candidate;
                }
            }

            return bestSchedule;
        }

        /// <summary>
        /// מכניס בקשה בתחילת המסלול
        /// </summary>
        private Schedule InsertRequestAtStart(Schedule baseSchedule, Request request)
        {
            Schedule newSchedule = new Schedule(elevatorIndex);

            float currentTime = (float)elevator.CurrentTime;
            int currentFloor = elevator.CurrentFloor;

            // עצירת איסוף
            float travelToPickup = (float)CalculateTravelTime(currentFloor, request.StartFloor);
            currentTime += travelToPickup;

            Stop pickupStop = new Stop
            {
                Floor = request.StartFloor,
                ArrivalTime = currentTime,
                Direction = request.StartFloor < request.DestinationFloor ? Direction.Up : Direction.Down
            };
            pickupStop.AddPickup(request);
            newSchedule.AddStop(pickupStop);

            currentTime += (float)Constant.StopTime;
            currentFloor = request.StartFloor;

            // עצירת הורדה
            float travelToDrop = (float)CalculateTravelTime(request.StartFloor, request.DestinationFloor);
            currentTime += travelToDrop;

            Stop dropStop = new Stop
            {
                Floor = request.DestinationFloor,
                ArrivalTime = currentTime,
                Direction = Direction.Idle
            };

            foreach (var call in request.Calls)
            {
                dropStop.AddDrop(call);
            }
            newSchedule.AddStop(dropStop);

            currentTime += (float)Constant.StopTime;
            currentFloor = request.DestinationFloor;

            // עכשיו הוסף את כל העצירות המקוריות (מעודכנות בזמן)
            foreach (var originalStop in baseSchedule.Stops)
            {
                float additionalTravel = (float)CalculateTravelTime(currentFloor, originalStop.Floor);
                currentTime += additionalTravel;

                Stop newStop = new Stop
                {
                    Floor = originalStop.Floor,
                    ArrivalTime = currentTime,
                    Direction = originalStop.Direction
                };

                // העתק איסופים והורדות
                newStop.Pickups.AddRange(originalStop.Pickups);
                newStop.Drops.AddRange(originalStop.Drops);
                newStop.Current.AddRange(originalStop.Current);

                newSchedule.AddStop(newStop);

                currentTime += (float)Constant.StopTime;
                currentFloor = originalStop.Floor;
            }

            newSchedule.ServedRequests.AddRange(baseSchedule.ServedRequests);
            newSchedule.ServedRequests.Add(request);
            newSchedule.TotalCost = CalculateTotalCost(newSchedule);

            return newSchedule;
        }

        /// <summary>
        /// מכניס בקשה אחרי עצירה ספציפית
        /// </summary>
        private Schedule InsertRequestAfterStop(Schedule baseSchedule, Request request, int afterStopIndex)
        {
            Schedule newSchedule = new Schedule(elevatorIndex);

            float currentTime = (float)elevator.CurrentTime;
            int currentFloor = elevator.CurrentFloor;

            // העתק עצירות עד נקודת ההכנסה
            for (int i = 0; i < afterStopIndex && i < baseSchedule.Stops.Count; i++)
            {
                var originalStop = baseSchedule.Stops[i];
                float travelTime = (float)CalculateTravelTime(currentFloor, originalStop.Floor);
                currentTime += travelTime;

                Stop newStop = new Stop
                {
                    Floor = originalStop.Floor,
                    ArrivalTime = currentTime,
                    Direction = originalStop.Direction
                };

                newStop.Pickups.AddRange(originalStop.Pickups);
                newStop.Drops.AddRange(originalStop.Drops);
                newStop.Current.AddRange(originalStop.Current);

                newSchedule.AddStop(newStop);

                currentTime += (float)Constant.StopTime;
                currentFloor = originalStop.Floor;
            }

            // הכנס את הבקשה החדשה
            // עצירת איסוף
            float travelToPickup = (float)CalculateTravelTime(currentFloor, request.StartFloor);
            currentTime += travelToPickup;

            Stop pickupStop = new Stop
            {
                Floor = request.StartFloor,
                ArrivalTime = currentTime,
                Direction = request.StartFloor < request.DestinationFloor ? Direction.Up : Direction.Down
            };
            pickupStop.AddPickup(request);
            newSchedule.AddStop(pickupStop);

            currentTime += (float)Constant.StopTime;
            currentFloor = request.StartFloor;

            // עצירת הורדה
            float travelToDrop = (float)CalculateTravelTime(request.StartFloor, request.DestinationFloor);
            currentTime += travelToDrop;

            Stop dropStop = new Stop
            {
                Floor = request.DestinationFloor,
                ArrivalTime = currentTime,
                Direction = Direction.Idle
            };

            foreach (var call in request.Calls)
            {
                dropStop.AddDrop(call);
            }
            newSchedule.AddStop(dropStop);

            currentTime += (float)Constant.StopTime;
            currentFloor = request.DestinationFloor;

            // העתק את שאר העצירות
            for (int i = afterStopIndex; i < baseSchedule.Stops.Count; i++)
            {
                var originalStop = baseSchedule.Stops[i];
                float travelTime = (float)CalculateTravelTime(currentFloor, originalStop.Floor);
                currentTime += travelTime;

                Stop newStop = new Stop
                {
                    Floor = originalStop.Floor,
                    ArrivalTime = currentTime,
                    Direction = originalStop.Direction
                };

                newStop.Pickups.AddRange(originalStop.Pickups);
                newStop.Drops.AddRange(originalStop.Drops);
                newStop.Current.AddRange(originalStop.Current);

                newSchedule.AddStop(newStop);

                currentTime += (float)Constant.StopTime;
                currentFloor = originalStop.Floor;
            }

            newSchedule.ServedRequests.AddRange(baseSchedule.ServedRequests);
            newSchedule.ServedRequests.Add(request);
            newSchedule.TotalCost = CalculateTotalCost(newSchedule);

            return newSchedule;
        }

        /// <summary>
        /// STEP 3: Branch & Bound מלא - מאמר סעיף 3.1
        /// </summary>
        private List<Schedule> RunBranchAndBound(double threshold)
        {
            List<Schedule> M = new List<Schedule>(); // תוצאות
            PriorityQueue<PricingNode, double> Q = new PriorityQueue<PricingNode, double>(); // תור עדיפויות
            double θ = threshold; // Threshold for pruning

            // יצירת צמתי שורש - מאמר: "There is a separate root node r for every floor f where the elevator can still stop at next"
            List<PricingNode> rootNodes = CreateRootNodes();

            foreach (var rootNode in rootNodes)
            {
                double lowerBound = CalculateLowerBound(rootNode);
                if (lowerBound < θ)
                {
                    Q.Enqueue(rootNode, lowerBound);
                }
            }

            // Main Branch & Bound loop
            int iterations = 0;
            int maxIterations = 10000; // הגבלת איטרציות למניעת לולאה אינסופית

            while (Q.Count > 0 && M.Count < maxSchedules && iterations < maxIterations)
            {
                iterations++;

                PricingNode v = Q.Dequeue();

                // בדיקה אם הצומת הוא אפשרי (feasible)
                if (v.IsLast())
                {
                    Schedule schedule = v.GetSchedule();
                    double reducedCost = CalculateReducedCost(schedule);

                    if (reducedCost < θ)
                    {
                        M.Add(schedule);

                        // עדכון threshold - מאמר: "θ is set to the minimum reduced cost of a schedule in M"
                        if (M.Count > 0)
                        {
                            θ = M.Min(s => CalculateReducedCost(s));
                        }

                        // Early stopping אם מצאנו מספיק לוחות זמנים
                        if (M.Count >= maxSchedules)
                        {
                            Console.WriteLine($"מצאנו {M.Count} לוחות זמנים אחרי {iterations} איטרציות");
                            break;
                        }
                    }
                    continue;
                }

                // Branching - יצירת צמתים בנים
                List<PricingNode> N = v.Branch();

                foreach (var u in N)
                {
                    double uLowerBound = CalculateLowerBound(u);

                    // Pruning - מאמר: "Each u ∈ N is added to Q provided that its lower bound is less than θ"
                    if (uLowerBound < θ)
                    {
                        Q.Enqueue(u, uLowerBound);
                    }
                }
            }

            Console.WriteLine($"Branch & Bound הסתיים: {M.Count} לוחות זמנים, {iterations} איטרציות");
            return M;
        }

        /// <summary>
        /// יצירת צמתי שורש - מאמר סעיף 3.1
        /// </summary>
        private List<PricingNode> CreateRootNodes()
        {
            List<PricingNode> rootNodes = new List<PricingNode>();

            // Fi(e) - set of floors that are admissible for the first stop in a schedule
            HashSet<int> Fi = GetAdmissibleFloorsForFirstStop();
            List<Request> allowedRequests = GetAllowedUnassignedRequests();

            foreach (int f in Fi)
            {
                // Sr - schedule corresponding to dropping all loaded calls with first stop at floor f
                Schedule Sr = CreateBaseScheduleWithFirstStopAt(f);

                // יצירת צומת שורש - תואם לקוד המקורי שלך
                PricingNode rootNode = new PricingNode(
                    currentFloor: f,
                    currentTime: Sr.Stops.LastOrDefault()?.ArrivalTime ?? 0,
                    currentLoad: CalculateLoadAfterSchedule(Sr),
                    servedAssignedRequests: new HashSet<Request>(assignedRequests), // כל הבקשות המשויכות כבר "נאספו"
                    unservedAssignedRequests: new List<Request>(), // אין יותר בקשות משויכות שטרם נאספו
                    servedOptionalRequests: new HashSet<Request>(), // טרם נאספו בקשות אופציונליות
                    unservedOptionalRequests: new List<Request>(allowedRequests), // כל הבקשות האופציונליות זמינות
                    currentSchedule: Sr,
                    elevatorCapacity: elevator.Capacity,
                    maxFloors: instance.numFloors
                );

                rootNodes.Add(rootNode);
            }

            Console.WriteLine($"יצרנו {rootNodes.Count} צמתי שורש");
            return rootNodes;
        }

        /// <summary>
        /// יצירת לוח זמנים בסיסי עם עצירה ראשונה בקומה f - מאמר סעיף 3.1
        /// </summary>
        private Schedule CreateBaseScheduleWithFirstStopAt(int f)
        {
            Schedule schedule = new Schedule(elevatorIndex);

            // חשב זמן נסיעה לקומה f
            float travelTime = (float)CalculateTravelTime(elevator.CurrentFloor, f);
            float currentTime = (float)elevator.CurrentTime + travelTime;
            int currentFloor = f;

            // הוסף עצירה ראשונה בקומה f
            Stop firstStop = new Stop
            {
                Floor = f,
                ArrivalTime = currentTime,
                Direction = DetermineInitialDirection(f)
            };
            schedule.AddStop(firstStop);
            currentTime += (float)Constant.StopTime;

            // הוסף עצירות להורדת נוסעים קיימים (Sr במאמר)
            var sortedLoadedCalls = SortLoadedCallsByDirection();
            foreach (var call in sortedLoadedCalls)
            {
                if (call.DestinationFloor == f)
                {
                    // כבר נוריד בעצירה הראשונה
                    firstStop.AddDrop(call);
                    continue;
                }

                float travelToDrop = (float)CalculateTravelTime(currentFloor, call.DestinationFloor);
                currentTime += travelToDrop;

                Stop dropStop = new Stop
                {
                    Floor = call.DestinationFloor,
                    ArrivalTime = currentTime,
                    Direction = DetermineDirection(currentFloor, call.DestinationFloor)
                };
                dropStop.AddDrop(call);
                schedule.AddStop(dropStop);

                currentFloor = call.DestinationFloor;
                currentTime += (float)Constant.StopTime;
            }

            // הוסף עצירות לבקשות משויכות
            foreach (var request in assignedRequests)
            {
                // עצירת איסוף
                float travelToPickup = (float)CalculateTravelTime(currentFloor, request.StartFloor);
                currentTime += travelToPickup;

                Stop pickupStop = new Stop
                {
                    Floor = request.StartFloor,
                    ArrivalTime = currentTime,
                    Direction = request.StartFloor < request.DestinationFloor ? Direction.Up : Direction.Down
                };
                pickupStop.AddPickup(request);
                schedule.AddStop(pickupStop);

                currentFloor = request.StartFloor;
                currentTime += (float)Constant.StopTime;

                // עצירת הורדה
                float travelToDrop = (float)CalculateTravelTime(request.StartFloor, request.DestinationFloor);
                currentTime += travelToDrop;

                Stop dropStop = new Stop
                {
                    Floor = request.DestinationFloor,
                    ArrivalTime = currentTime,
                    Direction = Direction.Idle
                };

                foreach (var call in request.Calls)
                {
                    dropStop.AddDrop(call);
                }
                schedule.AddStop(dropStop);

                currentFloor = request.DestinationFloor;
                currentTime += (float)Constant.StopTime;
            }

            // הוסף בקשות משויכות לרשימת הבקשות המשורתות
            foreach (var request in assignedRequests)
            {
                schedule.ServedRequests.Add(request);
            }

            schedule.TotalCost = CalculateTotalCost(schedule);
            return schedule;
        }

        /// <summary>
        /// חישוב חסם תחתון - מאמר סעיף 3.2
        /// "consists of two parts: a lower bound on the reduced cost of requests already picked up 
        /// and a lower bound on the additional reduced cost for serving still unserved requests"
        /// </summary>
        private double CalculateLowerBound(PricingNode v)
        {
            // Part 1: חסם תחתון על הבקשות שכבר נאספו
            // "The reduced cost for the picked up requests are at least c̃(Sv)"
            double servedCost = v.GetSchedule().TotalCost;
            double servedDualSum = 0;

            foreach (var request in v.ServedOptionalRequests)
            {
                if (!assignedRequests.Contains(request)) // רק בקשות אופציונליות
                {
                    int requestIndex = unassignedRequests.IndexOf(request);
                    if (requestIndex >= 0 && requestIndex < requestDuals.Length)
                    {
                        servedDualSum += requestDuals[requestIndex];
                    }
                }
            }

            // Part 2: חסם תחתון על עלות נוספת לבקשות שטרם שורתו
            double additionalCost = 0;

            // בקשות משויכות שטרם נאספו (חייבות להיות מטופלות)
            foreach (var request in v.UnservedAssignedRequests)
            {
                double requestCost = EstimateMinimalRequestCost(v, request);
                additionalCost += requestCost;
            }

            // בקשות אופציונליות שטרם נאספו - רק אם כדאי
            foreach (var request in v.UnservedOptionalRequests)
            {
                double requestCost = EstimateMinimalRequestCost(v, request);

                // Dual fixing - מאמר: "If πρ ≤ c̄(ρ) it will never be favorable to serve this request"
                int requestIndex = unassignedRequests.IndexOf(request);
                double πρ = (requestIndex >= 0 && requestIndex < requestDuals.Length) ?
                    requestDuals[requestIndex] : 0;

                if (πρ > requestCost)
                {
                    additionalCost += requestCost - πρ;
                }
            }

            return servedCost - servedDualSum + additionalCost - elevatorDual;
        }

        /// <summary>
        /// הערכת עלות מינימלית לבקשה - מאמר סעיף 3.2
        /// </summary>
        private double EstimateMinimalRequestCost(PricingNode v, Request ρ)
        {
            // חישוב t+(ρ) - זמן איסוף מוקדם ביותר
            double tPlus = CalculateEarliestPickupTime(v, ρ);

            double requestCost = 0;
            foreach (var c in ρ.Calls)
            {
                // חישוב t-(c) - זמן הורדה מוקדם ביותר
                double tMinus = CalculateEarliestDropTime(v, ρ, c, tPlus);

                // חישוב עלויות לפי הנוסחה במאמר
                double waitTime = Math.Max(0, tPlus - c.ReleaseTime.ToOADate());
                double travelTime = tMinus - tPlus;

                requestCost += c.WaitCost * waitTime + c.TravelCost * travelTime;
            }

            // הוסף עלות קיבולת
            requestCost += CalculateCapacityPenalty(v, ρ);

            return requestCost;
        }

        /// <summary>
        /// חישוב זמן איסוף מוקדם ביותר t+(ρ) - מאמר סעיף 3.2
        /// </summary>
        private double CalculateEarliestPickupTime(PricingNode v, Request ρ)
        {
            int fPlus = ρ.StartFloor;
            Direction ρDirection = ρ.StartFloor < ρ.DestinationFloor ? Direction.Up : Direction.Down;

            // בדוק אם כיוון הבקשה הפוך לכיוון הנוכחי
            Direction currentDirection = v.GetSchedule().Stops.LastOrDefault()?.Direction ?? Direction.Idle;

            if (currentDirection != Direction.Idle && currentDirection != ρDirection)
            {
                // "the elevator has to visit all drop floors before it can pickup ρ"
                return CalculateTimeAfterAllDropFloors(v) + CalculateTravelTime(GetLastDropFloor(v), fPlus);
            }
            else
            {
                // נסיעה ישירה לקומת האיסוף
                return v.CurrentTime + CalculateTravelTime(v.CurrentFloor, fPlus);
            }
        }

        /// <summary>
        /// חישוב זמן הורדה מוקדם ביותר t-(c) - מאמר סעיף 3.2
        /// </summary>
        private double CalculateEarliestDropTime(PricingNode v, Request ρ, Call c, double tPlus)
        {
            // לפי הנוסחה במאמר: t-(c) = t+(ρ) + max{τstop, |ρ|τload} + travel_time + stops
            double stopTime = Math.Max(Constant.StopTime, ρ.Calls.Count * Constant.LoadTime);
            double travelTime = CalculateTravelTime(ρ.StartFloor, c.DestinationFloor);

            return tPlus + stopTime + travelTime;
        }

        /// <summary>
        /// חישוב עלות קיבולת - מאמר סעיף 3.2
        /// </summary>
        private double CalculateCapacityPenalty(PricingNode v, Request ρ)
        {
            // במערכת IA - מאמר סעיף 3.2
            int currentLoad = v.CurrentLoad;
            int requestSize = ρ.Calls.Count;

            if (currentLoad + requestSize > elevator.Capacity)
            {
                return instance.capacityPenalty * (currentLoad + requestSize - elevator.Capacity);
            }

            return 0;
        }

        // פונקציות עזר
        private double CalculateTimeAfterAllDropFloors(PricingNode v)
        {
            // פשוט - במימוש מלא צריך לחשב דרך כל ה-drop floors
            return v.CurrentTime + v.CurrentLoad * Constant.StopTime;
        }

        private int GetLastDropFloor(PricingNode v)
        {
            return v.CurrentFloor; // פשוט למימוש
        }

        private void SaveScheduleForNextRun(Schedule schedule)
        {
            if (!oldSchedules.Contains(schedule))
            {
                oldSchedules.Add(schedule);
            }
        }

        private Schedule CreateFallbackSchedule()
        {
            return CreateBaseScheduleWithAssignedRequests();
        }

        private Schedule CreateBaseScheduleWithAssignedRequests()
        {
            Schedule schedule = new Schedule(elevatorIndex);

            if (assignedRequests.Count == 0)
                return schedule;

            float currentTime = 0;
            int currentFloor = elevator.CurrentFloor;

            foreach (var request in assignedRequests)
            {
                // עצירת איסוף
                float travelToPickup = (float)CalculateTravelTime(currentFloor, request.StartFloor);
                currentTime += travelToPickup;

                Stop pickupStop = new Stop
                {
                    Floor = request.StartFloor,
                    ArrivalTime = currentTime,
                    Direction = request.StartFloor < request.DestinationFloor ? Direction.Up : Direction.Down
                };
                pickupStop.AddPickup(request);
                schedule.AddStop(pickupStop);

                currentFloor = request.StartFloor;
                currentTime += (float)Constant.StopTime;

                // עצירת הורדה  
                float travelToDrop = (float)CalculateTravelTime(request.StartFloor, request.DestinationFloor);
                currentTime += travelToDrop;

                Stop dropStop = new Stop
                {
                    Floor = request.DestinationFloor,
                    ArrivalTime = currentTime,
                    Direction = Direction.Idle
                };

                foreach (var call in request.Calls)
                {
                    dropStop.AddDrop(call);
                }
                schedule.AddStop(dropStop);

                currentFloor = request.DestinationFloor;
                currentTime += (float)Constant.StopTime;
            }

            foreach (var request in assignedRequests)
            {
                schedule.ServedRequests.Add(request);
            }

            schedule.TotalCost = CalculateTotalCost(schedule);
            return schedule;
        }

        private float GetLastStopTime(Schedule schedule)
        {
            if (schedule.Stops.Count == 0)
                return (float)elevator.CurrentTime;
            return schedule.Stops.Last().ArrivalTime + (float)Constant.StopTime;
        }

        private int GetLastStopFloor(Schedule schedule)
        {
            if (schedule.Stops.Count == 0)
                return elevator.CurrentFloor;
            return schedule.Stops.Last().Floor;
        }

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

            // הוסף קומות הורדה של נוסעים קיימים
            foreach (var call in elevator.LoadedCalls)
            {
                floors.Add(call.DestinationFloor);
            }

            // הוסף קומות איסוף של בקשות משויכות
            foreach (var request in assignedRequests)
            {
                floors.Add(request.StartFloor);
            }

            return floors;
        }

        private Direction DetermineInitialDirection(int firstFloor)
        {
            if (elevator.CurrentFloor < firstFloor) return Direction.Up;
            if (elevator.CurrentFloor > firstFloor) return Direction.Down;
            return elevator.CurrentDirection;
        }

        private List<Call> SortLoadedCallsByDirection()
        {
            return elevator.LoadedCalls
                .OrderBy(call => Math.Abs(call.DestinationFloor - elevator.CurrentFloor))
                .ToList();
        }

        private Direction DetermineDirection(int fromFloor, int toFloor)
        {
            if (fromFloor < toFloor) return Direction.Up;
            if (fromFloor > toFloor) return Direction.Down;
            return Direction.Idle;
        }

        // הפונקציות הקיימות נשארות ללא שינוי
        private List<Request> GetAllowedUnassignedRequests()
        {
            return unassignedRequests
                .Where(r => !forbiddenRequests.Contains(r) && !assignedRequests.Contains(r))
                .ToList();
        }

        private float CalculateTotalCost(Schedule schedule)
        {
            float totalCost = 0;

            foreach (var stop in schedule.Stops)
            {
                // Calculate wait costs
                foreach (var request in stop.Pickups)
                {
                    foreach (var call in request.Calls)
                    {
                        float waitTime = (float)Math.Max(0, stop.ArrivalTime - call.ReleaseTime.ToOADate());
                        totalCost += (float)(call.WaitCost * waitTime);
                    }
                }

                // Calculate travel costs
                foreach (var call in stop.Drops)
                {
                    float travelTime = 0;
                    foreach (var prevStop in schedule.Stops)
                    {
                        if (prevStop == stop) break;

                        foreach (var request in prevStop.Pickups)
                        {
                            if (request.Calls.Contains(call))
                            {
                                travelTime = stop.ArrivalTime - prevStop.ArrivalTime;
                                break;
                            }
                        }

                        if (travelTime > 0) break;
                    }

                    totalCost += (float)(call.TravelCost * travelTime);
                }

                // Add capacity penalty if needed
                int currentLoad = CalculateLoadAtStop(schedule, stop);
                if (currentLoad > elevator.Capacity)
                {
                    totalCost += (float)(instance.capacityPenalty * (currentLoad - elevator.Capacity));
                }
            }

            return totalCost;
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

        private double CalculateReducedCost(Schedule schedule)
        {
            double cost = schedule.TotalCost;
            double dualSum = 0;

            // רק בקשות לא משויכות נכנסות לחישוב הדואל - מאמר: c̃(S) := c(S) - ∑ρ∈Ru∩S πρ - πe
            foreach (var request in schedule.ServedRequests)
            {
                if (!assignedRequests.Contains(request))  // רק אם זו בקשה מ-Ru (לא משויכת)
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

        private double CalculateTravelTime(int fromFloor, int toFloor)
        {
            int distance = Math.Abs(toFloor - fromFloor);
            if (distance == 0) return 0;

            return Constant.ElevatorStartupTime + distance * Constant.DrivePerFloorTime;
        }
    }
}