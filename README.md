# 3DCellularWorld

A simulator game where you can create and edit 3D cellular automaton voxel worlds using various elements.

This type of simulator is sometimes referred to as a 3d "falling sand simulator". This one is implemented using cellular automata for the benefits of parallelization.
## Writing
Read the [Technical Report](https://cis.temple.edu/tagit/publications/TAGIT-TR-18.pdf).

Read the [Wiki](https://github.com/ccrock4t/3DCellularWorld/wiki).

## Videos
https://www.youtube.com/watch?v=o1E0cWx2p6w

## Pictures

![pictureOfElementsList](https://github.com/ccrock4t/3DCellularWorld/blob/main/Assets/Images/elements.png?raw=true)
![pictureOfIsland](https://github.com/ccrock4t/3DCellularWorld/blob/main/Assets/Images/background.PNG?raw=true)



![pictureOfFlying](https://github.com/ccrock4t/3DCellularWorld/blob/main/Assets/Images/flying.gif?raw=true)
![pictureOfElements](https://github.com/ccrock4t/3DCellularWorld/blob/main/Assets/Images/elements.gif?raw=true)

# Requirements
Built with Unity.

You may select to run the algorithm on either your CPU or your GPU. The two versions look different: the CPU version renders meshes of voxels which are then drawn on the camera, whereas the GPU version directly draws the relevant voxel color for each camera pixel.
