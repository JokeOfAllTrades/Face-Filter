# USAGE
# python detect_faces_video.py --prototxt deploy.prototxt.txt --model res10_300x300_ssd_iter_140000.caffemodel

# import the necessary packages
from imutils.video import VideoStream
import numpy as np
import argparse
import imutils
import time
import socket
import cv2
import struct
import sys


try:
# construct the argument parse and parse the arguments
	ap = argparse.ArgumentParser()
	ap.add_argument("-p", "--prototxt", required=True,
		help="path to Caffe 'deploy' prototxt file")
	ap.add_argument("-m", "--model", required=True,
		help="path to Caffe pre-trained model")
	ap.add_argument("-c", "--confidence", type=float, default=0.5,
		help="minimum probability to filter weak detections")
	args = vars(ap.parse_args())
# load our serialized model from disk
	net = cv2.dnn.readNetFromCaffe(args["prototxt"], args["model"])

	sockRect = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
	sockRect.connect(('localhost', 5057))
	sockImage = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
	sockImage.bind(('localhost', 5058))
	sockDeath = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
	sockDeath.bind(('localhost', 5059))
	sockDeath.setblocking(0)
		
except Exception as e:
	exc_type, exc_obj, exc_tb = sys.exc_info()
	print(e, exc_type, exc_tb.tb_lineno)

timeStart = time.time()
print(f'Start: {timeStart}')
# loop over the frames from the video stream
counter = 0
averageTime = 0
while True:
	averageTime = averageTime * counter
	counter = counter + 1
	timeOne = time.time()
	#print(f'Time one:   {timeOne}')

	# grab the frame from the threaded video stream and resize it
	# to have a maximum width of 300 pixels		

	frameBuff = sockImage.recv(65527)
	frameMat = np.frombuffer(frameBuff, dtype=np.uint8)
	frame = cv2.imdecode(frameMat,cv2.IMREAD_COLOR)
	frame = imutils.resize(frame, width=300)

	# grab the frame dimensions and convert it to a blob
	(h, w) = frame.shape[:2]
	blob = cv2.dnn.blobFromImage(cv2.resize(frame, (300, 300)), 1.0,
		(300, 300), (104.0, 177.0, 123.0), crop = True)

	# pass the blob through the network and obtain the detections and
	# predictions
	net.setInput(blob)
	detections = net.forward()

	# loop over the detections
	#timeTwo = time.time()
	#print(f'Time two:   {timeTwo}')
	#for i in range(0, detections.shape[2]):
		# extract the confidence (i.e., probability) associated with the
		# prediction
	#	confidence = detections[0, 0, i, 2]

		# filter out weak detections by ensuring the `confidence` is
		# greater than the minimum confidence
	#	if confidence < args["confidence"]:
	#		continue

		# compute the (x, y)-coordinates of the bounding box for the
		# object
	#	box = detections[0, 0, i, 3:7] * np.array([w, h, w, h])
	#	(startX, startY, endX, endY) = box.astype("int")

		#cv2.rectangle(frame, (startX, startY), (endX, endY), (0, 0, 255), 2)
		#sockRect.send( np.array((struct.pack('<i',startX))) )
		#sockRect.send( np.array((struct.pack('<i',startY))) )
		#sockRect.send( np.array((struct.pack('<i',endX))) )
		#sockRect.send( np.array((struct.pack('<i',endY))) )
	
	# show the output frame
	cv2.imshow("Frame", frame)
	key = cv2.waitKey(1) & 0xFF


	# if the `q` key was pressed, break from the loop
	key = None
	try:
		timeThree = time.time()
		timeDifference = timeThree - timeOne
		#print(f'Time difference: {timeDifference}')
		averageTime = (averageTime + timeDifference) / counter
		print(f'Time average:    {averageTime}')
		key = sockDeath.recv(1);
	except:
		pass
	if key == None:
		continue
	if key == b'q':
		break

# do a bit of cleanup
#cv2.destroyAllWindows()
sockRect.shutdown(socket.SHUT_RDWR)
sockRect.close()
sockImage.shutdown(socket.SHUT_RDWR)
sockImage.close()
sockDeath.shutdown(socket.SHUT_RDWR)
sockDeath.close()
timeStop = time.time()
print(f'End: {timeStop}')
#sys.exit()
