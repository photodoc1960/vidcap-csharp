using System;
using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace EegScreenCapture.UI
{
    public partial class RegionSelectorWindow : Window
    {
        private bool _isDragging;
        private System.Windows.Point _startPoint;
        public Rectangle SelectedRegion { get; private set; }

        public RegionSelectorWindow()
        {
            InitializeComponent();
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                _isDragging = true;
                _startPoint = e.GetPosition(this);
                SelectionRectangle.Visibility = Visibility.Visible;
                Canvas.SetLeft(SelectionRectangle, _startPoint.X);
                Canvas.SetTop(SelectionRectangle, _startPoint.Y);
                SelectionRectangle.Width = 0;
                SelectionRectangle.Height = 0;
            }
        }

        private void Window_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isDragging)
            {
                var currentPoint = e.GetPosition(this);
                var x = Math.Min(_startPoint.X, currentPoint.X);
                var y = Math.Min(_startPoint.Y, currentPoint.Y);
                var width = Math.Abs(currentPoint.X - _startPoint.X);
                var height = Math.Abs(currentPoint.Y - _startPoint.Y);

                Canvas.SetLeft(SelectionRectangle, x);
                Canvas.SetTop(SelectionRectangle, y);
                SelectionRectangle.Width = width;
                SelectionRectangle.Height = height;
            }
        }

        private void Window_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left && _isDragging)
            {
                _isDragging = false;
                InstructionText.Text = "Selection made. Press ENTER to confirm, or drag again to reselect.";
            }
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (SelectionRectangle.Width > 10 && SelectionRectangle.Height > 10)
                {
                    // Get DPI scaling factor
                    var source = PresentationSource.FromVisual(this);
                    double dpiScaleX = 1.0;
                    double dpiScaleY = 1.0;

                    if (source != null)
                    {
                        dpiScaleX = source.CompositionTarget.TransformToDevice.M11;
                        dpiScaleY = source.CompositionTarget.TransformToDevice.M22;
                    }

                    // Convert WPF logical pixels to physical screen pixels
                    var x = (int)(Canvas.GetLeft(SelectionRectangle) * dpiScaleX);
                    var y = (int)(Canvas.GetTop(SelectionRectangle) * dpiScaleY);
                    var width = (int)(SelectionRectangle.Width * dpiScaleX);
                    var height = (int)(SelectionRectangle.Height * dpiScaleY);

                    SelectedRegion = new Rectangle(x, y, width, height);

                    // Log for debugging
                    System.IO.File.AppendAllText("region-selection-debug.log",
                        $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - DPI Scale: {dpiScaleX}x{dpiScaleY}\n" +
                        $"  WPF Logical: ({Canvas.GetLeft(SelectionRectangle)}, {Canvas.GetTop(SelectionRectangle)}) {SelectionRectangle.Width}x{SelectionRectangle.Height}\n" +
                        $"  Physical: ({x}, {y}) {width}x{height}\n");

                    DialogResult = true;
                    Close();
                }
                else
                {
                    MessageBox.Show("Selection too small. Please select a larger area.", "Invalid Selection",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            else if (e.Key == Key.Escape)
            {
                DialogResult = false;
                Close();
            }
        }
    }
}
