import numpy as np
import socket
import time
from PIL import Image as img
import json
import io
import rclpy
from rclpy.node import Node
from sensor_msgs.msg import Image
from cv_bridge import CvBridge, CvBridgeError


class StreamProxy(Node):
	def __init__(self, 
				port=12374, 
				ip='127.0.0.1', 
				time_period=0.3, 
				topic='/camera/color/image_raw'):

		super().__init__('stream_proxy_publisher')
		self.image_publisher_ = self.create_publisher(Image, topic, 10)
		self.timer_period = time_period
		self.timer = self.create_timer(self.timer_period, self.forward)
		self.bridge = CvBridge()
        
		self.socket = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
		flag_connection = False
		
		while(not flag_connection):
			try:
				self.client =self.socket.connect((ip, port))
				self.socket.settimeout(20.0)
				flag_connection = True
			except socket.error:
				print("Can't connect with robot! Trying again...")	
				time.sleep(1)		

	def request_image(self, data="fetch"):
		self.socket.send(data.encode())
		start_char = '{'
		end_char = '}'
		buffer = ""
		start_flag = False
		try:
			while True:
				data = self.socket.recv(65600).decode('utf-8')
				if not data:
					break
				
				if start_char in data and not start_flag:
					start_flag = True
					buffer += data

				elif start_flag: 
					buffer += data

				if end_char in data and  start_flag:
					start_flag = False
					break
				
			json_data = json.loads(buffer)
			image_data = json_data['image']
			image_data = io.BytesIO(bytes(image_data))
			image = img.open(image_data).convert('RGB')
			return json_data["format"], np.array(image)

		except TimeoutError as err:
			return None, None

	def forward(self):
		try:
			img_type, img_data = self.request_image()
		
			if img_type is None or img_data is None:
				raise RuntimeWarning("Not possible to obtain data from the simulator")
			
			msg = self.bridge.cv2_to_imgmsg(img_data, "rgb8")
			self.image_publisher_.publish(msg)
		 
		except CvBridgeError as e:
			self.add_msg_to_error_logger(str(e))
		
		except RuntimeWarning as e:
			self.add_msg_to_info_logger(str(e))

	def add_msg_to_info_logger(self, msg):
		self.get_logger().info(msg)

	def add_msg_to_error_logger(self, msg):
		self.get_logger().error(msg)
	
	def close_connection(self):
		self.socket.close()


if __name__ == '__main__':
	rclpy.init()
	proxy = StreamProxy()
	rclpy.spin(proxy)
	proxy.destroy_node()
	rclpy.shutdown()
