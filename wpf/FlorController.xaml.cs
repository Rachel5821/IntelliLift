using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using MaterialDesignThemes.Wpf;

namespace wpf
{
    /// <summary>
    /// Interaction logic for FlorController.xaml
    /// </summary>
    public partial class FlorController : UserControl
    {
        // שדות למעקב אחר הקומה הנבחרת ואירוע שינוי
        private int _selectedFloor = -1;
        private Random _random = new Random();
        public event EventHandler<int> FloorSelected;

        public FlorController()
        {
            InitializeComponent();
        }

        public void GenerateFlors(int count)
        {
            ContentPanel.Children.Clear();
            // יצירת פנלים לארגון הכפתורים בצורה נוחה
            WrapPanel floorPanel = new WrapPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(5)
            };
            ContentPanel.Children.Add(floorPanel);

            // יצירת כפתורי הקומות בסגנון צג מעלית
            for (int i = count; i >= 1; i--)
            {
                int floorNumber = i;
                string floorText = i.ToString();

                // מסגרת לכפתור שמדמה צג של מעלית - קטן יותר כמבוקש
                Border elevatorDisplay = new Border
                {
                    Width = 60,  // הקטנה ל-60 במקום 80
                    Height = 60, // הקטנה ל-60 במקום 80
                    Margin = new Thickness(6), // הקטנת המרווח גם כן
                    Background = new SolidColorBrush(Color.FromRgb(20, 20, 20)),
                    CornerRadius = new CornerRadius(6),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(70, 70, 70)),
                    BorderThickness = new Thickness(2),
                    Effect = new System.Windows.Media.Effects.DropShadowEffect
                    {
                        Color = Colors.Black,
                        Direction = 320,
                        ShadowDepth = 3, // הקטנת הצל
                        Opacity = 0.5,
                        BlurRadius = 7  // הקטנת הטשטוש
                    }
                };

                // צג LED שנראה כמו במעלית
                Grid displayGrid = new Grid();
                elevatorDisplay.Child = displayGrid;

                // הוספת רקע גרדיאנט שמדמה זכוכית/מסך
                Rectangle glassEffect = new Rectangle
                {
                    Fill = new LinearGradientBrush
                    {
                        StartPoint = new Point(0, 0),
                        EndPoint = new Point(1, 1),
                        GradientStops = new GradientStopCollection
                        {
                            new GradientStop(Color.FromArgb(40, 100, 100, 100), 0),
                            new GradientStop(Color.FromArgb(30, 50, 50, 50), 1)
                        }
                    },
                    RadiusX = 5,
                    RadiusY = 5
                };
                displayGrid.Children.Add(glassEffect);

                // הוספת המספר בפונט דיגיטלי
                TextBlock floorNumberText = new TextBlock
                {
                    Text = floorText,
                    FontSize = 26, // הקטנת גודל הפונט
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(Color.FromRgb(255, 140, 0)), // כתום-אדום LED
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    FontFamily = new FontFamily("Courier New"), // פונט דיגיטלי
                    Effect = new System.Windows.Media.Effects.BlurEffect
                    {
                        Radius = 0.7,
                        KernelType = System.Windows.Media.Effects.KernelType.Gaussian
                    }
                };
                displayGrid.Children.Add(floorNumberText);

                // הוספת אינטראקטיביות
                elevatorDisplay.Cursor = Cursors.Hand;
                elevatorDisplay.MouseEnter += (sender, e) =>
                {
                    elevatorDisplay.BorderBrush = new SolidColorBrush(Color.FromRgb(255, 140, 0));
                    floorNumberText.Foreground = new SolidColorBrush(Color.FromRgb(255, 200, 0));
                };
                elevatorDisplay.MouseLeave += (sender, e) =>
                {
                    // אם לא נבחר - חזרה לצבע רגיל
                    if (_selectedFloor != floorNumber)
                    {
                        elevatorDisplay.BorderBrush = new SolidColorBrush(Color.FromRgb(70, 70, 70));
                        floorNumberText.Foreground = new SolidColorBrush(Color.FromRgb(255, 140, 0));
                    }
                };

                // הוספת אירוע לחיצה - עם מספר  כמבוקש
                elevatorDisplay.MouseDown += (sender, e) =>
                {
                    // בחירת קומה והדגשה
                    _selectedFloor = floorNumber;

                    // יצירת מספר רנדומלי בין 1 ל-100
                    int randomNumber = _random.Next(1, count);

                    // שינוי הטקסט למספר הרנדומלי
                    floorNumberText.Text = randomNumber.ToString();

                    // אפקט ויזואלי של לחיצה על כפתור
                    elevatorDisplay.BorderBrush = new SolidColorBrush(Color.FromRgb(255, 255, 0));
                    floorNumberText.Foreground = new SolidColorBrush(Color.FromRgb(255, 255, 0));

                    // הפעלת אירוע שהקומה נבחרה
                    FloorSelected?.Invoke(this, randomNumber);
                };

                // הוספה לפנל
                floorPanel.Children.Add(elevatorDisplay);
            }
        }
    }
}