# KDA Garage asset build

Original procedural geometry and textures by Roderic Systems; no external building assets are read. Run build_garage.py to create the installable Garage/garage.mu and six textures, or pass --output for staging. preview_garage.py renders the written MU using --asset and --output paths.

Unity axes are Y up and Z outward through the door. Coordinates, normals, winding and colliders convert at the MU writer boundary. The model is 7.50 m wide, 5.37 m tall and 11.54 m deep. There are 37 rendered box meshes, 444 triangles and six solid box colliders. KDAEntrance is at local (0,0,5.65). The building remains a closed exterior; the runtime provides a separate workshop interior.

The catalog name is KDAGarage. KDAWorldGarage resolves the entrance transform for F8 entry and Drive Out. Launch gap is seven world metres and does not grow with model scale. Build reports describe the current asset, not a player placement.
