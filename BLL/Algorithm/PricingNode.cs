using Microsoft.VisualBasic;
using Project;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Project.Algorithm
{
    public class PricingNode
    {
        public int CurrentFloor { get; private set; }
        public double CurrentTime { get; private set; }
        public int CurrentLoad { get; private set; }

        public List<Request> UnservedAssignedRequests { get; private set; }
        public List<Request> UnservedOptionalRequests { get; private set; }
        public HashSet<Request> ServedAssignedRequests { get; private set; }
        public HashSet<Request> ServedOptionalRequests { get; private set; }

        public Schedule CurrentSchedule { get; private set; }
        public Stop CurrentStop { get; private set; }

        private readonly int elevatorCapacity;
        private readonly int maxFloors;

        public PricingNode(
            int currentFloor,
            double currentTime,
            int currentLoad,
            HashSet<Request> servedAssignedRequests,
            List<Request> unservedAssignedRequests,
            HashSet<Request> servedOptionalRequests,
            List<Request> unservedOptionalRequests,
            Schedule currentSchedule,
            int elevatorCapacity,
            int maxFloors)
        {
            CurrentFloor = currentFloor;
            CurrentTime = currentTime;
            CurrentLoad = currentLoad;

            ServedAssignedRequests = servedAssignedRequests ?? new HashSet<Request>();
            UnservedAssignedRequests = unservedAssignedRequests ?? new List<Request>();
            ServedOptionalRequests = servedOptionalRequests ?? new HashSet<Request>();
            UnservedOptionalRequests = unservedOptionalRequests ?? new List<Request>();

            CurrentSchedule = currentSchedule ?? throw new ArgumentNullException(nameof(currentSchedule));

            this.elevatorCapacity = elevatorCapacity;
            this.maxFloors = maxFloors;

            if (CurrentSchedule.Stops.Count > 0)
            {
                CurrentStop = CurrentSchedule.Stops[CurrentSchedule.Stops.Count - 1];
            }
            else
            {
                CurrentStop = null;
            }
        }

        public bool IsLast()
        {
            bool hasMandatoryWork = UnservedAssignedRequests.Count > 0 ||
                                   CurrentLoad > 0 ||
                                   HasPendingDropCommitments();

            if (hasMandatoryWork)
            {
                return false;
            }

            if (UnservedOptionalRequests.Count > 0 && ServedOptionalRequests.Count == 0)
            {
                return false;
            }

            if (UnservedOptionalRequests.Count > 3)
            {
                return false;
            }

            return true;
        }

        public double GetReducedCost(double[] requestDuals, double elevatorDual, List<Request> unassignedRequests)
        {
            double cost = CurrentSchedule.TotalCost;
            double dualSum = 0;

            foreach (var request in ServedOptionalRequests)
            {
                int requestIndex = unassignedRequests.IndexOf(request);
                if (requestIndex >= 0 && requestIndex < requestDuals.Length)
                {
                    dualSum += requestDuals[requestIndex];
                }
            }

            return cost - dualSum - elevatorDual;
        }

        public Schedule GetSchedule()
        {
            foreach (var request in ServedOptionalRequests)
            {
                if (!CurrentSchedule.ServedRequests.Contains(request))
                {
                    CurrentSchedule.ServedRequests.Add(request);
                }
            }

            return CurrentSchedule;
        }

        public List<PricingNode> Branch()
        {
            List<PricingNode> children = new List<PricingNode>();

            bool hasDrops = HasDropsAtCurrentFloor();
            if (hasDrops)
            {
                PricingNode dropNode = CreateDropNode();
                children.Add(dropNode);
                return children;
            }

            List<Request> assignedPickups = GetPickableAssignedRequests();
            foreach (var request in assignedPickups)
            {
                PricingNode childNode = CreatePickupAssignedNode(request);
                children.Add(childNode);
            }

            if (children.Count > 0) return children;

            if (CurrentLoad < elevatorCapacity)
            {
                List<Request> optionalPickups = GetPickableOptionalRequests();
                foreach (var request in optionalPickups.Take(1))
                {
                    if (CurrentLoad + request.Calls.Count <= elevatorCapacity)
                    {
                        PricingNode childNode = CreatePickupOptionalNode(request);
                        children.Add(childNode);
                        break;
                    }
                }
            }

            if (UnservedOptionalRequests.Count > 0 && CurrentLoad >= elevatorCapacity)
            {
                PricingNode skipNode = new PricingNode(
                    currentFloor: CurrentFloor,
                    currentTime: CurrentTime,
                    currentLoad: CurrentLoad,
                    servedAssignedRequests: new HashSet<Request>(ServedAssignedRequests),
                    unservedAssignedRequests: new List<Request>(UnservedAssignedRequests),
                    servedOptionalRequests: new HashSet<Request>(ServedOptionalRequests),
                    unservedOptionalRequests: new List<Request>(),
                    currentSchedule: new Schedule(CurrentSchedule),
                    elevatorCapacity: elevatorCapacity,
                    maxFloors: maxFloors
                );

                children.Add(skipNode);
                return children;
            }

            if (children.Count == 0 && HasPendingDropCommitments())
            {
                List<PricingNode> moveNodes = CreateMoveNodes();
                children.AddRange(moveNodes.Take(2));
            }

            if (children.Count == 0 && UnservedOptionalRequests.Count > 0)
            {
                List<PricingNode> moveNodes = CreateMoveNodes();
                children.AddRange(moveNodes);
            }

            if (children.Count == 0)
            {
                PricingNode endNode = new PricingNode(
                    currentFloor: CurrentFloor,
                    currentTime: CurrentTime,
                    currentLoad: 0,
                    servedAssignedRequests: new HashSet<Request>(ServedAssignedRequests),
                    unservedAssignedRequests: new List<Request>(),
                    servedOptionalRequests: new HashSet<Request>(ServedOptionalRequests),
                    unservedOptionalRequests: new List<Request>(),
                    currentSchedule: new Schedule(CurrentSchedule),
                    elevatorCapacity: elevatorCapacity,
                    maxFloors: maxFloors
                );

                children.Add(endNode);
            }

            return children;
        }

        private List<Request> GetPickableAssignedRequests()
        {
            Direction currentDirection = CurrentStop?.Direction ?? Direction.Idle;

            return UnservedAssignedRequests
                .Where(r => r.StartFloor == CurrentFloor &&
                           (currentDirection == Direction.Idle ||
                            currentDirection == DetermineDirection(r.StartFloor, r.DestinationFloor)))
                .ToList();
        }

        private List<Request> GetPickableOptionalRequests()
        {
            Direction currentDirection = CurrentStop?.Direction ?? Direction.Idle;
            return UnservedOptionalRequests.Take(2).ToList();
        }

        private bool HasDropsAtCurrentFloor()
        {
            foreach (var stop in CurrentSchedule.Stops)
            {
                foreach (var pickup in stop.Pickups)
                {
                    foreach (var call in pickup.Calls)
                    {
                        if (call.DestinationFloor == CurrentFloor)
                        {
                            return true;
                        }
                    }
                }
            }

            if (CurrentStop != null && CurrentStop.DropFloors.Contains(CurrentFloor))
            {
                return true;
            }

            return false;
        }

        private bool HasPendingDropCommitments()
        {
            if (CurrentStop == null)
                return false;

            return CurrentStop.DropFloors.Count > 0;
        }

        private List<PricingNode> CreateMoveNodes()
        {
            List<PricingNode> moveNodes = new List<PricingNode>();
            HashSet<(int floor, Direction dir)> potentialNextSteps = new HashSet<(int, Direction)>();

            Direction currentElevatorDirection = CurrentStop?.Direction ?? Direction.Idle;

            if (currentElevatorDirection != Direction.Idle)
            {
                int nextFloorInCurrentDir = CurrentFloor + (currentElevatorDirection == Direction.Up ? 1 : -1);
                if (IsValidFloor(nextFloorInCurrentDir))
                {
                    potentialNextSteps.Add((nextFloorInCurrentDir, currentElevatorDirection));
                }
            }

            Direction oppositeDirection = Direction.Idle;
            if (currentElevatorDirection == Direction.Up)
            {
                oppositeDirection = Direction.Down;
            }
            else if (currentElevatorDirection == Direction.Down)
            {
                oppositeDirection = Direction.Up;
            }
            else
            {
                oppositeDirection = Direction.Down;
            }

            int nextFloorOpposite = CurrentFloor + (oppositeDirection == Direction.Up ? 1 : -1);

            bool shouldConsiderOpposite = false;

            if (currentElevatorDirection != Direction.Idle && IsLastFloorInDirection(currentElevatorDirection))
            {
                shouldConsiderOpposite = true;
            }
            else if (!HasPendingDropCommitments() &&
                     (UnservedAssignedRequests.Any(r => DetermineDirection(CurrentFloor, r.StartFloor) == oppositeDirection) ||
                      UnservedOptionalRequests.Any(r => DetermineDirection(CurrentFloor, r.StartFloor) == oppositeDirection)))
            {
                shouldConsiderOpposite = true;
            }
            else if (currentElevatorDirection == Direction.Idle &&
                     (UnservedAssignedRequests.Any() || UnservedOptionalRequests.Any()))
            {
                shouldConsiderOpposite = true;
            }

            if (shouldConsiderOpposite && IsValidFloor(nextFloorOpposite) && nextFloorOpposite != CurrentFloor)
            {
                potentialNextSteps.Add((nextFloorOpposite, oppositeDirection));
            }

            if (currentElevatorDirection == Direction.Idle && !potentialNextSteps.Any())
            {
                if (IsValidFloor(CurrentFloor + 1))
                {
                    potentialNextSteps.Add((CurrentFloor + 1, Direction.Up));
                }
                if (IsValidFloor(CurrentFloor - 1))
                {
                    potentialNextSteps.Add((CurrentFloor - 1, Direction.Down));
                }
            }

            foreach (var step in potentialNextSteps)
            {
                moveNodes.Add(CreateMoveNode(step.floor, step.dir));
            }

            return moveNodes.Distinct().ToList();
        }

        private bool IsLastFloorInDirection(Direction direction)
        {
            return (direction == Direction.Up && CurrentFloor == maxFloors) ||
                   (direction == Direction.Down && CurrentFloor == 1);
        }

        private int GetNextDropFloor()
        {
            if (CurrentStop == null || CurrentStop.DropFloors.Count == 0)
                return CurrentFloor;

            Direction currentDirection = CurrentStop.Direction;

            if (currentDirection == Direction.Up)
            {
                return CurrentStop.DropFloors
                    .Where(f => f > CurrentFloor)
                    .DefaultIfEmpty(CurrentFloor)
                    .Min();
            }
            else
            {
                return CurrentStop.DropFloors
                    .Where(f => f < CurrentFloor)
                    .DefaultIfEmpty(CurrentFloor)
                    .Max();
            }
        }

        private PricingNode CreatePickupAssignedNode(Request request)
        {
            HashSet<Request> newServedAssignedRequests = new HashSet<Request>(ServedAssignedRequests);
            newServedAssignedRequests.Add(request);

            List<Request> newUnservedAssignedRequests = new List<Request>(UnservedAssignedRequests);
            newUnservedAssignedRequests.Remove(request);

            Schedule newSchedule = new Schedule(CurrentSchedule);

            Direction pickupDirection = DetermineDirection(request.StartFloor, request.DestinationFloor);
            Stop pickupStop = new Stop
            {
                Floor = CurrentFloor,
                ArrivalTime = (float)CurrentTime,
                Direction = pickupDirection
            };

            pickupStop.AddPickup(request);
            newSchedule.AddStop(pickupStop);

            double waitCost = 0;
            foreach (var call in request.Calls)
            {
                double waitTime = Math.Max(0, CurrentTime - call.ReleaseTime.ToOADate());
                waitCost += call.WaitCost * waitTime;
            }

            double capacityCost = 0;
            if (CurrentLoad + request.Calls.Count > elevatorCapacity)
            {
                capacityCost = Constant.CapacityPenalty * (CurrentLoad + request.Calls.Count - elevatorCapacity);
            }

            newSchedule.TotalCost += (float)(waitCost + capacityCost);

            double newTime = CurrentTime + Constant.StopTime;
            int newLoad = CurrentLoad + request.Calls.Count;

            foreach (var call in request.Calls)
            {
                pickupStop.DropFloors.Add(call.DestinationFloor);
            }

            return new PricingNode(
                CurrentFloor,
                newTime,
                newLoad,
                newServedAssignedRequests,
                newUnservedAssignedRequests,
                ServedOptionalRequests,
                UnservedOptionalRequests,
                newSchedule,
                elevatorCapacity,
                maxFloors
            );
        }

        private PricingNode CreatePickupOptionalNode(Request request)
        {
            HashSet<Request> newServedOptionalRequests = new HashSet<Request>(ServedOptionalRequests);
            newServedOptionalRequests.Add(request);
            List<Request> newUnservedOptionalRequests = new List<Request>(UnservedOptionalRequests);
            newUnservedOptionalRequests.Remove(request);

            Schedule newSchedule = new Schedule(CurrentSchedule);

            double travelTime = CalculateTravelTime(CurrentFloor, request.StartFloor);
            double arrivalTime = CurrentTime + travelTime;

            Direction pickupDirection = DetermineDirection(request.StartFloor, request.DestinationFloor);
            Stop pickupStop = new Stop
            {
                Floor = request.StartFloor,
                ArrivalTime = (float)arrivalTime,
                Direction = pickupDirection
            };

            pickupStop.AddPickup(request);
            newSchedule.AddStop(pickupStop);

            double waitCost = 0;
            foreach (var call in request.Calls)
            {
                double waitTime = Math.Max(0, arrivalTime - call.ReleaseTime.ToOADate());
                waitCost += call.WaitCost * waitTime;
            }

            double capacityCost = 0;
            if (CurrentLoad + request.Calls.Count > elevatorCapacity)
            {
                capacityCost = Constant.CapacityPenalty * (CurrentLoad + request.Calls.Count - elevatorCapacity);
            }

            double travelCost = travelTime * 1.0;

            newSchedule.TotalCost += (float)(waitCost + capacityCost + travelCost);

            double newTime = arrivalTime + Constant.StopTime;
            int newLoad = CurrentLoad + request.Calls.Count;

            foreach (var call in request.Calls)
            {
                pickupStop.DropFloors.Add(call.DestinationFloor);
            }

            return new PricingNode(
                request.StartFloor,
                newTime,
                newLoad,
                ServedAssignedRequests,
                UnservedAssignedRequests,
                newServedOptionalRequests,
                newUnservedOptionalRequests,
                newSchedule,
                elevatorCapacity,
                maxFloors
            );
        }

        private PricingNode CreateDropNode()
        {
            Schedule newSchedule = new Schedule(CurrentSchedule);

            Stop dropStop = new Stop
            {
                Floor = CurrentFloor,
                ArrivalTime = (float)CurrentTime,
                Direction = CurrentStop?.Direction ?? Direction.Idle
            };

            List<Call> dropsHere = FindDropsAtCurrentFloor();

            foreach (var call in dropsHere)
            {
                dropStop.AddDrop(call);
            }

            newSchedule.AddStop(dropStop);

            double travelCost = 0;
            foreach (var call in dropsHere)
            {
                double pickupTime = FindPickupTimeForCall(call);
                double travelTime = CurrentTime - pickupTime;
                travelCost += call.TravelCost * travelTime;
            }

            newSchedule.TotalCost += (float)travelCost;

            double newTime = CurrentTime + Constant.StopTime;
            int newLoad = CurrentLoad - dropsHere.Count;

            HashSet<int> newDropFloors = new HashSet<int>(dropStop.DropFloors);
            newDropFloors.Remove(CurrentFloor);
            dropStop.DropFloors = newDropFloors;

            return new PricingNode(
                CurrentFloor,
                newTime,
                newLoad,
                ServedAssignedRequests,
                UnservedAssignedRequests,
                ServedOptionalRequests,
                UnservedOptionalRequests,
                newSchedule,
                elevatorCapacity,
                maxFloors
            );
        }

        private PricingNode CreateMoveNode(int nextFloor, Direction direction)
        {
            Schedule newSchedule = new Schedule(CurrentSchedule);

            double travelTime = CalculateTravelTime(CurrentFloor, nextFloor);
            double newTime = CurrentTime + travelTime;

            Stop moveStop = new Stop
            {
                Floor = nextFloor,
                ArrivalTime = (float)newTime,
                Direction = direction
            };

            if (CurrentStop != null)
            {
                foreach (int floor in CurrentStop.DropFloors)
                {
                    moveStop.DropFloors.Add(floor);
                }
            }

            newSchedule.AddStop(moveStop);

            return new PricingNode(
                nextFloor,
                newTime,
                CurrentLoad,
                ServedAssignedRequests,
                UnservedAssignedRequests,
                ServedOptionalRequests,
                UnservedOptionalRequests,
                newSchedule,
                elevatorCapacity,
                maxFloors
            );
        }

        private List<Call> FindDropsAtCurrentFloor()
        {
            List<Call> drops = new List<Call>();

            foreach (var stop in CurrentSchedule.Stops)
            {
                foreach (var pickup in stop.Pickups)
                {
                    foreach (var call in pickup.Calls)
                    {
                        if (call.DestinationFloor == CurrentFloor)
                        {
                            drops.Add(call);
                        }
                    }
                }
            }

            return drops;
        }

        private double FindPickupTimeForCall(Call call)
        {
            foreach (var stop in CurrentSchedule.Stops)
            {
                foreach (var pickup in stop.Pickups)
                {
                    if (pickup.Calls.Contains(call))
                    {
                        return stop.ArrivalTime;
                    }
                }
            }
            return 0;
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

        private bool IsValidFloor(int floor)
        {
            return floor >= 1 && floor <= maxFloors;
        }
    }
}