#!/usr/bin/env python3
import os
import sys
import traceback
import cv2
import torch
import torch.nn.functional as F
import numpy as np
from flask import Flask, request, jsonify
import boto3
import logging
from prnet.api import PRN

# -------------------------------------------------------------------
# Configure Logging
LOG_FILE_PATH = os.path.join(os.getcwd(), "ml_deca_error.log")
logging.basicConfig(
    level=logging.DEBUG,  # Ensure all log levels are captured
    format="%(asctime)s [%(levelname)s] %(name)s: %(message)s",
    handlers=[
        logging.FileHandler(LOG_FILE_PATH, mode="a"),  # Append logs to file
        logging.StreamHandler()  # Also log to console
    ],
)
logger = logging.getLogger("ml_prnet")

# AWS S3 Configurations (for production, secure these via environment variables)
AWS_ACCESS_KEY = "AKIAUP7RI5QI6QH64G6C"
AWS_SECRET_KEY = "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"
AWS_BUCKET_NAME = "patients-tree"
AWS_REGION = "us-east-2"

# Initialize Flask App
app = Flask(__name__)

# Initialize S3 Client
try:
    s3_client = boto3.client(
        "s3",
        aws_access_key_id=AWS_ACCESS_KEY,
        aws_secret_access_key=AWS_SECRET_KEY,
        region_name=AWS_REGION,
    )
    logger.info("S3 client initialized successfully.")
except Exception as e:
    logger.error("Error initializing S3 client: %s", e)
    traceback.print_exc()
    sys.exit(1)

# -------------------------------------------------------------------
# Initialize PRNet Model
try:
    logger.info("Loading PRNet model...")
    prn = PRN(is_dlib=False)  # Initialize PRNet without dlib dependency
    logger.info("PRNet model loaded successfully.")
except Exception as e:
    logger.error("Error initializing PRNet model: %s", e)
    traceback.print_exc()
    sys.exit(1)

# -------------------------------------------------------------------
# Download and Process Images from S3
def download_s3_image(s3_key, local_path):
    try:
        s3_client.download_file(AWS_BUCKET_NAME, s3_key, local_path)
        logger.info("Downloaded %s from S3", s3_key)
        return local_path
    except Exception as e:
        logger.error("Error downloading %s from S3: %s", s3_key, e)
        traceback.print_exc()
        return None

# -------------------------------------------------------------------
# Process 4 Images and Generate 3D Model
def process_images(front, left, right, up):
    try:
        logger.info("Processing images for 3D model generation")
        images = []
        for img_path in [front, left, right, up]:
            image = cv2.imread(img_path)
            if image is None:
                raise ValueError("Invalid image format or corrupted file: " + img_path)
            images.append(image)

        # Convert images to PRNet-compatible format and process
        results = [prn.process(image) for image in images]
        merged_model = np.mean(results, axis=0)  # Simple averaging approach
        mesh_path = os.path.join("uploads", "3d_face.obj")
        prn.save_obj(mesh_path, merged_model)
        logger.info("3D model saved: %s", mesh_path)
        return mesh_path
    except Exception as e:
        logger.error("Error processing images: %s", e)
        traceback.print_exc()
        return None

# -------------------------------------------------------------------
# Upload File to AWS S3
def upload_to_s3(file_path, s3_key):
    try:
        s3_client.upload_file(file_path, AWS_BUCKET_NAME, s3_key)
        file_url = f"https://{AWS_BUCKET_NAME}.s3.{AWS_REGION}.amazonaws.com/{s3_key}"
        return file_url
    except Exception as e:
        logger.error("Error uploading to S3: %s", e)
        traceback.print_exc()
        return None

# -------------------------------------------------------------------
# Flask API Endpoint@app.route("/flask-api/generate", methods=["POST"])
def generate_3d_face():
    try:
        request_data = request.get_json()
        front_key = request_data.get("front")
        left_key = request_data.get("left")
        right_key = request_data.get("right")
        up_key = request_data.get("up")

        if not all([front_key, left_key, right_key, up_key]):
            return jsonify({"error": "All four images are required"}), 400

        front_path = download_s3_image(front_key, "uploads/front.jpg")
        left_path = download_s3_image(left_key, "uploads/left.jpg")
        right_path = download_s3_image(right_key, "uploads/right.jpg")
        up_path = download_s3_image(up_key, "uploads/up.jpg")

        if not all([front_path, left_path, right_path, up_path]):
            return jsonify({"error": "Failed to download images from S3"}), 500

        mesh_path = process_images(front_path, left_path, right_path, up_path)
        if not mesh_path:
            return jsonify({"error": "Failed to generate 3D model"}), 500

        s3_key = f"3dmodels/{os.path.basename(mesh_path)}"
        file_url = upload_to_s3(mesh_path, s3_key)
        if not file_url:
            return jsonify({"error": "Failed to upload 3D model"}), 500

        return jsonify({"mesh_file_url": file_url}), 200
    except Exception as e:
        logger.error("Server error: %s", e)
        traceback.print_exc()
        return jsonify({"error": str(e)}), 500

# -------------------------------------------------------------------
# Run Flask Server
if __name__ == "__main__":
    try:
        logger.info("Starting Flask server on port 5001")
        app.run(host="0.0.0.0", port=5001, debug=True)
    except Exception as e:
        logger.error("Error running Flask server: %s", e)
        traceback.print_exc()
        sys.exit(1)
