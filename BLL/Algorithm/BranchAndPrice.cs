using Microsoft.VisualBasic;
using Project;

namespace Project.Algorithm
{
    public class BranchAndPrice
    {
        private ProblemInstance problemInstance;
        private List<SubModel> subModel;
        private double LB;
        private int numbNodes;
        private long timeInMaster;
        private long timeInSub;
        private int timesInMaster;
        private int timesInSub;
        private long lastTimeInLag;
        private bool doLR;
        private const double EPS = 1e-6;

        public BranchAndPrice(ProblemInstance problemInstance)
        {
            this.problemInstance = problemInstance;
            subModel = new List<SubModel>();
            numbNodes = 0;
            timeInMaster = 0;
            timeInSub = 0;
            timesInMaster = 0;
            timesInSub = 0;
            lastTimeInLag = 0;
            doLR = false;

            for (int i = 0; i < problemInstance.numElevators; i++)
            {
                subModel.Add(new SubModel(problemInstance, i));
            }
        }

        public Solution Solve()
        {
            MasterModel masterModel = new MasterModel(problemInstance);
            PartialSolution partialSolution = new PartialSolution();

            return ColumnGeneration(masterModel, partialSolution);
        }

        private Solution ColumnGeneration(MasterModel masterModel, PartialSolution partialSolutions)
        {
            long startTime;
            Solution problemSolutionMM = null;
            Solution problemSolutionSP;
            int device = -1;
            int startDevice;
            int iteration = 0;
            lastTimeInLag = 0;
            bool columnsAdded = true;

            // לולאה חיצונית - כל עוד מוסיפים עמודות חדשות
            while (columnsAdded && iteration < 1000) // הגבלת מספר איטרציות למניעת לולאה אינסופית
            {
                iteration++;
                columnsAdded = false;

                // יצירת עמודות לכל מעלית ברצף
                device = (device + 1) % problemInstance.numElevators;

                // פתרון ה-Master Problem
                timesInMaster++;
                startTime = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
                problemSolutionMM = masterModel.Solve();
                timeInMaster += DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond - startTime;

                // אם אין פתרון אפשרי, ננסה להוסיף עמודות אפשריות
                if (problemSolutionMM == null)
                {
                    Console.WriteLine("Master problem infeasible");

                    // ניסיון ליצור עמודות שיעזרו למצוא פתרון אפשרי
                    List<Column> feasibilityColumns = GenerateFeasibilityColumns(null);

                    foreach (var column in feasibilityColumns)
                    {
                        masterModel.AddSchedule(ConvertScheduleFromColumn(column), column.AssignedElevator.Id);
                        columnsAdded = true;
                    }

                    if (!columnsAdded)
                        return null; // אין פתרון אפשרי

                    continue; // נסה שוב עם העמודות החדשות
                }

                // ניסיון להוסיף עמודות חדשות
                int newColumnResult = CreateNewColumns(masterModel, partialSolutions, problemSolutionMM);
                if (newColumnResult == -1)
                    return null; // אירעה בעיה
                if (newColumnResult == 1)
                {
                    columnsAdded = true;
                    continue; // נוספו עמודות, נפתור שוב את המודל הראשי
                }

                // עבור על כל המעליות ופתור את תת-הבעיה לכל אחת
                startDevice = device;
                do
                {
                    // הגדרת מקדמים חדשים בתת-המודל בהתבסס על הערכים הדואליים
                    subModel[device].ChangeCoefficientsInSubModel(
                        problemSolutionMM.GetRequestDuals(),
                        partialSolutions,
                        1,
                        0);

                    // פתרון תת-הבעיה
                    startTime = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
                    problemSolutionSP = subModel[device].Solve(problemSolutionMM.GetElevatorsDuals()[device]);
                    timeInSub += DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond - startTime;
                    timesInSub++;

                    // בדיקה שיש פתרון תקף
                    if (problemSolutionSP == null)
                    {
                        device = (device + 1) % problemInstance.numElevators;
                        if (device == startDevice)
                        {
                            // אם עברנו על כל המעליות ולא מצאנו פתרון, מחזירים את הפתרון הנוכחי
                            if (numbNodes == 0) LB = problemSolutionMM.ObjectiveValue;
                            return problemSolutionMM;
                        }
                        continue;
                    }

                    // בדיקה אם העלות המופחתת שלילית
                    if (IsReducedCostNegative(problemSolutionSP))
                    {
                        // המרת הפתרון לעמודה
                        Column newColumn = ConvertToColumn(problemSolutionSP);

                        // הוספת העמודה למודל הראשי
                        masterModel.AddSchedule(ConvertScheduleFromColumn(newColumn), device);
                        columnsAdded = true;
                        break;
                    }

                    // מעבר למעלית הבאה
                    device = (device + 1) % problemInstance.numElevators;

                    // אם עברנו על כל המעליות ולא מצאנו עמודה עם עלות מופחתת שלילית,
                    // הגענו לפתרון אופטימלי
                    if (device == startDevice && !columnsAdded)
                    {
                        if (numbNodes == 0) LB = problemSolutionMM.ObjectiveValue;
                        return problemSolutionMM;
                    }

                } while (device != startDevice);

                // Lagrangian relaxation - אם מופעל
                if (doLR && columnsAdded)
                {
                    int lagrangeResult = LagrangeRelaxation(problemSolutionMM, problemSolutionSP, iteration, device, partialSolutions);
                    if (lagrangeResult == 0) return null;
                    if (lagrangeResult == 1) return problemSolutionMM;
                }
            }

            // בדיקה אחרונה של הפתרון לפני החזרתו
            Solution result = problemSolutionMM;

            if (result == null)
            {
                Console.WriteLine("אזהרה: לא הצלחנו למצוא פתרון אפשרי למודל הראשי.");

                // יצירת פתרון "ריק" עם ערך אובייקטיבי גבוה
                return new Solution(new double[0], new ScheduleCollection(), double.MaxValue);
            }

            return result;
        }
        private int CreateNewColumns(MasterModel masterModel, PartialSolution partialSolutions, Solution problemSolutionMM)
        {
            // בדיקה שהפתרון אפשרי
            if (problemSolutionMM == null)
            {
                Console.WriteLine("Master problem infeasible");

                // הוספת קוד חדש: ניסיון ליצור עמודות שיעזרו למצוא פתרון אפשרי
                List<Column> newFeasibilityColumns = GenerateFeasibilityColumns(null); // עובר null כי אין פתרון

                foreach (var column in newFeasibilityColumns)
                {
                    masterModel.AddSchedule(ConvertScheduleFromColumn(column), column.AssignedElevator.Id);
                }

                // אם הוספנו עמודות חדשות, ננסה שוב
                if (newFeasibilityColumns.Count > 0)
                    return 1;

                return -1;
            }

            // בדיקה אם יש עמודות חדשות להוספה
            List<Column> newColumns = GenerateInitialColumns(partialSolutions, problemSolutionMM);
            bool columnsAdded = false;

            foreach (var column in newColumns)
            {
                if (column.ReducedCost < -EPS) // בדיקה אם העלות המופחתת שלילית
                {
                    masterModel.AddSchedule(ConvertScheduleFromColumn(column), column.AssignedElevator.Id);
                    columnsAdded = true;
                }
            }

            return columnsAdded ? 1 : 0;
        }

        private List<Column> GenerateFeasibilityColumns(Solution problemSolutionMM)
        {
            List<Column> columns = new List<Column>();

            // אם אין פתרון LP, אין משמעות לערכים דואליים, אז נחזיר רק עמודות בסיסיות
            double[] requestDuals = problemSolutionMM != null ? problemSolutionMM.GetRequestDuals() : new double[problemInstance.GetUnassignedRequests().Count];
            double[] elevatorDuals = problemSolutionMM != null ? problemSolutionMM.GetElevatorsDuals() : new double[problemInstance.numElevators];

            // יצירת עמודות מיוחדות שעוזרות למצוא פתרון אפשרי
            // לדוגמה, עמודות שמכסות כל בקשה אפשרית עם כל מעלית אפשרית
            for (int e = 0; e < problemInstance.numElevators; e++)
            {
                Elevator elevator = problemInstance.GetElevators()[e];

                foreach (var request in problemInstance.GetUnassignedRequests())
                {
                    Column column = new Column
                    {
                        ServedRequests = new List<Request> { request },
                        Floors = new List<int> { elevator.CurrentFloor, request.StartFloor, request.DestinationFloor },
                        AssignedElevator = elevator
                    };

                    // חישוב עלות
                    column.Cost = CalculateColumnCost(column, elevator);

                    // חישוב עלות מופחתת
                    int requestIndex = problemInstance.GetUnassignedRequests().IndexOf(request);
                    double dualSum = requestIndex >= 0 && requestDuals != null ? requestDuals[requestIndex] : 0;
                    column.ReducedCost = column.Cost - dualSum - (elevatorDuals != null ? elevatorDuals[e] : 0);

                    // נוסיף את העמודה אם העלות המופחתת שלילית או אם אנחנו מחפשים פתרון אפשרי
                    columns.Add(column);
                }

                // אופציה: עמודות עם מספר בקשות יחד
                if (problemInstance.GetUnassignedRequests().Count >= 2)
                {
                    var requests = problemInstance.GetUnassignedRequests();

                    for (int i = 0; i < requests.Count; i++)
                    {
                        for (int j = i + 1; j < requests.Count; j++)
                        {
                            Column column = new Column
                            {
                                ServedRequests = new List<Request> { requests[i], requests[j] },
                                Floors = new List<int> { elevator.CurrentFloor, requests[i].StartFloor,
                                       requests[i].DestinationFloor, requests[j].StartFloor,
                                       requests[j].DestinationFloor },
                                AssignedElevator = elevator
                            };

                            column.Cost = CalculateColumnCost(column, elevator);

                            // חישוב עלות מופחתת
                            double dualSum = 0;
                            foreach (var req in column.ServedRequests)
                            {
                                int reqIndex = problemInstance.GetUnassignedRequests().IndexOf(req);
                                if (reqIndex >= 0 && requestDuals != null)
                                    dualSum += requestDuals[reqIndex];
                            }
                            column.ReducedCost = column.Cost - dualSum - (elevatorDuals != null ? elevatorDuals[e] : 0);

                            columns.Add(column);
                        }
                    }
                }
            }

            return columns;
        }
        private List<Column> GenerateInitialColumns(PartialSolution partialSolutions, Solution problemSolutionMM)
        {
            // כאן נייצר עמודות ראשוניות אפשריות לפתרון
            // במקרה אמיתי, זה יכול להיות מבוסס על היוריסטיקות פשוטות
            List<Column> columns = new List<Column>();

            // אם אין פתרון LP, אין משמעות לערכים דואליים, אז נחזיר רק עמודות בסיסיות
            double[] requestDuals = problemSolutionMM != null ? problemSolutionMM.GetRequestDuals() : new double[problemInstance.GetUnassignedRequests().Count];
            double[] elevatorDuals = problemSolutionMM != null ? problemSolutionMM.GetElevatorsDuals() : new double[problemInstance.numElevators];

            for (int e = 0; e < problemInstance.numElevators; e++)
            {
                Elevator elevator = problemInstance.GetElevators()[e];

                // ליצור עמודה ריקה לכל מעלית
                Column emptyColumn = new Column
                {
                    ServedRequests = new List<Request>(),
                    Floors = new List<int> { elevator.CurrentFloor },
                    Cost = 0,
                    AssignedElevator = elevator
                };

                // חישוב עלות מופחתת לעמודה ריקה - רק השפעת הדואל של המעלית
                emptyColumn.ReducedCost = -1 * (elevatorDuals != null ? elevatorDuals[e] : 0);

                columns.Add(emptyColumn);

                // ליצור עמודה עבור כל בקשה בודדת
                foreach (var request in problemInstance.GetUnassignedRequests())
                {
                    Column singleRequestColumn = new Column
                    {
                        ServedRequests = new List<Request> { request },
                        Floors = new List<int> { elevator.CurrentFloor, request.StartFloor, request.DestinationFloor },
                        AssignedElevator = elevator
                    };

                    // חישוב עלות העמודה
                    singleRequestColumn.Cost = CalculateColumnCost(singleRequestColumn, elevator);

                    // חישוב עלות מופחתת
                    int requestIndex = problemInstance.GetUnassignedRequests().IndexOf(request);
                    double dualSum = requestIndex >= 0 && requestDuals != null ? requestDuals[requestIndex] : 0;
                    singleRequestColumn.ReducedCost = singleRequestColumn.Cost - dualSum - (elevatorDuals != null ? elevatorDuals[e] : 0);

                    columns.Add(singleRequestColumn);
                }
            }

            return columns;
        }

        private double CalculateColumnCost(Column column, Elevator elevator)
        {
            double cost = 0;
            int currentFloor = elevator.CurrentFloor;
            double currentTime = elevator.CurrentTime;

            foreach (var request in column.ServedRequests)
            {
                // זמן נסיעה לקומת הבקשה
                double travelToPickupTime = Constant.CalculateTravelTime(currentFloor, request.StartFloor);
                currentTime += travelToPickupTime;

                // זמן עצירה ואיסוף
                currentTime += Constant.StopTime;

                // זמן נסיעה ליעד
                double travelToDestTime = Constant.CalculateTravelTime(request.StartFloor, request.DestinationFloor);
                currentTime += travelToDestTime;

                // זמן עצירה והורדה
                currentTime += Constant.StopTime;

                // חישוב עלות המתנה ונסיעה
                // חישוב עלות המתנה ונסיעה
                foreach (var call in request.Calls)
                {
                    // זמן ההמתנה מרגע הקריאה עד האיסוף
                    double waitTime = currentTime - call.ReleaseTime.ToOADate() - travelToDestTime - Constant.StopTime;
                    waitTime = Math.Max(0, waitTime);

                    // עלות ההמתנה
                    cost += waitTime * call.WaitCost;

                    // עלות הנסיעה
                    cost += travelToDestTime * call.TravelCost;
                }

                currentFloor = request.DestinationFloor;
            }

            // בדיקת אילוצי קיבולת
            int totalLoaded = elevator.LoadedCalls.Count + column.ServedRequests.Sum(r => r.Calls.Count);
            if (totalLoaded > elevator.Capacity)
            {
                cost += (totalLoaded - elevator.Capacity) * Constant.CapacityPenalty;
            }

            return cost;
        }

      

        private bool IsReducedCostNegative(Solution subProblemSolution)
        {
            // הערך האובייקטיבי כבר מכיל את העלות המופחתת
            return subProblemSolution.ObjectiveValue < -EPS;
        }
        private Column ConvertToColumn(Solution subProblemSolution)
        {
            // המרת פתרון תת-בעיה לעמודה במודל הראשי
            Column column = new Column();

            // הוספת המעליות והבקשות מהפתרון
            List<Schedule> selectedSchedules = subProblemSolution.GetSelectedSchedules();
            if (selectedSchedules.Count > 0)
            {
                Schedule schedule = selectedSchedules[0];
                column.AssignedElevator = problemInstance.GetElevators()[schedule.ElevatorIndex];
                column.ServedRequests = new List<Request>(schedule.ServedRequests);

                // יצירת רשימת קומות על פי עצירות
                column.Floors = new List<int>();
                foreach (var stop in schedule.Stops)
                {
                    column.Floors.Add(stop.Floor);
                }

                // עלות הפתרון
                column.Cost = schedule.TotalCost;
            }

            return column;
        }

        private Schedule ConvertScheduleFromColumn(Column column)
        {
            if (column == null || column.AssignedElevator == null)
                return null;

            Schedule schedule = new Schedule(column.AssignedElevator.Id);
            schedule.ServedRequests = new List<Request>(column.ServedRequests);
            schedule.TotalCost = (float)column.Cost;

            // יצירת עצירות עם חישוב זמני הגעה וכיווני נסיעה
            double currentTime = 0;
            int currentFloor = column.AssignedElevator.CurrentFloor;

            for (int i = 0; i < column.Floors.Count; i++)
            {
                int nextFloor = column.Floors[i];

                // חישוב זמן הנסיעה
                double travelTime = Constant.CalculateTravelTime(currentFloor, nextFloor);
                currentTime += travelTime;

                // קביעת כיוון הנסיעה
                Direction direction = Direction.Idle;
                if (i < column.Floors.Count - 1)
                {
                    direction = nextFloor < column.Floors[i + 1] ? Direction.Up : Direction.Down;
                }

                // יצירת העצירה
                Stop stop = new Stop
                {
                    Floor = nextFloor,
                    ArrivalTime = (float)currentTime,
                    Direction = direction
                };

                schedule.AddStop(stop);

                // עדכון הקומה הנוכחית
                currentFloor = nextFloor;

                // הוספת זמן עצירה
                currentTime += Constant.StopTime;
            }

            return schedule;
        }
        private int LagrangeRelaxation(Solution mmSolution, Solution spSolution, int iteration, int deviceIndex, PartialSolution partialSolutions)
        {
            if (!doLR)
                return 1;

            // מימוש בסיסי של רלקסציית לגראנג'
            double[] currentDuals = mmSolution.GetRequestDuals();
            double stepSize = 2.0 / Math.Sqrt(iteration); // גודל צעד קטן עם הזמן

            // עדכון מכפילי לגראנג'
            for (int i = 0; i < currentDuals.Length; i++)
            {
                // חישוב ה-subgradient עבור האילוץ
                double subgradient = CalculateSubgradient(i, mmSolution, spSolution);

                // עדכון הערך הדואלי
                currentDuals[i] += stepSize * subgradient;

                // הערכים הדואליים צריכים להיות לא-שליליים
                currentDuals[i] = Math.Max(0, currentDuals[i]);
            }

            // בדיקת תנאי עצירה
            if (iteration > 50 || CheckConvergence(mmSolution, spSolution))
            {
                return 1; // סיום הרלקסציה
            }

            return 2; // המשך איטרציות של רלקסציה
        }

        private double CalculateSubgradient(int requestIndex, Solution mmSolution, Solution spSolution)
        {
            // חישוב ה-subgradient לאילוץ בקשה requestIndex
            double primalValue = 0;
            List<Schedule> schedules = mmSolution.GetSelectedSchedules();

            foreach (var schedule in schedules)
            {
                if (schedule.ServedRequestsContains(requestIndex))
                {
                    primalValue += 1; // כל לוח זמנים נבחר מוסיף 1 לערך
                }
            }

            // ההפרש מהערך הנדרש (1.0 - כל בקשה צריכה להיות משויכת פעם אחת)
            return primalValue - 1.0;
        }

        private bool CheckConvergence(Solution mmSolution, Solution spSolution)
        {
            // בדיקה אם יש התכנסות של האלגוריתם
            double gap = Math.Abs(mmSolution.ObjectiveValue - spSolution.ObjectiveValue);
            return gap < 1e-4;
        }
    }

    // מחלקת עזר לייצוג פתרון חלקי במהלך האלגוריתם
    public class PartialSolution
    {
        public List<Request> AssignedRequests { get; set; }
        public Dictionary<int, List<Request>> ElevatorAssignments { get; set; }

        public PartialSolution()
        {
            AssignedRequests = new List<Request>();
            ElevatorAssignments = new Dictionary<int, List<Request>>();
        }

        public void AssignRequestToElevator(Request request, int elevatorIndex)
        {
            AssignedRequests.Add(request);

            if (!ElevatorAssignments.ContainsKey(elevatorIndex))
            {
                ElevatorAssignments[elevatorIndex] = new List<Request>();
            }

            ElevatorAssignments[elevatorIndex].Add(request);
        }
    }

    // מחלקת עזר לייצוג תת-מודל לכל מעלית
    public class SubModel
    {
        private ProblemInstance instance;
        private int elevatorIndex;
        private double[] requestDuals;
        private List<Request> assignedRequests;
        private List<Request> forbiddenRequests;
        private double elevatorDual;

        public SubModel(ProblemInstance instance, int elevatorIndex)
        {
            this.instance = instance;
            this.elevatorIndex = elevatorIndex;
            assignedRequests = new List<Request>();
            forbiddenRequests = new List<Request>();
        }

        public void ChangeCoefficientsInSubModel(double[] requestDuals, PartialSolution partialSolution, int multiplier, int offset)
        {
            // שמירת הערכים הדואליים לשימוש בחישוב עלות מופחתת
            this.requestDuals = new double[requestDuals.Length];
            for (int i = 0; i < requestDuals.Length; i++)
            {
                this.requestDuals[i] = requestDuals[i] * multiplier + offset;
            }

            // שמירת אילוצים לפי בקשות שכבר משויכות
            assignedRequests = new List<Request>();
            if (partialSolution.ElevatorAssignments.ContainsKey(elevatorIndex))
            {
                assignedRequests.AddRange(partialSolution.ElevatorAssignments[elevatorIndex]);
            }
        }

        public Solution Solve(double elevatorDual)
        {
            // שמירת ערך דואלי של המעלית
            this.elevatorDual = elevatorDual;

            // פתרון תת-הבעיה עבור מעלית ספציפית
            PricingProblem pricingProblem = new PricingProblem(
                instance,
                elevatorIndex,
                requestDuals, // שימוש בערכים שנשמרו
                this.elevatorDual, // שימוש בערך הדואלי של המעלית
                5); // מספר לוחות זמנים מקסימלי

            // הוספת אילוצים נוספים לבעיית הפרייסינג
            pricingProblem.SetAssignedRequests(assignedRequests);
            pricingProblem.SetForbiddenRequests(forbiddenRequests);

            List<Schedule> schedules = pricingProblem.GenerateSchedulesWithNegativeReducedCost();

            if (schedules.Count == 0)
                return null;

            ScheduleCollection scheduleCollection = new ScheduleCollection();
            foreach (var schedule in schedules)
            {
                scheduleCollection.Add(schedule);
            }

            // יצירת ערכי המשתנים - בתת-בעיה יש רק משתנה אחד
            double[] values = new double[schedules.Count];
            values[0] = 1.0;

            // חישוב העלות המופחתת
            double reducedCost = schedules[0].TotalCost;
            for (int i = 0; i < instance.GetUnassignedRequests().Count; i++)
            {
                if (schedules[0].ServedRequestsContains(instance.GetUnassignedRequests()[i].Id))
                {
                    reducedCost -= requestDuals[i];
                }
            }
            reducedCost -= this.elevatorDual;

            return new Solution(values, scheduleCollection, reducedCost);
        }

        public void AddAssignedRequest(Request request)
        {
            if (!assignedRequests.Contains(request))
            {
                assignedRequests.Add(request);
            }
        }

        public void AddForbiddenRequest(Request request)
        {
            if (!forbiddenRequests.Contains(request))
            {
                forbiddenRequests.Add(request);
            }
        }
    }
}