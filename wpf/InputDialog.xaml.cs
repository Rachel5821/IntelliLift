using System;
using System.Windows;

namespace wpf
{
    public partial class InputDialog : Window
    {
        public string UserInput { get; private set; }

        public InputDialog()
        {
            InitializeComponent();
        }
        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            var input = ElevatorAndFloorTextBox.Text.Trim(); // הסרת רווחים מיותרים
            Console.WriteLine($"Input entered (trimmed): '{input}'");

            var parts = input.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries); // חיתוך בקווים או רווחים
            Console.WriteLine($"Parts: {string.Join(", ", parts)}");

            if (parts.Length == 2) // אם יש שני חלקים (Elevators, Floors)
            {
                // נוודא שהשניים הם מספרים חיוביים
                bool isElevatorValid = int.TryParse(parts[0], out int elevators);
                bool isFloorValid = int.TryParse(parts[1], out int floors);

                // הדפסת תוצאות הבדיקה
                Console.WriteLine($"Elevator valid: {isElevatorValid}, Floors valid: {isFloorValid}");
                Console.WriteLine($"Elevators: {elevators}, Floors: {floors}");

                // אם שני הערכים הם מספרים חיוביים
                if (isElevatorValid && elevators > 0 && isFloorValid && floors > 0)
                {
                    // הקלט תקין
                    UserInput = $"{elevators},{floors}";
                    this.DialogResult = true; // סוגר את הדיאלוג
                }
                else
                {
                    MessageBox.Show("Please enter positive numbers for both elevators and floors.");
                }
            }
            else
            {
                MessageBox.Show("Please enter valid input in the format: 'Number of Elevators, Number of Floors'.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }



    }
}
