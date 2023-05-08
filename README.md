
![StaticMagStegonographyThin](https://user-images.githubusercontent.com/52022661/234994382-ef05e863-c3c0-414c-b1d2-012790817c23.png)

Static Mag is a sci-fi action game where you steal weapons from enemies using a magnetic grapple and is being built with the Unity game engine. This repository contains a small subset of scripts for the game to get an idea of how I program.

## Features
### Custom Player Controller
* Wall Running
* Climbing
* Sliding
* Grappling
* Velocity Correction
### Advance AI
* Attack Coordinator
   * Assigns a subset of enemies to be attackers based on a scoring system that factors in player distance, player visibility, and enemy type.
* Cover System
   * Allows humanoid enemies to take cover behind obstacles or terrain when not attacking.
* Reverse Cover System
   * Attacking enemies move to positions that are exposed from cover so the player can shoot them easily, increasing the intesity of combat. 
* Distribution Based Enemy Accuracy
   * The accuracy of attacking enemies is determined by an animation curve in Unity, but interpreted as a distribution curve, where the X values represent the distance in meters a bullet will miss the player by, and the Y values represents occurance. This allows the difficulty of the game to be tuned without introducing complete randomness.
### Current Enemies
* Tac Soldier
  * Humanoid soldier whose weapons can be stolen by the player using the magnet.
  * Inverse kinematics that influence head, chest, and arms for procedural aiming.
* Olive
  * Floating turret that charges up and tracks the player with a laser beam.
  * Player can use the magnet to launch themselves towards it and destroy it with a punch.
  * Allows the player to jump across large gaps in the level.
* Tick
  * Small, aggressive four legged robot with a ticking time bomb.
  * Chases the player and when within range, leaps and explodes near the player.
  * Player can use the magnet to grab the robot and throw it at enemies before bomb timer expires.

Watch ["Static Mag: Overview and Key Features"](https://www.youtube.com/watch?v=nDraKM92OQI)

[![Static Mag Overview Video](https://user-images.githubusercontent.com/52022661/235011495-68117a46-e003-4dda-8ded-2d8106832e31.png)](https://youtu.be/nDraKM92OQI)

![Overview 00_00_07_12 Still002](https://user-images.githubusercontent.com/52022661/234996825-0096c71f-583c-4a89-ae80-0caac6f0a687.png)
![Overview 00_00_13_20 Still003](https://user-images.githubusercontent.com/52022661/234997708-c53d437c-7ea6-4847-b0d9-0c5f4fcad0b1.png)
![Overview 00_00_22_12 Still004](https://user-images.githubusercontent.com/52022661/234998429-a32fbcec-79ea-41f2-b0e7-dc39e9008120.png)
