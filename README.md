# .net 4.8 C# CS2 Map Parser

Converts .vphys files into a .tri format, which consists of a list of vec3 points, every three points represent a single triangle!

This approach is used because parsing .vphys files directly with VK3 Parser consumes a significant amount of memory (e.g., the Inferno map is ~5.9 GB) and contains a lot of unnecessary data. Converting to .tri reduces memory usage and speeds up the process of using these vectors in your code.

## Other Credits
- [Code Base](https://github.com/AtomicBool)
- [KV3 Parser](https://github.com/joepriit/cpp-kv3-parser)
- [Memory Lib](https://github.com/pain1929/csgo-game-test/blob/main/handle/)
