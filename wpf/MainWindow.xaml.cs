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

namespace wpf
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            PromptForElevatorAndFloorCount();

        }
        public void PromptForElevatorAndFloorCount()
        {
            var inputDialog = new InputDialog();

            // הצגת הדיאלוג
            if (inputDialog.ShowDialog() == true)
            {
                // קבלת הקלט מ-InputDialog
                var input = inputDialog.UserInput;

                // הדפסת הקלט הגולמי
                Console.WriteLine($"Raw input received from dialog: '{input}'");

                // פיצול הקלט לפי פסיק
                var values = input.Split(',');

                // הדפסת הערכים המפולחים
                Console.WriteLine($"Parts after splitting: {string.Join(", ", values)}");

                if (values.Length == 2)
                {
                    var elevator = values[0].Trim(); // חיתוך של רווחים אם יש
                    var floor = values[1].Trim();    // חיתוך של רווחים אם יש

                    // הדפסת הערכים אחרי ה-trim
                    Console.WriteLine($"Elevator part after trim: '{elevator}'");
                    Console.WriteLine($"Floor part after trim: '{floor}'");

                    // נוודא אם המרת המספרים מצליחה
                    if (int.TryParse(elevator, out int numberOfElevators) && numberOfElevators > 0 &&
                        int.TryParse(floor, out int numberOfFloors) && numberOfFloors > 0)
                    {
                        FlorController.GenerateFlors(numberOfFloors);
                        // קריאה לפונקציה שמייצרת את הפירים
                        ElevatorController.CreateElevatorShafts(numberOfElevators, numberOfFloors);
                    }
                    else
                    {
                        // במקרה של שגיאה בהמרת המספרים
                        MessageBox.Show("Please enter valid numbers for both elevators and floors.");
                    }
                }
                else
                {
                    // במקרה של שגיאה בפורמט
                    MessageBox.Show("Invalid input format. Please enter two numbers separated by a comma.");
                }
            }
        }



    //    private void GenerateFloor(object sender, RoutedEventArgs e)
    //    {
    //        if (int.TryParse(InputTextBox.Text, out int count) && count > 0)
    //        {
    //            // קריאה לפונקציה שיוצרת תיבות טקסט
    //            FlorController.GenerateFlors(count);
    //        }
    //        else
    //        {
    //            MessageBox.Show("Please enter a valid positive number.");
    //        }
    //    }
    //    private void GenerateShafts(object sender, RoutedEventArgs e)
    //    {

    //    }


    }
}
