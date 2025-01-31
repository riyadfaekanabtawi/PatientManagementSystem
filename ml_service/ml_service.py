from flask import Flask, request, jsonify
import boto3
import os
import uuid
import traceback
import urllib.request
import ssl
import cv2
import numpy as np
import open3d as o3d
import trimesh
import mediapipe as mp

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

TEMP_DIR = "temp"
os.makedirs(TEMP_DIR, exist_ok=True)

# Initialize MediaPipe for facial landmark detection
mp_face_mesh = mp.solutions.face_mesh
face_mesh = mp_face_mesh.FaceMesh(static_image_mode=True, max_num_faces=1, refine_landmarks=True)

# ✅ Disable SSL verification (Temporary Fix for Certificate Issues)
ssl._create_default_https_context = ssl._create_unverified_context

def download_image(url):
    """Download an image from a URL and return it as a numpy array."""
    try:
        response = urllib.request.urlopen(url)  # SSL verification is disabled globally
        image_array = np.asarray(bytearray(response.read()), dtype=np.uint8)
        return cv2.imdecode(image_array, cv2.IMREAD_COLOR)
    except Exception as e:
        print(f"❌ Failed to download image from {url}: {e}")
        return None

def generate_3d_face(front_img, left_img, right_img, back_img):
    try:
        print("📸 Downloading and processing images for 3D face reconstruction...")

        # Download images
        images = [download_image(url) for url in [front_img, left_img, right_img, back_img]]
        if any(img is None for img in images):
            print("❌ Error: One or more images could not be downloaded.")
            return None

        # Detect facial landmarks in all images
        points_3d = []
        for image in images:
            image_rgb = cv2.cvtColor(image, cv2.COLOR_BGR2RGB)
            results = face_mesh.process(image_rgb)
            if results.multi_face_landmarks:
                for face_landmarks in results.multi_face_landmarks:
                    for landmark in face_landmarks.landmark:
                        x, y = int(landmark.x * image.shape[1]), int(landmark.y * image.shape[0])
                        z = landmark.z * 100  # Scale depth values
                        points_3d.append([x, y, z])

        if not points_3d:
            print("❌ No facial landmarks detected!")
            return None

        # Convert to Open3D point cloud
        pcd = o3d.geometry.PointCloud()
        pcd.points = o3d.utility.Vector3dVector(np.array(points_3d))

        # Generate a mesh from the point cloud
        print("🔄 Creating 3D mesh...")
        mesh = o3d.geometry.TriangleMesh.create_from_point_cloud_alpha_shape(pcd, alpha=0.03)
        mesh.compute_vertex_normals()

        # Convert Open3D mesh to Trimesh and export as `.glb`
        print("📦 Exporting 3D model as `.glb`...")
        trimesh_mesh = trimesh.Trimesh(vertices=np.asarray(mesh.vertices), faces=np.asarray(mesh.triangles))
        model_filename = f"{uuid.uuid4()}.glb"
        model_path = os.path.join(TEMP_DIR, model_filename)
        trimesh_mesh.export(model_path, file_type="glb")  # Ensure it's a `.glb` file

        return model_path
    except Exception as e:
        print("❌ Error generating 3D face model:", e)
        traceback.print_exc()
        return None

@app.route("/generate", methods=["POST"])
def generate_3d_model():
    try:
        data = request.json
        print("📥 Received data:", data)  

        # Validate input
        front_img = data.get("front")
        left_img = data.get("left")
        right_img = data.get("right")
        back_img = data.get("back")

        if not all([front_img, left_img, right_img, back_img]):
            print("❌ Error: Missing images!")
            return jsonify({"error": "All four images are required"}), 400

        # Generate a 3D model from images
        model_path = generate_3d_face(front_img, left_img, right_img, back_img)
        if model_path is None:
            return jsonify({"error": "3D model generation failed."}), 500

        # Upload to S3
        s3_key = f"3dmodels/{os.path.basename(model_path)}"
        try:
            print(f"📤 Uploading model to S3: {s3_key}")
            s3_client.upload_file(model_path, AWS_BUCKET_NAME, s3_key)
            model_url = f"https://{AWS_BUCKET_NAME}.s3.{AWS_REGION}.amazonaws.com/{s3_key}"

            # ✅ Ensure JSON format with correct key
            response = {"modelFileUrl": model_url}
            print("✅ Returning JSON Response:", response)

            return jsonify(response)  # ✅ Always return JSON
        except Exception as e:
            print("❌ S3 Upload Error:", str(e))
            return jsonify({"error": "Failed to upload to S3", "details": str(e)}), 500

    except Exception as e:
        print("❌ Server Error:", str(e))
        traceback.print_exc()  
        return jsonify({"error": "Internal server error", "details": str(e)}), 500

# Run Flask Server
if __name__ == "__main__":
    app.run(host="0.0.0.0", port=5001, debug=True)