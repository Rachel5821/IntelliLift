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
        private List<Schedule> oldSchedules;
        private int K;
        private int maxBranchAndPriceIterations;
        private int maxSchedulesPerIteration;
        //////private double optimalityGap;
        private bool useOldSchedules;

        public ExactReplan(ProblemInstance instance, int k = 100,
                         int maxBranchAndPriceIterations = 1000,
                         int maxSchedulesPerIteration = 5,
                         double optimalityGap = 0.01,
                         bool useOldSchedules = true)
        {
            this.instance = instance;
            schedules = new ScheduleCollection();
            oldSchedules = new List<Schedule>();
            K = k;
            this.maxBranchAndPriceIterations = maxBranchAndPriceIterations;
            this.maxSchedulesPerIteration = maxSchedulesPerIteration;
            //this.optimalityGap = optimalityGap;
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

        private float CalculateScheduleCost(Schedule schedule, Elevator elevator, Request request)//per Request
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

        private Solution SolveByBranchAndPrice()//כאן מתנהל התור של הצמתים
        {
            Solution bestSolution = null;
            double bestObjectiveValue = double.MaxValue;

            MasterModel rootModel = new MasterModel(instance);
            InitializeWithHeuristicSolution(rootModel);
            BranchAndPriceNode rootNode = new BranchAndPriceNode(rootModel);

            PriorityQueue<BranchAndPriceNode, double> nodeQueue = new PriorityQueue<BranchAndPriceNode, double>();
            nodeQueue.Enqueue(rootNode, 0);

            int iterations = 0;
            DateTime startTime = DateTime.Now;

            while (nodeQueue.Count > 0 && iterations < maxBranchAndPriceIterations)
            {
                iterations++;

                if ((DateTime.Now - startTime).TotalSeconds > 60)
                {
                    break;
                }

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
                    childNode.LowerBound = childNode.Model.Solve()?.ObjectiveValue ?? double.MaxValue;
                    if (childNode.LowerBound < bestObjectiveValue)
                    {
                        nodeQueue.Enqueue(childNode, childNode.LowerBound);
                    }
                }
            }

            if (bestSolution == null)
            {
                return GetHeuristicSolution();
            }

            return bestSolution;
        }

        private Solution GetHeuristicSolution()
        {
            MasterModel model = new MasterModel(instance);
            InitializeWithHeuristicSolution(model);
            return model.Solve();
        }

        private void InitializeWithHeuristicSolution(MasterModel masterModel)
        {
            List<Request> unassignedRequests = instance.GetUnassignedRequests();
            List<Elevator> elevators = instance.GetElevators();

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

            for (int e = 0; e < elevators.Count; e++)
            {
                for (int r = 0; r < unassignedRequests.Count; r++)
                {
                    Schedule singleRequestSchedule = CreateScheduleForSingleRequest(elevators[e], e, unassignedRequests[r]);
                    masterModel.AddSchedule(singleRequestSchedule, e);
                }
            }
            if (unassignedRequests.Count >= 2)
{
    // schedule לכל מעלית עם 2 בקשות
    for (int e = 0; e < elevators.Count && e * 2 < unassignedRequests.Count; e++)
    {
        if ((e * 2 + 1) < unassignedRequests.Count)
        {
            var testSchedule = CreateScheduleForSingleRequest(elevators[e], e, unassignedRequests[e * 2]);
            testSchedule.ServedRequests.Add(unassignedRequests[e * 2 + 1]);
            masterModel.AddSchedule(testSchedule, e);
        }
    }
}
            //??כאן נוצרות לי בקשות ללא stops!
            //if (unassignedRequests.Count >= 2)
            //{
            //    for (int e = 0; e < elevators.Count; e++)
            //    {
            //        for (int r1 = 0; r1 < unassignedRequests.Count; r1++)
            //        {
            //            for (int r2 = r1 + 1; r2 < unassignedRequests.Count; r2++)
            //            {
            //                List<Request> twoRequests = new List<Request> { unassignedRequests[r1], unassignedRequests[r2] };
            //                Schedule twoRequestSchedule = CreateHeuristicSchedule(elevators[e], e, twoRequests);
            //                masterModel.AddSchedule(twoRequestSchedule, e);
            //            }
            //        }
            //    }
            //}
        }

        private Schedule CreateScheduleForSingleRequest(Elevator elevator, int elevatorIndex, Request request)
        {
            Schedule schedule = new Schedule(elevatorIndex);

            Stop initialStop = new Stop
            {
                Floor = elevator.CurrentFloor,
                Direction = elevator.CurrentDirection,
                ArrivalTime = 0
            };
            schedule.AddStop(initialStop);

            float pickupTime = (float)CalculateTravelTime(elevator.CurrentFloor, request.StartFloor);

            Direction pickupDirection = request.StartFloor < request.DestinationFloor ? Direction.Up : Direction.Down;

            Stop pickupStop = new Stop
            {
                Floor = request.StartFloor,
                Direction = pickupDirection,
                ArrivalTime = pickupTime
            };
            pickupStop.AddPickup(request);
            schedule.AddStop(pickupStop);

            float travelTime = (float)CalculateTravelTime(request.StartFloor, request.DestinationFloor);
            float dropTime = pickupTime + travelTime + (float)Constant.StopTime;

            Stop dropStop = new Stop
            {
                Floor = request.DestinationFloor,
                Direction = Direction.Idle,
                ArrivalTime = dropTime
            };

            foreach (Call call in request.Calls)
            {
                dropStop.AddDrop(call);
            }
            schedule.AddStop(dropStop);

            schedule.ServedRequests.Add(request);

            float cost = CalculateScheduleCost(schedule, elevator, request);
            schedule.TotalCost = cost;

            return schedule;
        }

        //private Schedule CreateHeuristicSchedule(Elevator elevator, int elevatorIndex, List<Request> requests)
        //{
        //    Schedule schedule = new Schedule(elevatorIndex);

        //    float totalCost = 0;
        //    foreach (var request in requests)
        //    {
        //        totalCost += CalculateRequestCost(elevator, request);
        //        schedule.ServedRequests.Add(request);
        //    }

        //    schedule.TotalCost = totalCost;
        //    return schedule;
        //}

        //private float CalculateRequestCost(Elevator elevator, Request request)
        //{
        //    double waitCost = 0;
        //    double travelCost = 0;

        //    foreach (var call in request.Calls)
        //    {
        //        waitCost += call.WaitCost * 10;
        //        travelCost += call.TravelCost * 15;
        //    }

        //    return (float)(waitCost + travelCost);
        //}
        private Solution SolveNodeWithColumnGeneration(BranchAndPriceNode node)
        {
            MasterModel model = node.Model;
            Solution bestSolution = null;
            double bestObjectiveValue = double.MaxValue;

            int iterations = 0;
            bool columnsAdded = true;
            Console.WriteLine("=== Starting Column Generation ===");

            while ((columnsAdded && iterations < K) || iterations == 0)
            {
                iterations++;

                Solution lpSolution = model.Solve();
                if (lpSolution == null)
                {
                    return null;
                }

                if (lpSolution.ObjectiveValue < bestObjectiveValue)
                {
                    bestSolution = lpSolution;
                    bestObjectiveValue = lpSolution.ObjectiveValue;
                }

                if (lpSolution.isIntegral&&iterations==0)
                {
                    return lpSolution;
                }

                columnsAdded = GenerateNewColumns(model, lpSolution);

                if (!columnsAdded)
                {
                    return lpSolution;
                }
            }

            return bestSolution;
        }

        private bool GenerateNewColumns(MasterModel model, Solution lpSolution)
        {
            int columnsBefore = model.Schedules.Count;  // ← הוסף זה

            bool columnsAdded = false;

            if (useOldSchedules)
            {
                double[] requestDuals = lpSolution.GetRequestDuals();
                double[] elevatorDuals = lpSolution.GetElevatorsDuals();

                foreach (var schedule in oldSchedules)
                {
                    double reducedCost = CalculateReducedCost(schedule, requestDuals, elevatorDuals);

                    if (reducedCost < -1e-6)
                    {
                        model.AddSchedule(schedule, schedule.ElevatorIndex);
                        columnsAdded = true;
                    }
                }

                if (columnsAdded)
                {
                    return true;
                }
            }

            for (int e = 0; e < instance.GetElevators().Count; e++)
            {
                double[] requestDuals = lpSolution.GetRequestDuals();
                double[] elevatorDuals = lpSolution.GetElevatorsDuals();

                PricingProblem pricingProblem = new PricingProblem(
                    instance,
                    e,
                    requestDuals,
                    elevatorDuals[e],
                    maxSchedulesPerIteration);

                List<Schedule> newSchedules = pricingProblem.GenerateSchedulesWithNegativeReducedCost();

                foreach (var schedule in newSchedules)
                {
                    model.AddSchedule(schedule, e);
                    columnsAdded = true;

                    if (useOldSchedules)
                    {
                        oldSchedules.Add(schedule);
                    }
                }
            }
            int columnsAfter = model.Schedules.Count;  
            Console.WriteLine($"🆕 Added {columnsAfter - columnsBefore} new columns (Total: {columnsAfter})");

            return columnsAdded;
        }

        public double CalculateReducedCost(Schedule schedule, double[] requestDuals, double[] elevatorDuals)
        {
            if (schedule == null)
            {
                throw new ArgumentNullException(nameof(schedule));
            }

            double cost = schedule.TotalCost;
            double dualSum = 0;

            List<Request> unassignedRequests = instance.GetUnassignedRequests();
            foreach (var request in schedule.ServedRequests)
            {
                int requestIndex = unassignedRequests.IndexOf(request);
                if (requestIndex >= 0 && requestIndex < requestDuals.Length)
                {
                    dualSum += requestDuals[requestIndex];
                }
            }

            return cost - dualSum - elevatorDuals[schedule.ElevatorIndex];
        }

        private List<BranchAndPriceNode> Branch(BranchAndPriceNode parentNode, Solution solution)
        {
            List<BranchAndPriceNode> childNodes = new List<BranchAndPriceNode>();

            (int requestIndex, List<int> elevatorGroup1, List<int> elevatorGroup2) = FindBranchingDecision(solution);

            if (requestIndex >= 0)
            {
                MasterModel leftModel = new MasterModel(parentNode.Model);
                leftModel.AddBranchingConstraint(requestIndex, elevatorGroup1, true);
                leftModel.AddBranchingConstraint(requestIndex, elevatorGroup2, false);
                BranchAndPriceNode leftNode = new BranchAndPriceNode(leftModel);
                childNodes.Add(leftNode);

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

            Dictionary<int, Dictionary<int, double>> requestToElevators = new Dictionary<int, Dictionary<int, double>>();

            for (int i = 0; i < unassignedRequests.Count; i++)
            {
                requestToElevators[i] = new Dictionary<int, double>();
            }

            for (int i = 0; i < selectedSchedules.Count; i++)
            {
                Schedule schedule = selectedSchedules[i];
                int elevatorIndex = schedule.ElevatorIndex;
                double value = values[i];

                if (value < 1e-6) continue;

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
                    continue;
                }

                var sortedElevators = elevatorValues.OrderByDescending(e => e.Value).ToList();

                double sum1 = 0;
                double sum2 = 0;

                List<int> group1 = new List<int>();
                List<int> group2 = new List<int>();

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

                double score = Math.Abs(0.5 - sum1) + Math.Abs(0.5 - sum2);

                if (score < bestScore && sum1 > 0 && sum1 < 1 && sum2 > 0 && sum2 < 1)
                {
                    bestScore = score;
                    bestRequestIndex = requestIndex;
                    bestGroup1 = group1;
                    bestGroup2 = group2;
                }
            }

            return (bestRequestIndex, bestGroup1, bestGroup2);
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