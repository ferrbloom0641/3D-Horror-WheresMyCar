# Escape the Parking Garage

First-person parking garage horror game by FERRBLOOM.

## Open the project

- Unity version: **6000.3.18f1**.
- Clone this repository, then add its root folder in Unity Hub. The root contains `Assets`, `Packages`, and `ProjectSettings`.
- Open `Assets/Scenes/Parking.unity`.
- Unity regenerates `Library`, `Temp`, and project files on the first import.

## Windows build

- Install Windows Build Support for the Unity version above.
- Select Windows / x86_64 in Build Profiles, with `Assets/Scenes/Parking.unity` enabled.
- Build into `Build/` or `Builds/`.
- The build callback applies the blue parking P icon and the Unity → FERRBLOOM startup logos.
- Distribute the executable together with its `_Data` folder, runtime folders, and DLLs. Exclude folders labeled `DoNotShip` from the distribution ZIP.

## Version control

Commit source files, required assets and their `.meta` files, `Packages`, and `ProjectSettings`.
Build output, Unity caches, local backups, exports, and one-off capture/build automation are ignored.
