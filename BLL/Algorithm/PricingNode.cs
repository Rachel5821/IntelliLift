using Microsoft.VisualBasic;
using Model;
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
        public double currentTime { get; private set; }
        public int currentLoad { get; private set; }
        public HashSet<Request> ServedRequests { get; set; }
        public List<Request> UnServedRequests { get; set; }
        public Schedule CurrentSchedule { get; private set; }
        public Stop CurrentStop { get; private set; }

        public PricingNode(int currentFloor, HashSet<Request> servedRequests, List<Request> unServedRequests, Schedule currentSchedule, int currentLoad,
                double currentTime)
        {
            CurrentFloor = currentFloor;
            this.currentTime = currentTime;
            this.currentLoad = currentLoad;
            ServedRequests = servedRequests ?? new HashSet<Request>();
            UnServedRequests = unServedRequests ?? new List<Request>();
            CurrentSchedule = currentSchedule ?? throw new ArgumentNullException(nameof(currentSchedule));

            // לוקח את העצירה האחרונה בלוח הזמנים כעצירה הנוכחית
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
            if (currentLoad > 0)
            {
                return false;
            }
            return true;
        }
        public double GetReducedCost(double[] requestDuals, double elevatorDual)
        {
            // העלות המופחתת היא העלות של לוח הזמנים פחות הערכים הדואליים

            double cost = CurrentSchedule.TotalCost;
            double dualSum = 0;

            // חישוב סכום הערכים הדואליים של הבקשות המשורתות
            foreach (var request in ServedRequests)
            {
                // נניח שיש דרך לקבל את האינדקס של הבקשה במערך הבקשות הכללי
                int requestIndex = GetRequestIndex(request);
                if (requestIndex >= 0 && requestIndex < requestDuals.Length)
                {
                    dualSum += requestDuals[requestIndex];
                }
            }

            // החזרת העלות המופחתת
            return cost - dualSum - elevatorDual;
        }
        private PricingNode CreateMoveNode(int nextFloor)
        {
            // יצירת העתק של לוח הזמנים
            Schedule newSchedule = new Schedule(CurrentSchedule);

            // חישוב זמן הנסיעה לקומה הבאה
            double travelTime = CalculateTravelTime(CurrentFloor, nextFloor);
            double newTime = currentTime + travelTime;

            // יצירת עצירה חדשה
            Stop moveStop = new Stop
            {
                Floor = nextFloor,
                ArrivalTime = (float)newTime,
                Direction = CurrentStop?.Direction ?? Direction.Idle
            };

            // הוספת העצירה ללוח הזמנים
            newSchedule.AddStop(moveStop);

            // יצירת צומת חדשה - שים לב שמעבירים את הקומה החדשה וזמן חדש
            return new PricingNode(
                nextFloor,  // קומה חדשה!
                ServedRequests,
                UnServedRequests,
                newSchedule,  // לוח זמנים חדש!
                currentLoad,
                newTime);  // זמן חדש!
        }


        private double CalculateTravelTime(int fromFloor, int toFloor)
        {
            // חישוב זמן הנסיעה בין שתי קומות
            int distance = Math.Abs(toFloor - fromFloor);
            if (distance == 0) return 0;

            return Constant.ElevatorStartupTime + distance * Constant.DrivePerFloorTime;
        }
        private int GetRequestIndex(Request request)
        {
            return request.Id;
        }
        public Schedule GetSchedule()
        {
            return CurrentSchedule;
        }
        private int GetElevatorCapacity()//??
        {
            // פונקציה שמחזירה את הקיבולת של המעלית
            // יש להתאים זאת למבנה הנתונים של המערכת
            return 10; // ערך לדוגמה
        }


        public List<PricingNode> Branch()
        {
            List<PricingNode> children = new List<PricingNode>();

            // קביעת הכיוון הנוכחי של המעלית
            Direction currentDirection = Direction.Idle;
            if (CurrentStop != null)
            {
                currentDirection = CurrentStop.Direction;
            }

            // במערכת IA, יש חשיבות לכיוון הנסיעה - 
            // אפשר לאסוף רק בקשות התואמות את כיוון הנסיעה הנוכחי

            // צומת לאיסוף בקשות בקומה הנוכחית
            foreach (var request in UnServedRequests.Where(r => r.StartFloor == CurrentFloor))
            {
                // רק אם הכיוון תואם או שהמעלית במצב Idle
                Direction requestDirection = request.StartFloor < request.DestinationFloor ?
                                            Direction.Up : Direction.Down;

                if (currentDirection == Direction.Idle || currentDirection == requestDirection)
                {
                    // בדיקת קיבולת
                    if (currentLoad + request.Calls.Count <= GetElevatorCapacity())
                    {
                        PricingNode childNode = CreatePickUpNode(request);
                        children.Add(childNode);
                    }
                }
            }

            // צומת להורדה - רק אם יש הורדות בקומה הנוכחית
            if (HasDrops())
            {
                PricingNode childNode = CreateDropNode();
                children.Add(childNode);
            }

            // צומת לנסיעה לקומה הבאה (למעלה או למטה)
            if (currentDirection != Direction.Idle)
            {
                int nextFloor = CurrentFloor + (currentDirection == Direction.Up ? 1 : -1);
                if (IsValidFloor(nextFloor))
                {
                    PricingNode childNode = CreateMoveNode(nextFloor);
                    children.Add(childNode);
                }
            }

            return children;
        }

        private bool HasDrops()
        {
            // בדיקה אם יש הורדות בקומה הנוכחית
            if (CurrentStop != null && CurrentStop.DropFloors.Contains(CurrentFloor))
            {
                return true;
            }

            // בדיקה נוספת - אם יש נוסעים שיעדם הוא הקומה הנוכחית
            foreach (var stop in CurrentSchedule.Stops)
            {
                foreach (var pickup in stop.Pickups)
                {
                    if (pickup.DestinationFloor == CurrentFloor)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private bool IsValidFloor(int floor)
        {
            // בדיקה אם הקומה תקפה (בתוך טווח הקומות של הבניין)
            // יש להתאים זאת למבנה הנתונים של המערכת
            return floor >= 1 && floor <= 20; //??איך אני  משתמשת פה בnumFloors
        }

        private PricingNode CreatePickUpNode(Request request)
        {
            // יצירת העתק של הבקשות המשורתות והוספת הבקשה החדשה
            HashSet<Request> newServedRequests = new HashSet<Request>(ServedRequests);
            newServedRequests.Add(request);

            // יצירת העתק של הבקשות הלא משורתות והסרת הבקשה שנאספה
            List<Request> newUnServedRequests = new List<Request>(UnServedRequests);
            newUnServedRequests.Remove(request);

            // יצירת העתק של לוח הזמנים
            Schedule newSchedule = new Schedule(CurrentSchedule);

            // יצירת עצירה חדשה לאיסוף
            Stop pickupStop = new Stop
            {
                Floor = CurrentFloor,
                ArrivalTime = (float)currentTime,
                Direction = request.StartFloor < request.DestinationFloor ? Direction.Up : Direction.Down
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
                double waitTime = Math.Max(0, currentTime - call.ReleaseTime.ToOADate());
                waitCost += call.WaitCost * waitTime;
            }

            // הוספת העלות ללוח הזמנים
            newSchedule.TotalCost += (float)waitCost;

            // זמן חדש אחרי האיסוף
            double newTime = currentTime + Constant.StopTime;

            // עומס חדש אחרי האיסוף
            int newLoad = currentLoad + request.Calls.Count;

            // יצירת צומת חדשה
            return new PricingNode(
                CurrentFloor,
                newServedRequests,
                newUnServedRequests,
                newSchedule,
                newLoad,
                newTime
                );
        }
        private PricingNode CreateDropNode()
        {
            // יצירת העתק של לוח הזמנים
            Schedule newSchedule = new Schedule(CurrentSchedule);

            // יצירת עצירה חדשה להורדה
            Stop dropStop = new Stop
            {
                Floor = CurrentFloor,
                ArrivalTime = (float)currentTime,
                Direction = CurrentStop?.Direction ?? Direction.Idle
            };

            // מציאת קריאות להורדה בקומה הנוכחית
            List<Call> dropsHere = new List<Call>();
            foreach (var stop in CurrentSchedule.Stops)
            {
                foreach (var pickup in stop.Pickups)
                {
                    foreach (var call in pickup.Calls)
                    {
                        if (call.DestinationFloor == CurrentFloor)
                        {
                            dropsHere.Add(call);
                        }
                    }
                }
            }

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
                double pickupTime = 0;
                foreach (var stop in CurrentSchedule.Stops)
                {
                    foreach (var pickup in stop.Pickups)
                    {
                        if (pickup.Calls.Contains(call))
                        {
                            pickupTime = stop.ArrivalTime;
                            break;
                        }
                    }
                    if (pickupTime > 0) break;
                }

                // זמן הנסיעה מזמן האיסוף עד זמן ההורדה
                double travelTime = currentTime - pickupTime;
                travelCost += call.TravelCost * travelTime;
            }

            // הוספת העלות ללוח הזמנים
            newSchedule.TotalCost += (float)travelCost;

            // זמן חדש אחרי ההורדה
            double newTime = currentTime + Constant.StopTime;

            // עומס חדש אחרי ההורדה
            int newLoad = currentLoad - dropsHere.Count;

            // יצירת צומת חדשה
            return new PricingNode(
                CurrentFloor,
                ServedRequests,
                UnServedRequests,
                newSchedule,  // לוח זמנים חדש!
                newLoad,  // עומס חדש!
                newTime);  // זמן חדש!
        }
    }
}
