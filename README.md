# CustomAI
Regression neural network builder and visualizer, developed in WPF & Python.  
*Work in progress.*

## KEY FEATURES
- GUI automatically scales up to the resolution of the computer executing the application.
- Customize hidden topology of the neural network (input & output layer is automatic).
- Automatically divides one data file into a training, a validation and a test dataset.
- No hard limit to data volume or network topology size (as much as the memory can handle).
- Informs where in the training or validation phase the network is by logging progress.
- Plots live MAE, MSE & RMSE graphs of the validation dataset after every epoch.
- Abort training at any time instantly.

## DOWNLOAD
1. Simply download the 'CustomAI.exe' file to your computer.
2. If blocked, unblock the application in its properties (no virus, I promise).
3. Download the 'NeuralNetwork.py' file (view as RAW, save & rename extension from .txt to .py).
4. Make sure 'NeuralNetwork.py' is in the same directory as 'CustomAI.exe'.
5. Having Python and Numpy installed is a prerequisite.

![ExecutionLog](/Screenshots/ExecutionLog.png)
![NetworkPerformance](/Screenshots/NetworkPerformance.png)
![ExecutionError](/Screenshots/ExecutionError.png)
