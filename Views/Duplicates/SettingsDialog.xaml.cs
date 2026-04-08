using System;
using System.Windows;

namespace OrganizadorDeFotos.DesktopApp.Views.Duplicates
{
    public partial class SettingsDialog : Window
    {
        public double TimeThreshold { get; private set; }
        public double SimilarityThreshold { get; private set; }

        public SettingsDialog(double currentTimeThreshold, double currentSimilarityThreshold)
        {
            InitializeComponent();

            TimeThresholdSlider.Value = Math.Max(1, Math.Min(30, currentTimeThreshold));
            SimilarityThresholdSlider.Value = Math.Max(0.60, Math.Min(0.99, currentSimilarityThreshold));
            UpdateTimeLabel();
            UpdateSimilarityLabel();
        }

        private void TimeThresholdSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (TimeThresholdValue != null)
            {
                UpdateTimeLabel();
            }
        }

        private void SimilarityThresholdSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (SimilarityThresholdValue != null)
            {
                UpdateSimilarityLabel();
            }
        }

        private void UpdateTimeLabel()
        {
            if (TimeThresholdValue == null || TimeThresholdSlider == null) return;
            TimeThresholdValue.Text = $"{TimeThresholdSlider.Value:0} s";
        }

        private void UpdateSimilarityLabel()
        {
            if (SimilarityThresholdValue == null || SimilarityThresholdSlider == null) return;
            SimilarityThresholdValue.Text = $"{SimilarityThresholdSlider.Value:0.00}";
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            TimeThreshold = Math.Max(1, Math.Min(30, TimeThresholdSlider.Value));
            SimilarityThreshold = Math.Max(0.60, Math.Min(0.99, SimilarityThresholdSlider.Value));
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}