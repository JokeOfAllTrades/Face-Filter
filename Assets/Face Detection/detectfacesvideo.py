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
import keyboard

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

# initialize the video stream and allow the cammera sensor to warmup
vs = VideoStream(src=0).start()
time.sleep(2.0)

sockRect = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
sockRect.connect(('localhost', 5056))
sockImage = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
sockImage.connect(('localhost', 5057))

# loop over the frames from the video stream
while True:
    # grab the frame from the threaded video stream and resize it
    # to have a maximum width of 400 pixels
    frame = vs.read()
    frame = imutils.resize(frame, width=400)

    # grab the frame dimensions and convert it to a blob
    (h, w) = frame.shape[:2]
    blob = cv2.dnn.blobFromImage(cv2.resize(frame, (300, 300)), 1.0,
        (300, 300), (104.0, 177.0, 123.0))

    # pass the blob through the network and obtain the detections and
    # predictions
    net.setInput(blob)
    detections = net.forward()

    # loop over the detections
    for i in range(0, detections.shape[2]):
        # extract the confidence (i.e., probability) associated with the
        # prediction
        confidence = detections[0, 0, i, 2]

        # filter out weak detections by ensuring the `confidence` is
        # greater than the minimum confidence
        if confidence < args["confidence"]:
            continue

        # compute the (x, y)-coordinates of the bounding box for the
        # object
        box = detections[0, 0, i, 3:7] * np.array([w, h, w, h])
        (startX, startY, endX, endY) = box.astype("int")

        cv2.rectangle(frame, (startX, startY), (endX, endY), (0, 0, 255), 2)




        sockRect.sendall( np.array( (struct.pack('<i',startX),struct.pack('<i',startY),struct.pack('<i',endX), struct.pack('<i',endY)) ) )
    
    ret, frameBuff = cv2.imencode('.jpg', frame)
    sockImage.sendall(frameBuff)
    # show the output frame
    # cv2.imshow("Frame", frame)

    # if the `q` key was pressed, break from the loop
    if keyboard.is_pressed('q'):
        break

# do a bit of cleanup
cv2.destroyAllWindows()
vs.stop()
sockRect.shutdown(socket.SHUT_RDWR)
sockRect.close()
sockImage.shutdown(socket.SHUT_RDWR)
sockImage.shutdown(socket.SHUT_RDWR)
