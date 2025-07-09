using ILOG.Concert;
using ILOG.CPLEX;
using Project;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Project.Algorithm
{
    public class BranchingConstraintInfo
    {
        public int RequestIndex { get; set; }
        public List<int> ElevatorGroup { get; set; }
        public bool IsAssigned { get; set; }

        public BranchingConstraintInfo(int requestIndex, List<int> elevatorGroup, bool isAssigned)
        {
            RequestIndex = requestIndex;
            ElevatorGroup = new List<int>(elevatorGroup); // העתקה עמוקה
            IsAssigned = isAssigned;
        }
    }

    public class MasterModel
    {
        private Cplex cplex;
        private ProblemInstance instance;
        private IObjective objective;
        private List<INumVar> scheduleVars; //משתנים עבור כל לוח זמנים

        private IRange[] requestConstraints;
        private IRange[] elevatorConstraints;
        private IRange[] elevatorLoadConstraints;
        private List<IRange> branchingConstraints;
        private Dictionary<IRange, BranchingConstraintInfo> constraintInfoMap; // מיפוי בין אילוצים למידע שלהם
        private ScheduleCollection schedules;

        public MasterModel(ProblemInstance problemInstance)
        {
            cplex = new Cplex();
            instance = problemInstance;
            scheduleVars = new List<INumVar>();
            branchingConstraints = new List<IRange>();
            constraintInfoMap = new Dictionary<IRange, BranchingConstraintInfo>();
            schedules = new ScheduleCollection();
            InitializeModel();
        }

        public MasterModel(MasterModel other)
        {
            cplex = new Cplex();
            instance = other.instance;
            scheduleVars = new List<INumVar>();
            branchingConstraints = new List<IRange>();
            constraintInfoMap = new Dictionary<IRange, BranchingConstraintInfo>();
            schedules = new ScheduleCollection(other.schedules);

            foreach (var schedule in other.schedules.GetAllSchedules())
            {
                AddSchedule(schedule, schedule.ElevatorIndex);
            }

            // העתקת אילוצי branching
            // שים לב: זו העתקה פשטנית שעשויה לא לעבוד מושלם
            foreach (var constraint in other.branchingConstraints)
            {
                if (other.constraintInfoMap.TryGetValue(constraint, out var info))
                {
                    AddBranchingConstraint(info.RequestIndex, info.ElevatorGroup, info.IsAssigned);
                }
            }
        }

        private void InitializeModel()
        {
            try
            {
                objective = cplex.AddMinimize();
                List<Request> unassignedRequests = instance.GetUnassignedRequests();
                requestConstraints = new IRange[unassignedRequests.Count];
                for (int i = 0; i < unassignedRequests.Count; i++)
                {
                    requestConstraints[i] = cplex.AddRange(1.0, 1.0);
                }

                elevatorConstraints = new IRange[instance.numElevators];
                for (int i = 0; i < instance.numElevators; i++)
                {
                    elevatorConstraints[i] = cplex.AddRange(1.0, 1.0);
                }

                elevatorLoadConstraints = new IRange[instance.numElevators];
                // int maxRequestsPerElevator = Math.Max(3, instance.GetUnassignedRequests().Count / instance.numElevators + 1);
                int maxRequestsPerElevator = instance.GetUnassignedRequests().Count; // ללא הגבלה

                for (int e = 0; e < instance.numElevators; e++)
                {
                    elevatorLoadConstraints[e] = cplex.AddRange(0.0, maxRequestsPerElevator);
                }
                cplex.SetParam(Cplex.Param.RootAlgorithm, Cplex.Algorithm.Primal);
            }
            catch (ILOG.Concert.Exception ex)
            {
                Console.WriteLine("Error initializing master model: " + ex.Message);
                throw;
            }
        }

        public void AddSchedule(Schedule schedule, int elevatorIndex)
        {
            if (elevatorIndex == 0)
            {
                assignmentsCalculated = false;
            }
            if (schedule.TotalCost >= 470)
            {
                Console.WriteLine($"🚫 לוח זמנים יקר (עלות {schedule.TotalCost}) - מחליף ב-fallback");

                // קבל את המעלית והבקשות מה-instance
                var elevators = instance.GetElevators();
                if (elevatorIndex < elevators.Count)
                {
                    Elevator elevator = elevators[elevatorIndex];
                    List<Request> allRequests = instance.GetUnassignedRequests();

                    // חלוקה חכמה של בקשות לפי כיוון ומרחק
                    List<Request> myRequests = SelectBestRequestsForElevator(elevator, allRequests, elevatorIndex);

                    if (myRequests.Count == 0)
                    {
                        Console.WriteLine($"❌ אין בקשות מתאימות למעלית {elevatorIndex}");
                        return; // אל תוסיף לוח ריק יקר
                    }

                    var route = CreateOptimalRouteSimple(elevator, myRequests);
                    Schedule fallbackSchedule = ConvertRouteToSchedule(route, myRequests, elevatorIndex);

                    if (fallbackSchedule != null)
                    {
                        schedule = fallbackSchedule; // החלף בכל מקרה!
                        Console.WriteLine($"✅ החלפתי בלוח fallback עם עלות {schedule.TotalCost} (היה {470})");
                    }
                    else
                    {
                        Console.WriteLine($"❌ Fallback נכשל, משתמש בלוח המקורי");
                        return; // אל תוסיף לוח גרוע
                    }
                }
            }

            Console.WriteLine($"🔍 מוסיף לוח זמנים למעלית {elevatorIndex} עם עלות {schedule.TotalCost}, עצירות: {schedule.Stops?.Count ?? 0}");
            try
            {
                schedules.Add(schedule);
                ILOG.Concert.Column column = cplex.Column(objective, schedule.TotalCost);
                column = column.And(cplex.Column(elevatorConstraints[elevatorIndex], 1.0));

                var unassignedRequests = instance.GetUnassignedRequests();

                // אילוצי בקשות
                for (int i = 0; i < unassignedRequests.Count; i++)
                {
                    var request = unassignedRequests[i];
                    if (schedule.ServedRequestsContains(request.Id))
                    {
                        column = column.And(cplex.Column(requestConstraints[i], 1.0));
                    }
                }

                // אילוצי ברנצ'ינג
                foreach (var branchingConstraint in branchingConstraints)
                {
                    if (constraintInfoMap.TryGetValue(branchingConstraint, out var constraintInfo))
                    {
                        bool affectsSchedule = false;
                        if (schedule.ServedRequestsContains(constraintInfo.RequestIndex))
                        {
                            if (constraintInfo.IsAssigned && constraintInfo.ElevatorGroup.Contains(elevatorIndex))
                            {
                                column = column.And(cplex.Column(branchingConstraint, 1.0));
                                affectsSchedule = true;
                            }
                            else if (!constraintInfo.IsAssigned && !constraintInfo.ElevatorGroup.Contains(elevatorIndex))
                            {
                                column = column.And(cplex.Column(branchingConstraint, 1.0));
                                affectsSchedule = true;
                            }
                        }
                        if (!affectsSchedule)
                        {
                            column = column.And(cplex.Column(branchingConstraint, 0.0));
                        }
                    }
                }

                // *** הוספת אילוץ עומס המעלית לפני יצירת המשתנה ***
                int numRequestsInSchedule = schedule.ServedRequests.Count;
                if (numRequestsInSchedule > 0)
                {
                    column = column.And(cplex.Column(elevatorLoadConstraints[elevatorIndex], numRequestsInSchedule));
                }

                // יצירת המשתנה עם כל האילוצים
                INumVar var = cplex.NumVar(column, 0, 1, NumVarType.Float);
                scheduleVars.Add(var);
            }
            catch (ILOG.Concert.Exception ex)
            {
                Console.WriteLine("Error adding schedule: " + ex.Message);
                throw;
            }
        }

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

        private static Dictionary<int, List<Request>> assignmentsForCurrentBatch = new Dictionary<int, List<Request>>();
        private static bool assignmentsCalculated = false;

        private List<Request> SelectBestRequestsForElevator(Elevator elevator, List<Request> allRequests, int elevatorIndex)
        {
            if (allRequests.Count == 0) return new List<Request>();

            // אם זו הפעם הראשונה בסיבוב הזה - חשב הקצאות לכל המעליות
            if (!assignmentsCalculated)
            {
                CalculateOptimalAssignments(allRequests);
                assignmentsCalculated = true;
            }

            // החזר את הבקשות שהוקצו למעלית הזו
            if (assignmentsForCurrentBatch.ContainsKey(elevatorIndex))
            {
                var myRequests = assignmentsForCurrentBatch[elevatorIndex];
                Console.WriteLine($"📊 מעלית {elevatorIndex}: קיבלה {myRequests.Count} בקשות");
                return myRequests;
            }

            Console.WriteLine($"📊 מעלית {elevatorIndex}: לא קיבלה בקשות");
            return new List<Request>();
        }

        private void CalculateOptimalAssignments(List<Request> allRequests)
        {
            assignmentsForCurrentBatch.Clear();

            var elevators = instance.GetElevators();
            int numElevators = elevators.Count;
            int requestsPerElevator = 2; // כל מעלית רוצה 2 בקשות

            Console.WriteLine($"🎯 מחשב הקצאה אופטימלית עבור {allRequests.Count} בקשות ו-{numElevators} מעליות");

            // אתחל רשימות ריקות לכל מעלית
            for (int i = 0; i < numElevators; i++)
            {
                assignmentsForCurrentBatch[i] = new List<Request>();
            }

            // שלב 1: כל מעלית מדרגת את כל הבקשות
            var elevatorPreferences = new List<(int elevatorIndex, List<(Request request, double score)> rankedRequests)>();

            for (int i = 0; i < numElevators; i++)
            {
                var elevator = elevators[i];
                var rankedRequests = allRequests
                    .Select(request => (
                        request: request,
                        score: CalculateRequestScore(elevator, request, i, numElevators)
                    ))
                    .OrderBy(x => x.score) // ציון נמוך = טוב יותר
                    .ToList();

                elevatorPreferences.Add((i, rankedRequests));

                Console.WriteLine($"🎯 מעלית {i}: דירגה בקשות לפי העדפה");
            }

            var takenRequests = new HashSet<int>();

            // שלב 2: תחילה, תן לכל מעלית בקשות שרק היא רוצה (ללא התנגשות)
            Console.WriteLine("🔄 שלב 1: מחלק בקשות ללא התנגשות");

            foreach (var (elevatorIndex, rankedRequests) in elevatorPreferences)
            {
                foreach (var (request, score) in rankedRequests)
                {
                    if (assignmentsForCurrentBatch[elevatorIndex].Count >= requestsPerElevator) break;
                    if (takenRequests.Contains(request.Id)) continue;

                    // בדוק אם מעלית אחרת גם רוצה את הבקשה הזו
                    bool wantedByOthers = elevatorPreferences
                        .Where(other => other.elevatorIndex != elevatorIndex)
                        .Any(other => other.rankedRequests.Take(requestsPerElevator * 2) // רק הטובות ביותר
                                      .Any(choice => choice.request.Id == request.Id));

                    if (!wantedByOthers)
                    {
                        assignmentsForCurrentBatch[elevatorIndex].Add(request);
                        takenRequests.Add(request.Id);
                        Console.WriteLine($"✅ מעלית {elevatorIndex}: לקחה בקשה {request.Id} (ללא התנגשות, ציון: {score:F1})");
                    }
                }
            }

            // שלב 3: עכשיו פתור התנגשויות לפי עדיפות אינדקס
            Console.WriteLine("🔄 שלב 2: פותר התנגשויות לפי עדיפות אינדקס");

            for (int priority = 0; priority < numElevators; priority++)
            {
                var (elevatorIndex, rankedRequests) = elevatorPreferences[priority];

                foreach (var (request, score) in rankedRequests)
                {
                    if (assignmentsForCurrentBatch[elevatorIndex].Count >= requestsPerElevator) break;
                    if (takenRequests.Contains(request.Id)) continue;

                    assignmentsForCurrentBatch[elevatorIndex].Add(request);
                    takenRequests.Add(request.Id);
                    Console.WriteLine($"🏆 מעלית {elevatorIndex}: לקחה בקשה {request.Id} (עדיפות אינדקס, ציון: {score:F1})");
                }
            }

            // סיכום
            Console.WriteLine("📋 סיכום הקצאות:");
            for (int i = 0; i < numElevators; i++)
            {
                var requests = assignmentsForCurrentBatch[i];
                if (requests.Count > 0)
                {
                    var requestIds = string.Join(", ", requests.Select(r => r.Id));
                    Console.WriteLine($"   מעלית {i}: {requests.Count} בקשות [{requestIds}]");
                }
                else
                {
                    Console.WriteLine($"   מעלית {i}: אין בקשות");
                }
            }
        }


        //private List<Request> SelectBestRequestsForElevator(Elevator elevator, List<Request> allRequests, int elevatorIndex)
        //{
        //    if (allRequests.Count == 0) return new List<Request>();

        //    // איפוס עבור חישוב חדש
        //    if (elevatorIndex == 0 || isFirstCall)
        //    {
        //        takenRequestIds.Clear();
        //        isFirstCall = false;
        //    }

        //    int numElevators = instance.numElevators;
        //    int requestsPerElevator = Math.Max(1, allRequests.Count / numElevators);

        //    // מסנן בקשות שעדיין זמינות
        //    var availableRequests = allRequests
        //        .Where(r => !takenRequestIds.Contains(r.Id))
        //        .ToList();

        //    if (availableRequests.Count == 0)
        //    {
        //        Console.WriteLine($"📊 מעלית {elevatorIndex}: אין בקשות זמינות");
        //        return new List<Request>();
        //    }

        //    // דירוג בקשות זמינות לפי התאמה למעלית הזו
        //    var scoredRequests = availableRequests.Select(request => new
        //    {
        //        Request = request,
        //        Score = CalculateRequestScore(elevator, request, elevatorIndex, numElevators)
        //    })
        //    .OrderBy(x => x.Score) // ציון נמוך = יותר טוב
        //    .ToList();

        //    // בחירת הבקשות הכי טובות עבור המעלית הזו
        //    var selectedRequests = scoredRequests
        //        .Take(requestsPerElevator)
        //        .Select(x => x.Request)
        //        .ToList();

        //    // סימון הבקשות שנבחרו כתפוסות
        //    foreach (var request in selectedRequests)
        //    {
        //        takenRequestIds.Add(request.Id);
        //    }

        //    Console.WriteLine($"📊 מעלית {elevatorIndex}: בחרה {selectedRequests.Count} בקשות מתוך {availableRequests.Count} זמינות (נותרו {availableRequests.Count - selectedRequests.Count})");

        //    return selectedRequests;
        //}

        private double CalculateRequestScore(Elevator elevator, Request request, int elevatorIndex, int numElevators)
        {
            double score = 0;

            // 1. מרחק מהבקשה (ככל שקרוב יותר - ציון טוב יותר)
            double distanceScore = Math.Abs(request.StartFloor - elevator.CurrentFloor);

            // 2. התאמה לכיוון הנוכחי של המעלית
            double directionScore = 0;
            if (elevator.CurrentDirection == Direction.Up && request.StartFloor > elevator.CurrentFloor)
                directionScore = 0; // כיוון מתאים
            else if (elevator.CurrentDirection == Direction.Down && request.StartFloor < elevator.CurrentFloor)
                directionScore = 0; // כיוון מתאים
            else if (elevator.CurrentDirection == Direction.Idle)
                directionScore = 5; // ניטרלי
            else
                directionScore = 20; // כיוון לא מתאים

            // 3. התאמה לכיוון הבקשה עצמה
            Direction requestDirection = request.StartFloor < request.DestinationFloor ? Direction.Up : Direction.Down;
            double requestDirectionScore = 0;
            if (elevator.CurrentDirection == requestDirection || elevator.CurrentDirection == Direction.Idle)
                requestDirectionScore = 0;
            else
                requestDirectionScore = 10;

            // 4. חלוקה שווה - עדיפות לפי אינדקס מעלית (עוזר למנוע התנגשויות)
            double distributionScore = (request.Id % numElevators == elevatorIndex) ? 0 : 3;

            // 5. עדיפות לבקשות קצרות (מרחק נסיעה קטן)
            double tripLengthScore = Math.Abs(request.DestinationFloor - request.StartFloor) * 0.1;

            score = distanceScore + directionScore + requestDirectionScore + distributionScore + tripLengthScore;

            return score;
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

        private Schedule ConvertRouteToSchedule(List<int> route, List<Request> requests, int elevatorIndex)
        {
            Schedule schedule = new Schedule(elevatorIndex);
            float currentTime = 0;
            int currentFloor = route[0]; // התחל מהקומה הראשונה במסלול

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

                // הוסף את העצירה רק אם יש בה פעילות או זו הקומה הראשונה
                if (stop.Pickups.Count > 0 || stop.Drops.Count > 0 || floor == route[0])
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

            return schedule;
        }

        private double CalculateTravelTime(int fromFloor, int toFloor)
        {
            // השתמש בפונקציה הקיימת שלך או חישוב פשוט
            return Math.Abs(toFloor - fromFloor) * 2.5; // לדוגמה: 2.5 שניות לקומה
        }

        private float CalculateTotalCost(Schedule schedule)
        {
            if (schedule.Stops == null || schedule.Stops.Count <= 1)
                return 0;

            float totalCost = 0;
            for (int i = 0; i < schedule.Stops.Count - 1; i++)
            {
                int distance = Math.Abs(schedule.Stops[i + 1].Floor - schedule.Stops[i].Floor);
                totalCost += distance * 15; // לדוגמה: 15 יחידות עלות לקומה
            }

            // הוסף עלות עצירות
            totalCost += (schedule.Stops.Count - 1) * 10; // 10 יחידות לכל עצירה

            return totalCost;
        }

        public void AddBranchingConstraint(int requestIndex, List<int> elevatorGroup, bool assign)
        {
            try
            {
                ILinearNumExpr expr = cplex.LinearNumExpr();
                for (int i = 0; i < scheduleVars.Count; i++)
                {
                    Schedule schedule = schedules.GetSchedule(i);
                    int elevatorIndex = schedule.ElevatorIndex;
                    if (schedule.ServedRequestsContains(requestIndex) && elevatorGroup.Contains(elevatorIndex))
                    {
                        expr.AddTerm(1.0, scheduleVars[i]);
                    }
                }
                IRange constraint;
                if (assign)
                {
                    constraint = cplex.AddEq(expr, 1.0);
                }
                else
                {
                    constraint = cplex.AddEq(expr, 0.0);
                }

                // שמירת המידע על האילוץ
                BranchingConstraintInfo constraintInfo = new BranchingConstraintInfo(requestIndex, elevatorGroup, assign);
                constraintInfoMap[constraint] = constraintInfo;

                branchingConstraints.Add(constraint);
            }
            catch (ILOG.Concert.Exception ex)
            {
                Console.WriteLine("Error adding branching constraint: " + ex.Message);
                throw;
            }
        }

        public void RemoveBranchingConstraint()
        {
            if (branchingConstraints.Count > 0)
            {
                try
                {
                    var constraint = branchingConstraints[branchingConstraints.Count - 1];
                    cplex.Remove(constraint);

                    if (constraintInfoMap.ContainsKey(constraint))
                    {
                        constraintInfoMap.Remove(constraint);
                    }

                    branchingConstraints.RemoveAt(branchingConstraints.Count - 1);
                }
                catch (ILOG.Concert.Exception ex)
                {
                    Console.WriteLine("Error removing branching constraint: " + ex.Message);
                    throw;
                }
            }
        }

        public Solution Solve()
        {
            try
            {
                if (cplex.Solve())
                {
                    double[] values = cplex.GetValues(scheduleVars.ToArray());
                    double objValue = cplex.GetObjValue();

                    double[] requestDuals = cplex.GetDuals(requestConstraints);
                    double[] elevatorDuals = cplex.GetDuals(elevatorConstraints);

                    return new Solution(values, schedules, objValue, requestDuals, elevatorDuals);
                }
                return null;
            }
            catch (ILOG.Concert.Exception ex)
            {
                Console.WriteLine("Error solving model: " + ex.Message);
                throw;
            }
        }

        public double[] GetRequestDualPrices()
        {
            try
            {
                return cplex.GetDuals(requestConstraints);
            }
            catch (ILOG.Concert.Exception ex)
            {
                Console.WriteLine("Error getting dual prices: " + ex.Message);
                throw;
            }
        }

        public double[] GetElevatorDualPrices()
        {
            try
            {
                return cplex.GetDuals(elevatorConstraints);
            }
            catch (ILOG.Concert.Exception ex)
            {
                Console.WriteLine("Error getting dual prices: " + ex.Message);
                throw;
            }
        }

        public ScheduleCollection Schedules
        {
            get { return schedules; }
        }

        public ProblemInstance Instance
        {
            get { return instance; }
        }

        public void End()
        {
            try
            {
                cplex.End();
            }
            catch (ILOG.Concert.Exception ex)
            {
                Console.WriteLine("Error ending cplex: " + ex.Message);
            }
        }
    }
}