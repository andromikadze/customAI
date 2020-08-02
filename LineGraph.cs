using System;
using System.Collections.Generic;
using System.Data;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace CustomAI {
	class LineGraph {
        private readonly Canvas canvas;
        private readonly List<Line> xIntervals = new List<Line>();
        private readonly List<Point> points = new List<Point>();
        private readonly List<Line> connections = new List<Line>();

        private double canvasSize, xInterval, yInterval, diameter, highestValue = 1;

        public LineGraph(Canvas canvas) {
            this.canvas = canvas;
        }
        public void updateGraph() {
            xInterval = canvas.ActualWidth / Math.Max(points.Count - 1, 1);
            yInterval = canvas.ActualHeight / highestValue;
            canvasSize = canvas.ActualWidth * canvas.ActualHeight;
            diameter = canvasSize / 100000;
            
            clearAll();
            drawCoordinateSystem();

            updatePoints();
            foreach (Point point in points)
                canvas.Children.Add(point.ellipse);

            updateConnections();
            foreach (Line connection in connections)
                canvas.Children.Add(connection);
        }
        public void resetGraph() {
            clearXIntervals();
            clearPoints();
            highestValue = 1;
		}

        private void drawCoordinateSystem() {
            drawXAxis();
            drawYAxis();
            drawXIntervals();
            drawYIntervals();
        }
        private void drawXAxis() {
            for (int i = 0; i <= 10; i++) {
                Line line = new Line {
                    X1 = 0,
                    Y1 = canvas.ActualHeight / 10 * i,
                    X2 = canvas.ActualWidth,
                    Y2 = canvas.ActualHeight / 10 * i,
                    Stroke = Brushes.Gray,
                    StrokeThickness = canvasSize / (i == 10 ? 300000 : 1000000)
                };

                canvas.Children.Add(line);
            }
        }
        private void drawYAxis() {
            Line line = new Line {
                X1 = 0,
                Y1 = 0,
                X2 = 0,
                Y2 = canvas.ActualHeight,
                Stroke = Brushes.Gray,
                StrokeThickness = canvasSize / 300000
            };

            canvas.Children.Add(line);
        }
        private void drawXIntervals() {
            for (int i = 0; i < Math.Min(points.Count, 11); i++) {
                Line line = new Line {
                    X1 = xInterval * Math.Round(i * (points.Count - 1) / Math.Min(Math.Max(points.Count - 1, 1.0), 10.0)),
                    Y1 = canvas.ActualHeight - canvasSize / 200000,
                    X2 = xInterval * Math.Round(i * (points.Count - 1) / Math.Min(Math.Max(points.Count - 1, 1.0), 10.0)),
                    Y2 = canvas.ActualHeight + canvasSize / 200000,
                    Stroke = Brushes.Gray,
                    StrokeThickness = canvasSize / 500000
                };

                xIntervals.Add(line);
                canvas.Children.Add(line);
            }
        }
        private void drawYIntervals() {
            for (int i = 0; i <= 10; i++) {
                Line line = new Line {
                    X1 = canvasSize / -200000,
                    Y1 = canvas.ActualHeight / 10 * i,
                    X2 = canvasSize / 200000,
                    Y2 = canvas.ActualHeight / 10 * i,
                    Stroke = Brushes.Gray,
                    StrokeThickness = canvasSize / 500000
                };

                canvas.Children.Add(line);
			}
        }

        public void addPoint(double value) {
            if (value > highestValue)
                highestValue = value;
            xInterval = canvas.ActualWidth / Math.Max(points.Count, 1);
            yInterval = canvas.ActualHeight / highestValue;

            Point point = new Point(points.Count, value, diameter, canvas);
            point.updatePosition(xInterval, yInterval, canvas.ActualHeight, diameter);
            points.Add(point);
            canvas.Children.Add(point.ellipse);
            updatePoints();

            clearXIntervals();
            drawXIntervals();

            if (points.Count > 1)
                addConnection();
            updateConnections();
        }
        private void updatePoints() {
            for (int epoch = 0; epoch < points.Count; epoch++)
                points[epoch].updatePosition(xInterval, yInterval, canvas.ActualHeight, diameter);
		}
        
        private void addConnection() {
            Point point = points[points.Count - 1];
            Point previousPoint = points[points.Count - 2];

            Line line = new Line() {
                X1 = Canvas.GetLeft(point.ellipse) + diameter / 2,
                Y1 = Canvas.GetTop(point.ellipse) + diameter / 2,
                X2 = Canvas.GetLeft(previousPoint.ellipse) + diameter / 2,
                Y2 = Canvas.GetTop(previousPoint.ellipse) + diameter / 2,
                Stroke = Brushes.White,
                StrokeThickness = canvasSize / 300000
            };

            connections.Add(line);
            canvas.Children.Add(line);
        }
        private void updateConnections() {
            for (int point = 1; point < points.Count; point++) {
                connections[point - 1].X1 = Canvas.GetLeft(points[point].ellipse) + diameter / 2;
                connections[point - 1].Y1 = Canvas.GetTop(points[point].ellipse) + diameter / 2;
                connections[point - 1].X2 = Canvas.GetLeft(points[point - 1].ellipse) + diameter / 2;
                connections[point - 1].Y2 = Canvas.GetTop(points[point - 1].ellipse) + diameter / 2;
                connections[point - 1].StrokeThickness = canvasSize / 300000;
            }
		}

        private void clearPoints() {
            foreach (Point point in points)
                canvas.Children.Remove(point.ellipse);
            points.Clear();
            foreach (Line line in connections)
                canvas.Children.Remove(line);
            connections.Clear();
        }
        private void clearXIntervals() {
            foreach (Line line in xIntervals)
                canvas.Children.Remove(line);
            xIntervals.Clear();
        }
        private void clearAll() {
            xIntervals.Clear();
            canvas.Children.Clear();
        }

        private class Point {
            private readonly Canvas canvas;
            public readonly Ellipse ellipse = new Ellipse() {
                Fill = Brushes.White,
            };
            private readonly StackPanel info = new StackPanel() { Background = Brushes.DimGray };
            private readonly double epoch, value;

            public Point(int epoch, double value, double diameter, Canvas canvas) {
                this.epoch = epoch;
                this.value = value;
                this.canvas = canvas;
                createInfo();
                
				ellipse.MouseEnter += Ellipse_MouseEnter;
                info.MouseLeave += G_MouseLeave;
            }
			private void createInfo() {
                Label x = new Label() {
                    Content = "Epoch: " + epoch,
                    Foreground = Brushes.White,
                    HorizontalContentAlignment = HorizontalAlignment.Center
                };
                info.Children.Add(x);

                Label y = new Label() {
                    Content = "Value: " + Math.Round(value, 2),
                    Foreground = Brushes.White,
                    HorizontalContentAlignment = HorizontalAlignment.Center
                };
                info.Children.Add(y);
            }
            
            public void updatePosition(double xInterval, double yInterval, double canvasHeight, double diameter) {
                ellipse.Width = diameter;
                ellipse.Height = diameter;
                (info.Children[0] as Label).FontSize = Math.Max(diameter, 1);
                (info.Children[1] as Label).FontSize = Math.Max(diameter, 1);

                Canvas.SetLeft(ellipse, xInterval * epoch - diameter / 2);
                Canvas.SetTop(ellipse, canvasHeight - yInterval * value - diameter / 2);
                Canvas.SetLeft(info, xInterval * epoch - diameter / 2);
                Canvas.SetTop(info, canvasHeight - yInterval * value - diameter / 2);
            }

			private void Ellipse_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e) {
                List<StackPanel> stackPanels = new List<StackPanel>();
                for (int i = 0; i < canvas.Children.Count; i++)
                    if (canvas.Children[i] is StackPanel)
                        stackPanels.Add(canvas.Children[i] as StackPanel);
                foreach (StackPanel stackPanel in stackPanels)
                    canvas.Children.Remove(stackPanel);

                canvas.Children.Add(info);
			}
            private void G_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e) {
                canvas.Children.Remove(info);
            }
        }
    }
}
