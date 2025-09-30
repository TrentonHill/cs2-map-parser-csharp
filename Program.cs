using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

class Program
{
    [StructLayout(LayoutKind.Sequential)]
    public struct Vector3
    {
        public float X, Y, Z;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct Triangle
    {
        public Vector3 P1, P2, P3;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct Edge
    {
        public byte Next, Twin, Origin, Face;
    }

    private static bool InRange(char x, char a, char b) => x >= a && x <= b;

    private static int GetBits(char x) => InRange(x, '0', '9') ? x - '0' : (x & (~0x20)) - 'A' + 0xA;

    private static byte GetByte(string x, int index) => (byte)((GetBits(x[index]) << 4) | GetBits(x[index + 1]));

    private static List<T> BytesToVec<T>(string bytes) where T : struct
    {
        try
        {
            var byteList = new List<byte>();
            int i = 0;
            while (i < bytes.Length)
            {
                if (bytes[i] == ' ')
                {
                    i++;
                }
                else
                {
                    if (i + 1 >= bytes.Length)
                    {
                        throw new FormatException("Invalid byte string format: incomplete byte pair.");
                    }
                    byteList.Add(GetByte(bytes, i));
                    i += 2;
                }
            }

            var size = Marshal.SizeOf(typeof(T));
            var result = new List<T>();
            var buffer = new byte[size];
            int bufferIndex = 0;

            foreach (var b in byteList)
            {
                buffer[bufferIndex++] = b;
                if (bufferIndex == size)
                {
                    GCHandle handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
                    try
                    {
                        T value = (T)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(T));
                        result.Add(value);
                    }
                    finally
                    {
                        handle.Free();
                    }
                    bufferIndex = 0;
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in BytesToVec: {ex.Message}");
            throw;
        }
    }

    private static List<int> GetCollisionAttributeIndices(Kv3Parser parser)
    {
        Console.WriteLine("Fetching collision attribute indices...");
        var indices = new List<int>();
        int index = 0;
        while (true)
        {
            string collisionGroupString = parser.GetValue($"m_collisionAttributes[{index}].m_CollisionGroupString");
            Console.WriteLine($"Checking m_collisionAttributes[{index}]: {collisionGroupString}");
            if (string.IsNullOrEmpty(collisionGroupString))
            {
                break;
            }
            if (collisionGroupString == "\"default\"" || collisionGroupString == "\"Default\"")
            {
                indices.Add(index);
            }
            index++;
        }
        Console.WriteLine($"Found {indices.Count} collision attribute indices.");
        return indices;
    }

    static void Main(string[] args)
    {
        try
        {
            Console.WriteLine("Starting .vphys file processing...");
            if (args.Length == 0)
            {
                Console.WriteLine("Please drag and drop a .vphys file onto the executable.");
                Console.ReadKey();
                return;
            }

            string fileName = args[0];
            Console.WriteLine($"Input file: {fileName}");
            if (!File.Exists(fileName) || Path.GetExtension(fileName).ToLower() != ".vphys")
            {
                Console.WriteLine("Invalid or missing .vphys file.");
                Console.ReadKey();
                return;
            }

            string exportFileName = Path.ChangeExtension(fileName, ".tri");
            Console.WriteLine($"Output file will be: {exportFileName}");
            var parser = new Kv3Parser();
            var triangles = new List<Triangle>();

            Console.WriteLine("Reading file content...");
            string content = File.ReadAllText(fileName);
            Console.WriteLine($"File content length: {content.Length} characters");
            Console.WriteLine("Parsing file content...");
            parser.Parse(content);

            Console.WriteLine("Getting collision attribute indices...");
            var collisionAttributeIndices = GetCollisionAttributeIndices(parser);

            // Process hulls
            Console.WriteLine("Processing hulls...");
            int index = 0;
            int countHulls = 0;
            while (true)
            {
                string collisionIndexStr = parser.GetValue($"m_parts[0].m_rnShape.m_hulls[{index}].m_nCollisionAttributeIndex");
                if (string.IsNullOrEmpty(collisionIndexStr))
                {
                    Console.WriteLine($"\nHulls: {index} (Total)");
                    Console.WriteLine($"Found {countHulls} hulls with tag 0");
                    break;
                }

                Console.WriteLine($"Processing hull[{index}] with collision index: {collisionIndexStr}");
                int collisionIndex = int.Parse(collisionIndexStr);
                if (collisionAttributeIndices.Contains(collisionIndex))
                {
                    string vertexPositionsStr = parser.GetValue($"m_parts[0].m_rnShape.m_hulls[{index}].m_Hull.m_VertexPositions");
                    if (string.IsNullOrEmpty(vertexPositionsStr))
                    {
                        vertexPositionsStr = parser.GetValue($"m_parts[0].m_rnShape.m_hulls[{index}].m_Hull.m_Vertices");
                    }
                    Console.WriteLine($"Vertex positions string length: {vertexPositionsStr.Length}");

                    var vertexProcessed = BytesToVec<float>(vertexPositionsStr);
                    Console.WriteLine($"Vertices processed: {vertexProcessed.Count / 3}");
                    var vertices = new List<Vector3>();
                    for (int i = 0; i < vertexProcessed.Count; i += 3)
                    {
                        vertices.Add(new Vector3 { X = vertexProcessed[i], Y = vertexProcessed[i + 1], Z = vertexProcessed[i + 2] });
                    }

                    var facesProcessed = BytesToVec<byte>(parser.GetValue($"m_parts[0].m_rnShape.m_hulls[{index}].m_Hull.m_Faces"));
                    Console.WriteLine($"Faces processed: {facesProcessed.Count}");
                    var edgesTmp = BytesToVec<byte>(parser.GetValue($"m_parts[0].m_rnShape.m_hulls[{index}].m_Hull.m_Edges"));
                    var edgesProcessed = new List<Edge>();
                    for (int i = 0; i < edgesTmp.Count; i += 4)
                    {
                        edgesProcessed.Add(new Edge
                        {
                            Next = edgesTmp[i],
                            Twin = edgesTmp[i + 1],
                            Origin = edgesTmp[i + 2],
                            Face = edgesTmp[i + 3]
                        });
                    }
                    Console.WriteLine($"Edges processed: {edgesProcessed.Count}");

                    foreach (var startEdge in facesProcessed)
                    {
                        int edge = edgesProcessed[startEdge].Next;
                        int iteration = 0;
                        while (edge != startEdge)
                        {
                            if (iteration++ > 10000) // Prevent infinite loops
                            {
                                Console.WriteLine("Warning: Possible infinite loop detected in hull edge processing.");
                                break;
                            }
                            int nextEdge = edgesProcessed[edge].Next;
                            triangles.Add(new Triangle
                            {
                                P1 = vertices[edgesProcessed[startEdge].Origin],
                                P2 = vertices[edgesProcessed[edge].Origin],
                                P3 = vertices[edgesProcessed[nextEdge].Origin]
                            });
                            edge = nextEdge;
                        }
                    }

                    countHulls++;
                }
                index++;
            }

            // Process meshes
            Console.WriteLine("Processing meshes...");
            index = 0;
            int countMeshes = 0;
            while (true)
            {
                string collisionIndexStr = parser.GetValue($"m_parts[0].m_rnShape.m_meshes[{index}].m_nCollisionAttributeIndex");
                if (string.IsNullOrEmpty(collisionIndexStr))
                {
                    Console.WriteLine($"\nMeshes: {index} (Total)");
                    Console.WriteLine($"Found {countMeshes} meshes with tag 0");
                    break;
                }

                Console.WriteLine($"Processing mesh[{index}] with collision index: {collisionIndexStr}");
                int collisionIndex = int.Parse(collisionIndexStr);
                if (collisionAttributeIndices.Contains(collisionIndex))
                {
                    var triangleProcessed = BytesToVec<int>(parser.GetValue($"m_parts[0].m_rnShape.m_meshes[{index}].m_Mesh.m_Triangles"));
                    Console.WriteLine($"Triangles processed: {triangleProcessed.Count / 3}");
                    var vertexProcessed = BytesToVec<float>(parser.GetValue($"m_parts[0].m_rnShape.m_meshes[{index}].m_Mesh.m_Vertices"));
                    Console.WriteLine($"Vertices processed: {vertexProcessed.Count / 3}");

                    var vertices = new List<Vector3>();
                    for (int i = 0; i < vertexProcessed.Count; i += 3)
                    {
                        vertices.Add(new Vector3 { X = vertexProcessed[i], Y = vertexProcessed[i + 1], Z = vertexProcessed[i + 2] });
                    }

                    for (int i = 0; i < triangleProcessed.Count; i += 3)
                    {
                        triangles.Add(new Triangle
                        {
                            P1 = vertices[triangleProcessed[i]],
                            P2 = vertices[triangleProcessed[i + 1]],
                            P3 = vertices[triangleProcessed[i + 2]]
                        });
                    }

                    countMeshes++;
                }
                index++;
            }

            // Write triangles to output file
            Console.WriteLine($"Writing {triangles.Count} triangles to {exportFileName}...");
            using (var fs = new FileStream(exportFileName, FileMode.Create))
            using (var bw = new BinaryWriter(fs))
            {
                foreach (var triangle in triangles)
                {
                    bw.Write(triangle.P1.X);
                    bw.Write(triangle.P1.Y);
                    bw.Write(triangle.P1.Z);
                    bw.Write(triangle.P2.X);
                    bw.Write(triangle.P2.Y);
                    bw.Write(triangle.P2.Z);
                    bw.Write(triangle.P3.X);
                    bw.Write(triangle.P3.Y);
                    bw.Write(triangle.P3.Z);
                }
            }

            Console.WriteLine($"Processed file: {fileName} -> {exportFileName}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error processing file: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }
        Console.WriteLine("Press any key to exit...");
        Console.ReadKey();
    }
}