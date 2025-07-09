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
            if (false && useOldSchedules && TryPricingOldSchedules(resultSchedules, threshold))
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
                    double reducedCost = CalculateReducedCostSimple(bestSchedule);
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
            Console.WriteLine($"🏁 [B&B RESULT] קיבלנו {branchAndBoundResults.Count} לוחות מ-B&B"); // ✅ הוסף את זה

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
                Console.WriteLine("✅ יוצר לוח זמנים fallback"); // ✅ הוסף את זה

                Schedule fallbackSchedule = CreateFallbackSchedule();
                if (fallbackSchedule != null)
                {
                    resultSchedules.Add(fallbackSchedule);
                    SaveScheduleForNextRun(fallbackSchedule);
                }
            }
            Console.WriteLine($"🔍 PricingProblem מחזיר {resultSchedules.Count} לוחות זמנים"); // ✅ הוסף את זה

            return resultSchedules;
        }

        /// <summary>
        /// STEP 1: Pricing of old schedules - מאמר סעיף 3.3
        /// </summary>
        private bool TryPricingOldSchedules(List<Schedule> resultSchedules, double threshold)
        {
            Console.WriteLine("🔍 בודק לוחות זמנים ישנים..."); // ✅ הוסף את זה

            if (!useOldSchedules || oldSchedules.Count == 0)
                return false;

            bool foundSchedules = false;

            foreach (var oldSchedule in oldSchedules)
            {
                // בדוק אם הלוח הישן שייך למעלית הזו
                if (oldSchedule.ElevatorIndex != elevatorIndex)
                    continue;

                // חשב עלות מופחתת עם הערכים הדואליים החדשים
                double reducedCost = CalculateReducedCostSimple(oldSchedule);

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
            Console.WriteLine("🔍 פתרון אופטימלי לבקשה יחידה..."); // ✅ הוסף את זה

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
        public double CalculateReducedCost(Schedule schedule, double[] requestDuals, double[] elevatorDuals)
        {
            Console.WriteLine($"📊 [DEBUG CalculateReducedCost]:");
            Console.WriteLine($"  - Schedule.TotalCost: {schedule?.TotalCost ?? 0}");
            Console.WriteLine($"  - Schedule.ServedRequests: {schedule?.ServedRequests?.Count ?? 0}");
            if (schedule == null)
            {
                throw new ArgumentNullException(nameof(schedule));
            }

            double cost = schedule.TotalCost;
            double dualSum = 0;

            // חישוב סכום הדואלים של הבקשות
            List<Request> unassignedRequests = instance.GetUnassignedRequests();
            foreach (var request in schedule.ServedRequests)
            {
                int requestIndex = unassignedRequests.IndexOf(request);
                if (requestIndex >= 0 && requestIndex < requestDuals.Length)
                {
                    dualSum += requestDuals[requestIndex];
                }
            }

            // חישוב העלות המופחתת
            return cost - dualSum - elevatorDuals[schedule.ElevatorIndex];
        }
        private List<Schedule> RunBranchAndBound(double threshold)
        {
            Console.WriteLine($"🎯 [THRESHOLD DEBUG] מתחיל B&B עם threshold (θ): {threshold}"); // ✅ הוסף את זה

            List<Schedule> M = new List<Schedule>();
            PriorityQueue<PricingNode, double> Q = new PriorityQueue<PricingNode, double>();
            double θ = threshold;

            DateTime startTime = DateTime.Now;
            int maxIterations = 100; // ✅ הקטן מ-1000 ל-100
            int maxSeconds = 3;      // ✅ מקסימום 3 שניות
            int maxQueueSize = 50;   // ✅ מקסימום 50 צמתים בתור

            List<PricingNode> rootNodes = CreateRootNodes();

            foreach (var rootNode in rootNodes)
            {
                double lowerBound = CalculateLowerBound(rootNode);
                if (lowerBound < θ)
                {
                    Q.Enqueue(rootNode, lowerBound);
                }
            }

            int iterations = 0;

            while (Q.Count > 0 && M.Count < maxSchedules && iterations < maxIterations)
            {
                iterations++;

                // ✅ בדיקות עצירה מוקדמת
                if ((DateTime.Now - startTime).TotalSeconds > maxSeconds)
                {
                    Console.WriteLine($"⏰ TIMEOUT! עוצר אחרי {iterations} איטרציות");
                    break;
                }

                if (Q.Count > maxQueueSize)
                {
                    Console.WriteLine($"🔥 התור גדול מדי ({Q.Count})! עוצר חיפוש");
                    break;
                }

                if (iterations % 10 == 0)
                {
                    Console.WriteLine($"🔄 איטרציה {iterations}, תור: {Q.Count}, פתרונות: {M.Count}");
                }

                PricingNode v = Q.Dequeue();

                if (v.IsLast())
                {
                    Schedule schedule = v.GetSchedule();

                    double reducedCost = CalculateReducedCost(schedule, requestDuals, requestDuals);

                    Console.WriteLine($"🔍 [SOLUTION CHECK] מצא צומת סופי:");
                    Console.WriteLine($"    - ReducedCost: {reducedCost:F2}");
                    Console.WriteLine($"    - Threshold (θ): {θ:F2}");
                    Console.WriteLine($"    - reducedCost < θ? {reducedCost < θ}");

                    if (reducedCost < θ)
                    {
                        M.Add(schedule);
                        Console.WriteLine($"✅ מצאנו פתרון! עלות: {reducedCost:F2}");
                        if (M.Count >= maxSchedules) break;
                    }
                    else
                    {
                        Console.WriteLine($"❌ פתרון לא מספיק טוב ({reducedCost:F2} >= {θ:F2})");
                    }
                    continue;
                }

                List<PricingNode> N = v.Branch();

                // ✅ הגבל מספר בנים
                if (N.Count > 5)
                {
                    N = N.Take(5).ToList();
                    Console.WriteLine($"⚠️ הגבלתי ל-5 בנים");
                }

                foreach (var u in N)
                {
                    double uLowerBound = CalculateLowerBound(u);
                    if (uLowerBound < θ)
                    {
                        Q.Enqueue(u, uLowerBound);
                    }
                }
            }

            Console.WriteLine($"🏁 B&B הסתיים: {M.Count} פתרונות, {iterations} איטרציות");
            return M;
        }

        //private List<Schedule> RunBranchAndBound(double threshold)
        //{
        //    Console.WriteLine($"[DEBUG] מתחיל B&B עם threshold: {threshold}");

        //    List<Schedule> M = new List<Schedule>();
        //    PriorityQueue<PricingNode, double> Q = new PriorityQueue<PricingNode, double>();
        //    double θ = threshold;

        //    // יצירת צמתי שורש
        //    List<PricingNode> rootNodes = CreateRootNodes();
        //    Console.WriteLine($"[DEBUG] נוצרו {rootNodes.Count} צמתי שורש");

        //    // בדיקת כל צומת שורש
        //    foreach (var rootNode in rootNodes)
        //    {
        //        Console.WriteLine($"[DEBUG] בודק צומת שורש:");
        //        Console.WriteLine($"  - CurrentFloor: {rootNode.CurrentFloor}");
        //        Console.WriteLine($"  - CurrentTime: {rootNode.CurrentTime}");
        //        Console.WriteLine($"  - CurrentLoad: {rootNode.CurrentLoad}");
        //        Console.WriteLine($"  - UnservedAssignedRequests: {rootNode.UnservedAssignedRequests.Count}");
        //        Console.WriteLine($"  - UnservedOptionalRequests: {rootNode.UnservedOptionalRequests.Count}");
        //        Console.WriteLine($"  - IsLast(): {rootNode.IsLast()}");

        //        double lowerBound = CalculateLowerBound(rootNode);
        //        Console.WriteLine($"  - LowerBound: {lowerBound}");
        //        Console.WriteLine($"  - θ (threshold): {θ}");
        //        Console.WriteLine($"  - lowerBound < θ? {lowerBound < θ}");

        //        if (lowerBound < θ)
        //        {
        //            Q.Enqueue(rootNode, lowerBound);
        //            Console.WriteLine($"  - ✅ הוכנס לתור");
        //        }
        //        else
        //        {
        //            Console.WriteLine($"  - ❌ נגזם בשלב זה");
        //        }
        //    }

        //    Console.WriteLine($"[DEBUG] גודל התור לפני התחלת הלולאה: {Q.Count}");

        //    // הלולאה הראשית
        //    int iterations = 0;
        //    int maxIterations = 50

        //    while (Q.Count > 0 && M.Count < maxSchedules && iterations < maxIterations)
        //    {
        //        iterations++;
        //        Console.WriteLine($"[DEBUG] איטרציה {iterations}, גודל תור: {Q.Count}");

        //        PricingNode v = Q.Dequeue();

        //        Console.WriteLine($"[DEBUG] עיבוד צומת:");
        //        Console.WriteLine($"  - CurrentFloor: {v.CurrentFloor}");
        //        Console.WriteLine($"  - IsLast(): {v.IsLast()}");

        //        if (v.IsLast())
        //        {
        //            Console.WriteLine($"[DEBUG] זהו צומת סופי!");

        //            Schedule schedule = v.GetSchedule();
        //            double reducedCost = CalculateReducedCost(schedule);

        //            Console.WriteLine($"  - ReducedCost: {reducedCost}");
        //            Console.WriteLine($"  - θ: {θ}");
        //            Console.WriteLine($"  - reducedCost < θ? {reducedCost < θ}");

        //            if (reducedCost < θ)
        //            {
        //                M.Add(schedule);
        //                Console.WriteLine($"  - ✅ לוח זמנים נוסף ל-M! סה\"כ: {M.Count}");

        //                // עדכון threshold
        //                if (M.Count > 0)
        //                {
        //                    double newTheta = M.Min(s => CalculateReducedCost(s));
        //                    if (newTheta < θ)
        //                    {
        //                        θ = newTheta;
        //                        Console.WriteLine($"  - θ עודכן ל-{θ}");
        //                    }
        //                }

        //                if (M.Count >= maxSchedules) break;
        //            }
        //            else
        //            {
        //                Console.WriteLine($"  - ❌ עלות מופחתת לא טובה מספיק");
        //            }
        //            continue;
        //        }

        //        // Branching
        //        Console.WriteLine($"[DEBUG] מבצע branching...");
        //        List<PricingNode> N = v.Branch();
        //        Console.WriteLine($"[DEBUG] נוצרו {N.Count} צמתים בנים");

        //        foreach (var u in N)
        //        {
        //            double uLowerBound = CalculateLowerBound(u);
        //            Console.WriteLine($"  - צומת בן: LowerBound={uLowerBound}, θ={θ}");

        //            if (uLowerBound < θ)
        //            {
        //                Q.Enqueue(u, uLowerBound);
        //                Console.WriteLine($"    ✅ הוכנס לתור");
        //            }
        //            else
        //            {
        //                Console.WriteLine($"    ❌ נגזם");
        //            }
        //        }
        //    }

        //    Console.WriteLine($"[DEBUG] B&B הסתיים: {M.Count} לוחות, {iterations} איטרציות");
        //    return M;
        //}
        //private List<Schedule> RunBranchAndBound(double threshold)
        //{
        //    List<Schedule> M = new List<Schedule>();
        //    PriorityQueue<PricingNode, double> Q = new PriorityQueue<PricingNode, double>();
        //    double θ = threshold;

        //    List<PricingNode> rootNodes = CreateRootNodes();

        //    foreach (var rootNode in rootNodes)
        //    {
        //        double lowerBound = CalculateLowerBound(rootNode);
        //        if (lowerBound < θ)
        //        {
        //            Q.Enqueue(rootNode, lowerBound);
        //        }
        //    }

        //    int iterations = 0;
        //    int maxIterations = 5000; // הקטן את המגבלה

        //    while (Q.Count > 0 && M.Count < maxSchedules && iterations < maxIterations)
        //    {
        //        iterations++;

        //        // ✅ בדיקת Pruning מוקדמת
        //        if (Q.Count > 1000)
        //        {
        //            Console.WriteLine($"[B&B] התור גדל מדי ({Q.Count}). מפסיק חיפוש.");
        //            break;
        //        }

        //        PricingNode v = Q.Dequeue();

        //        // בדיקה אם הצומת הוא אפשרי
        //        if (v.IsLast())
        //        {
        //            Schedule schedule = v.GetSchedule();
        //            double reducedCost = CalculateReducedCost(schedule);

        //            if (reducedCost < θ)
        //            {
        //                M.Add(schedule);
        //                Console.WriteLine($"[B&B] ✅ מצאנו לוח זמנים! עלות: {reducedCost:F4}");

        //                // עדכון threshold
        //                if (M.Count > 0)
        //                {
        //                    double newTheta = M.Min(s => CalculateReducedCost(s));
        //                    if (newTheta < θ)
        //                    {
        //                        θ = newTheta;

        //                        // ✅ נקה צמתים עם LowerBound גרוע
        //                        // (במימוש מלא - פה רק מדפיסים)
        //                        Console.WriteLine($"[B&B] θ עודכן ל-{θ:F4}");
        //                    }
        //                }

        //                if (M.Count >= maxSchedules) break;
        //            }
        //            continue;
        //        }

        //        // Branching
        //        List<PricingNode> N = v.Branch();

        //        // ✅ הגבלת מספר הבנים
        //        if (N.Count > 10)
        //        {
        //            N = N.Take(10).ToList(); // רק 10 הראשונים
        //        }

        //        foreach (var u in N)
        //        {
        //            double uLowerBound = CalculateLowerBound(u);

        //            if (uLowerBound < θ)
        //            {
        //                Q.Enqueue(u, uLowerBound);
        //            }
        //        }
        //    }

        //    Console.WriteLine($"[B&B] סיום: {M.Count} לוחות, {iterations} איטרציות, תור: {Q.Count}");
        //    return M;
        //}
        //private List<Schedule> RunBranchAndBound(double threshold)
        //{
        //    List<Schedule> M = new List<Schedule>(); // תוצאות
        //    PriorityQueue<PricingNode, double> Q = new PriorityQueue<PricingNode, double>(); // תור עדיפויות
        //    double θ = threshold; // Threshold for pruning

        //    Console.WriteLine($"[B&B] מתחיל RunBranchAndBound עם Threshold (θ) = {threshold}");

        //    // יצירת צמתי שורש - מאמר: "There is a separate root node r for every floor f where the elevator can still stop at next"
        //    List<PricingNode> rootNodes = CreateRootNodes();
        //    Console.WriteLine($"[B&B] נוצרו {rootNodes.Count} צמתי שורש.");

        //    foreach (var rootNode in rootNodes)
        //    {
        //        double lowerBound = CalculateLowerBound(rootNode);
        //        Console.WriteLine($"[B&B] צומת שורש: CurrentFloor={rootNode.CurrentFloor}, CurrentTime={rootNode.CurrentTime}, LowerBound={lowerBound:F4}.");
        //        if (lowerBound < θ)
        //        {
        //            Q.Enqueue(rootNode, lowerBound);
        //            Console.WriteLine($"  [B&B] -> צומת שורש הוכנס לתור Q (LowerBound < θ).");
        //        }
        //        else
        //        {
        //            Console.WriteLine($"  [B&B] -> צומת שורש נגזם (LowerBound >= θ).");
        //        }
        //    }

        //    // Main Branch & Bound loop
        //    int iterations = 0;
        //    int maxIterations = 10000; // הגבלת איטרציות למניעת לולאה אינסופית

        //    Console.WriteLine($"[B&B] --- תחילת לולאת Branch & Bound ---");
        //    while (Q.Count > 0 && M.Count < maxSchedules && iterations < maxIterations)
        //    {
        //        iterations++;
        //        Console.WriteLine($"[B&B] ---- איטרציה {iterations} ----");
        //        Console.WriteLine($"[B&B] מצב תור: Q.Count={Q.Count}, M.Count={M.Count}, Threshold (θ)={θ:F4}.");

        //        PricingNode v = Q.Dequeue();
        //        Console.WriteLine($"[B&B] נשלף צומת מהתור: CurrentFloor={v.CurrentFloor}, CurrentTime={v.CurrentTime:F2}, CurrentLoad={v.CurrentLoad}, IsLast()={v.IsLast()}.");

        //        // בדיקה אם הצומת הוא אפשרי (feasible)
        //        if (v.IsLast())
        //        {
        //            Console.WriteLine($"  [B&B] צומת הוא 'עלה'. מנסה להרכיב לוח זמנים.");
        //            Schedule schedule = v.GetSchedule();
        //            double reducedCost = CalculateReducedCost(schedule);
        //            Console.WriteLine($"    [B&B] עלות מופחתת של לוח זמנים: {reducedCost:F4}. השוואה ל-θ: {θ:F4}.");

        //            if (reducedCost < θ)
        //            {
        //                M.Add(schedule);
        //                Console.WriteLine($"    [B&B] ✅ לוח זמנים התווסף ל-M. (עלות מופחתת {reducedCost:F4} < {θ:F4}). מספר לוחות זמנים ב-M: {M.Count}.");

        //                // עדכון threshold - מאמר: "θ is set to the minimum reduced cost of a schedule in M"
        //                if (M.Count > 0)
        //                {
        //                    double newTheta = M.Min(s => CalculateReducedCost(s));
        //                    if (newTheta < θ) // Only update if it's actually better
        //                    {
        //                        θ = newTheta;
        //                        Console.WriteLine($"    [B&B] θ עודכן ל- {θ:F4} (נמצא לוח זמנים טוב יותר).");
        //                    }
        //                }

        //                // Early stopping אם מצאנו מספיק לוחות זמנים
        //                if (M.Count >= maxSchedules)
        //                {
        //                    Console.WriteLine($"[B&B] מצאנו {M.Count} לוחות זמנים (מקסימום נדרש). סיום מוקדם אחרי {iterations} איטרציות.");
        //                    break;
        //                }
        //            }
        //            else
        //            {
        //                Console.WriteLine($"    [B&B] ❌ לוח זמנים לא התווסף ל-M (עלות מופחתת {reducedCost:F4} >= θ {θ:F4}).");
        //            }
        //            continue; // עובר לצומת הבא בתור אם זה היה צומת עלה
        //        }

        //        // Branching - יצירת צמתים בנים
        //        List<PricingNode> N = v.Branch();
        //        Console.WriteLine($"  [B&B] מסתעף מצומת. נוצרו {N.Count} צמתים בנים.");

        //        if (N.Count == 0)
        //        {
        //            Console.WriteLine($"    [B&B] אין צמתים בנים אפשריים מצומת זה. (אולי בגלל אילוצים או שאין לאן להמשיך).");
        //        }

        //        foreach (var u in N)
        //        {
        //            double uLowerBound = CalculateLowerBound(u);
        //            Console.WriteLine($"    [B&B] צומת בן: CurrentFloor={u.CurrentFloor}, CurrentTime={u.CurrentTime:F2}, CurrentLoad={u.CurrentLoad}, LowerBound={uLowerBound:F4}.");

        //            // Pruning - מאמר: "Each u ∈ N is added to Q provided that its lower bound is less than θ"
        //            if (uLowerBound < θ)
        //            {
        //                Q.Enqueue(u, uLowerBound);
        //                Console.WriteLine($"      [B&B] -> צומת בן הוכנס לתור Q (LowerBound < θ).");
        //            }
        //            else
        //            {
        //                Console.WriteLine($"      [B&B] -> צומת בן נגזם (LowerBound >= θ).");
        //            }
        //        }
        //    }

        //    Console.WriteLine($"[B&B] --- סיום Branch & Bound ---");
        //    Console.WriteLine($"[B&B] Branch & Bound הסתיים: {M.Count} לוחות זמנים, {iterations} איטרציות.");
        //    return M;
        //}

        /// <summary>
        /// STEP 3: Branch & Bound מלא - מאמר סעיף 3.1
        /// </summary>
        //private List<Schedule> RunBranchAndBound(double threshold)
        //{
        //    List<Schedule> M = new List<Schedule>(); // תוצאות
        //    PriorityQueue<PricingNode, double> Q = new PriorityQueue<PricingNode, double>(); // תור עדיפויות
        //    double θ = threshold; // Threshold for pruning

        //    // יצירת צמתי שורש - מאמר: "There is a separate root node r for every floor f where the elevator can still stop at next"
        //    List<PricingNode> rootNodes = CreateRootNodes();

        //    foreach (var rootNode in rootNodes)
        //    {
        //        double lowerBound = CalculateLowerBound(rootNode);
        //        if (lowerBound < θ)
        //        {
        //            Q.Enqueue(rootNode, lowerBound);
        //        }
        //    }

        //    // Main Branch & Bound loop
        //    int iterations = 0;
        //    int maxIterations = 10000; // הגבלת איטרציות למניעת לולאה אינסופית

        //    while (Q.Count > 0 && M.Count < maxSchedules && iterations < maxIterations)
        //    {
        //        iterations++;

        //        PricingNode v = Q.Dequeue();

        //        // בדיקה אם הצומת הוא אפשרי (feasible)
        //        if (v.IsLast())
        //        {
        //            Schedule schedule = v.GetSchedule();
        //            double reducedCost = CalculateReducedCost(schedule);

        //            if (reducedCost < θ)
        //            {
        //                M.Add(schedule);

        //                // עדכון threshold - מאמר: "θ is set to the minimum reduced cost of a schedule in M"
        //                if (M.Count > 0)
        //                {
        //                    θ = M.Min(s => CalculateReducedCost(s));
        //                }

        //                // Early stopping אם מצאנו מספיק לוחות זמנים
        //                if (M.Count >= maxSchedules)
        //                {
        //                    Console.WriteLine($"מצאנו {M.Count} לוחות זמנים אחרי {iterations} איטרציות");
        //                    break;
        //                }
        //            }
        //            continue;
        //        }

        //        // Branching - יצירת צמתים בנים
        //        List<PricingNode> N = v.Branch();

        //        foreach (var u in N)
        //        {
        //            double uLowerBound = CalculateLowerBound(u);

        //            // Pruning - מאמר: "Each u ∈ N is added to Q provided that its lower bound is less than θ"
        //            if (uLowerBound < θ)
        //            {
        //                Q.Enqueue(u, uLowerBound);
        //            }
        //        }
        //    }

        //    Console.WriteLine($"Branch & Bound הסתיים: {M.Count} לוחות זמנים, {iterations} איטרציות");
        //    return M;
        //}

        /// <summary>
        /// יצירת צמתי שורש - מאמר סעיף 3.1
        /// </summary>
        //private List<PricingNode> CreateRootNodes()
        //{
        //    List<PricingNode> rootNodes = new List<PricingNode>();

        //    // Fi(e) - set of floors that are admissible for the first stop in a schedule
        //    HashSet<int> Fi = GetAdmissibleFloorsForFirstStop();
        //    List<Request> allowedRequests = GetAllowedUnassignedRequests();

        //    foreach (int f in Fi)
        //    {
        //        // Sr - schedule corresponding to dropping all loaded calls with first stop at floor f
        //        Schedule Sr = CreateBaseScheduleWithFirstStopAt(f);

        //        // יצירת צומת שורש - תואם לקוד המקורי שלך
        //        PricingNode rootNode = new PricingNode(
        //            currentFloor: f,
        //            currentTime: Sr.Stops.LastOrDefault()?.ArrivalTime ?? 0,
        //            currentLoad: CalculateLoadAfterSchedule(Sr),
        //            servedAssignedRequests: new HashSet<Request>(assignedRequests), // כל הבקשות המשויכות כבר "נאספו"
        //            unservedAssignedRequests: new List<Request>(), // אין יותר בקשות משויכות שטרם נאספו
        //            servedOptionalRequests: new HashSet<Request>(), // טרם נאספו בקשות אופציונליות
        //            unservedOptionalRequests: new List<Request>(allowedRequests), // כל הבקשות האופציונליות זמינות
        //            currentSchedule: Sr,
        //            elevatorCapacity: elevator.Capacity,
        //            maxFloors: instance.numFloors
        //        );

        //        rootNodes.Add(rootNode);
        //    }

        //    Console.WriteLine($"יצרנו {rootNodes.Count} צמתי שורש");
        //    return rootNodes;
        //}
        private Schedule CreateBaseScheduleWithLoadedCalls()
        {
            Schedule schedule = new Schedule(elevatorIndex);

            if (elevator.LoadedCalls == null || elevator.LoadedCalls.Count == 0)
            {
                // אין נוסעים - לוח זמנים ריק
                return schedule;
            }

            float currentTime = (float)elevator.CurrentTime;
            int currentFloor = elevator.CurrentFloor;

            // מיין את הנוסעים לפי קומת יעד
            var sortedCalls = elevator.LoadedCalls
                .OrderBy(call => Math.Abs(call.DestinationFloor - currentFloor))
                .ToList();

            foreach (var call in sortedCalls)
            {
                // נסע לקומת הורדה
                float travelTime = (float)CalculateTravelTime(currentFloor, call.DestinationFloor);
                currentTime += travelTime;

                // יצור עצירת הורדה
                Stop dropStop = new Stop
                {
                    Floor = call.DestinationFloor,
                    ArrivalTime = currentTime,
                    Direction = Direction.Idle
                };
                dropStop.AddDrop(call);
                schedule.AddStop(dropStop);

                // עדכן מיקום וזמן
                currentFloor = call.DestinationFloor;
                currentTime += (float)Constant.StopTime;
            }

            schedule.TotalCost = CalculateTotalCost(schedule);
            return schedule;
        }
        private List<PricingNode> CreateRootNodes()
        {
            List<PricingNode> rootNodes = new List<PricingNode>();

            // Fi(e) - קומות שהמעלית יכולה לעצור בהן בעצירה הראשונה
            HashSet<int> admissibleFloors = GetAdmissibleFloorsForFirstStop();
            List<Request> allowedRequests = GetAllowedUnassignedRequests();

            Console.WriteLine($"יוצר צמתי שורש לקומות: {string.Join(",", admissibleFloors)}");

            foreach (int floor in admissibleFloors)
            {
                // Sr - לוח זמנים להורדת כל הנוסעים הקיימים עם עצירה ראשונה בקומה f
                Schedule Sr = CreateBaseScheduleWithFirstStopAt(floor);

                // לפי המאמר: כל צומת שורש מכיל את כל הבקשות הלא משויכות
                PricingNode rootNode = new PricingNode(
                    currentFloor: floor,
                    currentTime: Sr.Stops.LastOrDefault()?.ArrivalTime ?? elevator.CurrentTime,
                    currentLoad: CalculateLoadAfterSchedule(Sr),
                    servedAssignedRequests: new HashSet<Request>(assignedRequests), // כל המשויכות "נאספו"
                    unservedAssignedRequests: new List<Request>(), // אין יותר משויכות
                    servedOptionalRequests: new HashSet<Request>(),
                    unservedOptionalRequests: new List<Request>(allowedRequests), // ← כל הבקשות הלא משויכות!
                    currentSchedule: Sr,
                    elevatorCapacity: elevator.Capacity,
                    maxFloors: instance.numFloors
                );

                rootNodes.Add(rootNode);
                Console.WriteLine($"צומת שורש לקומה {floor}: {allowedRequests.Count} בקשות אופציונליות");
            }

            return rootNodes;
        }
        //private List<PricingNode> CreateRootNodes()
        //{
        //    List<PricingNode> rootNodes = new List<PricingNode>();

        //    // יצירת לוח זמנים בסיסי (ריק)
        //    Schedule baseSchedule = new Schedule(elevatorIndex);
        //    Stop initialStop = new Stop
        //    {
        //        Floor = elevator.CurrentFloor,
        //        ArrivalTime = (float)elevator.CurrentTime,
        //        Direction = Direction.Idle
        //    };
        //    baseSchedule.AddStop(initialStop);
        //    baseSchedule.TotalCost = 0;

        //    List<Request> allowedRequests = GetAllowedUnassignedRequests();

        //    // ✅ צומת שורש עם בקשה אחת (לא ריק!)
        //    if (allowedRequests.Count > 0)
        //    {
        //        Request firstRequest = allowedRequests[0];

        //        PricingNode rootNode = new PricingNode(
        //            currentFloor: elevator.CurrentFloor,
        //            currentTime: elevator.CurrentTime,
        //            currentLoad: 0,
        //            servedAssignedRequests: new HashSet<Request>(),
        //            unservedAssignedRequests: new List<Request>(), // אין משויכות
        //            servedOptionalRequests: new HashSet<Request>(),
        //            unservedOptionalRequests: new List<Request> { firstRequest }, // ← בקשה אחת!
        //            currentSchedule: baseSchedule,
        //            elevatorCapacity: elevator.Capacity,
        //            maxFloors: instance.numFloors
        //        );

        //        rootNodes.Add(rootNode);
        //        Console.WriteLine($"יצרנו צומת שורש עם בקשה אחת: {firstRequest.Id}");
        //    }
        //    else
        //    {
        //        // אם אין בקשות, צור צומת ריק
        //        PricingNode emptyRoot = new PricingNode(
        //            currentFloor: elevator.CurrentFloor,
        //            currentTime: elevator.CurrentTime,
        //            currentLoad: 0,
        //            servedAssignedRequests: new HashSet<Request>(),
        //            unservedAssignedRequests: new List<Request>(),
        //            servedOptionalRequests: new HashSet<Request>(),
        //            unservedOptionalRequests: new List<Request>(),
        //            currentSchedule: baseSchedule,
        //            elevatorCapacity: elevator.Capacity,
        //            maxFloors: instance.numFloors
        //        );

        //        rootNodes.Add(emptyRoot);
        //        Console.WriteLine($"יצרנו צומת שורש ריק");
        //    }

        //    return rootNodes;
        //}

        //private List<PricingNode> CreateRootNodes()
        //{
        //    List<PricingNode> rootNodes = new List<PricingNode>();

        //    // נתחיל רק מהקומה הנוכחית של המעלית
        //    int currentFloor = elevator.CurrentFloor;

        //    // יצירת לוח זמנים בסיסי להורדת נוסעים קיימים
        //    Schedule baseSchedule = CreateBaseScheduleWithLoadedCalls();

        //    // חישוב מצב אחרי הורדת כל הנוסעים הקיימים
        //    float finalTime = baseSchedule.Stops.LastOrDefault()?.ArrivalTime ?? (float)elevator.CurrentTime;
        //    int finalFloor = baseSchedule.Stops.LastOrDefault()?.Floor ?? elevator.CurrentFloor;
        //    int finalLoad = 0; // אחרי הורדת כולם

        //    List<Request> allowedRequests = GetAllowedUnassignedRequests();

        //    PricingNode rootNode = new PricingNode(
        //        currentFloor: finalFloor,
        //        currentTime: finalTime,
        //        currentLoad: finalLoad,
        //        servedAssignedRequests: new HashSet<Request>(assignedRequests),
        //        unservedAssignedRequests: new List<Request>(), // אין יותר משויכות
        //        servedOptionalRequests: new HashSet<Request>(),
        //        unservedOptionalRequests: new List<Request>(allowedRequests),
        //        currentSchedule: baseSchedule,
        //        elevatorCapacity: elevator.Capacity,
        //        maxFloors: instance.numFloors
        //    );

        //    rootNodes.Add(rootNode);
        //    Console.WriteLine($"יצרנו צומת שורש יחיד: קומה {finalFloor}, זמן {finalTime}, עומס {finalLoad}");

        //    return rootNodes;
        //}

        /// <summary>
        /// יצירת לוח זמנים בסיסי עם עצירה ראשונה בקומה f - מאמר סעיף 3.1
        /// </summary>
        private Schedule CreateBaseScheduleWithFirstStopAt(int firstFloor)
        {
            Schedule schedule = new Schedule(elevatorIndex);

            float currentTime = (float)elevator.CurrentTime;
            int currentFloor = elevator.CurrentFloor;

            // נסע לקומת העצירה הראשונה
            if (firstFloor != currentFloor)
            {
                float travelTime = (float)CalculateTravelTime(currentFloor, firstFloor);
                currentTime += travelTime;
            }

            // עצירה ראשונה
            Stop firstStop = new Stop
            {
                Floor = firstFloor,
                ArrivalTime = currentTime,
                Direction = DetermineInitialDirection(firstFloor)
            };
            schedule.AddStop(firstStop);
            currentFloor = firstFloor;
            currentTime += (float)Constant.StopTime;

            // הוסף הורדות של נוסעים קיימים
            var remainingCalls = new List<Call>(elevator.LoadedCalls ?? new List<Call>());

            // הורד בעצירה הראשונה מי שצריך
            var dropsAtFirstStop = remainingCalls.Where(call => call.DestinationFloor == firstFloor).ToList();
            foreach (var call in dropsAtFirstStop)
            {
                firstStop.AddDrop(call);
                remainingCalls.Remove(call);
            }

            // הוסף עצירות להורדות הנותרות
            foreach (var call in remainingCalls.OrderBy(c => Math.Abs(c.DestinationFloor - currentFloor)))
            {
                float travelTime = (float)CalculateTravelTime(currentFloor, call.DestinationFloor);
                currentTime += travelTime;

                Stop dropStop = new Stop
                {
                    Floor = call.DestinationFloor,
                    ArrivalTime = currentTime,
                    Direction = Direction.Idle
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

            schedule.TotalCost = CalculateTotalCost(schedule);
            return schedule;
        }

        //private Schedule CreateBaseScheduleWithFirstStopAt(int f)
        //{
        //    Schedule schedule = new Schedule(elevatorIndex);

        //    // חשב זמן נסיעה לקומה f
        //    float travelTime = (float)CalculateTravelTime(elevator.CurrentFloor, f);
        //    float currentTime = (float)elevator.CurrentTime + travelTime;
        //    int currentFloor = f;

        //    // הוסף עצירה ראשונה בקומה f
        //    Stop firstStop = new Stop
        //    {
        //        Floor = f,
        //        ArrivalTime = currentTime,
        //        Direction = DetermineInitialDirection(f)
        //    };
        //    schedule.AddStop(firstStop);
        //    currentTime += (float)Constant.StopTime;

        //    // הוסף עצירות להורדת נוסעים קיימים (Sr במאמר)
        //    var sortedLoadedCalls = SortLoadedCallsByDirection();
        //    foreach (var call in sortedLoadedCalls)
        //    {
        //        if (call.DestinationFloor == f)
        //        {
        //            // כבר נוריד בעצירה הראשונה
        //            firstStop.AddDrop(call);
        //            continue;
        //        }

        //        float travelToDrop = (float)CalculateTravelTime(currentFloor, call.DestinationFloor);
        //        currentTime += travelToDrop;

        //        Stop dropStop = new Stop
        //        {
        //            Floor = call.DestinationFloor,
        //            ArrivalTime = currentTime,
        //            Direction = DetermineDirection(currentFloor, call.DestinationFloor)
        //        };
        //        dropStop.AddDrop(call);
        //        schedule.AddStop(dropStop);

        //        currentFloor = call.DestinationFloor;
        //        currentTime += (float)Constant.StopTime;
        //    }

        //    // הוסף עצירות לבקשות משויכות
        //    foreach (var request in assignedRequests)
        //    {
        //        // עצירת איסוף
        //        float travelToPickup = (float)CalculateTravelTime(currentFloor, request.StartFloor);
        //        currentTime += travelToPickup;

        //        Stop pickupStop = new Stop
        //        {
        //            Floor = request.StartFloor,
        //            ArrivalTime = currentTime,
        //            Direction = request.StartFloor < request.DestinationFloor ? Direction.Up : Direction.Down
        //        };
        //        pickupStop.AddPickup(request);
        //        schedule.AddStop(pickupStop);

        //        currentFloor = request.StartFloor;
        //        currentTime += (float)Constant.StopTime;

        //        // עצירת הורדה
        //        float travelToDrop = (float)CalculateTravelTime(request.StartFloor, request.DestinationFloor);
        //        currentTime += travelToDrop;

        //        Stop dropStop = new Stop
        //        {
        //            Floor = request.DestinationFloor,
        //            ArrivalTime = currentTime,
        //            Direction = Direction.Idle
        //        };

        //        foreach (var call in request.Calls)
        //        {
        //            dropStop.AddDrop(call);
        //        }
        //        schedule.AddStop(dropStop);

        //        currentFloor = request.DestinationFloor;
        //        currentTime += (float)Constant.StopTime;
        //    }

        //    // הוסף בקשות משויכות לרשימת הבקשות המשורתות
        //    foreach (var request in assignedRequests)
        //    {
        //        schedule.ServedRequests.Add(request);
        //    }

        //    schedule.TotalCost = CalculateTotalCost(schedule);
        //    return schedule;
        //}

        /// <summary>
        /// חישוב חסם תחתון - מאמר סעיף 3.2
        /// "consists of two parts: a lower bound on the reduced cost of requests already picked up 
        /// and a lower bound on the additional reduced cost for serving still unserved requests"
        /// </summary>
        private double CalculateLowerBound(PricingNode v)
        {
            Console.WriteLine($"[LowerBound DEBUG] מתחיל חישוב...");

            double servedCost = v.GetSchedule().TotalCost;
            Console.WriteLine($"  - servedCost: {servedCost}");

            double servedDualSum = 0;
            Console.WriteLine($"  - ServedOptionalRequests count: {v.ServedOptionalRequests.Count}");
            foreach (var request in v.ServedOptionalRequests)
            {
                int requestIndex = unassignedRequests.IndexOf(request);
                if (requestIndex >= 0 && requestIndex < requestDuals.Length)
                {
                    servedDualSum += requestDuals[requestIndex];
                    Console.WriteLine($"    + dual[{requestIndex}] = {requestDuals[requestIndex]}");
                }
            }
            Console.WriteLine($"  - servedDualSum: {servedDualSum}");

            double additionalCost = 0;
            Console.WriteLine($"  - UnservedAssignedRequests count: {v.UnservedAssignedRequests.Count}");
            foreach (var request in v.UnservedAssignedRequests)
            {
                double requestCost = EstimateMinimalRequestCost(v, request);
                additionalCost += requestCost;
                Console.WriteLine($"    + assigned cost = {requestCost}");
            }
            Console.WriteLine($"  - additionalCost (assigned): {additionalCost}");

            double optionalCost = 0;
            Console.WriteLine($"  - UnservedOptionalRequests count: {v.UnservedOptionalRequests.Count}");
            foreach (var request in v.UnservedOptionalRequests)
            {
                double requestCost = EstimateMinimalRequestCost(v, request);
                int requestIndex = unassignedRequests.IndexOf(request);
                double πρ = (requestIndex >= 0 && requestIndex < requestDuals.Length) ?
                    requestDuals[requestIndex] : 0;

                if (πρ > requestCost)
                {
                    optionalCost += requestCost - πρ;
                    Console.WriteLine($"    + optional[{requestIndex}]: cost={requestCost}, dual={πρ}, net={requestCost - πρ}");
                }
            }
            Console.WriteLine($"  - optionalCost: {optionalCost}");
            Console.WriteLine($"  - elevatorDual: {elevatorDual}");

            double result = servedCost - servedDualSum + additionalCost + optionalCost - elevatorDual;
            Console.WriteLine($"  - תוצאה סופית: {servedCost} - {servedDualSum} + {additionalCost} + {optionalCost} - {elevatorDual} = {result}");

            return result;
        }

        //private double CalculateLowerBound(PricingNode v)
        //{
        //    // Part 1: חסם תחתון על הבקשות שכבר נאספו
        //    // "The reduced cost for the picked up requests are at least c̃(Sv)"
        //    double servedCost = v.GetSchedule().TotalCost;
        //    double servedDualSum = 0;

        //    foreach (var request in v.ServedOptionalRequests)
        //    {
        //        if (!assignedRequests.Contains(request)) // רק בקשות אופציונליות
        //        {
        //            int requestIndex = unassignedRequests.IndexOf(request);
        //            if (requestIndex >= 0 && requestIndex < requestDuals.Length)
        //            {
        //                servedDualSum += requestDuals[requestIndex];
        //            }
        //        }
        //    }

        //    // Part 2: חסם תחתון על עלות נוספת לבקשות שטרם שורתו
        //    double additionalCost = 0;

        //    // בקשות משויכות שטרם נאספו (חייבות להיות מטופלות)
        //    foreach (var request in v.UnservedAssignedRequests)
        //    {
        //        double requestCost = EstimateMinimalRequestCost(v, request);
        //        additionalCost += requestCost;
        //    }

        //    // בקשות אופציונליות שטרם נאספו - רק אם כדאי
        //    foreach (var request in v.UnservedOptionalRequests)
        //    {
        //        double requestCost = EstimateMinimalRequestCost(v, request);

        //        // Dual fixing - מאמר: "If πρ ≤ c̄(ρ) it will never be favorable to serve this request"
        //        int requestIndex = unassignedRequests.IndexOf(request);
        //        double πρ = (requestIndex >= 0 && requestIndex < requestDuals.Length) ?
        //            requestDuals[requestIndex] : 0;

        //        if (πρ > requestCost)
        //        {
        //            additionalCost += requestCost - πρ;
        //        }
        //    }

        //    return servedCost - servedDualSum + additionalCost - elevatorDual;
        //}

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

        //private Schedule CreateFallbackSchedule()
        //{
        //    return CreateBaseScheduleWithAssignedRequests();
        //}
        private List<int> CreateOptimalRouteSimple(Elevator elevator, List<Request> requests)
        {
            if (requests.Count == 0)
                return new List<int> { elevator.CurrentFloor };

            List<int> route = new List<int> { elevator.CurrentFloor };

            if (elevator.LoadedCalls != null && elevator.LoadedCalls.Count > 0)
            {
                var existingDropFloors = elevator.LoadedCalls
                    .Select(call => call.DestinationFloor)
                    .Where(floor => floor != elevator.CurrentFloor)
                    .Distinct()
                    .OrderBy(floor => Math.Abs(floor - elevator.CurrentFloor));

                foreach (var floor in existingDropFloors)
                {
                    if (!route.Contains(floor))
                        route.Add(floor);
                }
            }

            var sortedRequests = requests.OrderBy(r =>
            {
                if (elevator.CurrentDirection == Direction.Up && r.StartFloor >= elevator.CurrentFloor)
                    return Math.Abs(r.StartFloor - elevator.CurrentFloor);
                else if (elevator.CurrentDirection == Direction.Down && r.StartFloor <= elevator.CurrentFloor)
                    return Math.Abs(r.StartFloor - elevator.CurrentFloor);
                else
                    return 1000 + Math.Abs(r.StartFloor - elevator.CurrentFloor);
            }).ToList();

            foreach (var request in sortedRequests)
            {
                if (!route.Contains(request.StartFloor))
                {
                    int bestPickupIndex = FindBestInsertionIndex(route, request.StartFloor);
                    route.Insert(bestPickupIndex, request.StartFloor);
                }

                if (!route.Contains(request.DestinationFloor))
                {
                    int pickupIndex = route.IndexOf(request.StartFloor);
                    int bestDropIndex = FindBestInsertionIndex(route, request.DestinationFloor, pickupIndex + 1);
                    route.Insert(bestDropIndex, request.DestinationFloor);
                }
            }

            return route;
        }

        private int FindBestInsertionIndex(List<int> route, int newFloor, int minIndex = 1)
        {
            if (route.Count <= minIndex)
                return route.Count;

            double minCost = double.MaxValue;
            int bestIndex = route.Count;

            for (int i = minIndex; i <= route.Count; i++)
            {
                double cost = CalculateInsertionCost(route, newFloor, i);
                if (cost < minCost)
                {
                    minCost = cost;
                    bestIndex = i;
                }
            }

            return bestIndex;
        }

        private double CalculateInsertionCost(List<int> route, int newFloor, int insertIndex)
        {
            if (insertIndex == 0 || insertIndex > route.Count)
                return double.MaxValue;

            if (insertIndex == route.Count)
            {
                return Math.Abs(newFloor - route[route.Count - 1]);
            }

            int prevFloor = route[insertIndex - 1];
            int nextFloor = route[insertIndex];

            double originalCost = Math.Abs(nextFloor - prevFloor);
            double newCost = Math.Abs(newFloor - prevFloor) + Math.Abs(nextFloor - newFloor);

            return newCost - originalCost;
        }
        private Schedule CreateFallbackSchedule()
        {
            Console.WriteLine("🔧 יוצר fallback עם CreateOptimalRouteSimple");

            // שלב 1: טפל בבקשות החובה (assigned) + הבקשות החדשות (optional)
            List<Request> allRequests = new List<Request>();

            // הוסף בקשות חובה (אם יש)
            if (assignedRequests != null)
                allRequests.AddRange(assignedRequests);

            // הוסף בקשות אופציונליות (זה מה שה-B&B לא הצליח לטפל בו)
            if (unassignedRequests != null)
                allRequests.AddRange(unassignedRequests);

            if (allRequests.Count == 0)
            {
                Console.WriteLine("❌ אין בקשות ל-fallback");
                return new Schedule(elevatorIndex);
            }

            // שלב 2: יצירת מסלול אופטימלי עם האלגוריתם הפשוט
            var route = CreateOptimalRouteSimple(elevator, allRequests);

            Console.WriteLine($"📍 Fallback route: {string.Join("→", route)}");

            // שלב 3: המרת המסלול ל-Schedule
            return ConvertRouteToSchedule(route, allRequests);
        }

        private Schedule ConvertRouteToSchedule(List<int> route, List<Request> requests)
        {
            Schedule schedule = new Schedule(elevatorIndex);
            float currentTime = 0;
            int currentFloor = elevator.CurrentFloor;

            foreach (int floor in route)
            {
                // חישוב זמן נסיעה לקומה
                float travelTime = (float)CalculateTravelTime(currentFloor, floor);
                currentTime += travelTime;

                Stop stop = new Stop
                {
                    Floor = floor,
                    ArrivalTime = currentTime,
                    Direction = Direction.Idle // נעדכן בהמשך
                };

                // הוסף כל הבקשות הרלוונטיות לעצירה הזו
                foreach (var request in requests)
                {
                    // איסוף
                    if (request.StartFloor == floor)
                    {
                        stop.AddPickup(request);
                        stop.Direction = request.StartFloor < request.DestinationFloor ? Direction.Up : Direction.Down;
                    }

                    // הורדה
                    if (request.DestinationFloor == floor)
                    {
                        foreach (var call in request.Calls)
                        {
                            stop.AddDrop(call);
                        }
                    }
                }

                // הוסף את העצירה רק אם יש בה פעילות
                if (stop.Pickups.Count > 0 || stop.Drops.Count > 0 || floor == elevator.CurrentFloor)
                {
                    schedule.AddStop(stop);
                }

                currentFloor = floor;
                currentTime += (float)Constant.StopTime;
            }

            // הוסף כל הבקשות לרשימת הבקשות המטופלות
            foreach (var request in requests)
            {
                schedule.ServedRequests.Add(request);
            }

            schedule.TotalCost = CalculateTotalCost(schedule);
            Console.WriteLine($"💰 Fallback cost: {schedule.TotalCost}, stops: {schedule.Stops.Count}");

            return schedule;
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
            int load = elevator.LoadedCalls?.Count ?? 0;

            foreach (var stop in schedule.Stops)
            {
                load += stop.Pickups.Sum(r => r.Calls.Count);
                load -= stop.Drops.Count;
            }

            return Math.Max(0, load);
        }
        //private int CalculateLoadAfterSchedule(Schedule schedule)
        //{
        //    int load = elevator.LoadedCalls.Count;

        //    foreach (var stop in schedule.Stops)
        //    {
        //        load += stop.Pickups.Sum(r => r.Calls.Count);
        //        load -= stop.Drops.Count;
        //    }

        //    return Math.Max(0, load);
        //}
        private HashSet<int> GetAdmissibleFloorsForFirstStop()
        {
            HashSet<int> floors = new HashSet<int>();

            // הקומה הנוכחית תמיד אפשרית
            floors.Add(elevator.CurrentFloor);

            // קומות הורדה של נוסעים קיימים
            foreach (var call in elevator.LoadedCalls ?? new List<Call>())
            {
                floors.Add(call.DestinationFloor);
            }

            // קומות איסוף של בקשות משויכות
            foreach (var request in assignedRequests)
            {
                floors.Add(request.StartFloor);
            }

            // אם המעלית מלאה, רק קומת ההורדה הבאה
            if (elevator.LoadedCalls?.Count >= elevator.Capacity)
            {
                var nextDrop = elevator.LoadedCalls
                    .Select(call => call.DestinationFloor)
                    .OrderBy(floor => Math.Abs(floor - elevator.CurrentFloor))
                    .FirstOrDefault();

                floors.Clear();
                floors.Add(nextDrop);
            }

            return floors;
        }
        //private HashSet<int> GetAdmissibleFloorsForFirstStop()
        //{
        //    HashSet<int> floors = new HashSet<int> { elevator.CurrentFloor };

        //    // הוסף קומות הורדה של נוסעים קיימים
        //    foreach (var call in elevator.LoadedCalls)
        //    {
        //        floors.Add(call.DestinationFloor);
        //    }

        //    // הוסף קומות איסוף של בקשות משויכות
        //    foreach (var request in assignedRequests)
        //    {
        //        floors.Add(request.StartFloor);
        //    }

        //    return floors;
        //}

        //private Direction DetermineInitialDirection(int firstFloor)
        //{
        //    if (elevator.CurrentFloor < firstFloor) return Direction.Up;
        //    if (elevator.CurrentFloor > firstFloor) return Direction.Down;
        //    return elevator.CurrentDirection;
        //}
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

        private double CalculateReducedCostSimple(Schedule schedule)
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