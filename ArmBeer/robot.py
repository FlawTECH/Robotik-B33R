from Adafruit_PCA9685 import PCA9685
from time import sleep
import paho.mqtt.client as mqtt
from threading import Thread, Event, RLock

class Canceled(Exception):
    pass

class Robot(object):
    # Channels
    BODY_CHANNEL = 0
    TRUNK_CHANNEL = 1
    SHOULDER_CHANNEL = 2
    ELBOW_CHANNEL = 3
    WRIST_CHANNEL = 4
    HAND_CHANNEL = 5

    # Arms positions when rised
    TRUNK_UP = 440
    SHOULDER_UP = 290
    ELBOW_UP = 240
    # Arms positions when grabing
    TRUNK_DOWN = 625
    SHOULDER_DOWN = 250
    ELBOW_DOWN = 500

    # Min/max values for each motor
    MIN_BODY = 110
    MAX_BODY = 650
    MIN_TRUNK = 290
    MAX_TRUNK = 640
    MIN_SHOULDER = 140
    MAX_SHOULDER = 640
    MIN_ELBOW = 220
    MAX_ELBOW = 640

    # Slots start, stop
    SLOT_1 = (200, 330)
    SLOT_2 = (330, 460)

    def __init__(self, mqtt_client):
        self._pwm = PCA9685(0x41)
        self._pwm.set_pwm_freq(60)
        self.cancel_event = Event()
        self._lock = RLock()
        self.mqtt_client = mqtt_client

    def calibrate(self):
        with self._lock:
            self.body = 325
            self.trunk = Robot.TRUNK_UP
            self.shoulder = Robot.SHOULDER_UP
            self.elbow = Robot.ELBOW_UP
            self.wrist = 550
            self.hand = 380

    def grab(self, angle=None):
        with self._lock:
            trunk_get = iter(xrange(Robot.TRUNK_UP, Robot.TRUNK_DOWN, (Robot.TRUNK_DOWN - Robot.TRUNK_UP) / 20))
            shoulder_get = iter(xrange(Robot.SHOULDER_UP, Robot.SHOULDER_DOWN, (Robot.SHOULDER_DOWN - Robot.SHOULDER_UP) / 20))
            elbow_get = iter(xrange(Robot.ELBOW_UP, Robot.ELBOW_DOWN, (Robot.ELBOW_DOWN - Robot.ELBOW_UP) / 20))

            try:
                for trunk in trunk_get:
                    sleep(0.02)
                    self.trunk = int(trunk)
                    self.shoulder = int(next(shoulder_get))
                    self.elbow = int(next(elbow_get))
            except StopIteration:
                pass

            self.trunk = Robot.TRUNK_DOWN
            self.shoulder = Robot.SHOULDER_DOWN
            self.elbow = Robot.ELBOW_DOWN

    def rise(self):
        with self._lock:
            trunk_rise = xrange(Robot.TRUNK_DOWN, Robot.TRUNK_UP, (Robot.TRUNK_UP - Robot.TRUNK_DOWN) / 20)
            shoulder_rise = iter(xrange(Robot.SHOULDER_DOWN, Robot.SHOULDER_UP, (Robot.SHOULDER_UP - Robot.SHOULDER_DOWN) / 20))
            elbow_rise = iter(xrange(Robot.ELBOW_DOWN, Robot.ELBOW_UP, (Robot.ELBOW_UP - Robot.ELBOW_DOWN) / 20))

            try:
                for trunk in trunk_rise:
                    sleep(0.02)
                    self.trunk = int(trunk)
                    self.shoulder = int(next(shoulder_rise))
                    self.elbow = int(next(elbow_rise))
            except StopIteration:
                pass

            self.trunk = Robot.TRUNK_UP
            self.shoulder = Robot.SHOULDER_UP
            self.elbow = Robot.ELBOW_UP

    def open(self):
        self.hand = 450

    def close(self):
        self.hand = 395

    def scan(self):
        with self._lock:
            previous_pos = self.body
            self.body = Robot.MIN_BODY
            sleep(1)
            for pos in xrange(Robot.MIN_BODY, Robot.MAX_BODY, 1):
                if self.cancel_event.is_set():
                    self.cancel_event.clear()
                    break
                self.body = pos
                sleep(0.01)
            self.mqtt_client.publish("arm/reply/scan")
            self.body = previous_pos

    def getbody(self): return self._body
    def setbody(self, value):
        self._body = max(min(value, Robot.MAX_BODY), Robot.MIN_BODY)
        self._pwm.set_pwm(Robot.BODY_CHANNEL, 0, self._body)
    body = property(getbody, setbody)

    def gettrunk(self): return self._trunk
    def settrunk(self, value):
        self._trunk = max(min(value, Robot.MAX_TRUNK), Robot.MIN_TRUNK)
        self._pwm.set_pwm(Robot.TRUNK_CHANNEL, 0, self._trunk)
    trunk = property(gettrunk, settrunk)

    def getshoulder(self): return self._shoulder
    def setshoulder(self, value):
        self._shoulder = max(min(value, Robot.MAX_SHOULDER), Robot.MIN_SHOULDER)
        self._pwm.set_pwm(Robot.SHOULDER_CHANNEL, 0, self._shoulder)
    shoulder = property(getshoulder, setshoulder)

    def getelbow(self): return self._elbow
    def setelbow(self, value):
        self._elbow = max(min(value, Robot.MAX_ELBOW), Robot.MIN_ELBOW)
        self._pwm.set_pwm(Robot.ELBOW_CHANNEL, 0, self._elbow)
    elbow = property(getelbow, setelbow)

    def getwrist(self): return self._wrist
    def setwrist(self, value):
        self._wrist = value
        self._pwm.set_pwm(Robot.WRIST_CHANNEL, 0, self._wrist)
    wrist = property(getwrist, setwrist)

    def gethand(self): return self._hand
    def sethand(self, value):
        self._hand = value
        self._pwm.set_pwm(Robot.HAND_CHANNEL, 0, self._hand)
    hand = property(gethand, sethand)

    def move_to_slot_1(self):
        self.body = (Robot.SLOT_1[0] + Robot.SLOT_1[1]) // 2

    def move_to_slot_2(self):
        self.body = (Robot.SLOT_2[0] + Robot.SLOT_2[1]) // 2

client = mqtt.Client()

def on_connect(client, userdata, flags, rc):
    client.subscribe("yolo/cmd")

def on_message(client, userdata, msg):
    print(msg.topic, msg.payload)
    xargs = msg.payload.split(",")
    if xargs[0] == "pickup":
        robot.open()
        sleep(0.2)
        robot.grab()
        sleep(0.2)
        robot.close()
        sleep(0.2)
        robot.rise()
    elif xargs[0] == "moveto":
        robot.body = int(xargs[1])
    elif xargs[0] == "getbody":
        for slot in [Robot.SLOT_1, Robot.SLOT_2]:
            if robot.body in xrange(*slot):
                client.publish("arm/reply/body", str((slot[0] + slot[1]) // 2))
                break
        else:
            print("Position out of range")
    else:
        {
            "scan": Thread(target=robot.scan).start,
            "stop": robot.cancel_event.set,
        }[xargs[0]]()

client.on_connect = on_connect
client.on_message = on_message
client.connect("10.100.0.61", 1883, 60)

robot = Robot(client)
robot.calibrate()
client.loop_start()

