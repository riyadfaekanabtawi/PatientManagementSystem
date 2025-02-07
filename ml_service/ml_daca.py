#!/usr/bin/env python3
import os
import sys
import traceback
import cv2
import torch
import torch.nn.functional as F
from flask import Flask, request, jsonify
import boto3
import logging

# -------------------------------------------------------------------
# Configure Logging
logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s [%(levelname)s] %(name)s: %(message)s",
)
logger = logging.getLogger("ml_deca")

# -------------------------------------------------------------------
# Environment Variables & Configuration
os.environ["CUDA_VISIBLE_DEVICES"] = "-1"
os.environ["PYTORCH_NO_CUDA_MEMORY_CACHING"] = "1"
sys.path.append(os.path.abspath("/home/ubuntu/PatientManagementSystem/DECA"))

# AWS S3 Configurations
AWS_ACCESS_KEY = "your_aws_access_key"
AWS_SECRET_KEY = "your_aws_secret_key"
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
# Import DECA Model & Configuration
from decalib.deca import DECA
from decalib.utils.config import cfg as deca_cfg

# Initialize DECA
try:
    logger.info("Loading DECA model...")
    deca_cfg.device = "cpu"
    deca_cfg.model.use_tex = False
    deca = DECA(config=deca_cfg)
    deca.device = torch.device("cpu")
    deca.to(torch.device("cpu"))
    logger.info("DECA model loaded successfully.")
except Exception as e:
    logger.error("Error initializing DECA model: %s", e)
    traceback.print_exc()
    sys.exit(1)

# -------------------------------------------------------------------
# Fixing the Forward Method
try:
    def new_forward(self, x):
        if hasattr(self, "encoder"):
            features = self.encoder(x)
        elif hasattr(self, "base"):
            features = self.base(x)
        elif hasattr(self, "conv"):
            features = self.conv(x)
        else:
            raise AttributeError("No known convolutional attribute found in E_flame.")

        if features.dim() == 4:
            features = F.adaptive_avg_pool2d(features, (1, 1))
        
        features = features.view(features.size(0), -1)
        return self.layers(features)
    
    deca.E_flame.__class__.forward = new_forward
    logger.info("Successfully patched deca.E_flame.forward.")
except Exception as e:
    logger.error("Error patching forward method: %s", e)
    traceback.print_exc()

# -------------------------------------------------------------------
# Image Processing Function
def process_image(image_path):
    try:
        logger.info("Processing image: %s", image_path)
        image = cv2.imread(image_path)
        if image is None:
            raise ValueError("Invalid image format or corrupted file")
        image = cv2.cvtColor(image, cv2.COLOR_BGR2RGB)
        image_tensor = torch.tensor(image).permute(2, 0, 1).unsqueeze(0).float() / 255.0

        # Ensure correct shape for DECA
        image_tensor = F.interpolate(image_tensor, size=(224, 224), mode="bilinear", align_corners=False)
        image_tensor = image_tensor.to(torch.device("cpu"))
        
        with torch.no_grad():
            codedict = deca.encode(image_tensor)
            opdict = deca.decode(codedict)

        mesh_path = os.path.join("uploads", "3d_face.obj")
        deca.save_obj(mesh_path, opdict)
        logger.info("3D model saved: %s", mesh_path)
        return mesh_path
    except Exception as e:
        logger.error("Error processing image: %s", e)
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
        files = request.files
        front_img = files.get("front")
        if not front_img:
            return jsonify({"error": "Front image is required"}), 400

        image_path = os.path.join("uploads", "front_image.jpg")
        front_img.save(image_path)

        mesh_path = process_image(image_path)
        if not mesh_path:
            return jsonify({"error": "Failed to generate 3D face model"}), 500

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
