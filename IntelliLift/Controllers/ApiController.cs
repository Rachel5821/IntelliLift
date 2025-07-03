    using Microsoft.AspNetCore.Mvc;
    using Project; // נניח ש-ProblemInstance, Elevator, Request, Call, Direction נמצאים כאן
    using Project.Algorithm; // נניח ש-ExactReplan, Solution נמצאים כאן
    using Project.DTO; // ה-DTOs החדשים שלך
    using System; // עבור Exception, DateTime
    using System.Collections.Generic; // עבור List
    using System.Linq; // עבור FirstOrDefault, Sum, Select

    namespace ElevatorServer.Controllers
    {
        [ApiController]
        [Route("api/[controller]")]
        public class ElevatorController : ControllerBase
        {
            [HttpPost("solve")]
            public ActionResult SolveElevatorProblem([FromBody] ElevatorSolveRequestBody request)
            {
                try
                {
                    // ברירות מחדל - ניגש למאפיינים עם אות גדולה
                    int numElevators = request.NumElevators ?? 8; // <<<< תיקון: NumElevators
                    int numFloors = request.NumFloors ?? 30;     // <<<< תיקון: NumFloors

                    // בנה את הבעיה עם ברירות מחדל
                    var instance = CreateProblemInstance(request, numElevators, numFloors);

                    // פתור
                    var algorithm = new ExactReplan(instance);
                    var solution = algorithm.solve();

                    // בנה תשובה מלאה
                    var result = BuildCompleteResponse(solution, instance, numElevators);

                    return Ok(result);
                }
                catch (Exception ex)
                {
                    // חשוב לכלול את הודעת השגיאה הפנימית אם רוצים לראות אותה
                    return BadRequest(new { error = ex.Message, stackTrace = ex.StackTrace, innerException = ex.InnerException?.Message });
                }
            }

            private ProblemInstance CreateProblemInstance(ElevatorSolveRequestBody request, int numElevators, int numFloors)
            {
                var instance = new ProblemInstance(
                    numElevators: numElevators,
                    numFloors: numFloors,
                    stopTime: 2.0,
                    loadTime: 1.0,
                    drivePerFloorTime: 1.5,
                    capacityPenalty: 100.0
                );

                // צור מעליות על בסיס הקלט, אם סופקו
                // <<<< תיקון: Elevators עם אות גדולה
                if (request.Elevators != null && request.Elevators.Any()) // השתמש ב-Any() לבדיקה אם הרשימה מכילה פריטים
                {
                    foreach (var elevatorData in request.Elevators) // <<<< תיקון: ElevatorData הוא כבר מסוג ElevatorStateInput
                    {
                        var elevator = new Elevator
                        {
                            Id = elevatorData.Id, // <<<< תיקון: Id
                            Capacity = elevatorData.Capacity, // <<<< תיקון: Capacity
                            CurrentFloor = elevatorData.CurrentFloor, // <<<< תיקון: CurrentFloor
                            CurrentDirection = elevatorData.CurrentDirection, // <<<< תיקון: CurrentDirection (כבר Direction Enum)
                            CurrentTime = (float)elevatorData.CurrentTime // <<<< תיקון: CurrentTime
                        };
                        instance.AddElevator(elevator);
                    }
                }
                else
                {
                    // צור מעליות ברירת מחדל
                    for (int i = 1; i <= numElevators; i++)
                    {
                        var elevator = new Elevator
                        {
                            Id = i,
                            Capacity = 8,
                            CurrentFloor = 1, // כל המעליות מתחילות בקומה 1
                            CurrentDirection = Direction.Idle,
                            CurrentTime = 0
                        };
                        instance.AddElevator(elevator);
                    }
                }

                // הוסף בקשות
                // <<<< תיקון: Requests עם אות גדולה (כבר מאותחל ברשימה ריקה אם לא סופק ב-JSON)
                if (request.Requests != null && request.Requests.Any())
                {
                    foreach (var requestData in request.Requests) // <<<< תיקון: requestData הוא כבר מסוג ElevatorRequestInput
                    {
                        var req = new Request
                        {
                            ReleaseTime = requestData.ReleaseTime, // <<<< תיקון: ReleaseTime (כבר DateTime)
                            StartFloor = requestData.StartFloor,   // <<<< תיקון: StartFloor
                            DestinationFloor = requestData.DestinationFloor // <<<< תיקון: DestinationFloor
                        };

                        req.AddCall(new Call
                        {
                            ReleaseTime = requestData.ReleaseTime, // <<<< תיקון: ReleaseTime
                            StartFloor = requestData.StartFloor,   // <<<< תיקון: StartFloor
                            DestinationFloor = requestData.DestinationFloor, // <<<< תיקון: DestinationFloor
                            WaitCost = 1.0,
                            TravelCost = 1.0
                        });

                        instance.AddRequest(req);
                    }
                }

                return instance;
            }

            // שאר הפונקציות נראות תקינות ואינן דורשות שינויים בגלל המעבר ל-DTOs,
            // מכיוון שהן מטפלות באובייקטים הפנימיים של הפתרון והבעיה (Solution, ProblemInstance, Elevator, Request וכו').
            // רק לוודא שכל ה-using's נכונים לכל המחלקות המעורבות.

            private object BuildCompleteResponse(Solution solution, ProblemInstance instance, int numElevators)
            {
                var response = new
                {
                    success = solution != null,
                    totalCost = solution?.ObjectiveValue ?? 0,

                    // 1. הקצאות - איזו בקשה לאיזו מעלית
                    assignments = ExtractAssignments(solution, instance),

                    // 2. מסלולים - לסימולציה
                    routes = ExtractRoutes(solution, instance, numElevators),

                    // 3. סטטוס - למידע כללי
                    status = ExtractStatus(solution, instance, numElevators)
                };

                return response;
            }

            private List<object> ExtractAssignments(Solution solution, ProblemInstance instance)
            {
                var assignments = new List<object>();

                if (solution != null)
                {
                    var schedules = solution.GetSelectedSchedules();

                    foreach (var schedule in schedules)
                    {
                        foreach (var request in schedule.ServedRequests)
                        {
                            assignments.Add(new
                            {
                                requestId = request.Id,
                                elevatorId = schedule.ElevatorIndex,
                                startFloor = request.StartFloor,
                                destinationFloor = request.DestinationFloor
                            });
                        }
                    }
                }

                return assignments;
            }

            private List<object> ExtractRoutes(Solution solution, ProblemInstance instance, int numElevators)
            {
                var routes = new List<object>();

                // צור רוט לכל מעלית
                for (int elevatorId = 1; elevatorId <= numElevators; elevatorId++)
                {
                    object elevatorRoute; // הגדרה כ-object כללי

                    // מצא את הלוח הזמנים של המעלית הזו
                    if (solution != null)
                    {
                        var schedules = solution.GetSelectedSchedules();
                        var elevatorSchedule = schedules.FirstOrDefault(s => s.ElevatorIndex == elevatorId - 1);

                        if (elevatorSchedule != null && elevatorSchedule.Stops != null)
                        {
                            // בנה את האובייקט עם הנתונים האמיתיים
                            elevatorRoute = new
                            {
                                elevatorId = elevatorId,
                                stops = elevatorSchedule.Stops.Select(stop => new
                                {
                                    floor = stop.Floor,
                                    arrivalTime = stop.ArrivalTime,
                                    direction = stop.Direction.ToString(),
                                    pickups = stop.Pickups.Select(r => r.Id).ToList(),
                                    dropsCount = stop.Drops.Count
                                }).ToList()
                            };
                        }
                        else
                        {
                            // מעלית ללא לוח זמנים
                            elevatorRoute = new
                            {
                                elevatorId = elevatorId,
                                stops = new List<object>()
                            };
                        }
                    }
                    else
                    {
                        // אין פתרון
                        elevatorRoute = new
                        {
                            elevatorId = elevatorId,
                            stops = new List<object>()
                        };
                    }

                    routes.Add(elevatorRoute);
                }

                return routes;
            }

            private object ExtractStatus(Solution solution, ProblemInstance instance, int numElevators)
            {
                var totalRequests = instance.GetUnassignedRequests().Count;
                var assignedRequests = 0;

                if (solution != null)
                {
                    var schedules = solution.GetSelectedSchedules();
                    assignedRequests = schedules.Sum(s => s.ServedRequests?.Count ?? 0);
                }

                // בנה מצב לכל מעלית
                var elevatorStatuses = new List<object>();
                var elevators = instance.GetElevators();

                for (int i = 0; i < numElevators; i++)
                {
                    var elevator = elevators[i];

                    // מצא אם יש לוח זמנים למעלית הזו
                    var hasSchedule = false;
                    var nextStopFloor = (int?)null;
                    var nextStopTime = (float?)null;

                    if (solution != null)
                    {
                        var schedules = solution.GetSelectedSchedules();
                        var elevatorSchedule = schedules.FirstOrDefault(s => s.ElevatorIndex == i);

                        if (elevatorSchedule != null && elevatorSchedule.Stops?.Count > 0)
                        {
                            hasSchedule = true;
                            var firstStop = elevatorSchedule.Stops.First();
                            nextStopFloor = firstStop.Floor;
                            nextStopTime = (float)firstStop.ArrivalTime; // לוודא שהטיפוס מתאים
                        }
                    }

                    elevatorStatuses.Add(new
                    {
                        elevatorId = elevator.Id,
                        currentFloor = elevator.CurrentFloor,
                        direction = elevator.CurrentDirection.ToString(), // Up/Down/Idle
                        doorsOpen = elevator.CurrentDirection == Direction.Idle, // דלתות פתוחות כשהמעלית במנוחה
                        currentLoad = elevator.LoadedCalls?.Count ?? 0,
                        capacity = elevator.Capacity,
                        status = hasSchedule ? "ACTIVE" : "IDLE", // האם יש למעלית משימות
                        nextStop = nextStopFloor,
                        nextStopTime = nextStopTime,
                        assignedRequests = solution?.GetSelectedSchedules()
                            .FirstOrDefault(s => s.ElevatorIndex == i)?.ServedRequests?.Count ?? 0
                    });
                }

                return new
                {
                    // סטטיסטיקות כלליות
                    totalElevators = numElevators,  
                    totalRequests = totalRequests,
                    assignedRequests = assignedRequests,
                    unassignedRequests = totalRequests - assignedRequests,
                    optimizationStatus = solution != null ? "SUCCESS" : "FAILED",
                    solutionTime = DateTime.Now.ToString("HH:mm:ss"),

                    // מצב כל מעלית
                    elevators = elevatorStatuses
                };
            }
        }
    }