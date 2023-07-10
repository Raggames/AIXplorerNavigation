# AIXplorerNavigation

https://github.com/Raggames/AIXplorerNavigation/assets/51744359/60eb3111-37c5-42ea-bce7-4730219d8bc7

<b> PRESENTATION </b>

With procedurally generated terrains, NavMesh baking can be a big performance issue. 
I encountered this situation while working on my my multiplayer DungeonCrawler/Exploration project.

I decided to workaround this problem by creating a Navigation Systemm that allow agents to bake a small area around themselves that allow them to move
using a modified  A* implementation (originally by Sebastian Lague). The original A* implementation was optimized with BinaryHeap sorting so it executes pretty fast.
The idea is that we won't need the level to be baked before the agents start navigates. But it will be baked dynamically, on demand. 

<b> LOGIC </b>

When the agent wants to navigate to any position on the terrain, he will firstly try to compute a pathfinding solution to it, with the GridNodes that the NavigationCore already knows. 

If the pathfinding find a complete solution (= the start and destination are contained in a linked cloud of GridNodes), it just return the path.

If the pathfinding algorithm don't find a way to the destination, it will look for the closest point he can get (from the destination, wich can be found in the A* Closed Set),
and then create a path to it (a partial path to destination). 

In that partial path situation, when the agent will have reached that point, he will then bake a small area on this new position, and then retry to execute a pathfinding.
The process will repeat until the agent found a complete path and achieved his destination. The process can increase the area of exploration if the path is not found, until some limitations
that can be setted on the NavigationComponent.

The movement of agents is simply made by using a characterController and a patrol behaviour between the path waypoints.

<b> 20 Agents Test </b>

Just popping random moves with 20 agents simultaneously. 

The few framerate loss is due mainly to the Path Gizmos.

The execution of pathfinding is scheduled "one by one" on another thread.

https://github.com/Raggames/AIXplorerNavigation/assets/51744359/01eb8d7d-b024-4834-96db-965dab507c18

<b> CONLUSION ? </b>

This is not a perfect solution that has a major drawback : the use of spherecastings to test the terrain position (walkable/unwalkable). Nevertheless, after some stress testings and by computing 
the pathfinding on another thread (simply C# Tasks), the solution seems largely acceptable for a small scope / indy game, as it can hanble many dozens of agents without dropping the frame rate.
 
Moreover, the logic of this navigation can probably be improved by tiling the terrain in smaller areas (the agent could even transit from one area to another), so the number of points known by the NavigationCore can be small enough.
The baking of the 'Navigation Mesh' could also be done without using any raycasting with a Procedural Generation algorithm that can provide the positions and sizes of objects on the terrain (avoiding raycasts). 

<b> TEST </b>

In the Sample Scene, 
 - click on 'N' to make the agents start navigate to the mouse.
 - click on 'B' to make all agents move randomly.
 - Hold CRTL + Moving mouse allows you to bake the terrain like a brush.



<b> NEXT ? </b>

I'm currently working on an optimization of the system by using Unity's Job System to compute more pathfindings at a time. It requires large modifications in the logic as for now, the GridNodes used by A* are shared upon all entities that uses the same NavigationCore. I'm not sure this solution will be much better, but I give it a try anyway.
