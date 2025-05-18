# Elevator Group Reoptimization Algorithm

## Important Note
This is a private academic/research project developed for educational and research purposes.

## Overview
This project implements an **Exact Reoptimization Algorithm** for scheduling elevator groups in high-rise buildings, based on the column generation technique and Branch & Price methodology. The algorithm optimizes elevator schedules to minimize passenger waiting and travel times.

## Features

- **Immediate Assignment (IA) System Support**: Optimized for conventional destination call systems where passengers are immediately assigned to elevators upon request
- **Exact Optimization**: Implements Branch & Price algorithm to find optimal solutions for elevator scheduling problems
- **Column Generation**: Uses pricing problems solved via Branch & Bound to efficiently generate schedules with negative reduced cost
- **Real-time Reoptimization**: Computes new schedules each time new passenger requests arrive
- **Comprehensive Cost Modeling**: Includes waiting times, travel times, and capacity penalty costs

## Algorithm Components

### 1. Master Problem (`MasterModel.cs`)
- Set partitioning formulation using CPLEX
- Handles request assignment constraints and elevator constraints
- Supports branching constraints for Branch & Price tree search

### 2. Pricing Problem (`PricingProblem.cs`)
- Generates schedules with negative reduced cost for each elevator
- Implements Branch & Bound methodology for efficient schedule enumeration
- Calculates lower bounds using earliest pickup/drop time estimates

### 3. Branch & Price Framework (`BranchAndPrice.cs`, `ExactReplan.cs`)
- Coordinates column generation and integer programming solving
- Implements branching strategy based on request-to-elevator assignments
- Handles both single-request and multi-request optimization scenarios

## Key Classes

### Data Models
- **`ProblemInstance`**: Represents the elevator scheduling problem instance
- **`Elevator`**: Contains elevator state (position, direction, loaded calls)
- **`Request`**: Groups passenger calls from same floor to same destination
- **`Call`**: Individual passenger request with origin, destination, and timing
- **`Schedule`**: Sequence of stops for a specific elevator
- **`Stop`**: Represents elevator stop with pickups, drops, and timing

### Algorithm Components
- **`ExactReplan`**: Main algorithm orchestrator
- **`BranchAndPrice`**: Implements column generation and branching
- **`PricingProblem`**: Solves subproblems for individual elevators
- **`MasterModel`**: Handles the restricted master problem using CPLEX

## Technical Implementation

### Dependencies
- **CPLEX**: Commercial optimization solver for linear programming
- **.NET Framework**: Core runtime environment

### Algorithm Flow
1. **Problem Instance Creation**: Define building, elevators, and passenger requests
2. **Initial Column Generation**: Generate feasible schedules using heuristics
3. **Master Problem Solution**: Solve LP relaxation to get dual values
4. **Pricing Problems**: Generate schedules with negative reduced cost
5. **Branch & Price**: Use integer programming tree search if needed
6. **Solution Return**: Optimal elevator schedules minimizing total cost

### Performance Features
- **Single Request Optimization**: Direct enumeration for simple cases
- **Pricing of Old Schedules**: Reuse previously generated schedules
- **Lower Bound Calculations**: Efficient pruning of Branch & Bound tree
- **Capacity Handling**: Penalty-based approach for elevator overloading

## Usage Example

```csharp
// Create problem instance
ProblemInstance instance = new ProblemInstance(
   numElevators: 4, 
   numFloors: 20, 
   stopTime: 2.0, 
   loadTime: 1.0,
   drivePerFloorTime: 1.5, 
   capacityPenalty: 100.0
);

// Add elevators and requests
// ... (elevator and request setup)

// Solve using ExactReplan
ExactReplan solver = new ExactReplan(instance);
Solution solution = solver.solve();

// Extract optimal schedules
var optimalSchedules = solution.GetSelectedSchedules();

## Research Background

This implementation is based on the research paper:
**"An Exact Reoptimization Algorithm for the Scheduling of Elevator Groups"**
*by Benjamin Hiller, Torsten Klug, and Andreas Tuchscherer*

The algorithm has been shown to:
- Significantly improve waiting times compared to conventional 2-button systems
- Handle complex passenger flow patterns (up-peak, down-peak, interfloor traffic)
- Achieve near-optimal solutions in real-time scenarios

## Performance Characteristics

- **IA Systems**: Extremely fast (< 0.01s) for single request scenarios
- **Complex Scenarios**: Handles up to 30+ requests with multiple elevators
- **Scalability**: Efficient for buildings with 10-25 floors and 4-8 elevators

## License

This project is provided for educational and research purposes.
