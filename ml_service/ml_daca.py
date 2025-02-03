import os
import cv2
import numpy as np
import torch
from flask import Flask, request, jsonify
from decalib.deca import DECA
from decalib.utils.config import cfg as deca_cfg
from decalib.utils.tensor_cropper import crop_tensor
import boto3

app = Flask(__name__)

# AWS S3 Configurations
AWS_ACCESS_KEY = "AKIAUP7RI5QI6QH64G6C"
AWS_SECRET_KEY = "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"
AWS_BUCKET_NAME = "patients-tree"
AWS_REGION = "us-east-2"

# Initialize S3 client
s3_client = boto3.client(
    "s3",
    aws_access_key_id=AWS_ACCESS_KEY,
    aws_secret_access_key=AWS_SECRET_KEY,
    region_name=AWS_REGION
)

# Temporary uploads folder for processing images and saving models
UPLOAD_FOLDER = os.path.join(os.getcwd(), "uploads")
os.makedirs(UPLOAD_FOLDER, exist_ok=True)

# Initialize DECA
print("Step 1: Loading DECA model...")
deca_cfg.model.use_tex = False  # Disable texture generation for simplicity
deca = DECA(config=deca_cfg)

# Function to process an image using DECA
def process_image(image_path):
    """
    Process an input image with DECA and generate a 3D face model.
    """
    try:
        print("Step 3: Loading and processing the image...")
        # Load and preprocess the image
        image = cv2.imread(image_path)
        image = cv2.cvtColor(image, cv2.COLOR_BGR2RGB)  # Convert to RGB
        image_tensor = torch.tensor(image).permute(2, 0, 1).unsqueeze(0).float() / 255.0

        # Generate 3D reconstruction using DECA
        print("Step 4: Generating 3D face model using DECA...")
        with torch.no_grad():
            codedict = deca.encode(image_tensor)
            opdict = deca.decode(codedict)

        # Save the 3D model as .obj
        print("Step 5: Saving 3D model...")
        mesh_path = os.path.join(UPLOAD_FOLDER, "3d_face.obj")
        deca.save_obj(mesh_path, opdict)

        return mesh_path
    except Exception as e:
        print(f"Error processing image: {e}")
        return None

# Function to upload a file to AWS S3
def upload_to_s3(file_path, s3_key):
    try:
        print(f"Uploading {file_path} to S3 bucket {AWS_BUCKET_NAME}...")
        s3_client.upload_file(file_path, AWS_BUCKET_NAME, s3_key)
        file_url = f"https://{AWS_BUCKET_NAME}.s3.{AWS_REGION}.amazonaws.com/{s3_key}"
        print(f"Upload successful: {file_url}")
        return file_url
    except Exception as e:
        print(f"Error uploading to S3: {e}")
        return None

# Flask route for 3D face model generation
@app.route("/flask-api/generate", methods=["POST"])
def generate_3d_face():
    """
    Flask API endpoint to generate a 3D face model.
    """
    try:
        print("Step 2: Receiving uploaded images...")
        # Save uploaded front image
        files = request.files
        front_img = files.get('front')
        if not front_img:
            return jsonify({'error': 'Front image is required'}), 400
        
        # Save the uploaded file locally
        front_img_path = os.path.join(UPLOAD_FOLDER, 'front_image.jpg')
        front_img.save(front_img_path)

        # Process the front image to generate the 3D model
        mesh_path = process_image(front_img_path)
        if not mesh_path:
            return jsonify({'error': 'Failed to generate 3D face model'}), 500

        # Upload to S3
        s3_key = f"3dmodels/{os.path.basename(mesh_path)}"
        file_url = upload_to_s3(mesh_path, s3_key)
        if not file_url:
            return jsonify({'error': 'Failed to upload 3D model to S3'}), 500

        print("Step 6: Returning the generated 3D model URL...")
        return jsonify({'mesh_file_url': file_url}), 200
    except Exception as e:
        print(f"Server error: {e}")
        return jsonify({'error': str(e)}), 500

if __name__ == "__main__":
    print("Step 0: Starting the Flask server...")
    app.run(host="0.0.0.0", port=5001, debug=True)
