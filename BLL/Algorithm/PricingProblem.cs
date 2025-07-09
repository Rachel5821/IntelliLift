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

        public static void SaveOldSchedules(List<Schedule> schedules)
        {
            oldSchedules.Clear();
            if (schedules != null)
            {
                oldSchedules.AddRange(schedules);
            }
        }

        public List<Schedule> GenerateSchedulesWithNegativeReducedCost()
        {
            List<Schedule> resultSchedules = new List<Schedule>();
            double threshold = -1.0e-6;

            if (false && useOldSchedules && TryPricingOldSchedules(resultSchedules, threshold))
            {
                if (resultSchedules.Count >= maxSchedules)
                {
                    return resultSchedules;
                }
            }

            List<Request> allowedRequests = GetAllowedUnassignedRequests();
            if (allowedRequests.Count == 1)
            {
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

            List<Schedule> branchAndBoundResults = RunBranchAndBound(threshold);

            foreach (var schedule in branchAndBoundResults)
            {
                resultSchedules.Add(schedule);
                SaveScheduleForNextRun(schedule);

                if (resultSchedules.Count >= maxSchedules)
                    break;
            }

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

        private bool TryPricingOldSchedules(List<Schedule> resultSchedules, double threshold)
        {
            if (!useOldSchedules || oldSchedules.Count == 0)
                return false;

            bool foundSchedules = false;

            foreach (var oldSchedule in oldSchedules)
            {
                if (oldSchedule.ElevatorIndex != elevatorIndex)
                    continue;

                double reducedCost = CalculateReducedCostSimple(oldSchedule);

                if (reducedCost < threshold)
                {
                    resultSchedules.Add(oldSchedule);
                    foundSchedules = true;

                    if (resultSchedules.Count >= maxSchedules)
                        break;
                }
            }

            return foundSchedules;
        }

        private Schedule SolveSingleRequestCaseOptimal(Request singleRequest)
        {
            Schedule baseSchedule = CreateBaseScheduleWithAssignedRequests();

            List<Schedule> candidates = new List<Schedule>();

            Schedule startOption = InsertRequestAtStart(baseSchedule, singleRequest);
            if (startOption != null)
                candidates.Add(startOption);

            for (int i = 0; i <= baseSchedule.Stops.Count; i++)
            {
                Schedule insertOption = InsertRequestAfterStop(baseSchedule, singleRequest, i);
                if (insertOption != null)
                    candidates.Add(insertOption);
            }

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

        private Schedule InsertRequestAtStart(Schedule baseSchedule, Request request)
        {
            Schedule newSchedule = new Schedule(elevatorIndex);

            float currentTime = (float)elevator.CurrentTime;
            int currentFloor = elevator.CurrentFloor;

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

        private Schedule InsertRequestAfterStop(Schedule baseSchedule, Request request, int afterStopIndex)
        {
            Schedule newSchedule = new Schedule(elevatorIndex);

            float currentTime = (float)elevator.CurrentTime;
            int currentFloor = elevator.CurrentFloor;

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

        private List<Schedule> RunBranchAndBound(double threshold)
        {
            List<Schedule> M = new List<Schedule>();
            PriorityQueue<PricingNode, double> Q = new PriorityQueue<PricingNode, double>();
            double θ = threshold;

            DateTime startTime = DateTime.Now;
            int maxIterations = 100;
            int maxSeconds = 3;
            int maxQueueSize = 50;

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

                if ((DateTime.Now - startTime).TotalSeconds > maxSeconds)
                {
                    break;
                }

                if (Q.Count > maxQueueSize)
                {
                    break;
                }

                PricingNode v = Q.Dequeue();

                if (v.IsLast())
                {
                    Schedule schedule = v.GetSchedule();

                    double reducedCost = CalculateReducedCost(schedule, requestDuals, requestDuals);

                    if (reducedCost < θ)
                    {
                        M.Add(schedule);
                        if (M.Count >= maxSchedules) break;
                    }
                    continue;
                }

                List<PricingNode> N = v.Branch();

                if (N.Count > 5)
                {
                    N = N.Take(5).ToList();
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

            return M;
        }

        private List<PricingNode> CreateRootNodes()
        {
            List<PricingNode> rootNodes = new List<PricingNode>();

            HashSet<int> admissibleFloors = GetAdmissibleFloorsForFirstStop();
            List<Request> allowedRequests = GetAllowedUnassignedRequests();

            foreach (int floor in admissibleFloors)
            {
                Schedule Sr = CreateBaseScheduleWithFirstStopAt(floor);

                PricingNode rootNode = new PricingNode(
                    currentFloor: floor,
                    currentTime: Sr.Stops.LastOrDefault()?.ArrivalTime ?? elevator.CurrentTime,
                    currentLoad: CalculateLoadAfterSchedule(Sr),
                    servedAssignedRequests: new HashSet<Request>(assignedRequests),
                    unservedAssignedRequests: new List<Request>(),
                    servedOptionalRequests: new HashSet<Request>(),
                    unservedOptionalRequests: new List<Request>(allowedRequests),
                    currentSchedule: Sr,
                    elevatorCapacity: elevator.Capacity,
                    maxFloors: instance.numFloors
                );

                rootNodes.Add(rootNode);
            }

            return rootNodes;
        }

        private Schedule CreateBaseScheduleWithFirstStopAt(int firstFloor)
        {
            Schedule schedule = new Schedule(elevatorIndex);

            float currentTime = (float)elevator.CurrentTime;
            int currentFloor = elevator.CurrentFloor;

            if (firstFloor != currentFloor)
            {
                float travelTime = (float)CalculateTravelTime(currentFloor, firstFloor);
                currentTime += travelTime;
            }

            Stop firstStop = new Stop
            {
                Floor = firstFloor,
                ArrivalTime = currentTime,
                Direction = DetermineInitialDirection(firstFloor)
            };
            schedule.AddStop(firstStop);
            currentFloor = firstFloor;
            currentTime += (float)Constant.StopTime;

            var remainingCalls = new List<Call>(elevator.LoadedCalls ?? new List<Call>());

            var dropsAtFirstStop = remainingCalls.Where(call => call.DestinationFloor == firstFloor).ToList();
            foreach (var call in dropsAtFirstStop)
            {
                firstStop.AddDrop(call);
                remainingCalls.Remove(call);
            }

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

            foreach (var request in assignedRequests)
            {
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

        private double CalculateLowerBound(PricingNode v)
        {
            double servedCost = v.GetSchedule().TotalCost;

            double servedDualSum = 0;
            foreach (var request in v.ServedOptionalRequests)
            {
                int requestIndex = unassignedRequests.IndexOf(request);
                if (requestIndex >= 0 && requestIndex < requestDuals.Length)
                {
                    servedDualSum += requestDuals[requestIndex];
                }
            }

            double additionalCost = 0;
            foreach (var request in v.UnservedAssignedRequests)
            {
                double requestCost = EstimateMinimalRequestCost(v, request);
                additionalCost += requestCost;
            }

            double optionalCost = 0;
            foreach (var request in v.UnservedOptionalRequests)
            {
                double requestCost = EstimateMinimalRequestCost(v, request);
                int requestIndex = unassignedRequests.IndexOf(request);
                double πρ = (requestIndex >= 0 && requestIndex < requestDuals.Length) ?
                    requestDuals[requestIndex] : 0;

                if (πρ > requestCost)
                {
                    optionalCost += requestCost - πρ;
                }
            }

            double result = servedCost - servedDualSum + additionalCost + optionalCost - elevatorDual;

            return result;
        }

        private double EstimateMinimalRequestCost(PricingNode v, Request ρ)
        {
            double tPlus = CalculateEarliestPickupTime(v, ρ);

            double requestCost = 0;
            foreach (var c in ρ.Calls)
            {
                double tMinus = CalculateEarliestDropTime(v, ρ, c, tPlus);

                double waitTime = Math.Max(0, tPlus - c.ReleaseTime.ToOADate());
                double travelTime = tMinus - tPlus;

                requestCost += c.WaitCost * waitTime + c.TravelCost * travelTime;
            }

            requestCost += CalculateCapacityPenalty(v, ρ);

            return requestCost;
        }

        private double CalculateEarliestPickupTime(PricingNode v, Request ρ)
        {
            int fPlus = ρ.StartFloor;
            Direction ρDirection = ρ.StartFloor < ρ.DestinationFloor ? Direction.Up : Direction.Down;

            Direction currentDirection = v.GetSchedule().Stops.LastOrDefault()?.Direction ?? Direction.Idle;

            if (currentDirection != Direction.Idle && currentDirection != ρDirection)
            {
                return CalculateTimeAfterAllDropFloors(v) + CalculateTravelTime(GetLastDropFloor(v), fPlus);
            }
            else
            {
                return v.CurrentTime + CalculateTravelTime(v.CurrentFloor, fPlus);
            }
        }

        private double CalculateEarliestDropTime(PricingNode v, Request ρ, Call c, double tPlus)
        {
            double stopTime = Math.Max(Constant.StopTime, ρ.Calls.Count * Constant.LoadTime);
            double travelTime = CalculateTravelTime(ρ.StartFloor, c.DestinationFloor);

            return tPlus + stopTime + travelTime;
        }

        private double CalculateCapacityPenalty(PricingNode v, Request ρ)
        {
            int currentLoad = v.CurrentLoad;
            int requestSize = ρ.Calls.Count;

            if (currentLoad + requestSize > elevator.Capacity)
            {
                return instance.capacityPenalty * (currentLoad + requestSize - elevator.Capacity);
            }

            return 0;
        }

        private double CalculateTimeAfterAllDropFloors(PricingNode v)
        {
            return v.CurrentTime + v.CurrentLoad * Constant.StopTime;
        }

        private int GetLastDropFloor(PricingNode v)
        {
            return v.CurrentFloor;
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
            List<Request> allRequests = new List<Request>();

            if (assignedRequests != null)
                allRequests.AddRange(assignedRequests);

            if (unassignedRequests != null)
                allRequests.AddRange(unassignedRequests);

            if (allRequests.Count == 0)
            {
                return new Schedule(elevatorIndex);
            }

            var route = CreateOptimalRouteSimple(elevator, allRequests);

            return ConvertRouteToSchedule(route, allRequests);
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

        private Schedule ConvertRouteToSchedule(List<int> route, List<Request> requests)
        {
            Schedule schedule = new Schedule(elevatorIndex);
            float currentTime = 0;
            int currentFloor = elevator.CurrentFloor;

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

                if (stop.Pickups.Count > 0 || stop.Drops.Count > 0 || floor == elevator.CurrentFloor)
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

        private Schedule CreateBaseScheduleWithAssignedRequests()
        {
            Schedule schedule = new Schedule(elevatorIndex);

            if (assignedRequests.Count == 0)
                return schedule;

            float currentTime = 0;
            int currentFloor = elevator.CurrentFloor;

            foreach (var request in assignedRequests)
            {
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

        private HashSet<int> GetAdmissibleFloorsForFirstStop()
        {
            HashSet<int> floors = new HashSet<int>();

            floors.Add(elevator.CurrentFloor);

            foreach (var call in elevator.LoadedCalls ?? new List<Call>())
            {
                floors.Add(call.DestinationFloor);
            }

            foreach (var request in assignedRequests)
            {
                floors.Add(request.StartFloor);
            }

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

        private Direction DetermineInitialDirection(int firstFloor)
        {
            if (elevator.CurrentFloor < firstFloor) return Direction.Up;
            if (elevator.CurrentFloor > firstFloor) return Direction.Down;
            return elevator.CurrentDirection;
        }

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
                foreach (var request in stop.Pickups)
                {
                    foreach (var call in request.Calls)
                    {
                        float waitTime = (float)Math.Max(0, stop.ArrivalTime - call.ReleaseTime.ToOADate());
                        totalCost += (float)(call.WaitCost * waitTime);
                    }
                }

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
            int load = elevator.LoadedCalls?.Count ?? 0;

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

            foreach (var request in schedule.ServedRequests)
            {
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

        private double CalculateTravelTime(int fromFloor, int toFloor)
        {
            int distance = Math.Abs(toFloor - fromFloor);
            if (distance == 0) return 0;

            return Constant.ElevatorStartupTime + distance * Constant.DrivePerFloorTime;
        }
    }
}