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
            ElevatorGroup = new List<int>(elevatorGroup);
            IsAssigned = isAssigned;
        }
    }

    public class MasterModel
    {
        private Cplex cplex;
        private ProblemInstance instance;
        private IObjective objective;
        private List<INumVar> scheduleVars;

        private IRange[] requestConstraints;
        private IRange[] elevatorConstraints;
        private IRange[] elevatorLoadConstraints;
        private List<IRange> branchingConstraints;
        private Dictionary<IRange, BranchingConstraintInfo> constraintInfoMap;
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
                int maxRequestsPerElevator = instance.GetUnassignedRequests().Count;

                for (int e = 0; e < instance.numElevators; e++)
                {
                    elevatorLoadConstraints[e] = cplex.AddRange(0.0, maxRequestsPerElevator);
                }
                cplex.SetParam(Cplex.Param.RootAlgorithm, Cplex.Algorithm.Primal);
            }
            catch (ILOG.Concert.Exception ex)
            {
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
                var elevators = instance.GetElevators();
                if (elevatorIndex < elevators.Count)
                {
                    Elevator elevator = elevators[elevatorIndex];
                    List<Request> allRequests = instance.GetUnassignedRequests();

                    List<Request> myRequests = SelectBestRequestsForElevator(elevator, allRequests, elevatorIndex);

                    if (myRequests.Count == 0)
                    {
                        return;
                    }

                    var route = CreateOptimalRouteSimple(elevator, myRequests);
                    Schedule fallbackSchedule = ConvertRouteToSchedule(route, myRequests, elevatorIndex);

                    if (fallbackSchedule != null)
                    {
                        schedule = fallbackSchedule;
                    }
                    else
                    {
                        return;
                    }
                }
            }

            try
            {
                schedules.Add(schedule);
                ILOG.Concert.Column column = cplex.Column(objective, schedule.TotalCost);
                column = column.And(cplex.Column(elevatorConstraints[elevatorIndex], 1.0));

                var unassignedRequests = instance.GetUnassignedRequests();

                for (int i = 0; i < unassignedRequests.Count; i++)
                {
                    var request = unassignedRequests[i];
                    if (schedule.ServedRequestsContains(request.Id))
                    {
                        column = column.And(cplex.Column(requestConstraints[i], 1.0));
                    }
                }

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

                int numRequestsInSchedule = schedule.ServedRequests.Count;
                if (numRequestsInSchedule > 0)
                {
                    column = column.And(cplex.Column(elevatorLoadConstraints[elevatorIndex], numRequestsInSchedule));
                }

                INumVar var = cplex.NumVar(column, 0, 1, NumVarType.Float);
                scheduleVars.Add(var);
            }
            catch (ILOG.Concert.Exception ex)
            {
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

            if (!assignmentsCalculated)
            {
                CalculateOptimalAssignments(allRequests);
                assignmentsCalculated = true;
            }

            if (assignmentsForCurrentBatch.ContainsKey(elevatorIndex))
            {
                var myRequests = assignmentsForCurrentBatch[elevatorIndex];
                return myRequests;
            }

            return new List<Request>();
        }

        private void CalculateOptimalAssignments(List<Request> allRequests)
        {
            assignmentsForCurrentBatch.Clear();

            var elevators = instance.GetElevators();
            int numElevators = elevators.Count;
            int requestsPerElevator = 2;

            for (int i = 0; i < numElevators; i++)
            {
                assignmentsForCurrentBatch[i] = new List<Request>();
            }

            var elevatorPreferences = new List<(int elevatorIndex, List<(Request request, double score)> rankedRequests)>();

            for (int i = 0; i < numElevators; i++)
            {
                var elevator = elevators[i];
                var rankedRequests = allRequests
                    .Select(request => (
                        request: request,
                        score: CalculateRequestScore(elevator, request, i, numElevators)
                    ))
                    .OrderBy(x => x.score)
                    .ToList();

                elevatorPreferences.Add((i, rankedRequests));
            }

            var takenRequests = new HashSet<int>();

            foreach (var (elevatorIndex, rankedRequests) in elevatorPreferences)
            {
                foreach (var (request, score) in rankedRequests)
                {
                    if (assignmentsForCurrentBatch[elevatorIndex].Count >= requestsPerElevator) break;
                    if (takenRequests.Contains(request.Id)) continue;

                    bool wantedByOthers = elevatorPreferences
                        .Where(other => other.elevatorIndex != elevatorIndex)
                        .Any(other => other.rankedRequests.Take(requestsPerElevator * 2)
                                      .Any(choice => choice.request.Id == request.Id));

                    if (!wantedByOthers)
                    {
                        assignmentsForCurrentBatch[elevatorIndex].Add(request);
                        takenRequests.Add(request.Id);
                    }
                }
            }

            for (int priority = 0; priority < numElevators; priority++)
            {
                var (elevatorIndex, rankedRequests) = elevatorPreferences[priority];

                foreach (var (request, score) in rankedRequests)
                {
                    if (assignmentsForCurrentBatch[elevatorIndex].Count >= requestsPerElevator) break;
                    if (takenRequests.Contains(request.Id)) continue;

                    assignmentsForCurrentBatch[elevatorIndex].Add(request);
                    takenRequests.Add(request.Id);
                }
            }
        }

        private double CalculateRequestScore(Elevator elevator, Request request, int elevatorIndex, int numElevators)
        {
            double score = 0;

            double distanceScore = Math.Abs(request.StartFloor - elevator.CurrentFloor);

            double directionScore = 0;
            if (elevator.CurrentDirection == Direction.Up && request.StartFloor > elevator.CurrentFloor)
                directionScore = 0;
            else if (elevator.CurrentDirection == Direction.Down && request.StartFloor < elevator.CurrentFloor)
                directionScore = 0;
            else if (elevator.CurrentDirection == Direction.Idle)
                directionScore = 5;
            else
                directionScore = 20;

            Direction requestDirection = request.StartFloor < request.DestinationFloor ? Direction.Up : Direction.Down;
            double requestDirectionScore = 0;
            if (elevator.CurrentDirection == requestDirection || elevator.CurrentDirection == Direction.Idle)
                requestDirectionScore = 0;
            else
                requestDirectionScore = 10;

            double distributionScore = (request.Id % numElevators == elevatorIndex) ? 0 : 3;

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
            int currentFloor = route[0];

            foreach (int floor in route)
            {
                float travelTime = (float)CalculateTravelTime(currentFloor, floor);
                currentTime += travelTime;

                Stop stop = new Stop
                {
                    Floor = floor,
                    ArrivalTime = currentTime,
                    Direction = Direction.Idle
                };

                foreach (var request in requests)
                {
                    if (request.StartFloor == floor)
                    {
                        stop.AddPickup(request);
                        stop.Direction = request.StartFloor < request.DestinationFloor ? Direction.Up : Direction.Down;
                    }

                    if (request.DestinationFloor == floor)
                    {
                        foreach (var call in request.Calls)
                        {
                            stop.AddDrop(call);
                        }
                    }
                }

                if (stop.Pickups.Count > 0 || stop.Drops.Count > 0 || floor == route[0])
                {
                    schedule.AddStop(stop);
                }

                currentFloor = floor;
                currentTime += (float)Constant.StopTime;
            }

            foreach (var request in requests)
            {
                schedule.ServedRequests.Add(request);
            }

            schedule.TotalCost = CalculateTotalCost(schedule);

            return schedule;
        }

        private double CalculateTravelTime(int fromFloor, int toFloor)
        {
            return Math.Abs(toFloor - fromFloor) * 2.5;
        }

        private float CalculateTotalCost(Schedule schedule)
        {
            if (schedule.Stops == null || schedule.Stops.Count <= 1)
                return 0;

            float totalCost = 0;
            for (int i = 0; i < schedule.Stops.Count - 1; i++)
            {
                int distance = Math.Abs(schedule.Stops[i + 1].Floor - schedule.Stops[i].Floor);
                totalCost += distance * 15;
            }

            totalCost += (schedule.Stops.Count - 1) * 10;

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

                BranchingConstraintInfo constraintInfo = new BranchingConstraintInfo(requestIndex, elevatorGroup, assign);
                constraintInfoMap[constraint] = constraintInfo;

                branchingConstraints.Add(constraint);
            }
            catch (ILOG.Concert.Exception ex)
            {
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
            }
        }
    }
}