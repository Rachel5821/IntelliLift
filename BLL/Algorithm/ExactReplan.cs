using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Project;

namespace Project.Algorithm
{
    public class ExactReplan
    {
        private ProblemInstance instance;
        private ScheduleCollection schedules;
        private List<Schedule> oldSchedules; // הוספת רשימת לוחות זמנים ישנים
        private int K;
        private int maxBranchAndPriceIterations;
        private int maxSchedulesPerIteration;
        private double optimalityGap;
        private bool useOldSchedules;

        public ExactReplan(ProblemInstance instance, int k = 100,
                         int maxBranchAndPriceIterations = 1000,
                         int maxSchedulesPerIteration = 5,
                         double optimalityGap = 0.01,
                         bool useOldSchedules = true)
        {
            this.instance = instance;
            schedules = new ScheduleCollection();
            oldSchedules = new List<Schedule>(); // אתחול
            K = k;
            this.maxBranchAndPriceIterations = maxBranchAndPriceIterations;
            this.maxSchedulesPerIteration = maxSchedulesPerIteration;
            this.optimalityGap = optimalityGap;
            this.useOldSchedules = useOldSchedules;
        }

        public Solution solve()
        {
            if (instance.GetUnassignedRequests().Count == 1)
            {
                return SolveSingleRequestCase();
            }
            return SolveByBranchAndPrice();
        }

        private Solution SolveSingleRequestCase()
        {
            Request singleRequest = instance.GetUnassignedRequests().First();
            List<Elevator> elevators = instance.GetElevators();

            double minCost = double.MaxValue;
            Schedule bestSchedule = null;
            int bestElevatorIndex = -1;

            for (int e = 0; e < elevators.Count; e++)
            {
                Elevator elevator = elevators[e];
                Schedule schedule = CreateScheduleForSingleRequest(elevator, e, singleRequest);
                if (schedule.TotalCost < minCost)
                {
                    minCost = schedule.TotalCost;
                    bestSchedule = schedule;
                    bestElevatorIndex = e;
                }
            }
            schedules.Add(bestSchedule);
            double[] values = new double[1] { 1.0 };
            return new Solution(values, schedules, minCost);
        }



        private float CalculateScheduleCost(Schedule schedule, Elevator elevator, Request request)
        {
            float waitCost = 0;
            float travelCost = 0;
            float capacityCost = 0;
            double travelToPickupTime = CalculateTravelTime(elevator.CurrentFloor, request.StartFloor);
            double pickupTime = elevator.CurrentTime + travelToPickupTime + Constant.StopTime;

            double travelToDestTime = CalculateTravelTime(request.StartFloor, request.DestinationFloor);
            double dropTime = pickupTime + travelToDestTime + Constant.StopTime;
            foreach (var call in request.Calls)
            {
                double waitTime = Math.Max(0, pickupTime - call.ReleaseTime.ToOADate());
                waitCost += (float)(call.WaitCost * waitTime);
                travelCost += (float)(call.TravelCost * travelToDestTime);

            }
            if (elevator.LoadedCalls.Count + request.Calls.Count > elevator.Capacity)
            {
                capacityCost = (float)(Constant.CapacityPenalty * (elevator.LoadedCalls.Count + request.Calls.Count - elevator.Capacity));
            }
            return waitCost + travelCost + capacityCost;
        }

        internal double CalculateTravelTime(int fromFloor, int toFloor)
        {
            int distance = Math.Abs(toFloor - fromFloor);
            if (distance == 0) return 0;
            return Constant.ElevatorStartupTime + distance * Constant.DrivePerFloorTime;
        }

        private Solution SolveByBranchAndPrice()
        {
            Solution bestSolution = null;
            double bestObjectiveValue = double.MaxValue;

            // יצירת צומת שורש של עץ ה-Branch & Price
            MasterModel rootModel = new MasterModel(instance);
            InitializeWithHeuristicSolution(rootModel);
            BranchAndPriceNode rootNode = new BranchAndPriceNode(rootModel);

            // תור עדיפויות לצמתים לפי ערך חסם תחתון
            PriorityQueue<BranchAndPriceNode, double> nodeQueue = new PriorityQueue<BranchAndPriceNode, double>();
            nodeQueue.Enqueue(rootNode, 0);

            int iterations = 0;
            DateTime startTime = DateTime.Now;

            // לולאת החיפוש העיקרית
            while (nodeQueue.Count > 0 && iterations < maxBranchAndPriceIterations)
            {
                iterations++;

                // בדיקת זמן - הפסקה אם עברנו את זמן החיפוש המקסימלי
                if ((DateTime.Now - startTime).TotalSeconds > 60) // הגבלה ל-60 שניות
                {
                    Console.WriteLine($"זמן החיפוש הגיע לגבול. מספר איטרציות: {iterations}");
                    break;
                }

                // שליפת הצומת הבא עם החסם התחתון הנמוך ביותר
                BranchAndPriceNode currentNode = nodeQueue.Dequeue();

                // פתרון ה-LP על ידי Column Generation
                Solution nodeSolution = SolveNodeWithColumnGeneration(currentNode);

                // אם אין פתרון אפשרי, או שהפתרון גרוע מהטוב ביותר שמצאנו
                if (nodeSolution == null || nodeSolution.ObjectiveValue >= bestObjectiveValue)
                {
                    continue; // נעבור לצומת הבא
                }

                // בדיקה אם מצאנו פתרון שלם
                if (nodeSolution.isIntegral)
                {
                    if (nodeSolution.ObjectiveValue < bestObjectiveValue)
                    {
                        bestSolution = nodeSolution;
                        bestObjectiveValue = nodeSolution.ObjectiveValue;
                        Console.WriteLine($"מצאנו פתרון שלם עם ערך {bestObjectiveValue} באיטרציה {iterations}");
                    }
                    continue; // אין צורך לפצל צומת עם פתרון שלם
                }

                // פיצול הצומת ל-2 צמתים חדשים
                List<BranchAndPriceNode> childNodes = Branch(currentNode, nodeSolution);

                // הוספת הצמתים החדשים לתור
                foreach (var childNode in childNodes)
                {
                    childNode.LowerBound = childNode.Model.Solve()?.ObjectiveValue ?? double.MaxValue;
                    if (childNode.LowerBound < bestObjectiveValue)
                    {
                        nodeQueue.Enqueue(childNode, childNode.LowerBound);
                    }
                }

                // הצגת התקדמות החיפוש
                if (iterations % 10 == 0)
                {
                    Console.WriteLine($"איטרציה {iterations}: " +
                                     $"הפתרון הטוב ביותר = {bestObjectiveValue}, " +
                                     $"גודל התור = {nodeQueue.Count}");
                }
            }

            Console.WriteLine($"סה\"כ איטרציות: {iterations}");

            if (bestSolution == null)
            {
                // אם לא מצאנו פתרון שלם, נשתמש בפתרון היוריסטי
                Console.WriteLine("לא מצאנו פתרון שלם אופטימלי. שימוש בפתרון היוריסטי.");
                return GetHeuristicSolution();
            }

            return bestSolution;
        }

        private Solution GetHeuristicSolution()
        {
            // יישום של פתרון היוריסטי פשוט במקרה שלא מצאנו פתרון אופטימלי
            // לדוגמה, אפשר להשתמש ב-BestInsert אם הוא מיושם

            MasterModel model = new MasterModel(instance);
            InitializeWithHeuristicSolution(model);
            return model.Solve();
        }

        private void InitializeWithHeuristicSolution(MasterModel masterModel)
        {
            List<Request> unassignedRequests = instance.GetUnassignedRequests();
            List<Elevator> elevators = instance.GetElevators();

            // יצירת לוחות זמנים ריקים לכל המעליות
            for (int e = 0; e < elevators.Count; e++)
            {
                Elevator elevator = elevators[e];
                Schedule emptySchedule = new Schedule(e);
                Stop initialStop = new Stop
                {
                    Floor = elevator.CurrentFloor,
                    ArrivalTime = 0,
                    Direction = Direction.Idle
                };
                emptySchedule.AddStop(initialStop);
                emptySchedule.TotalCost = 0;
                masterModel.AddSchedule(emptySchedule, e);
            }

            // יצירת לוחות זמנים פשוטים לכל זוג אפשרי של מעלית-בקשה
            for (int e = 0; e < elevators.Count; e++)
            {
                for (int r = 0; r < unassignedRequests.Count; r++)
                {
                    Schedule singleRequestSchedule = CreateScheduleForSingleRequest(elevators[e], e, unassignedRequests[r]);
                    masterModel.AddSchedule(singleRequestSchedule, e);
                }
            }

            // [אופציונלי] יצירת לוחות זמנים עם שתי בקשות
            if (unassignedRequests.Count >= 2)
            {
                for (int e = 0; e < elevators.Count; e++)
                {
                    for (int r1 = 0; r1 < unassignedRequests.Count; r1++)
                    {
                        for (int r2 = r1 + 1; r2 < unassignedRequests.Count; r2++)
                        {
                            List<Request> twoRequests = new List<Request> { unassignedRequests[r1], unassignedRequests[r2] };
                            Schedule twoRequestSchedule = CreateHeuristicSchedule(elevators[e], e, twoRequests);
                            masterModel.AddSchedule(twoRequestSchedule, e);
                        }
                    }
                }
            }
        }

        private Schedule CreateScheduleForSingleRequest(Elevator elevator, int elevatorIndex, Request request)
        {
            Schedule schedule = new Schedule(elevatorIndex);

            // עצירה ראשונה במיקום הנוכחי של המעלית
            Stop initialStop = new Stop
            {
                Floor = elevator.CurrentFloor,
                Direction = elevator.CurrentDirection,
                ArrivalTime = 0
            };
            schedule.AddStop(initialStop);

            // חישוב זמן הנסיעה לקומת האיסוף
            float pickupTime = (float)CalculateTravelTime(elevator.CurrentFloor, request.StartFloor);

            // כיוון הנסיעה מקומת האיסוף
            Direction pickupDirection = request.StartFloor < request.DestinationFloor ? Direction.Up : Direction.Down;

            // עצירה לאיסוף
            Stop pickupStop = new Stop
            {
                Floor = request.StartFloor,
                Direction = pickupDirection,
                ArrivalTime = pickupTime
            };
            pickupStop.AddPickup(request);
            schedule.AddStop(pickupStop);

            // חישוב זמן הנסיעה לקומת היעד
            float travelTime = (float)CalculateTravelTime(request.StartFloor, request.DestinationFloor);
            float dropTime = pickupTime + travelTime + (float)Constant.StopTime;

            // עצירה להורדה
            Stop dropStop = new Stop
            {
                Floor = request.DestinationFloor,
                Direction = Direction.Idle, // אחרי הורדה אין כיוון מוגדר
                ArrivalTime = dropTime
            };

            // הוספת קריאות להורדה
            foreach (Call call in request.Calls)
            {
                dropStop.AddDrop(call);
            }
            schedule.AddStop(dropStop);

            // הוספת הבקשה לרשימת הבקשות המטופלות
            schedule.ServedRequests.Add(request);

            // חישוב עלות הלוח
            float cost = CalculateScheduleCost(schedule, elevator, request);
            schedule.TotalCost = cost;

            return schedule;
        }
        private Schedule CreateHeuristicSchedule(Elevator elevator, int elevatorIndex, List<Request> requests)
        {
            Schedule schedule = new Schedule(elevatorIndex);

            float totalCost = 0;
            foreach (var request in requests)
            {
                totalCost += CalculateRequestCost(elevator, request);
                schedule.ServedRequests.Add(request);
            }

            schedule.TotalCost = totalCost;
            return schedule;
        }

        private float CalculateRequestCost(Elevator elevator, Request request)
        {
            double waitCost = 0;
            double travelCost = 0;

            foreach (var call in request.Calls)
            {
                waitCost += call.WaitCost * 10; // הערך 10 הוא זמן המתנה משוער
                travelCost += call.TravelCost * 15; // הערך 15 הוא זמן נסיעה משוער
            }

            return (float)(waitCost + travelCost);
        }

        private Solution SolveBranchAndPrice(BranchAndPriceNode rootNode)
        {
            Solution bestSolution = null;
            double bestObjectiveValue = double.MaxValue;

            PriorityQueue<BranchAndPriceNode, double> nodeQueue = new PriorityQueue<BranchAndPriceNode, double>();
            nodeQueue.Enqueue(rootNode, 0);

            int iterations = 0;
            while (nodeQueue.Count > 0 && iterations < maxBranchAndPriceIterations)
            {
                iterations++;

                BranchAndPriceNode currentNode = nodeQueue.Dequeue();

                Solution nodeSolution = SolveNodeWithColumnGeneration(currentNode);

                if (nodeSolution == null || nodeSolution.ObjectiveValue >= bestObjectiveValue)
                {
                    continue;
                }

                if (nodeSolution.isIntegral)
                {
                    if (nodeSolution.ObjectiveValue < bestObjectiveValue)
                    {
                        bestSolution = nodeSolution;
                        bestObjectiveValue = nodeSolution.ObjectiveValue;
                    }
                    continue;
                }

                List<BranchAndPriceNode> childNodes = Branch(currentNode, nodeSolution);
                foreach (var childNode in childNodes)
                {
                    nodeQueue.Enqueue(childNode, childNode.LowerBound);
                }
            }

            return bestSolution;
        }

        private Solution SolveNodeWithColumnGeneration(BranchAndPriceNode node)
        {
            MasterModel model = node.Model;
            Solution bestSolution = null;
            double bestObjectiveValue = double.MaxValue;

            int iterations = 0;
            bool columnsAdded = true;

            while (columnsAdded && iterations < K)
            {
                iterations++;

                // פתרון ה-LP הנוכחי
                Solution lpSolution = model.Solve();
                if (lpSolution == null)
                {
                    Console.WriteLine("הצומת אינו אפשרי");
                    return null; // אין פתרון אפשרי
                }

                // שמירת הפתרון הטוב ביותר
                if (lpSolution.ObjectiveValue < bestObjectiveValue)
                {
                    bestSolution = lpSolution;
                    bestObjectiveValue = lpSolution.ObjectiveValue;
                }
                Console.WriteLine($"איטרציה {iterations}: האם הפתרון שלם? {lpSolution.isIntegral}");

                // בדיקה אם יש פתרון שלם
                if (lpSolution.isIntegral /*&& iterations >= 2*/)
                {
                    Console.WriteLine($"מצאנו פתרון שלם במודל המוגבל, ערך: {lpSolution.ObjectiveValue}");

                    var selectedSchedules = lpSolution.GetSelectedSchedules();
                    Console.WriteLine($"מספר לוחות זמנים נבחרו: {selectedSchedules.Count}");

                    foreach (var schedule in selectedSchedules)
                    {
                        Console.WriteLine($"לוח זמנים למעלית {schedule.ElevatorIndex}: {schedule.Stops?.Count ?? 0} עצירות, {schedule.ServedRequests?.Count ?? 0} בקשות");
                        Console.WriteLine($"  TotalCost: {schedule.TotalCost}"); // ✅ הוסף את זה

                        // ✅ הוסף את זה - הדפס את העצירות אם יש
                        if (schedule.Stops != null && schedule.Stops.Count > 0)
                        {
                            foreach (var stop in schedule.Stops)
                            {
                                Console.WriteLine($"    עצירה בקומה {stop.Floor} בזמן {stop.ArrivalTime}");
                            }
                        }
                    }

                    return lpSolution;
                }
                columnsAdded = GenerateNewColumns(model, lpSolution);


                // חיפוש והוספת עמודות חדשות
                columnsAdded = GenerateNewColumns(model, lpSolution);

                // אם לא הוספנו עמודות חדשות, יש לנו LP מינימלי ואפשר לעבור לפיצול
                if (!columnsAdded)
                {
                    Console.WriteLine($"אין יותר עמודות להוסיף, ערך LP: {lpSolution.ObjectiveValue}");

                    // פתרון אחרון ב-LP
                    return lpSolution;
                }
            }

            // הגענו למספר האיטרציות המקסימלי או שמצאנו פתרון מיטבי ל-LP
            if (iterations >= K)
            {
                Console.WriteLine($"הגענו למספר איטרציות מקסימלי ({K})");
            }

            return bestSolution;
        }

        private bool GenerateNewColumns(MasterModel model, Solution lpSolution)
        {
            Console.WriteLine("נכנס לcolumngeneration");
            bool columnsAdded = false;

            // נסיון שימוש בלוחות זמנים ישנים אם האופציה מאופשרת
            if (useOldSchedules)
            {
                double[] requestDuals = lpSolution.GetRequestDuals();
                double[] elevatorDuals = lpSolution.GetElevatorsDuals();

                // בדיקת לוחות זמנים ישנים
                foreach (var schedule in oldSchedules)
                {
                    double reducedCost = CalculateReducedCost(schedule, requestDuals, elevatorDuals);

                    if (reducedCost < -1e-6) // עלות מופחתת שלילית
                    {
                        model.AddSchedule(schedule, schedule.ElevatorIndex);
                        columnsAdded = true;
                    }
                }

                // אם הוספנו לוחות זמנים ישנים, נחזור לפני פתרון בעיית הפרייסינג
                if (columnsAdded)
                {
                    Console.WriteLine("הוספנו לוחות זמנים ישנים עם עלות מופחתת שלילית");
                    return true;
                }
            }

            // נעבור על כל המעליות ונפתור את בעיית הפרייסינג לכל אחת מהן
            for (int e = 0; e < instance.GetElevators().Count; e++)
            {
                double[] requestDuals = lpSolution.GetRequestDuals();
                double[] elevatorDuals = lpSolution.GetElevatorsDuals();

                // יצירת בעיית הפרייסינג עבור מעלית e
                PricingProblem pricingProblem = new PricingProblem(
                    instance,
                    e,
                    requestDuals,
                    elevatorDuals[e],
                    maxSchedulesPerIteration);

                // פתרון בעיית הפרייסינג
                List<Schedule> newSchedules = pricingProblem.GenerateSchedulesWithNegativeReducedCost();

                // אם מצאנו לוחות זמנים, נוסיף אותם למודל
                foreach (var schedule in newSchedules)
                {
                    Console.WriteLine($"מצאנו לוח זמנים חדש למעלית {e} עם עלות מופחתת {CalculateReducedCost(schedule, requestDuals, elevatorDuals)}");
                    model.AddSchedule(schedule, e);
                    columnsAdded = true;

                    // שמירת הלוח שנוצר באוסף הלוחות הישנים אם האופציה מאופשרת
                    if (useOldSchedules)
                    {
                        oldSchedules.Add(schedule);
                    }
                }
            }

            return columnsAdded;
        }

        // הוספת שיטה לחישוב Reduced Cost
        public double CalculateReducedCost(Schedule schedule, double[] requestDuals, double[] elevatorDuals)
        {
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
        private List<BranchAndPriceNode> Branch(BranchAndPriceNode parentNode, Solution solution)
        {
            List<BranchAndPriceNode> childNodes = new List<BranchAndPriceNode>();

            // מציאת פיצול לפי בקשה ומעליות שמשרתות אותה
            (int requestIndex, List<int> elevatorGroup1, List<int> elevatorGroup2) = FindBranchingDecision(solution);

            if (requestIndex >= 0)
            {
                // יצירת צומת השמאלי - בקשה משויכת לקבוצת מעליות 1
                MasterModel leftModel = new MasterModel(parentNode.Model);
                leftModel.AddBranchingConstraint(requestIndex, elevatorGroup1, true);
                leftModel.AddBranchingConstraint(requestIndex, elevatorGroup2, false);
                BranchAndPriceNode leftNode = new BranchAndPriceNode(leftModel);
                childNodes.Add(leftNode);

                // יצירת צומת הימני - בקשה משויכת לקבוצת מעליות 2
                MasterModel rightModel = new MasterModel(parentNode.Model);
                rightModel.AddBranchingConstraint(requestIndex, elevatorGroup2, true);
                rightModel.AddBranchingConstraint(requestIndex, elevatorGroup1, false);
                BranchAndPriceNode rightNode = new BranchAndPriceNode(rightModel);
                childNodes.Add(rightNode);
            }

            return childNodes;
        }

        private (int requestIndex, List<int> group1, List<int> group2) FindBranchingDecision(Solution solution)
        {
            List<Request> unassignedRequests = instance.GetUnassignedRequests();
            List<Schedule> selectedSchedules = solution.GetSelectedSchedules();
            double[] values = solution.GetValues();

            // מיפוי בקשות למעליות
            Dictionary<int, Dictionary<int, double>> requestToElevators = new Dictionary<int, Dictionary<int, double>>();

            // אתחול המיפוי
            for (int i = 0; i < unassignedRequests.Count; i++)
            {
                requestToElevators[i] = new Dictionary<int, double>();
            }

            // מילוי המיפוי עם ערכי הפתרון
            for (int i = 0; i < selectedSchedules.Count; i++)
            {
                Schedule schedule = selectedSchedules[i];
                int elevatorIndex = schedule.ElevatorIndex;
                double value = values[i];

                if (value < 1e-6) continue; // דילוג על ערכים קרובים מאוד ל-0

                foreach (var request in schedule.ServedRequests)
                {
                    int requestIndex = unassignedRequests.IndexOf(request);
                    if (requestIndex >= 0)
                    {
                        if (!requestToElevators[requestIndex].ContainsKey(elevatorIndex))
                        {
                            requestToElevators[requestIndex][elevatorIndex] = 0;
                        }

                        requestToElevators[requestIndex][elevatorIndex] += value;
                    }
                }
            }

            // חיפוש בקשה לפיצול
            // הקריטריון: בקשה עם הכי קרוב ל-0.5/0.5 בין שתי קבוצות מעליות
            double bestScore = double.MaxValue;
            int bestRequestIndex = -1;
            List<int> bestGroup1 = null;
            List<int> bestGroup2 = null;

            foreach (var kvp in requestToElevators)
            {
                int requestIndex = kvp.Key;
                var elevatorValues = kvp.Value;

                if (elevatorValues.Count < 2)
                {
                    continue; // צריך לפחות 2 מעליות כדי לפצל
                }

                // מיון המעליות לפי הערך
                var sortedElevators = elevatorValues.OrderByDescending(e => e.Value).ToList();

                // חלוקה לשתי קבוצות כך שנקבל הכי קרוב ל-0.5/0.5
                double sum1 = 0;
                double sum2 = 0;

                List<int> group1 = new List<int>();
                List<int> group2 = new List<int>();

                // מיון המעליות לשתי קבוצות כדי לקבל התפלגות מאוזנת
                foreach (var elevator in sortedElevators)
                {
                    if (sum1 <= sum2)
                    {
                        group1.Add(elevator.Key);
                        sum1 += elevator.Value;
                    }
                    else
                    {
                        group2.Add(elevator.Key);
                        sum2 += elevator.Value;
                    }
                }

                // חישוב כמה רחוק ההתפלגות מ-0.5/0.5
                double score = Math.Abs(0.5 - sum1) + Math.Abs(0.5 - sum2);

                if (score < bestScore && sum1 > 0 && sum1 < 1 && sum2 > 0 && sum2 < 1)
                {
                    bestScore = score;
                    bestRequestIndex = requestIndex;
                    bestGroup1 = group1;
                    bestGroup2 = group2;
                }
            }

            if (bestRequestIndex >= 0)
            {
                Console.WriteLine($"בחירת בקשה {bestRequestIndex} לפיצול " +
                                 $"קבוצה 1: {string.Join(",", bestGroup1)} (סכום={requestToElevators[bestRequestIndex].Where(e => bestGroup1.Contains(e.Key)).Sum(e => e.Value)}), " +
                                 $"קבוצה 2: {string.Join(",", bestGroup2)} (סכום={requestToElevators[bestRequestIndex].Where(e => bestGroup2.Contains(e.Key)).Sum(e => e.Value)})");

                return (bestRequestIndex, bestGroup1, bestGroup2);
            }

            return (-1, null, null); // לא נמצאה בקשה מתאימה לפיצול
        }
    }

    public class BranchAndPriceNode
    {
        public MasterModel Model { get; }
        public double LowerBound { get; set; }

        public BranchAndPriceNode(MasterModel model)
        {
            Model = model;
            LowerBound = 0.0;
        }
    }
}