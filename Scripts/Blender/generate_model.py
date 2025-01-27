import bpy

def generate_3d_model(front_image, left_image, right_image, back_image, output_path):
    # Clear existing objects
    bpy.ops.wm.read_factory_settings(use_empty=True)
    
    # Set up images as planes (requires "Import Images as Planes" add-on)
    bpy.ops.import_image.to_plane(files=[
        {"name": front_image}, 
        {"name": left_image}, 
        {"name": right_image}, 
        {"name": back_image}
    ])

    # Arrange the planes (front, left, right, back)
    objects = bpy.context.scene.objects
    objects[0].location = (0, 0, 0)  # Front
    objects[1].location = (-2, 0, 0)  # Left
    objects[2].location = (2, 0, 0)  # Right
    objects[3].location = (0, -2, 0)  # Back
    
    # Combine planes into a single object
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.join()
    
    # Export the 3D model
    bpy.ops.export_scene.gltf(filepath=output_path, export_format='GLB')

# Example usage
if __name__ == "__main__":
    generate_3d_model(
        front_image="front.jpg",
        left_image="left.jpg",
        right_image="right.jpg",
        back_image="back.jpg",
        output_path="/path/to/output/3d_model.glb"
    )