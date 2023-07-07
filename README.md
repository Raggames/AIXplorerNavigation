# AIXplorerNavigation

[![Watch the video](https://github.com/Raggames/AIXplorerNavigation/blob/main/Videos/AIXplorer.png)]([https://youtu.be/vt5fpE0bzSY](https://www.youtube.com/watch?v=OuUDYfzAtek))

<b> PRESENTATION </b>

With procedurally generated terrains, NavMesh baking can be a big performance issue. 
I encountered this situation while working on my my multiplayer DungeonCrawler/Exploration project.

I decided to workaround this situation by creating a Navigation Systemm that allow agents to bake a small area around themselves that allow them to move
using a modified  A* implementation (originally by Sebastian Lague).
The A* itself is optimized with BinaryHeap sorting so it executes pretty fast.

When the agent wants to navigate to any position on the terrain, he will firstly try to compute a pathfinding solution to it. If the pathfinding algorithm don't find
a way, he will look for the closest point he can get from the destination (wich can be found in the A* Closed Set) and create a path to it (a partial path to destination). 
The agent will reach that point and bake a small area on this new position, and then retry to execute a pathfinding.
The process will repeat until the agent found a complete path and achieved his destination.

The movement of agents is simply made by using a characterController and a patrol behaviour between the path waypoints.

This is not a perfect solution that has two major drawbacks :
- the use of spherecastings to test the terrain position (walkable/unwalkable)
- the use of a preexisting empty two dimensionnal array of points that is filled with datas when explored by agent. Iterating over it will increase compute time exponentially as the grid gets bigger.
 
Nevertheless, the logic of this navigation can probably be improved by tiling the terrain in smaller areas (the agent could transit from one area to another), so the number of points can be small enough.
The baking of the 'Navigation Mesh' could also be done without using any raycasting if the procedural algorithm used can provide the positions of objects on the terrain. 

<b> TEST </b>

In the Sample Scene, click on 'N' to make the agent start navigate to its destination. 
Hold CRTL + Moving mouse allows you to bake the terrain like a brush.
