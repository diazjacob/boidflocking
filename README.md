# boidflocking
A research paper written in late 2020 on the subject of optimization of boid flock systems. This paper focuses on the interaction between CPU and GPU calculations and strives to find the best balance between them. I go over the definition of a boid, my method for creating them, and how we can increase the efficiency of their computation. I finally compare the naive approach (O(n^2)) to my final approach (O(n)) with SIMD GPU operations.

## Some Images:

A visualization of all of the calculations the boids take into account every frame
<img src="/pics/Boid_rays.png" width="600">

The final result of the optimization:
<img src="/pics/Captureeee.png" width="600">

A visualization of the texture buffer that acts to connect the C# code to the relevant compute shader
<img src="/pics/Final_TextureBuffer.png" width="600">


