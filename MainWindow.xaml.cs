using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Shapes;

namespace CustomAI {
    public partial class MainWindow : Window {
        private string dataPath;
        private readonly Process process = new Process();
        private bool running = false;
        private DateTime timer;

        private enum stage { DATASET, TOPOLOGY, SIZE, EPOCH, TRAIN_PROGRESS, VALID_PROGRESS, ERROR, TEST, TEST_PROGRESS };
        private Thread messageReceiver;
        private readonly Queue<string> messageQueue = new Queue<string>();
        private Paragraph topologyStats, datasetStats, epochStats;

        private readonly List<LineGraph> graphs = new List<LineGraph>();
        private Inline trainProgress = new Run("\nTraining... ");
        private Inline validProgress = new Run("\nValidating... ");
        private Inline testProgress = new Run("\nMeasuring... ");

        public MainWindow() {
            InitializeComponent();
        }
        private void Window_Loaded(object sender, RoutedEventArgs e) {
            lockSettings();
			TrainingSlider.ValueChanged += Ratio_ValueChanged;
            ValidationSlider.ValueChanged += Ratio_ValueChanged;
            initGraphs();
            initProcess();
        }
        private void Window_Closing(object sender, CancelEventArgs e) {
            if (running)
                process.Kill();
            if (messageReceiver != null && messageReceiver.IsAlive)
                messageReceiver.Abort();
        }

        private void initGraphs() {
            graphs.Add(new LineGraph(MAE.Children[0] as Canvas));
            graphs.Add(new LineGraph(MSE.Children[0] as Canvas));
            graphs.Add(new LineGraph(RMSE.Children[0] as Canvas));
        }
        private void initProcess() {
            string error = "";

            process.StartInfo = new ProcessStartInfo() {
                FileName = findPythonDirectory(),
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            process.OutputDataReceived += new DataReceivedEventHandler((_sender, _e) => {
                if (_e.Data != null) {
                    string data = _e.Data;
                    messageQueue.Enqueue(data);
                }
            });

            process.ErrorDataReceived += new DataReceivedEventHandler((_sender, _e) => {
                if (_e.Data != null) {
                    string data = _e.Data;
                    error += data.Trim();
                }
            });

            process.EnableRaisingEvents = true;
            process.Exited += new EventHandler((_sender, _e) => {
                process.CancelOutputRead();
                process.CancelErrorRead();
                process.Close();

                Dispatcher.Invoke(() => {
                    if (error != "") {
                        if (epochStats == null) {
                            datasetStats.Inlines.Add(new Run(error.Trim()) { Foreground = Brushes.Red });
                            Log.Document.Blocks.Add(datasetStats);
                        } else {
                            epochStats.Inlines.Add(new Run("\n" + error.Trim()) { Foreground = Brushes.Red });
                            Log.Document.Blocks.Add(epochStats);
						}
                        error = "";
                        Statistics.SelectedIndex = 0;
                        Log.ScrollToEnd();
                    }

                    unlockSettings();
                    running = false;
                    Train.Content = "Train";
                });
            });
        }
        private void recieveMessage() {
            while (running) {
                while (messageQueue.Count > 0) {
                    string data = messageQueue.Dequeue();
                    if (data == null)
                        continue;
                    int code = Int32.Parse(data.Substring(0, 1));
                    if (data.Length > 1)
                        data = data.Substring(2);
                    if (messageQueue.Count > 10)
                        Thread.Sleep(1);

                    Dispatcher.Invoke(() => {
                        switch (code) {
                            case (int)stage.DATASET:
                                datasetStats = new Paragraph();
                                datasetStats.Inlines.Add(new Run("\nDistributing data..."));
                                Log.Document.Blocks.Add(datasetStats);
                                break;
                            case (int)stage.TOPOLOGY:
                                topologyStats.Inlines.InsertAfter(topologyStats.Inlines.FirstInline, new Run("\nNetwork topology: " + data + " " + (Topology.Text.Trim() == "" ? "1" : Topology.Text.Trim() + " 1")));
                                break;
                            case (int)stage.SIZE:
                                int[] sizes = Array.ConvertAll(data.Split(' '), Int32.Parse);
                                datasetStats.Inlines.Add(new Run(" done.") { Foreground = Brushes.Lime });
                                datasetStats.Inlines.Add(new Run("\nTraining dataset: " + sizes[0] + "\nValidation dataset: " + sizes[1] + "\nTest dataset: " + sizes[2]));
                                break;
                            case (int)stage.EPOCH:
                                trainProgress = new Run("\nTraining... ");
                                validProgress = new Run("\nValidating... ");

                                epochStats = new Paragraph();
                                epochStats.Inlines.Add(new Run("\nEpoch " + data + ":"));
                                Log.Document.Blocks.Add(epochStats);
                                if (Statistics.SelectedIndex == 0)
                                    Log.ScrollToEnd();
                                break;
                            case (int)stage.TRAIN_PROGRESS:
                                if (!epochStats.Inlines.Contains(trainProgress)) {
                                    epochStats.Inlines.InsertAfter(epochStats.Inlines.LastInline, trainProgress);
                                    epochStats.Inlines.InsertAfter(epochStats.Inlines.LastInline, new Run("0%"));
                                }
                                epochStats.Inlines.Remove(epochStats.Inlines.LastInline);
                                epochStats.Inlines.InsertAfter(trainProgress, new Run(Int32.Parse(data) + "%") { Foreground = Brushes.Lime });
                                break;
                            case (int)stage.VALID_PROGRESS:
                                if (!epochStats.Inlines.Contains(validProgress)) {
                                    epochStats.Inlines.InsertAfter(epochStats.Inlines.LastInline, validProgress);
                                    epochStats.Inlines.InsertAfter(epochStats.Inlines.LastInline, new Run("0%"));
                                }
                                epochStats.Inlines.Remove(epochStats.Inlines.LastInline);
                                epochStats.Inlines.InsertAfter(validProgress, new Run(Int32.Parse(data) + "%") { Foreground = Brushes.Lime });
                                break;
                            case (int)stage.ERROR:
                                double[] errors = Array.ConvertAll(data.Split(' '), Double.Parse);
                                epochStats.Inlines.Add(new Run("\nMAE: " + errors[1] + "\nMSE: " + errors[2] + "\nRMSE: " + errors[3]));
                                if (errors[0] == 1) {
                                    graphs[0].addPoint(errors[1]);
                                    graphs[1].addPoint(errors[2]);
                                    graphs[2].addPoint(errors[3]);
                                }
                                break;
                            case (int)stage.TEST:
                                testProgress = new Run("\nMeasuring... ");
                                epochStats = new Paragraph();
                                epochStats.Inlines.Add(new Run("\nFinal performance:"));
                                Log.Document.Blocks.Add(epochStats);
                                if (Statistics.SelectedIndex == 0)
                                    Log.ScrollToEnd();
                                break;
                            case (int)stage.TEST_PROGRESS:
                                if (!epochStats.Inlines.Contains(testProgress)) {
                                    epochStats.Inlines.InsertAfter(epochStats.Inlines.LastInline, testProgress);
                                    epochStats.Inlines.InsertAfter(epochStats.Inlines.LastInline, new Run("0%"));
                                }
                                epochStats.Inlines.Remove(epochStats.Inlines.LastInline);
                                epochStats.Inlines.InsertAfter(testProgress, new Run(Int32.Parse(data) + "%") { Foreground = Brushes.Lime });
                                break;
                        }
                    });
                }
            }

            Dispatcher.Invoke(() => {
                Paragraph runtime = new Paragraph();
                DateTime now = DateTime.Now;
                now = now.AddTicks(-(now.Ticks % TimeSpan.TicksPerSecond));
                runtime.Inlines.Add(new Run("\nExecute terminated: " + now + "\nRuntime: " + (now - timer)));
                Log.Document.Blocks.Add(runtime);
                if (Statistics.SelectedIndex == 0)
                    Log.ScrollToEnd();
            });
        }
        private string findPythonDirectory() {
            string envVar = Environment.GetEnvironmentVariable("PATH");
            if (envVar.Contains("Python")) {
                string[] sep = envVar.Split(';');
                foreach (string path in sep)
                    if (path.Contains("Python"))
                        envVar = path;

                while (true)
                    if (!envVar.Substring(envVar.LastIndexOf("\\")).Contains("Python"))
                        envVar = envVar.Substring(0, envVar.LastIndexOf("\\"));
                    else {
                        envVar += "\\python.exe";
                        break;
                    }

                if (File.Exists(envVar)) {
                    unlockSettings();
                    return envVar;
				}
            }

            return askForPythonDirectory();
        }
        private string askForPythonDirectory() {
            MessageBox.Show("Unable to find 'python.exe' application. Please enter it.", "Python", MessageBoxButton.OK, MessageBoxImage.Exclamation, MessageBoxResult.OK);
            OpenFileDialog ofd = new OpenFileDialog() {
                Title = "Select Python Application",
                Filter = "Executable (*.exe)|*.exe",
            };

            if (ofd.ShowDialog() == true && ofd.CheckPathExists && ofd.FileName.EndsWith("\\python.exe")) {
                unlockSettings();
                return ofd.FileName;
            }

            MessageBox.Show("Unable to find Python directory.", "Python", MessageBoxButton.OK, MessageBoxImage.Exclamation, MessageBoxResult.OK);
            return "";
        }

        private void DataVolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) {
            DataVolume.Content = "Data Volume: " + DataVolumeSlider.Value + "%";
        }
        private void Ratio_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) {
            TrainingSlider.Maximum = 19 - ValidationSlider.Value;
            ValidationSlider.Maximum = 19 - TrainingSlider.Value;
            double testSlider = 20 - TrainingSlider.Value - ValidationSlider.Value;
            Ratio.Content = "Dataset Ratio: " + (int)TrainingSlider.Value * 5 + "/" + (int)ValidationSlider.Value * 5 + "/" + (int)testSlider * 5;
        }
        private void EpochCountSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) {
            EpochCount.Content = "Epoch Count: " + EpochCountSlider.Value;
        }
        private void LearningRateSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) {
            double lr = 100 / Math.Pow(10, LearningRateSlider.Value);
            LearningRate.Content = "Learning Rate: " + lr;
        }
        private void ImportData_Click(object sender, RoutedEventArgs e) {
            OpenFileDialog ofd = new OpenFileDialog() {
                Title = "Select Data",
                Filter = "Text Files (*.txt)|*.txt"
            };

            if (ofd.ShowDialog() == true && ofd.CheckPathExists) {
                dataPath = ofd.FileName;
                Train.IsEnabled = true;
			}
        }
        private void Train_Click(object sender, RoutedEventArgs e) {
            if (Train.Content.Equals("Train")) {
                Train.Content = "Abort";
                lockSettings();

                topologyStats = new Paragraph();
                datasetStats = new Paragraph();
                epochStats = new Paragraph();

                running = true;
                messageReceiver = new Thread(recieveMessage);
                messageReceiver.Start();

                graphs[0].resetGraph();
                graphs[1].resetGraph();
                graphs[2].resetGraph();

                string scriptPath = @"NeuralNetwork.py";
                string topol = Topology.Text.Trim() + " 1";
                double dataVolume = DataVolumeSlider.Value / 100;
                double trainRatio = TrainingSlider.Value * 5 / 100;
                double validRatio = ValidationSlider.Value * 5 / 100;
                double testRatio = 1 - trainRatio - validRatio;
                int maxEpoch = (int)EpochCountSlider.Value;
                double lr = 100 / Math.Pow(10, LearningRateSlider.Value);
                string delim = Delimiter.Text;

                timer = DateTime.Now;
                timer = timer.AddTicks(-(timer.Ticks % TimeSpan.TicksPerSecond));
                Log.Document.Blocks.Clear();
                Log.AppendText("Execute started: " + timer);

                topologyStats.Inlines.Add(new Run("\nData: " + dataPath));
                Log.Document.Blocks.Add(topologyStats);

                process.StartInfo.Arguments = $"\"{scriptPath}\" \"{dataPath}\" \"{topol}\" \"{dataVolume}\" \"{trainRatio}\" \"{validRatio}\" \"{testRatio}\" \"{maxEpoch}\" \"{lr}\" \"{delim}\"";
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
            } else
                process.Kill();
        }
        
        private void lockSettings() {
            Topology.IsEnabled = false;
            DataVolumeSlider.IsEnabled = false;
            TrainingSlider.IsEnabled = false;
            ValidationSlider.IsEnabled = false;
            EpochCountSlider.IsEnabled = false;
            LearningRateSlider.IsEnabled = false;
            Delimiter.IsEnabled = false;
            ImportData.IsEnabled = false;
        }
        private void unlockSettings() {
            Topology.IsEnabled = true;
            DataVolumeSlider.IsEnabled = true;
            TrainingSlider.IsEnabled = true;
            ValidationSlider.IsEnabled = true;
            EpochCountSlider.IsEnabled = true;
            LearningRateSlider.IsEnabled = true;
            Delimiter.IsEnabled = true;
            ImportData.IsEnabled = true;
        }

        private void Canvas_SizeChanged(object sender, SizeChangedEventArgs e) {
            foreach (LineGraph graph in graphs)
                graph.updateGraph();
        }
        private void ScrollViewer_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e) {
            ScrollViewer scrollViewer = sender as ScrollViewer;
            scrollViewer.ScrollToHorizontalOffset(scrollViewer.HorizontalOffset - e.Delta);
            e.Handled = true;
        }
    }
}