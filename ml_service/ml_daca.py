#!/usr/bin/env python3
import os
import sys
import traceback

# -------------------------------------------------------------------
# Force CPU usage and disable CUDA caching.
os.environ["CUDA_VISIBLE_DEVICES"] = "-1"
os.environ["PYTORCH_NO_CUDA_MEMORY_CACHING"] = "1"

# Add the DECA directory to the Python path.
sys.path.append(os.path.abspath("/home/ubuntu/PatientManagementSystem/DECA"))
# -------------------------------------------------------------------

import cv2
import torch
import torch.nn.functional as F

# -------------------------------------------------------------------
# Monkey-patch torch.load so that tensors load on the CPU.
original_torch_load = torch.load
def cpu_torch_load(*args, **kwargs):
    if "map_location" not in kwargs:
        kwargs["map_location"] = torch.device("cpu")
    return original_torch_load(*args, **kwargs)
torch.load = cpu_torch_load
# -------------------------------------------------------------------

from flask import Flask, request, jsonify
import boto3

# -------------------------------------------------------------------
# Configure logging.
import logging
logging.basicConfig(
    level=logging.DEBUG,
    format="%(asctime)s [%(levelname)s] %(name)s: %(message)s",
)
logger = logging.getLogger("ml_deca")
# -------------------------------------------------------------------

app = Flask(__name__)

# AWS S3 Configurations (for production, secure these via environment variables)
AWS_ACCESS_KEY = "AKIAUP7RI5QI6QH64G6C"
AWS_SECRET_KEY = "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"
AWS_BUCKET_NAME = "patients-tree"
AWS_REGION = "us-east-2"

# Initialize S3 client.
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

# Force PyTorch to use CPU.
torch.backends.cudnn.enabled = False
device = torch.device("cpu")

# Create a temporary uploads folder.
UPLOAD_FOLDER = os.path.join(os.getcwd(), "uploads")
os.makedirs(UPLOAD_FOLDER, exist_ok=True)
logger.info("Uploads folder: %s", UPLOAD_FOLDER)

# -------------------------------------------------------------------
# Import DECA model and configuration.
from decalib.deca import DECA
from decalib.utils.config import cfg as deca_cfg

# -------------------------------------------------------------------
# Initialize DECA.
try:
    logger.info("Step 1: Loading DECA model...")
    deca_cfg.device = "cpu"
    deca_cfg.model.use_tex = False  # Disable texture generation.
    deca = DECA(config=deca_cfg)
    deca.device = device
    deca.to(device)
    logger.info("DECA model loaded successfully.")
except Exception as e:
    logger.error("Error initializing DECA model: %s", e)
    traceback.print_exc()
    sys.exit(1)

# -------------------------------------------------------------------
# Patch the forward method of the encoder (deca.E_flame) to fix the feature shape.
try:
    def new_forward(self, x):
        # Log available attributes for debugging.
        logger.debug("Inside new_forward. Available attributes of E_flame: %s", dir(self))
        
        # Try known attributes to extract convolutional features.
        if hasattr(self, "encoder"):
            features = self.encoder(x)
            logger.debug("Using 'encoder' for feature extraction. features.shape = %s", features.shape)
        elif hasattr(self, "base"):
            features = self.base(x)
            logger.debug("Using 'base' for feature extraction. features.shape = %s", features.shape)
        elif hasattr(self, "conv"):
            features = self.conv(x)
            logger.debug("Using 'conv' for feature extraction. features.shape = %s", features.shape)
        else:
            raise AttributeError("No known feature extraction attribute found in E_flame.")

        # If the feature map is 4D and has spatial dimensions 2x2, pool it to 1x1.
        if features.dim() == 4:
            H, W = features.size(2), features.size(3)
            if H == 2 and W == 2:
                logger.debug("Feature map shape before pooling: %s", features.shape)
                features = F.adaptive_avg_pool2d(features, (1, 1))
                logger.debug("Feature map shape after pooling: %s", features.shape)
        else:
            logger.debug("Features are not 4D; shape = %s", features.shape)
            
        # Flatten features.
        features = features.view(features.size(0), -1)
        logger.debug("Flattened features shape: %s", features.shape)
        return self.layers(features)
    
    # Override the forward method of deca.E_flame.
    deca.E_flame.forward = new_forward.__get__(deca.E_flame, deca.E_flame.__class__)
    logger.info("Successfully patched deca.E_flame.forward with new_forward.")
except Exception as e:
    logger.error("Error patching deca.E_flame.forward: %s", e)
    traceback.print_exc()

# -------------------------------------------------------------------
# Function to process an image using DECA.
def process_image(image_path):
    logger.info("Step 3: Loading and processing the image from %s", image_path)
    try:
        # Load the image.
        image = cv2.imread(image_path)
        if image is None:
            raise ValueError("Invalid image format or corrupted file")
        image = cv2.cvtColor(image, cv2.COLOR_BGR2RGB)
        
        # Convert to tensor [1, 3, H, W] and normalize.
        image_tensor = torch.tensor(image).permute(2, 0, 1).unsqueeze(0).float() / 255.0
        image_tensor = image_tensor.to(device)
        logger.info("Image loaded and preprocessed successfully. Original shape: %s", image_tensor.shape)

        # If the image is not square, perform a center crop.
        _, _, h, w = image_tensor.shape
        if h != w:
            min_dim = min(h, w)
            top = (h - min_dim) // 2
            left = (w - min_dim) // 2
            image_tensor = image_tensor[:, :, top:top+min_dim, left:left+min_dim]
            logger.info("Image center-cropped to square. New shape: %s", image_tensor.shape)

        # Resize the image (try 256x256; if problems persist, you might experiment with 224x224).
        image_tensor = torch.nn.functional.interpolate(
            image_tensor, size=(256, 256), mode="bilinear", align_corners=False
        )
        logger.info("Image resized to 256x256 successfully. Final shape: %s", image_tensor.shape)

        # Generate 3D face model using DECA.
        logger.info("Step 4: Generating 3D face model using DECA...")
        with torch.no_grad():
            codedict = deca.encode(image_tensor)
            opdict = deca.decode(codedict)
        logger.info("3D face model generated successfully.")

        # Save the 3D model as an OBJ file.
        mesh_path = os.path.join(UPLOAD_FOLDER, "3d_face.obj")
        deca.save_obj(mesh_path, opdict)
        logger.info("Step 5: 3D model saved to %s", mesh_path)
        return mesh_path
    except Exception as e:
        logger.error("Error processing image: %s", e)
        traceback.print_exc()
        return None

# -------------------------------------------------------------------
# Function to upload a file to AWS S3.
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

# -------------------------------------------------------------------
# Flask API endpoint for 3D face model generation.
@app.route("/flask-api/generate", methods=["POST"])
def generate_3d_face():
    logger.info("Step 2: Receiving uploaded images...")
    try:
        files = request.files
        front_img = files.get("front")
        if not front_img:
            return jsonify({"error": "Front image is required"}), 400

        # Save the uploaded front image locally.
        front_img_path = os.path.join(UPLOAD_FOLDER, "front_image.jpg")
        front_img.save(front_img_path)
        logger.info("Front image saved to %s", front_img_path)

        # Process the image to generate a 3D model.
        mesh_path = process_image(front_img_path)
        if not mesh_path:
            return jsonify({"error": "Failed to generate 3D face model"}), 500

        # Upload the resulting 3D model to AWS S3.
        s3_key = f"3dmodels/{os.path.basename(mesh_path)}"
        file_url = upload_to_s3(mesh_path, s3_key)
        if not file_url:
            return jsonify({"error": "Failed to upload 3D model to S3"}), 500

        logger.info("Step 6: Returning the generated 3D model URL...")
        return jsonify({"mesh_file_url": file_url}), 200
    except Exception as e:
        logger.error("Server error: %s", e)
        traceback.print_exc()
        return jsonify({"error": str(e)}), 500

# -------------------------------------------------------------------
# Main entry point.
if __name__ == "__main__":
    try:
        logger.info("Step 0: Starting the Flask server on 0.0.0.0:5001")
        app.run(host="0.0.0.0", port=5001, debug=True)
    except Exception as e:
        logger.error("Error running Flask server: %s", e)
        traceback.print_exc()
        sys.exit(1)
