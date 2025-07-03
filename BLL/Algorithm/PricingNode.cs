using Microsoft.VisualBasic;
using Project;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Project.Algorithm
{
    /// <summary>
    /// מייצג צומת בעץ החיפוש של בעיית ה-Pricing כפי שמתואר במאמר בסעיף 3.1
    /// </summary>
    public class PricingNode
    {
        // שדות המתאימים למודל במאמר
        public int CurrentFloor { get; private set; }
        public double CurrentTime { get; private set; }
        public int CurrentLoad { get; private set; }

        // Av במאמר - בקשות משויכות שטרם נאספו
        public List<Request> UnservedAssignedRequests { get; private set; }

        // Ov במאמר - בקשות אופציונליות (לא משויכות) שטרם נאספו
        public List<Request> UnservedOptionalRequests { get; private set; }

        // בקשות שכבר נאספו (R(e) \ Av) - משויכות
        public HashSet<Request> ServedAssignedRequests { get; private set; }

        // בקשות אופציונליות שכבר נאספו (תת-קבוצה של Ru)
        public HashSet<Request> ServedOptionalRequests { get; private set; }

        // Sv במאמר - לוח זמנים עד כה
        public Schedule CurrentSchedule { get; private set; }

        // sv במאמר - העצירה הנוכחית
        public Stop CurrentStop { get; private set; }

        // מידע על המעלית - מתווסף לנוחות הגישה
        private readonly int elevatorCapacity;
        private readonly int maxFloors;

        /// <summary>
        /// מייצר צומת חדש בעץ החיפוש
        /// </summary>
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

            // קבוצות הבקשות - מחולקות לפי המאמר
            ServedAssignedRequests = servedAssignedRequests ?? new HashSet<Request>();
            UnservedAssignedRequests = unservedAssignedRequests ?? new List<Request>();
            ServedOptionalRequests = servedOptionalRequests ?? new HashSet<Request>();
            UnservedOptionalRequests = unservedOptionalRequests ?? new List<Request>();

            CurrentSchedule = currentSchedule ?? throw new ArgumentNullException(nameof(currentSchedule));

            this.elevatorCapacity = elevatorCapacity;
            this.maxFloors = maxFloors;

            // העצירה הנוכחית היא האחרונה בלוח הזמנים
            if (CurrentSchedule.Stops.Count > 0)
            {
                CurrentStop = CurrentSchedule.Stops[CurrentSchedule.Stops.Count - 1];
            }
            else
            {
                CurrentStop = null;
            }
        }

        /// <summary>
        /// בודק אם הצומת הוא צומת סופי (feasible node)
        /// לפי המאמר: צומת היא אפשרית אם Av ריק ו-sv הוא העצירה האחרונה של Sv
        /// </summary>
        public bool IsLast()
        {
            // צומת אפשרי אם:
            // 1. כל הבקשות המשויכות נאספו (Av ריק)
            // 2. אין עוד הורדות (כל הנוסעים הורדו)
            // 3. כל drop commitments טופלו

            return UnservedAssignedRequests.Count == 0 &&
                   CurrentLoad == 0 &&
                   !HasPendingDropCommitments();
        }

        /// <summary>
        /// חישוב העלות המופחתת כפי שמתואר במאמר בסעיף 3.1:
        /// c̃(S) = c(S) - ∑ρ∈Ru∩S πρ - πe
        /// </summary>
        public double GetReducedCost(double[] requestDuals, double elevatorDual, List<Request> unassignedRequests)
        {
            double cost = CurrentSchedule.TotalCost;
            double dualSum = 0;

            // חישוב סכום הערכים הדואליים
            // שים לב: רק בקשות מ-Ru (בקשות לא משויכות/אופציונליות) נכנסות לחישוב!
            // המאמר מדגיש את זה בנוסחה: ∑ρ∈Ru∩S πρ
            foreach (var request in ServedOptionalRequests)
            {
                int requestIndex = unassignedRequests.IndexOf(request);
                if (requestIndex >= 0 && requestIndex < requestDuals.Length)
                {
                    dualSum += requestDuals[requestIndex];
                }
            }

            // החזרת העלות המופחתת
            return cost - dualSum - elevatorDual;
        }

        /// <summary>
        /// מחזיר את לוח הזמנים הנוכחי
        /// </summary>
        public Schedule GetSchedule()
        {
            return CurrentSchedule;
        }

        /// <summary>
        /// מייצר את כל הצמתים הבנים האפשריים מהצומת הנוכחי
        /// כל פעולה אפשרית (pickup, drop או move) מובילה לצומת בן
        /// לפי המאמר סעיף 3.1: "A child node v' of node v arises by two actions:
        /// Either a request is picked up or the elevator moves to the next floor for dropping a loaded call."
        /// </summary>
        public List<PricingNode> Branch()
        {
            List<PricingNode> children = new List<PricingNode>();
            Direction currentDirection = CurrentStop?.Direction ?? Direction.Idle;

            // חלק 1: אם יש בקשות משויכות שטרם נאספו בקומה הנוכחית ובכיוון הנוכחי
            // לפי ה-first-stop pickup requirement מהמאמר, חייבים לאסוף אותן
            foreach (var request in GetPickableAssignedRequests())
            {
                PricingNode childNode = CreatePickupAssignedNode(request);
                children.Add(childNode);
            }

            // חלק 2: אם יש בקשות אופציונליות שטרם נאספו בקומה הנוכחית ובכיוון הנוכחי
            // המאמר מציין גם שניתן לעשות dual fixing אם πρ ≤ c̄(ρ)
            foreach (var request in GetPickableOptionalRequests())
            {
                // בדיקת קיבולת - בקשות אופציונליות נאספות רק אם יש מקום
                if (CurrentLoad + request.Calls.Count <= elevatorCapacity)
                {
                    PricingNode childNode = CreatePickupOptionalNode(request);
                    children.Add(childNode);
                }
            }

            // חלק 3: אם יש הורדות בקומה הנוכחית
            if (HasDropsAtCurrentFloor())
            {
                PricingNode childNode = CreateDropNode();
                children.Add(childNode);
            }

            // חלק 4: מעבר לקומה הבאה - לפי האילוצים של drop commitments
            List<PricingNode> moveNodes = CreateMoveNodes();
            children.AddRange(moveNodes);

            return children;
        }

        /// <summary>
        /// מחזיר את כל הבקשות המשויכות שניתן לאסוף בקומה הנוכחית ובכיוון הנוכחי
        /// </summary>
        private List<Request> GetPickableAssignedRequests()
        {
            Direction currentDirection = CurrentStop?.Direction ?? Direction.Idle;

            return UnservedAssignedRequests
                .Where(r => r.StartFloor == CurrentFloor &&
                           (currentDirection == Direction.Idle ||
                            currentDirection == DetermineDirection(r.StartFloor, r.DestinationFloor)))
                .ToList();
        }

        /// <summary>
        /// מחזיר את כל הבקשות האופציונליות שניתן לאסוף בקומה הנוכחית ובכיוון הנוכחי
        /// </summary>
        private List<Request> GetPickableOptionalRequests()
        {
            Direction currentDirection = CurrentStop?.Direction ?? Direction.Idle;

            return UnservedOptionalRequests
                .Where(r => r.StartFloor == CurrentFloor &&
                           (currentDirection == Direction.Idle ||
                            currentDirection == DetermineDirection(r.StartFloor, r.DestinationFloor)))
                .ToList();
        }

        /// <summary>
        /// בודק אם יש הורדות בקומה הנוכחית
        /// </summary>
        private bool HasDropsAtCurrentFloor()
        {
            // בדיקה אם יש קריאות שיש להוריד בקומה הנוכחית
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

            // בדיקה ב-drop commitments
            if (CurrentStop != null && CurrentStop.DropFloors.Contains(CurrentFloor))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// בודק אם יש drop commitments שטרם טופלו
        /// </summary>
        private bool HasPendingDropCommitments()
        {
            if (CurrentStop == null)
                return false;

            return CurrentStop.DropFloors.Count > 0;
        }

        /// <summary>
        /// מייצר צמתים למעבר לקומות הבאות, בהתאם לאילוצי drop commitments
        /// לפי המאמר, drop commitments משפיעים על הקומה הבאה והכיוון
        /// </summary>
        private List<PricingNode> CreateMoveNodes()
        {
            List<PricingNode> moveNodes = new List<PricingNode>();
            Direction currentDirection = CurrentStop?.Direction ?? Direction.Idle;

            // מקרה 1: אם יש drop commitments, חייבים לנוע לכיוון הקומה הבאה בדרך
            if (HasPendingDropCommitments())
            {
                int nextDropFloor = GetNextDropFloor();
                Direction requiredDirection = DetermineDirection(CurrentFloor, nextDropFloor);

                // אם הקומה הבאה היא כבר drop floor, לא צריך לנוע
                if (nextDropFloor != CurrentFloor)
                {
                    // צעד בכיוון הנדרש
                    int nextFloor = CurrentFloor + (requiredDirection == Direction.Up ? 1 : -1);
                    if (IsValidFloor(nextFloor))
                    {
                        moveNodes.Add(CreateMoveNode(nextFloor, requiredDirection));
                    }
                }
            }
            // מקרה 2: אם אין drop commitments, אפשר לנוע לכל כיוון (אם יש כיוון נוכחי)
            else if (currentDirection != Direction.Idle)
            {
                int nextFloor = CurrentFloor + (currentDirection == Direction.Up ? 1 : -1);
                if (IsValidFloor(nextFloor))
                {
                    moveNodes.Add(CreateMoveNode(nextFloor, currentDirection));
                }

                // לפי המאמר, אם אין drop commitments והגענו לקומה האחרונה בכיוון,
                // אפשר גם לשנות כיוון (מקרה מיוחד למערכת עם כמות גדולה של בקשות)
                int oppositeFloor = CurrentFloor + (currentDirection == Direction.Up ? -1 : 1);
                if (IsLastFloorInDirection(currentDirection) && IsValidFloor(oppositeFloor))
                {
                    Direction oppositeDirection = currentDirection == Direction.Up ? Direction.Down : Direction.Up;
                    moveNodes.Add(CreateMoveNode(oppositeFloor, oppositeDirection));
                }
            }

            return moveNodes;
        }

        /// <summary>
        /// בודק אם הקומה הנוכחית היא האחרונה בכיוון הנוכחי
        /// </summary>
        private bool IsLastFloorInDirection(Direction direction)
        {
            return (direction == Direction.Up && CurrentFloor == maxFloors) ||
                   (direction == Direction.Down && CurrentFloor == 1);
        }

        /// <summary>
        /// מחזיר את הקומה הבאה שיש בה drop commitment
        /// </summary>
        private int GetNextDropFloor()
        {
            if (CurrentStop == null || CurrentStop.DropFloors.Count == 0)
                return CurrentFloor;

            // לפי כיוון הנסיעה, מוצאים את הקומה הבאה
            Direction currentDirection = CurrentStop.Direction;

            if (currentDirection == Direction.Up)
            {
                // הקומה הנמוכה ביותר שגבוהה מהקומה הנוכחית
                return CurrentStop.DropFloors
                    .Where(f => f > CurrentFloor)
                    .DefaultIfEmpty(CurrentFloor)
                    .Min();
            }
            else // כיוון למטה או idle
            {
                // הקומה הגבוהה ביותר שנמוכה מהקומה הנוכחית
                return CurrentStop.DropFloors
                    .Where(f => f < CurrentFloor)
                    .DefaultIfEmpty(CurrentFloor)
                    .Max();
            }
        }

        /// <summary>
        /// מייצר צומת חדש לאיסוף בקשה משויכת
        /// </summary>
        private PricingNode CreatePickupAssignedNode(Request request)
        {
            // יצירת העתקים של קבוצות הבקשות
            HashSet<Request> newServedAssignedRequests = new HashSet<Request>(ServedAssignedRequests);
            newServedAssignedRequests.Add(request);

            List<Request> newUnservedAssignedRequests = new List<Request>(UnservedAssignedRequests);
            newUnservedAssignedRequests.Remove(request);

            // יצירת העתק של לוח הזמנים
            Schedule newSchedule = new Schedule(CurrentSchedule);

            // יצירת עצירה חדשה לאיסוף
            Direction pickupDirection = DetermineDirection(request.StartFloor, request.DestinationFloor);
            Stop pickupStop = new Stop
            {
                Floor = CurrentFloor,
                ArrivalTime = (float)CurrentTime,
                Direction = pickupDirection
            };

            // הוספת הבקשה לעצירת האיסוף
            pickupStop.AddPickup(request);

            // הוספת העצירה ללוח הזמנים
            newSchedule.AddStop(pickupStop);

            // חישוב עלויות עבור הבקשה שנאספה
            double waitCost = 0;
            foreach (var call in request.Calls)
            {
                // זמן ההמתנה מזמן הרישום עד זמן האיסוף
                double waitTime = Math.Max(0, CurrentTime - call.ReleaseTime.ToOADate());
                waitCost += call.WaitCost * waitTime;
            }

            // יתכן שיש גם עלות חריגת קיבולת
            double capacityCost = 0;
            if (CurrentLoad + request.Calls.Count > elevatorCapacity)
            {
                capacityCost = Constant.CapacityPenalty * (CurrentLoad + request.Calls.Count - elevatorCapacity);
            }

            // הוספת העלות ללוח הזמנים
            newSchedule.TotalCost += (float)(waitCost + capacityCost);

            // זמן חדש אחרי האיסוף
            double newTime = CurrentTime + Constant.StopTime;

            // עומס חדש אחרי האיסוף
            int newLoad = CurrentLoad + request.Calls.Count;

            // עדכון drop commitments
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

        /// <summary>
        /// מייצר צומת חדש לאיסוף בקשה אופציונלית
        /// </summary>
        private PricingNode CreatePickupOptionalNode(Request request)
        {
            // יצירת העתקים של קבוצות הבקשות
            HashSet<Request> newServedOptionalRequests = new HashSet<Request>(ServedOptionalRequests);
            newServedOptionalRequests.Add(request);

            List<Request> newUnservedOptionalRequests = new List<Request>(UnservedOptionalRequests);
            newUnservedOptionalRequests.Remove(request);

            // יצירת העתק של לוח הזמנים
            Schedule newSchedule = new Schedule(CurrentSchedule);

            // יצירת עצירה חדשה לאיסוף
            Direction pickupDirection = DetermineDirection(request.StartFloor, request.DestinationFloor);
            Stop pickupStop = new Stop
            {
                Floor = CurrentFloor,
                ArrivalTime = (float)CurrentTime,
                Direction = pickupDirection
            };

            // הוספת הבקשה לעצירת האיסוף
            pickupStop.AddPickup(request);

            // הוספת העצירה ללוח הזמנים
            newSchedule.AddStop(pickupStop);

            // חישוב עלויות עבור הבקשה שנאספה
            double waitCost = 0;
            foreach (var call in request.Calls)
            {
                // זמן ההמתנה מזמן הרישום עד זמן האיסוף
                double waitTime = Math.Max(0, CurrentTime - call.ReleaseTime.ToOADate());
                waitCost += call.WaitCost * waitTime;
            }

            // יתכן שיש גם עלות חריגת קיבולת
            double capacityCost = 0;
            if (CurrentLoad + request.Calls.Count > elevatorCapacity)
            {
                capacityCost = Constant.CapacityPenalty * (CurrentLoad + request.Calls.Count - elevatorCapacity);
            }

            // הוספת העלות ללוח הזמנים
            newSchedule.TotalCost += (float)(waitCost + capacityCost);

            // זמן חדש אחרי האיסוף
            double newTime = CurrentTime + Constant.StopTime;

            // עומס חדש אחרי האיסוף
            int newLoad = CurrentLoad + request.Calls.Count;

            // עדכון drop commitments
            foreach (var call in request.Calls)
            {
                pickupStop.DropFloors.Add(call.DestinationFloor);
            }

            return new PricingNode(
                CurrentFloor,
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

        /// <summary>
        /// מייצר צומת חדש להורדת נוסעים בקומה הנוכחית
        /// </summary>
        private PricingNode CreateDropNode()
        {
            // יצירת העתק של לוח הזמנים
            Schedule newSchedule = new Schedule(CurrentSchedule);

            // יצירת עצירה חדשה להורדה
            Stop dropStop = new Stop
            {
                Floor = CurrentFloor,
                ArrivalTime = (float)CurrentTime,
                Direction = CurrentStop?.Direction ?? Direction.Idle
            };

            // מציאת קריאות להורדה בקומה הנוכחית
            List<Call> dropsHere = FindDropsAtCurrentFloor();

            // הוספת ההורדות לעצירה
            foreach (var call in dropsHere)
            {
                dropStop.AddDrop(call);
            }

            // הוספת העצירה ללוח הזמנים
            newSchedule.AddStop(dropStop);

            // חישוב עלויות עבור ההורדות
            double travelCost = 0;
            foreach (var call in dropsHere)
            {
                // מציאת זמן האיסוף של הקריאה
                double pickupTime = FindPickupTimeForCall(call);

                // זמן הנסיעה מזמן האיסוף עד זמן ההורדה
                double travelTime = CurrentTime - pickupTime;
                travelCost += call.TravelCost * travelTime;
            }

            // הוספת העלות ללוח הזמנים
            newSchedule.TotalCost += (float)travelCost;

            // זמן חדש אחרי ההורדה
            double newTime = CurrentTime + Constant.StopTime;

            // עומס חדש אחרי ההורדה
            int newLoad = CurrentLoad - dropsHere.Count;

            // עדכון drop commitments - הסרת הקומה הנוכחית
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

        /// <summary>
        /// מייצר צומת חדש למעבר לקומה אחרת
        /// </summary>
        private PricingNode CreateMoveNode(int nextFloor, Direction direction)
        {
            // יצירת העתק של לוח הזמנים
            Schedule newSchedule = new Schedule(CurrentSchedule);

            // חישוב זמן הנסיעה
            double travelTime = CalculateTravelTime(CurrentFloor, nextFloor);
            double newTime = CurrentTime + travelTime;

            // יצירת עצירה חדשה
            Stop moveStop = new Stop
            {
                Floor = nextFloor,
                ArrivalTime = (float)newTime,
                Direction = direction
            };

            // העתקת drop commitments
            if (CurrentStop != null)
            {
                foreach (int floor in CurrentStop.DropFloors)
                {
                    moveStop.DropFloors.Add(floor);
                }
            }

            // הוספת העצירה ללוח הזמנים
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

        /// <summary>
        /// מוצא את כל הקריאות שיש להוריד בקומה הנוכחית
        /// </summary>
        private List<Call> FindDropsAtCurrentFloor()
        {
            List<Call> drops = new List<Call>();

            // מעבר על כל העצירות בלוח הזמנים
            foreach (var stop in CurrentSchedule.Stops)
            {
                // מעבר על כל האיסופים בעצירה
                foreach (var pickup in stop.Pickups)
                {
                    // מעבר על כל הקריאות באיסוף
                    foreach (var call in pickup.Calls)
                    {
                        // אם היעד של הקריאה הוא הקומה הנוכחית
                        if (call.DestinationFloor == CurrentFloor)
                        {
                            drops.Add(call);
                        }
                    }
                }
            }

            return drops;
        }

        /// <summary>
        /// מוצא את זמן האיסוף של קריאה
        /// </summary>
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

        /// <summary>
        /// מחשב את זמן הנסיעה בין שתי קומות
        /// </summary>
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