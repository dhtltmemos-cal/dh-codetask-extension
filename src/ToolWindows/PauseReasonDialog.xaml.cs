using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace DhCodetaskExtension.ToolWindows
{
    /// <summary>
    /// Simple dialog to select a pause reason from a predefined list.
    /// </summary>
    public partial class PauseReasonDialog : Window
    {
        public string SelectedReason { get; private set; } = string.Empty;

        public PauseReasonDialog(IList<string> reasons)
        {
            InitializeComponent();
            Topmost = true;
            foreach (var r in reasons)
            {
                var item = new RadioButton
                {
                    Content = r,
                    Margin  = new Thickness(0, 2, 0, 2),
                    FontSize = 11,
                    GroupName = "reason"
                };
                ReasonPanel.Children.Add(item);
            }
            // Pre-select first
            if (ReasonPanel.Children.Count > 0)
                ((RadioButton)ReasonPanel.Children[0]).IsChecked = true;
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            foreach (RadioButton rb in ReasonPanel.Children)
                if (rb.IsChecked == true) { SelectedReason = rb.Content?.ToString() ?? string.Empty; break; }
            if (string.IsNullOrEmpty(SelectedReason))
                SelectedReason = TxtCustom.Text?.Trim() ?? string.Empty;
            else if (!string.IsNullOrWhiteSpace(TxtCustom.Text))
                SelectedReason = TxtCustom.Text.Trim();
            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
