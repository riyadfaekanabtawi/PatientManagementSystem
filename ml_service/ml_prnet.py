import os
import sys
import torch
import numpy as np
import cv2
import boto3
from flask import Flask, request, jsonify

# Add PRNet-PyTorch to Python Path
sys.path.append("/home/ubuntu/PatientManagementSystem/PRNet-PyTorch")
from torchmodel import InitPRN2 as PRNet  # Using InitPRN2 as the PRNet model

# Initialize Flask App
app = Flask(__name__)

# Load PRNet-PyTorch Model
DEVICE = torch.device("cuda" if torch.cuda.is_available() else "cpu")
MODEL_PATH = "models/PRNet-20180409-epoch-90.pth"
prnet = PRNet().to(DEVICE)
prnet.load_state_dict(torch.load(MODEL_PATH, map_location=DEVICE))
prnet.eval()
print("✅ PRNet-PyTorch Model Loaded Successfully!")

# AWS S3 Configurations
AWS_ACCESS_KEY = "AKIAUP7RI5QI6QH64G6C"
AWS_SECRET_KEY = "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"
AWS_BUCKET_NAME = "patients-tree"
AWS_REGION = "us-east-2"

# Initialize S3 Client
s3_client = boto3.client(
    "s3",
    aws_access_key_id=AWS_ACCESS_KEY,
    aws_secret_access_key=AWS_SECRET_KEY,
    region_name=AWS_REGION,
)

# Define upload folder
UPLOAD_FOLDER = "uploads"
os.makedirs(UPLOAD_FOLDER, exist_ok=True)

# Function to upload a file to S3
def upload_to_s3(file_path, s3_key):
    try:
        s3_client.upload_file(file_path, AWS_BUCKET_NAME, s3_key)
        file_url = f"https://{AWS_BUCKET_NAME}.s3.{AWS_REGION}.amazonaws.com/{s3_key}"
        return file_url
    except Exception as e:
        return str(e)

# Function to preprocess the image
def preprocess_image(image_path):
    image = cv2.imread(image_path)
    if image is None:
        raise ValueError("Invalid image format or corrupted file")
    image = cv2.cvtColor(image, cv2.COLOR_BGR2RGB)
    image = cv2.resize(image, (256, 256))
    image = torch.tensor(image, dtype=torch.float32).permute(2, 0, 1).unsqueeze(0) / 255.0
    return image.to(DEVICE)

# API Endpoint to Generate 3D Face Model
@app.route("/flask-api/generate", methods=["POST"])
def generate_3d_face():
    try:
        file = request.files.get("image")
        if not file:
            return jsonify({"error": "No image provided"}), 400
        
        image_path = os.path.join(UPLOAD_FOLDER, "uploaded_image.jpg")
        file.save(image_path)
        
        # Preprocess the image
        image_tensor = preprocess_image(image_path)
        
        # Generate 3D face model
        with torch.no_grad():
            output = prnet(image_tensor)
        output_np = output.squeeze().cpu().numpy()
        
        # Save 3D face model as OBJ
        obj_path = os.path.join(UPLOAD_FOLDER, "3d_face.obj")
        np.savetxt(obj_path, output_np.reshape(-1, 3))
        
        # Upload OBJ file to S3
        s3_key = "3d_models/3d_face.obj"
        s3_url = upload_to_s3(obj_path, s3_key)
        
        return jsonify({"mesh_file_url": s3_url}), 200
    except Exception as e:
        return jsonify({"error": str(e)}), 500

# Run Flask Server
if __name__ == "__main__":
    app.run(host="0.0.0.0", port=5001, debug=True)
