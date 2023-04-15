using System;
using UnityEngine;
using Random = UnityEngine.Random;
using System.Collections.Generic;
using UnityEngine.UI;

namespace Lab6.Scripts
{

    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class Landscape1 : MonoBehaviour
    {
        private bool _isDirty;
        private Mesh _mesh;
        [SerializeField] private Gradient gradient;

        [Range(0, 1)] [SerializeField] private float gain = 0.1f;
        [Range(1, 3)] [SerializeField] private float lacunarity = 1f;
        [Range(1, 8)] [SerializeField] private int octaves = 1;

        [SerializeField] private float scale = 5f;
        [SerializeField] private Vector2 shift = Vector2.zero;
        [SerializeField] private int state = 0;
        [SerializeField] private int resolution = 256;
        [SerializeField] private float length = 256f;
        [SerializeField] private float height = 50f;
        [SerializeField] private Slider gainSlider;
        [SerializeField] private Slider lacSlider;
        [SerializeField] private Slider octaSlider;
        [SerializeField] private Slider stateSlider;

        private void Awake()
        {
            (GetComponent<MeshFilter>().mesh = _mesh = new Mesh { name = name }).MarkDynamic();
            gainSlider.onValueChanged.AddListener((value) => {gain = value; GenerateLandscape();});
            lacSlider.onValueChanged.AddListener((value) => {lacunarity =  value; GenerateLandscape();});
            octaSlider.onValueChanged.AddListener((value) => {octaves =  (int)(value); GenerateLandscape();});
            stateSlider.onValueChanged.AddListener((value) => {state =  (int)(value); GenerateLandscape();});
        }

        private void OnValidate()
        {
            _isDirty = true;
        }

        private void Update()
        {
            if (!_isDirty) return;
            GenerateLandscape();
            _isDirty = false;
        }

        private void GenerateLandscape()
        {
            // First, initialize the data structures
            Color[] colors = new Color[resolution * resolution];
            int[] triangles = new int[(resolution - 1) * (resolution - 1) * 3 * 2];
            Vector3[] vertices = new Vector3[resolution * resolution];

            // Then, loop over the vertices and populate the data structures: vertices are a resolution * resolution grid, so we need a double for loop over resolution to cover all vertices 
            InitializeVerticesColors(vertices, colors);

            InitializeTriangles(triangles);

            // ThermalErosion(vertices);

            // Assign the data structures to the mesh
            _mesh.Clear();
            _mesh.SetVertices(vertices);
            _mesh.SetColors(colors);
            _mesh.SetTriangles(triangles, 0);
            _mesh.RecalculateNormals();
        }

        private void InitializeVerticesColors(Vector3[] vertices, Color[] colors)
        {
            for (int i = 0; i < resolution; i++)
            {
                for (int j = 0; j < resolution; j++)
                {
                    int index = i * resolution + j;
                    Vector2 coords = new Vector2((float)i / (resolution - 1), (float)j / (resolution - 1));
                    var elevation = 1.414214f * FractalNoise(coords, gain, lacunarity, octaves, scale, shift, state);
                    elevation = islandFilter(coords,elevation); // comment this line out for no island filter
                    colors[index] = gradient.Evaluate(elevation);
                    vertices[index] = new Vector3(length * coords.x, height * elevation, length * coords.y);
                    index++;
                }
            }
        }

        private void InitializeTriangles(int[] triangles)
        {
            int currentVertex = 0;
            for (int i = 0; i < triangles.Length; i += 6)
            {
                triangles[i] = currentVertex;
                triangles[i + 1] = currentVertex + 1;
                triangles[i + 2] = currentVertex + resolution;
                triangles[i + 3] = currentVertex + resolution + 1;
                triangles[i + 4] = currentVertex + resolution;
                triangles[i + 5] = currentVertex + 1;
                currentVertex++;
            }
        }


        // This unfortunately doesn't really work
        private void ThermalErosion(Vector3[] vertices)
        {
            float angle_of_repose = 30f;
            float erosion_rate = 0.01f;
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3[] neighbours = calculateNeighbours(i, vertices);

                for (int j = 0; j < neighbours.Length; j++)
                {
                    var delta_elevation = vertices[i].y - neighbours[j].y;
                    // var distance = 0; //euclidean distance between vertices[i].x, vertices[i].z en neighbours[i].x, neighbours[i].z
                    var distance = Math.Sqrt((Math.Pow((float)vertices[i].x - (float)neighbours[j].x, 2) + Math.Pow((float)vertices[i].z - (float)neighbours[j].z, 2)));
                    var height_threshold = (float)distance * Mathf.Tan((float)(Math.PI) / 180f * angle_of_repose);//(radians(ANGLE_OF_REPOSE));

                    if (delta_elevation > height_threshold)
                    {
                        float erosion = erosion_rate * (delta_elevation - height_threshold);
                        vertices[i].y -= erosion;
                        neighbours[j].y += erosion;
                    }
                }
            }
        }


        private Vector3[] calculateNeighbours(int index, Vector3[] vertices)
        {
            int[] possibleNeighbours = { index - resolution - 1, index - resolution, index - resolution + 1, index - 1, index + 1, index + resolution - 1, index + resolution, index + resolution + 1 };
            List<Vector3> validNeighbours = new List<Vector3>();

            for (int i = 0; i < possibleNeighbours.Length; i++) {
               if(possibleNeighbours[i] > 0 && possibleNeighbours[i] < vertices.Length)
                {
                    validNeighbours.Add(vertices[possibleNeighbours[i]]);
                }
            }
            return validNeighbours.ToArray();
        }

        
        // for some reason you need to move the sliders (esp. octaves) a couple times before it shows different results
        private float islandFilter(Vector2 coords, float elevation){
            var halfSize = resolution / 2;
            var cx = (coords.x*(resolution-1)) - halfSize;
            var cy = (coords.y*(resolution-1)) - halfSize;
            var r = Mathf.Sqrt(cx * cx + cy * cy) / (resolution / 2);
            
            //Fall off to zero the last quarter of the radius.
            var p = (1 - r) * 3;
            if (p < 0 && p > -0.2)
                return 0;
            else if (p < -0.2)
                return 0;
            else if (p >= 1)
                return elevation;
            else
                return p * elevation;
        }



        private static float FractalNoise(Vector2 coords, float gain, float lacunarity, int octaves, float scale, Vector2 shift, int state)
        { 
            float noise = 0.0f;
            float amplitude = 1.0f;
            float frequency = 1.0f;
            for (int i = 0; i < octaves; i++) {
                float x = scale * frequency * coords.x + state + shift.x;
                float y = scale * frequency * coords.y + state + shift.y;
                noise += amplitude * Mathf.PerlinNoise(x, y) * 2f - 1f;
                amplitude *= gain;
                frequency *= lacunarity;
            }
            return Mathf.Max(noise, 0); //can't be negative!
        }
    }
}