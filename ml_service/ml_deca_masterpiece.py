#!/usr/bin/env python3
import os
import sys
import traceback
import logging
import boto3
from flask import Flask, request, jsonify
from mpx_genai_sdk import MasterpieceX

# Define log file path
LOG_FILE_PATH = os.path.join(os.getcwd(), "ml_deca_error.log")

# Configure logging to write to a file and console
logging.basicConfig(
    level=logging.DEBUG,
    format="%(asctime)s [%(levelname)s] %(name)s: %(message)s",
    handlers=[
        logging.FileHandler(LOG_FILE_PATH, mode="a"),
        logging.StreamHandler()
    ],
)

logger = logging.getLogger("ml_deca")

# AWS S3 Configurations
AWS_ACCESS_KEY = "AKIAUP7RI5QI6QH64G6C"
AWS_SECRET_KEY = "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"
AWS_BUCKET_NAME = "patients-tree"
AWS_REGION = "us-east-2"

# Initialize S3 client
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

# Masterpiece X API Configuration
MASTERPIECE_X_API_KEY = "zpka_7a8b1beb401e40deaea97a1dd6c794dc_1d431f38"
mpx_client = MasterpieceX(api_key=MASTERPIECE_X_API_KEY)

# Flask application
app = Flask(__name__)
UPLOAD_FOLDER = os.path.join(os.getcwd(), "uploads")
os.makedirs(UPLOAD_FOLDER, exist_ok=True)

# Function to upload a file to AWS S3
def upload_to_s3(file_path, s3_key):
    try:
        logger.info("Uploading %s to S3 bucket %s with key %s", file_path, AWS_BUCKET_NAME, s3_key)
        s3_client.upload_file(file_path, AWS_BUCKET_NAME, s3_key)
        file_url = f"https://{AWS_BUCKET_NAME}.s3.{AWS_REGION}.amazonaws.com/{s3_key}"
        logger.info("Upload successful: %s", file_url)
        return file_url
    except Exception as e:
        logger.error("Error uploading to S3: %s", e)
        traceback.print_exc()
        return None

# Flask API endpoint for 3D model generation
@app.route("/flask-api/generate", methods=["POST"])
def generate_3d_face():
    logger.info("Receiving uploaded images...")
    try:
        files = request.files
        front_img = files.get("front")
        if not front_img:
            return jsonify({"error": "Front image is required"}), 400

        # Save the uploaded front image locally
        front_img_path = os.path.join(UPLOAD_FOLDER, "front_image.jpg")
        front_img.save(front_img_path)
        logger.info("Front image saved to %s", front_img_path)

        # Upload the image to S3 to get a public URL
        s3_key = f"uploads/{os.path.basename(front_img_path)}"
        image_url = upload_to_s3(front_img_path, s3_key)
        if not image_url:
            return jsonify({"error": "Failed to upload front image to S3"}), 500

        # Send the image URL to Masterpiece X API to generate a 3D model
        logger.info("Sending image URL to Masterpiece X API for 3D model generation...")
        response = mpx_client.functions.imagetothreed(
            imageUrl=image_url,
            seed=1,
            textureSize=1024
        )
        
        request_id = response.get("requestId")
        if not request_id:
            return jsonify({"error": "Failed to start 3D model generation"}), 500

        # Poll the status endpoint to check when the 3D model is ready
        logger.info("Polling the status endpoint for 3D model generation...")
        for _ in range(10):  # Retry up to 10 times (adjust as necessary)
            status_response = mpx_client.status(request_id=request_id)
            if status_response.get("status") == "completed":
                model_url = status_response.get("result").get("modelUrl")
                if model_url:
                    logger.info("3D model successfully generated: %s", model_url)
                    return jsonify({"mesh_file_url": model_url}), 200
            elif status_response.get("status") == "failed":
                return jsonify({"error": "3D model generation failed"}), 500

        logger.error("3D model generation did not complete within the expected time")
        return jsonify({"error": "3D model generation timeout"}), 504

    except Exception as e:
        logger.error("Server error: %s", e)
        traceback.print_exc()
        return jsonify({"error": str(e)}), 500

# Main entry point
if __name__ == "__main__":
    try:
        logger.info("Starting the Flask server on 0.0.0.0:5001")
        app.run(host="0.0.0.0", port=5001, debug=True)
    except Exception as e:
        logger.error("Error running Flask server: %s", e)
        traceback.print_exc()
        sys.exit(1)
