using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace wpf
{
    public partial class ElevatorController : UserControl
    {
        public ElevatorController()
        {
            InitializeComponent();
        }

        public void CreateElevatorShafts(int numberOfElevators, int numberOfFloors)
        {
            ContentPanel.Children.Clear();
            ContentPanel.VerticalAlignment = VerticalAlignment.Stretch;

            Grid mainGrid = new Grid
            {
                Width = Double.NaN,
                Height = Double.NaN,
                VerticalAlignment = VerticalAlignment.Stretch
            };

            // הוספת שורות - כותרת למעלה ופיר המעלית למטה
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            // הוספת עמודות לפי מספר המעליות
            for (int i = 0; i < numberOfElevators; i++)
            {
                mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                if (i < numberOfElevators - 1)
                {
                    mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) }); // רווח בין הפירים
                }
            }

            for (int i = 0, col = 0; i < numberOfElevators; i++, col += 2)
            {
                // כותרת המעלית
                Border headerRectangle = new Border
                {
                    Height = 30,
                    Background = Brushes.Gray,
                    CornerRadius = new CornerRadius(5, 5, 0, 0)
                };

                Grid headerGrid = new Grid();
                headerGrid.ColumnDefinitions.Add(new ColumnDefinition());
                headerGrid.ColumnDefinitions.Add(new ColumnDefinition());
                headerGrid.ColumnDefinitions.Add(new ColumnDefinition());

                TextBlock upArrow = new TextBlock
                {
                    Text = "↑",
                    FontSize = 16,
                    Foreground = Brushes.White,
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                Grid.SetColumn(upArrow, 2);
                headerGrid.Children.Add(upArrow);

                TextBlock downArrow = new TextBlock
                {
                    Text = "↓",
                    FontSize = 16,
                    Foreground = Brushes.White,
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                Grid.SetColumn(downArrow, 0);
                headerGrid.Children.Add(downArrow);

                TextBlock elevatorNumber = new TextBlock
                {
                    Text = (i + 1).ToString(),
                    FontSize = 16,
                    Foreground = Brushes.White,
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                Grid.SetColumn(elevatorNumber, 1);
                headerGrid.Children.Add(elevatorNumber);

                headerRectangle.Child = headerGrid;
                Grid.SetRow(headerRectangle, 0);
                Grid.SetColumn(headerRectangle, col);
                mainGrid.Children.Add(headerRectangle);

                // פיר המעלית
                Grid elevatorGrid = new Grid
                {
                    Background = Brushes.LightGray,
                    VerticalAlignment = VerticalAlignment.Stretch,
                    HorizontalAlignment = HorizontalAlignment.Stretch
                };

                for (int j = 0; j < numberOfFloors; j++)
                {
                    elevatorGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                }

                for (int j = 0; j < numberOfFloors; j++)
                {
                    TextBlock floorNumber = new TextBlock
                    {
                        Text = (numberOfFloors - j).ToString(),
                        FontSize = 14,
                        Foreground = Brushes.Black,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    Grid.SetRow(floorNumber, j);
                    elevatorGrid.Children.Add(floorNumber);
                }

                Image elevator = new Image
                {
                    Width = 80,
                    Height = 100,
                    Source = new BitmapImage(new Uri("C:\\Users\\User\\Desktop\\פרויקט גמר\\IntelliLift\\wpf\\Images\\closedElevator.png")),
                    Stretch = Stretch.UniformToFill
                };
                Grid.SetRow(elevator, numberOfFloors - 1);
                elevatorGrid.Children.Add(elevator);

                Grid.SetRow(elevatorGrid, 1);
                Grid.SetColumn(elevatorGrid, col);
                mainGrid.Children.Add(elevatorGrid);
            }

            ContentPanel.Children.Add(mainGrid);
        }
    }
}
