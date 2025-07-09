//using Microsoft.VisualBasic;
//using Project;

//namespace Project.Algorithm
//{
//    public class BranchAndPrice
//    {
//        private ProblemInstance problemInstance;
//        private List<SubModel> subModel;
//        private double LB;
//        private int numbNodes;
//        private long timeInMaster;
//        private long timeInSub;
//        private int timesInMaster;
//        private int timesInSub;
//        private long lastTimeInLag;
//        private bool doLR;
//        private const double EPS = 1e-6;

//        public BranchAndPrice(ProblemInstance problemInstance)
//        {
//            this.problemInstance = problemInstance;
//            subModel = new List<SubModel>();
//            numbNodes = 0;
//            timeInMaster = 0;
//            timeInSub = 0;
//            timesInMaster = 0;
//            timesInSub = 0;
//            lastTimeInLag = 0;
//            doLR = false;

//            for (int i = 0; i < problemInstance.numElevators; i++)
//            {
//                subModel.Add(new SubModel(problemInstance, i));
//            }
//        }

//        public Solution Solve()
//        {
//            MasterModel masterModel = new MasterModel(problemInstance);
//            PartialSolution partialSolution = new PartialSolution();

//            return ColumnGeneration(masterModel, partialSolution);
//        }

//        private Solution ColumnGeneration(MasterModel masterModel, PartialSolution partialSolutions)
//        {
//            long startTime;
//            Solution problemSolutionMM = null;
//            Solution problemSolutionSP;
//            int device = -1;
//            int startDevice;
//            int iteration = 0;
//            lastTimeInLag = 0;
//            bool columnsAdded = true;

//            while (columnsAdded && iteration < 1000)
//            {
//                iteration++;
//                columnsAdded = false;

//                device = (device + 1) % problemInstance.numElevators;

//                timesInMaster++;
//                startTime = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
//                problemSolutionMM = masterModel.Solve();
//                timeInMaster += DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond - startTime;

//                if (problemSolutionMM == null)
//                {
//                    List<Column> feasibilityColumns = GenerateFeasibilityColumns(null);

//                    foreach (var column in feasibilityColumns)
//                    {
//                        masterModel.AddSchedule(ConvertScheduleFromColumn(column), column.AssignedElevator.Id);
//                        columnsAdded = true;
//                    }

//                    if (!columnsAdded)
//                        return null;

//                    continue;
//                }

//                int newColumnResult = CreateNewColumns(masterModel, partialSolutions, problemSolutionMM);
//                if (newColumnResult == -1)
//                    return null;
//                if (newColumnResult == 1)
//                {
//                    columnsAdded = true;
//                    continue;
//                }

//                startDevice = device;
//                do
//                {
//                    subModel[device].ChangeCoefficientsInSubModel(
//                        problemSolutionMM.GetRequestDuals(),
//                        partialSolutions,
//                        1,
//                        0);

//                    startTime = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
//                    problemSolutionSP = subModel[device].Solve(problemSolutionMM.GetElevatorsDuals()[device]);
//                    timeInSub += DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond - startTime;
//                    timesInSub++;

//                    if (problemSolutionSP == null)
//                    {
//                        device = (device + 1) % problemInstance.numElevators;
//                        if (device == startDevice)
//                        {
//                            if (numbNodes == 0) LB = problemSolutionMM.ObjectiveValue;
//                            return problemSolutionMM;
//                        }
//                        continue;
//                    }

//                    if (IsReducedCostNegative(problemSolutionSP))
//                    {
//                        Column newColumn = ConvertToColumn(problemSolutionSP);
//                        masterModel.AddSchedule(ConvertScheduleFromColumn(newColumn), device);
//                        columnsAdded = true;
//                        break;
//                    }

//                    device = (device + 1) % problemInstance.numElevators;

//                    if (device == startDevice && !columnsAdded)
//                    {
//                        if (numbNodes == 0) LB = problemSolutionMM.ObjectiveValue;
//                        return problemSolutionMM;
//                    }

//                } while (device != startDevice);

//                if (doLR && columnsAdded)
//                {
//                    int lagrangeResult = LagrangeRelaxation(problemSolutionMM, problemSolutionSP, iteration, device, partialSolutions);
//                    if (lagrangeResult == 0) return null;
//                    if (lagrangeResult == 1) return problemSolutionMM;
//                }
//            }

//            Solution result = problemSolutionMM;

//            if (result == null)
//            {
//                return new Solution(new double[0], new ScheduleCollection(), double.MaxValue);
//            }

//            return result;
//        }

//        private int CreateNewColumns(MasterModel masterModel, PartialSolution partialSolutions, Solution problemSolutionMM)
//        {
//            if (problemSolutionMM == null)
//            {
//                List<Column> newFeasibilityColumns = GenerateFeasibilityColumns(null);

//                foreach (var column in newFeasibilityColumns)
//                {
//                    masterModel.AddSchedule(ConvertScheduleFromColumn(column), column.AssignedElevator.Id);
//                }

//                if (newFeasibilityColumns.Count > 0)
//                    return 1;

//                return -1;
//            }

//            List<Column> newColumns = GenerateInitialColumns(partialSolutions, problemSolutionMM);
//            bool columnsAdded = false;

//            foreach (var column in newColumns)
//            {
//                if (column.ReducedCost < -EPS)
//                {
//                    masterModel.AddSchedule(ConvertScheduleFromColumn(column), column.AssignedElevator.Id);
//                    columnsAdded = true;
//                }
//            }

//            return columnsAdded ? 1 : 0;
//        }

//        private List<Column> GenerateFeasibilityColumns(Solution problemSolutionMM)
//        {
//            List<Column> columns = new List<Column>();

//            double[] requestDuals = problemSolutionMM != null ? problemSolutionMM.GetRequestDuals() : new double[problemInstance.GetUnassignedRequests().Count];
//            double[] elevatorDuals = problemSolutionMM != null ? problemSolutionMM.GetElevatorsDuals() : new double[problemInstance.numElevators];

//            for (int e = 0; e < problemInstance.numElevators; e++)
//            {
//                Elevator elevator = problemInstance.GetElevators()[e];

//                foreach (var request in problemInstance.GetUnassignedRequests())
//                {
//                    List<int> logicalRoute = CreateOptimalRouteSimple(elevator, new List<Request> { request });

//                    Column column = new Column
//                    {
//                        ServedRequests = new List<Request> { request },
//                        Floors = logicalRoute,
//                        AssignedElevator = elevator
//                    };

//                    column.Cost = CalculateColumnCost(column, elevator);

//                    int requestIndex = problemInstance.GetUnassignedRequests().IndexOf(request);
//                    double dualSum = requestIndex >= 0 && requestDuals != null ? requestDuals[requestIndex] : 0;
//                    column.ReducedCost = column.Cost - dualSum - (elevatorDuals != null ? elevatorDuals[e] : 0);

//                    columns.Add(column);
//                }

//                if (problemInstance.GetUnassignedRequests().Count >= 2)
//                {
//                    var requests = problemInstance.GetUnassignedRequests();

//                    for (int i = 0; i < requests.Count; i++)
//                    {
//                        for (int j = i + 1; j < requests.Count; j++)
//                        {
//                            List<int> logicalRoute = CreateOptimalRouteSimple(elevator, new List<Request> { requests[i], requests[j] });

//                            Column column = new Column
//                            {
//                                ServedRequests = new List<Request> { requests[i], requests[j] },
//                                Floors = logicalRoute,
//                                AssignedElevator = elevator
//                            };

//                            column.Cost = CalculateColumnCost(column, elevator);

//                            double dualSum = 0;
//                            foreach (var req in column.ServedRequests)
//                            {
//                                int reqIndex = problemInstance.GetUnassignedRequests().IndexOf(req);
//                                if (reqIndex >= 0 && requestDuals != null)
//                                    dualSum += requestDuals[reqIndex];
//                            }
//                            column.ReducedCost = column.Cost - dualSum - (elevatorDuals != null ? elevatorDuals[e] : 0);

//                            columns.Add(column);
//                        }
//                    }
//                }
//            }

//            return columns;
//        }

//        private List<Column> GenerateInitialColumns(PartialSolution partialSolutions, Solution problemSolutionMM)
//        {
//            List<Column> columns = new List<Column>();

//            double[] requestDuals = problemSolutionMM != null ? problemSolutionMM.GetRequestDuals() : new double[problemInstance.GetUnassignedRequests().Count];
//            double[] elevatorDuals = problemSolutionMM != null ? problemSolutionMM.GetElevatorsDuals() : new double[problemInstance.numElevators];

//            for (int e = 0; e < problemInstance.numElevators; e++)
//            {
//                Elevator elevator = problemInstance.GetElevators()[e];

//                Column emptyColumn = new Column
//                {
//                    ServedRequests = new List<Request>(),
//                    Floors = new List<int> { elevator.CurrentFloor },
//                    Cost = 0,
//                    AssignedElevator = elevator
//                };

//                emptyColumn.ReducedCost = -1 * (elevatorDuals != null ? elevatorDuals[e] : 0);

//                columns.Add(emptyColumn);

//                foreach (var request in problemInstance.GetUnassignedRequests())
//                {
//                    List<int> logicalRoute = CreateOptimalRouteSimple(elevator, new List<Request> { request });

//                    Column singleRequestColumn = new Column
//                    {
//                        ServedRequests = new List<Request> { request },
//                        Floors = logicalRoute,
//                        AssignedElevator = elevator
//                    };

//                    singleRequestColumn.Cost = CalculateColumnCost(singleRequestColumn, elevator);

//                    int requestIndex = problemInstance.GetUnassignedRequests().IndexOf(request);
//                    double dualSum = requestIndex >= 0 && requestDuals != null ? requestDuals[requestIndex] : 0;
//                    singleRequestColumn.ReducedCost = singleRequestColumn.Cost - dualSum - (elevatorDuals != null ? elevatorDuals[e] : 0);

//                    columns.Add(singleRequestColumn);
//                }
//            }

//            return columns;
//        }

//        private double CalculateColumnCost(Column column, Elevator elevator)
//        {
//            double cost = 0;
//            int currentFloor = elevator.CurrentFloor;
//            double currentTime = elevator.CurrentTime;

//            foreach (var request in column.ServedRequests)
//            {
//                double travelToPickupTime = Constant.CalculateTravelTime(currentFloor, request.StartFloor);
//                currentTime += travelToPickupTime;

//                currentTime += Constant.StopTime;

//                double travelToDestTime = Constant.CalculateTravelTime(request.StartFloor, request.DestinationFloor);
//                currentTime += travelToDestTime;

//                currentTime += Constant.StopTime;

//                foreach (var call in request.Calls)
//                {
//                    double waitTime = currentTime - call.ReleaseTime.ToOADate() - travelToDestTime - Constant.StopTime;
//                    waitTime = Math.Max(0, waitTime);

//                    cost += waitTime * call.WaitCost;
//                    cost += travelToDestTime * call.TravelCost;
//                }

//                currentFloor = request.DestinationFloor;
//            }

//            int totalLoaded = elevator.LoadedCalls.Count + column.ServedRequests.Sum(r => r.Calls.Count);
//            if (totalLoaded > elevator.Capacity)
//            {
//                cost += (totalLoaded - elevator.Capacity) * Constant.CapacityPenalty;
//            }

//            return cost;
//        }

//        private bool IsReducedCostNegative(Solution subProblemSolution)
//        {
//            return subProblemSolution.ObjectiveValue < -EPS;
//        }

//        private Column ConvertToColumn(Solution subProblemSolution)
//        {
//            Column column = new Column();

//            List<Schedule> selectedSchedules = subProblemSolution.GetSelectedSchedules();
//            if (selectedSchedules.Count > 0)
//            {
//                Schedule schedule = selectedSchedules[0];
//                column.AssignedElevator = problemInstance.GetElevators()[schedule.ElevatorIndex];
//                column.ServedRequests = new List<Request>(schedule.ServedRequests);

//                column.Floors = new List<int>();
//                foreach (var stop in schedule.Stops)
//                {
//                    column.Floors.Add(stop.Floor);
//                }

//                column.Cost = schedule.TotalCost;
//            }

//            return column;
//        }

//        private List<int> CreateOptimalRouteSimple(Elevator elevator, List<Request> requests)
//        {
//            if (requests.Count == 0)
//                return new List<int> { elevator.CurrentFloor };

//            List<int> route = new List<int> { elevator.CurrentFloor };

//            if (elevator.LoadedCalls != null && elevator.LoadedCalls.Count > 0)
//            {
//                var existingDropFloors = elevator.LoadedCalls
//                    .Select(call => call.DestinationFloor)
//                    .Where(floor => floor != elevator.CurrentFloor)
//                    .Distinct()
//                    .OrderBy(floor => Math.Abs(floor - elevator.CurrentFloor));

//                foreach (var floor in existingDropFloors)
//                {
//                    if (!route.Contains(floor))
//                        route.Add(floor);
//                }
//            }

//            var sortedRequests = requests.OrderBy(r =>
//            {
//                if (elevator.CurrentDirection == Direction.Up && r.StartFloor >= elevator.CurrentFloor)
//                    return Math.Abs(r.StartFloor - elevator.CurrentFloor);
//                else if (elevator.CurrentDirection == Direction.Down && r.StartFloor <= elevator.CurrentFloor)
//                    return Math.Abs(r.StartFloor - elevator.CurrentFloor);
//                else
//                    return 1000 + Math.Abs(r.StartFloor - elevator.CurrentFloor);
//            }).ToList();

//            foreach (var request in sortedRequests)
//            {
//                if (!route.Contains(request.StartFloor))
//                {
//                    int bestPickupIndex = FindBestInsertionIndex(route, request.StartFloor);
//                    route.Insert(bestPickupIndex, request.StartFloor);
//                }

//                if (!route.Contains(request.DestinationFloor))
//                {
//                    int pickupIndex = route.IndexOf(request.StartFloor);
//                    int bestDropIndex = FindBestInsertionIndex(route, request.DestinationFloor, pickupIndex + 1);
//                    route.Insert(bestDropIndex, request.DestinationFloor);
//                }
//            }

//            return route;
//        }

//        private int FindBestInsertionIndex(List<int> route, int newFloor, int minIndex = 1)
//        {
//            if (route.Count <= minIndex)
//                return route.Count;

//            double minCost = double.MaxValue;
//            int bestIndex = route.Count;

//            for (int i = minIndex; i <= route.Count; i++)
//            {
//                double cost = CalculateInsertionCost(route, newFloor, i);
//                if (cost < minCost)
//                {
//                    minCost = cost;
//                    bestIndex = i;
//                }
//            }

//            return bestIndex;
//        }

//        private double CalculateInsertionCost(List<int> route, int newFloor, int insertIndex)
//        {
//            if (insertIndex == 0 || insertIndex > route.Count)
//                return double.MaxValue;

//            if (insertIndex == route.Count)
//            {
//                return Math.Abs(newFloor - route[route.Count - 1]);
//            }

//            int prevFloor = route[insertIndex - 1];
//            int nextFloor = route[insertIndex];

//            double originalCost = Math.Abs(nextFloor - prevFloor);
//            double newCost = Math.Abs(newFloor - prevFloor) + Math.Abs(nextFloor - newFloor);

//            return newCost - originalCost;
//        }

//        private Schedule ConvertScheduleFromColumn(Column column)
//        {
//            if (column == null || column.AssignedElevator == null)
//                return null;

//            Schedule schedule = new Schedule(column.AssignedElevator.Id);
//            schedule.ServedRequests = new List<Request>(column.ServedRequests);
//            schedule.TotalCost = (float)column.Cost;

//            double currentTime = 0;
//            int currentFloor = column.AssignedElevator.CurrentFloor;

//            for (int i = 0; i < column.Floors.Count; i++)
//            {
//                int nextFloor = column.Floors[i];

//                double travelTime = Constant.CalculateTravelTime(currentFloor, nextFloor);
//                currentTime += travelTime;

//                Direction direction = Direction.Idle;
//                if (i < column.Floors.Count - 1)
//                {
//                    direction = nextFloor < column.Floors[i + 1] ? Direction.Up : Direction.Down;
//                }

//                Stop stop = new Stop
//                {
//                    Floor = nextFloor,
//                    ArrivalTime = (float)currentTime,
//                    Direction = direction
//                };

//                foreach (var request in column.ServedRequests)
//                {
//                    if (request.StartFloor == nextFloor)
//                    {
//                        stop.AddPickup(request);
//                    }
//                    if (request.DestinationFloor == nextFloor)
//                    {
//                        foreach (var call in request.Calls)
//                        {
//                            stop.AddDrop(call);
//                        }
//                    }
//                }

//                schedule.AddStop(stop);

//                currentFloor = nextFloor;
//                currentTime += Constant.StopTime;
//            }

//            return schedule;
//        }

//        private int LagrangeRelaxation(Solution mmSolution, Solution spSolution, int iteration, int deviceIndex, PartialSolution partialSolutions)
//        {
//            if (!doLR)
//                return 1;

//            double[] currentDuals = mmSolution.GetRequestDuals();
//            double stepSize = 2.0 / Math.Sqrt(iteration);

//            for (int i = 0; i < currentDuals.Length; i++)
//            {
//                double subgradient = CalculateSubgradient(i, mmSolution, spSolution);
//                currentDuals[i] += stepSize * subgradient;
//                currentDuals[i] = Math.Max(0, currentDuals[i]);
//            }

//            if (iteration > 50 || CheckConvergence(mmSolution, spSolution))
//            {
//                return 1;
//            }

//            return 2;
//        }

//        private double CalculateSubgradient(int requestIndex, Solution mmSolution, Solution spSolution)
//        {
//            double primalValue = 0;
//            List<Schedule> schedules = mmSolution.GetSelectedSchedules();

//            foreach (var schedule in schedules)
//            {
//                if (schedule.ServedRequestsContains(requestIndex))
//                {
//                    primalValue += 1;
//                }
//            }

//            return primalValue - 1.0;
//        }

//        private bool CheckConvergence(Solution mmSolution, Solution spSolution)
//        {
//            double gap = Math.Abs(mmSolution.ObjectiveValue - spSolution.ObjectiveValue);
//            return gap < 1e-4;
//        }
//    }

//    public class PartialSolution
//    {
//        public List<Request> AssignedRequests { get; set; }
//        public Dictionary<int, List<Request>> ElevatorAssignments { get; set; }

//        public PartialSolution()
//        {
//            AssignedRequests = new List<Request>();
//            ElevatorAssignments = new Dictionary<int, List<Request>>();
//        }

//        public void AssignRequestToElevator(Request request, int elevatorIndex)
//        {
//            AssignedRequests.Add(request);

//            if (!ElevatorAssignments.ContainsKey(elevatorIndex))
//            {
//                ElevatorAssignments[elevatorIndex] = new List<Request>();
//            }

//            ElevatorAssignments[elevatorIndex].Add(request);
//        }
//    }

//    public class SubModel
//    {
//        private ProblemInstance instance;
//        private int elevatorIndex;
//        private double[] requestDuals;
//        private List<Request> assignedRequests;
//        private List<Request> forbiddenRequests;
//        private double elevatorDual;

//        public SubModel(ProblemInstance instance, int elevatorIndex)
//        {
//            this.instance = instance;
//            this.elevatorIndex = elevatorIndex;
//            assignedRequests = new List<Request>();
//            forbiddenRequests = new List<Request>();
//        }

//        public void ChangeCoefficientsInSubModel(double[] requestDuals, PartialSolution partialSolution, int multiplier, int offset)
//        {
//            this.requestDuals = new double[requestDuals.Length];
//            for (int i = 0; i < requestDuals.Length; i++)
//            {
//                this.requestDuals[i] = requestDuals[i] * multiplier + offset;
//            }

//            assignedRequests = new List<Request>();
//            if (partialSolution.ElevatorAssignments.ContainsKey(elevatorIndex))
//            {
//                assignedRequests.AddRange(partialSolution.ElevatorAssignments[elevatorIndex]);
//            }
//        }

//        public Solution Solve(double elevatorDual)
//        {
//            this.elevatorDual = elevatorDual;

//            PricingProblem pricingProblem = new PricingProblem(
//                instance,
//                elevatorIndex,
//                requestDuals,
//                this.elevatorDual,
//                5);

//            pricingProblem.SetAssignedRequests(assignedRequests);
//            pricingProblem.SetForbiddenRequests(forbiddenRequests);

//            List<Schedule> schedules = pricingProblem.GenerateSchedulesWithNegativeReducedCost();

//            if (schedules.Count == 0)
//                return null;

//            ScheduleCollection scheduleCollection = new ScheduleCollection();
//            foreach (var schedule in schedules)
//            {
//                scheduleCollection.Add(schedule);
//            }

//            double[] values = new double[schedules.Count];
//            values[0] = 1.0;

//            double reducedCost = schedules[0].TotalCost;
//            for (int i = 0; i < instance.GetUnassignedRequests().Count; i++)
//            {
//                if (schedules[0].ServedRequestsContains(instance.GetUnassignedRequests()[i].Id))
//                {
//                    reducedCost -= requestDuals[i];
//                }
//            }
//            reducedCost -= this.elevatorDual;

//            return new Solution(values, scheduleCollection, reducedCost);
//        }

//        public void AddAssignedRequest(Request request)
//        {
//            if (!assignedRequests.Contains(request))
//            {
//                assignedRequests.Add(request);
//            }
//        }

//        public void AddForbiddenRequest(Request request)
//        {
//            if (!forbiddenRequests.Contains(request))
//            {
//                forbiddenRequests.Add(request);
//            }
//        }
//    }
//}