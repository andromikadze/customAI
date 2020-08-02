import numpy as np
import select
import socket
import sys
import time
import threading

class NeuralNetwork:
	def __init__(self, topology, lr):
		#store learning rate
		self.lr = lr

		#initialize neuron weights based on He-normal
		np.random.seed(20)
		self.neuronWeights = np.array([None] * (len(topology) - 1))
		for L in range(len(topology) - 1):
			self.neuronWeights[L] = np.random.randn(topology[L + 1], topology[L]) * np.sqrt(2 / topology[L])

		#initialize bias weights based on He-normal
		self.biasWeights = np.array([None] * (len(topology) - 1))
		for L in range(len(topology) - 1):
			self.biasWeights[L] = np.random.randn(topology[L + 1], 1) * np.sqrt(2)

		#initialize neuron output array size
		self.neuronOutputs = np.array([None] * (len(topology)))

	def train(self):
		rowCount = 0
		previousProgress = 0
		self.checkSpeed()
		for row in trainingData:
			inputs = row[0:topology[0]]
			target = row[topology[0]:topology[0] + 1]

			#propagate inputs forward and error backwards
			self.forward(np.array([inputs]).T)
			self.backwards(np.array([target]))

			#adjust weights
			self.neuronWeights -= self.lr * self.neuronWeightsAdj
			self.biasWeights -= self.lr * self.biasWeightsAdj

			#return progress
			rowCount += 1
			progress = round(100 * rowCount / trainVolume)
			if progress != previousProgress:
				if self.reportProgress:
					report(4, progress)
					previousProgress = progress
				elif self.timer.is_alive():
					self.timer.cancel()

	def validate(self):
		mae = 0
		mse = 0
		rowCount = 0
		previousProgress = 0
		self.checkSpeed()
		for row in validationData:
			inputs = row[0:topology[0]]
			target = row[topology[0]:topology[0] + 1][0]

			#propagate inputs forward
			self.forward(np.array([inputs]).T)
			output = self.neuronOutputs[len(self.neuronOutputs) - 1][0][0]

			#calculate errors
			mae += np.absolute(output - target)
			mse += np.square(output - target)
			
			#return progress
			rowCount += 1
			progress = round(100 * rowCount / validVolume)
			if progress != previousProgress:
				if self.reportProgress:
					report(5, progress)
					previousProgress = progress
				elif self.timer.is_alive():
					self.timer.cancel()
		
		#calculate errors
		mae /= validVolume
		mse /= validVolume
		rmse = np.sqrt(mse)

		#return errors
		report(6, 1, mae, mse, rmse)
	
	def test(self):
		report(7)
		mae = 0
		mse = 0
		rowCount = 0
		previousProgress = 0
		self.checkSpeed()
		for row in testData:
			inputs = row[0:topology[0]]
			target = row[topology[0]:topology[0] + 1][0]

			#propagate inputs forward
			self.forward(np.array([inputs]).T)
			output = self.neuronOutputs[len(self.neuronOutputs) - 1][0][0]

			#calculate errors
			mae += np.absolute(output - target)
			mse += np.square(output - target)
			
			#return progress
			rowCount += 1
			progress = round(100 * rowCount / testVolume)
			if progress != previousProgress:
				if self.reportProgress:
					report(8, progress)
					previousProgress = progress
				elif self.timer.is_alive():
					self.timer.cancel()
		
		#calculate errors
		mae /= testVolume
		mse /= testVolume
		rmse = np.sqrt(mse)
		
		#return errors
		report(6, 0, mae, mse, rmse)

	def forward(self, inputs):
		#store outputs of each neuron in each layer
		self.neuronOutputs[0] = inputs
		for L in range(1, len(self.neuronOutputs)):
			self.neuronOutputs[L] = self.relu(np.dot(self.neuronWeights[L - 1], self.neuronOutputs[L - 1]) + self.biasWeights[L - 1])
		
	def backwards(self, target):
		#initialize weight adjustment array size
		self.neuronWeightsAdj = np.array([None] * len(self.neuronWeights))
		self.biasWeightsAdj = np.array([None] * len(self.biasWeights))

		#neuron chain rule: dE/dLw = dE/dLo * dLo/dLn * dLn/dLw
		for L in reversed(range(len(self.neuronWeights))):
			dE_dLw = self.dE_dLo(L, target) * self.dLo_dLn(L) * self.dLn_dLw(L)
			self.neuronWeightsAdj[L] = dE_dLw.T

		#bias chain rule: dE/dLw = dE/dLo * dLo/dLn * 1
		for L in reversed(range(len(self.biasWeights))):
			dE_dLw = self.dE_dLo(L, target, True) * self.dLo_dLn(L, True).T
			self.biasWeightsAdj[L] = dE_dLw.T
				
	def relu(self, value, derivitive = False):
		#rectified linear unit activation function
		if np.any(np.isnan(value)) or np.any(np.isinf(value)):
			sys.exit("Exploding Gradient Error: Choose a smaller learning rate.")	
		if derivitive:
			return np.where(value > 0, 1, 0)
		else:
			return np.where(value > 0, 1, 0) * value
	
	def dE_dLo(self, startL, target, bias = False):
		#final output - target
		totalSum = self.neuronOutputs[len(self.neuronOutputs) - 1] - target

		#dE/dLo = sum(dE/dL+1o) * dL+1o/dL+1n * dL+1n_dL+1o
		for L in reversed(range(startL, len(self.neuronOutputs) - 2)):
			totalSum = np.sum(np.sum(totalSum) * self.dLo_dLn(L + 1).T * self.dLn_dLo(L + 1, bias), axis = 0)
		return totalSum

	def dLo_dLn(self, L, bias = False):
		if bias:
			return self.relu(self.biasWeights[L], True)
		else:
			return self.relu(self.neuronOutputs[L], True)

	def dLn_dLo(self, L, bias = False):
		if bias:
			return self.biasWeights[L]
		else:
			return self.neuronWeights[L]

	def dLn_dLw(self, L):
		return self.neuronOutputs[L]
	
	def checkSpeed(self):
		#check calculation speed
		self.reportProgress = False
		self.timer = threading.Timer(0.01, self.slow) 
		self.timer.start()

	def slow(self):
		#report progress if slow
		self.reportProgress = True
		self.timer.cancel()

#______________report back info______________#
def report(code, a = None, b = None, c = None, d = None):
	if a == None:
		print(code)
	elif b == None:
		print(code, a)
	elif c == None:
		print(code, a, b)
	elif d == None:
		print(code, a, b, c)
	else:
		print(code, a, b, c, d)
	sys.stdout.flush()

#______________error check topology______________#
try:
	topology = list(map(int, sys.argv[2].strip().split(" ")))
except ValueError:
	sys.exit("Topology Error: Invalid topology format.")
if sum(n < 1 for n in topology) > 0:
	sys.exit("Topology Error: Minimum neuron count in a layer is 1.")

#______________calculate dataset ratios______________#
report(0)
dataPath = sys.argv[1]
with open(dataPath) as f:
    for rowCount, x in enumerate(f, 1):
        pass
dataVolume = float(sys.argv[3])
trainRatio = float(sys.argv[4])
validRatio = float(sys.argv[5])
testRatio = float(sys.argv[6])
trainVolume = int(rowCount * dataVolume * trainRatio)
validVolume = int(rowCount * dataVolume * validRatio)
testVolume = int(rowCount * dataVolume * testRatio)
if trainVolume < 2 or validVolume < 2 or testVolume < 2:
	report(2, trainVolume, validVolume, testVolume)
	sys.exit("Data Error: Minimum dataset size is 2.")

#______________error check data______________#
delim = sys.argv[9]
try:
	allData = np.genfromtxt(dataPath, max_rows = trainVolume + validVolume + testVolume, delimiter = delim)
except ValueError:
	sys.exit("Data Error: Number of inputs is not uniform across all data.")
except MemoryError:
	sys.exit("Memory Error: Amount of data is too large.")
if np.all(np.isnan(allData)):
	sys.exit("Data Error: Invalid delimiter.")
elif np.any(np.isnan(allData)):
	sys.exit("Data Error: Data contains non-numeric elements.")
inputCount = len(allData[0]) - 1
topology = np.concatenate([[inputCount], topology])
report(1, inputCount)
report(2, trainVolume, validVolume, testVolume)

#______________create datasets______________#
trainingData = allData[0:trainVolume]
validationData = allData[trainVolume:trainVolume + validVolume]
testData = allData[trainVolume + validVolume:]

#______________initialize neural network______________#
lr = float(sys.argv[8])
try:
	nn = NeuralNetwork(topology, lr)
except:
	sys.exit("Memory Error: Network size is too large.")

#______________train, validate & test______________#
report(3, 0)
nn.validate()

maxEpoch = int(sys.argv[7])
for epoch in range(1, maxEpoch + 1):
	report(3, epoch)
	nn.train()
	nn.validate()
nn.test()