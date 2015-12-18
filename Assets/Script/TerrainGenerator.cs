using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
using System.Collections;
using System.Collections.Generic;
using System.IO;

namespace Automata
{
    public class TerrainGenerator : MonoBehaviour
    {
        public enum TerrainTextureSprite
        {
            GRASS_TOP,
            GRASS_SIDE,
            DIRT_TOP,
            DIRT_SIDE,
            WATER_SHALLOW,
            WATER_DEEP
        }

        [System.Serializable]
        public class TerrainTextureTile
        {
            public TerrainTextureSprite sprite;
            public Vector2 uvTilePos;
        }

        public int width = 192;
        public int height = 192;
        public float seed = 0;
        public float xOrg = 2;
        public float yOrg = 2;
        public float scale = 1.0f;
        public float heightScale = 10.0f;
        public float groundHeight = 3;
        public Material material;
        public int textureTile = 16;
        public TerrainTextureTile[] textureTilePos;

        private List<int> _widths;
        private List<int> _heights;
        private List<List<Vector3>> _newVertices;
        private List<List<Vector2>> _newUV;
        private List<List<int>> _newIndices;
        private List<List<float>> _blocks_region;

        private List<Mesh> _meshes;
        private List<GameObject> _regionGOs;
        private Vector2 _textureSize;

        private int _step = 48;

        // Use this for initialization
        void Start()
        {
            _textureSize = new Vector2(
                material.mainTexture.width,
                material.mainTexture.height);

            StartCoroutine(Generate());
        }

        public void GenerateTerrain()
        {
            StartCoroutine(Generate());
        }


        private IEnumerator Generate()
        {
            EditorUtility.DisplayProgressBar("Terrain Generator", "Init Blocks", 0 / 5);
            InitBlocks();
            yield return null;
            EditorUtility.DisplayProgressBar("Terrain Generator", "Init Meshes", 1 / 5);
            InitMeshes();
            yield return null;
            EditorUtility.DisplayProgressBar("Terrain Generator", "Generate Noise", 2 / 5);
            GenerateNoise();
            yield return null;
            EditorUtility.DisplayProgressBar("Terrain Generator", "Generate Mesh With Water", 3 / 5);
            GenerateMeshWithWater();
            yield return null;
            EditorUtility.DisplayProgressBar("Terrain Generator", "Update Mesh", 4 / 5);
            UpdateMesh();
            yield return null;
            EditorUtility.ClearProgressBar();
        }

        private void InitBlocks()
        {
            if (_widths == null)
                _widths = new List<int>();
            if (_heights == null)
                _heights = new List<int>();
            _widths.Clear();
            _heights.Clear();

            int w = 0;
            while (w < width - _step)
            {
                w += _step;
                _widths.Add(w);
            }
            _widths.Add(width);

            int h = 0;
            while (h < height - _step)
            {
                h += _step;
                _heights.Add(h);
            }
            _heights.Add(height);

            int regions = _widths.Count * _heights.Count;
            if (_newVertices == null) _newVertices = new List<List<Vector3>>();
            if (_newUV == null) _newUV = new List<List<Vector2>>();
            if (_newIndices == null) _newIndices = new List<List<int>>();
            if (_blocks_region == null) _blocks_region = new List<List<float>>();

            _newVertices.Clear();
            _newUV.Clear();
            _newIndices.Clear();
            _blocks_region.Clear();

            for (int i = 0; i < regions; ++i)
            {
                _newVertices.Add(new List<Vector3>());
                _newUV.Add(new List<Vector2>());
                _newIndices.Add(new List<int>());
                _blocks_region.Add(new List<float>());
            }

            // Initialize blocks region
            int x_step, y_step;
            x_step = y_step = 0;
            for (int yr = 0; yr < _heights.Count; ++yr)
            {
                x_step = 0;
                for (int xr = 0; xr < _widths.Count; ++xr)
                {
                    var blocks = _blocks_region[xr + (yr * _widths.Count)];
                    for (int y = y_step; y < _heights[yr]; ++y)
                    {
                        for (int x = x_step; x < _widths[xr]; ++x)
                        {
                            blocks.Add(0);
                        }
                    }
                    x_step = _widths[xr];
                }
                y_step = _heights[yr];
            }
        }

        private void InitMeshes()
        {
            int regions = _widths.Count * _heights.Count;
            if (_meshes == null) _meshes = new List<Mesh>();
            else
                if (_meshes.Count > 0)
                {
                    foreach (var mesh in _meshes)
                    {
                        Destroy(mesh);
                    }
                }

            if (_regionGOs == null) _regionGOs = new List<GameObject>();
            else
                if (_regionGOs.Count > 0)
                {
                    foreach (var go in _regionGOs)
                    {
                        Destroy(go);
                    }
                }

            for (int i = 0; i < regions; ++i)
            {
                _meshes.Add(new Mesh());
                var gObj = new GameObject("region");
                gObj.AddComponent<MeshFilter>().mesh = _meshes[i];
                gObj.AddComponent<MeshRenderer>().material = material;
                gObj.transform.parent = gameObject.transform;
                _regionGOs.Add(gObj);
            }
        }

        private void SetBlockValue(int x, int y, float value)
        {
            int xr, yr;
            int x_blocks, y_blocks;
            int width_block = _widths[0];
            x_blocks = y_blocks = 0;
            for (xr = 0; xr < _widths.Count; ++xr)
            {
                if (xr == 0)
                {
                    if (x < _widths[xr])
                    {
                        x_blocks = x;
                        width_block = _widths[xr];
                        break;
                    }
                }
                else
                {
                    if (x < _widths[xr] && x >= _widths[xr - 1])
                    {
                        x_blocks = x - _widths[xr - 1];
                        width_block = _widths[xr] - _widths[xr - 1];
                        break;
                    }
                }
            }
            for (yr = 0; yr < _heights.Count; ++yr)
            {
                if (yr == 0)
                {
                    if (y < _heights[yr])
                    {
                        y_blocks = y;
                        break;
                    }
                }
                else
                {
                    if (y < _heights[yr] && y >= _heights[yr - 1])
                    {
                        y_blocks = y - _heights[yr - 1];
                        break;
                    }
                }
            }

            _blocks_region[xr + (yr * _widths.Count)][x_blocks + (y_blocks * width_block)] = value;
        }

        private float GetBlockValue(int x, int y)
        {
            int xr, yr;
            int x_blocks, y_blocks;
            int width_block = _widths[0];
            x_blocks = y_blocks = 0;
            for (xr = 0; xr < _widths.Count; ++xr)
            {
                if (xr == 0)
                {
                    if (x < _widths[xr])
                    {
                        x_blocks = x;
                        width_block = _widths[xr];
                        break;
                    }
                }
                else
                {
                    if (x < _widths[xr] && x >= _widths[xr - 1])
                    {
                        x_blocks = x - _widths[xr - 1];
                        width_block = _widths[xr] - _widths[xr - 1];
                        break;
                    }
                }
            }
            for (yr = 0; yr < _heights.Count; ++yr)
            {
                if (yr == 0)
                {
                    if (y < _heights[yr])
                    {
                        y_blocks = y;
                        break;
                    }
                }
                else
                {
                    if (y < _heights[yr] && y >= _heights[yr - 1])
                    {
                        y_blocks = y - _heights[yr - 1];
                        break;
                    }
                }
            }

            return _blocks_region[xr + (yr * _widths.Count)][x_blocks + (y_blocks * width_block)];
        }

        private void GenerateNoise()
        {
            for (int y = 0; y < height; ++y)
            {
                for (int x = 0; x < width; ++x)
                {
                    float xCoord = seed + xOrg + ((float)x / width * scale);
                    float yCoord = seed + yOrg + ((float)y / height * scale);
                    float sample = Mathf.PerlinNoise(xCoord, yCoord);
                    float value = Mathf.Floor(sample * heightScale);
                    SetBlockValue(x, y, value);
                }
            }
        }

        private Vector2 GetTextureTilePos(TerrainTextureSprite sprite)
        {
            foreach (var tile in textureTilePos)
            {
                if (tile.sprite == sprite)
                {
                    return tile.uvTilePos;
                }
            }

            return Vector2.zero;
        }

        private void GenerateMesh()
        {
            int x_step, y_step;
            x_step = y_step = 0;
            for (int yr = 0; yr < _heights.Count; ++yr)
            {
                x_step = 0;
                for (int xr = 0; xr < _widths.Count; ++xr)
                {
                    int region = xr + (yr * _widths.Count);
                    _newVertices[region].Clear();
                    _newUV[region].Clear();
                    _newIndices[region].Clear();
                    int lastCount = 0;
                    for (int y = y_step; y < _heights[yr]; ++y)
                    {
                        for (int x = x_step; x < _widths[xr]; ++x)
                        {
                            int x_block = x - x_step;
                            int y_block = y - y_step;
                            int width_block = _widths[xr] - (xr > 0 ? _widths[xr - 1] : 0);
                            var b = _blocks_region[region][x_block + (y_block * width_block)];
                            TerrainTextureSprite textureSprite = TerrainTextureSprite.GRASS_TOP;
                            if (b < groundHeight)
                            {
                                textureSprite = TerrainTextureSprite.DIRT_TOP;
                            }
                            AddPlaneTop(region, x, b, y, GetTextureTilePos(textureSprite), ref lastCount);

                            textureSprite = TerrainTextureSprite.GRASS_SIDE;
                            if (b < groundHeight)
                                textureSprite = TerrainTextureSprite.DIRT_SIDE;

                            // Check front
                            if ((y - 1) < 0)
                            {
                                AddPlaneFront(region, x, b, y, GetTextureTilePos(textureSprite), ref lastCount);
                            }
                            else if (GetBlockValue(x, y - 1) < b)
                            {
                                float val = GetBlockValue(x, y - 1);
                                float diff = b - val;
                                float b_down = b;
                                for (int j = 0; j < diff; ++j)
                                {
                                    AddPlaneFront(region, x, b_down, y, GetTextureTilePos(textureSprite), ref lastCount);
                                    b_down -= 1;
                                }
                            }

                            // Check back
                            if ((y + 1) >= height)
                            {
                                AddPlaneBack(region, x, b, y, GetTextureTilePos(textureSprite), ref lastCount);
                            }
                            else if (GetBlockValue(x, y + 1) < b)
                            {
                                float val = GetBlockValue(x, y + 1);
                                float diff = b - val;
                                float b_down = b;
                                for (int j = 0; j < diff; ++j)
                                {
                                    AddPlaneBack(region, x, b_down, y, GetTextureTilePos(textureSprite), ref lastCount);
                                    b_down -= 1;
                                }
                            }

                            // Check Left
                            if ((x - 1) < 0)
                            {
                                AddPlaneLeft(region, x, b, y, GetTextureTilePos(textureSprite), ref lastCount);
                            }
                            else if (GetBlockValue(x - 1, y) < b)
                            {
                                float val = GetBlockValue(x - 1, y);
                                float diff = b - val;
                                float b_down = b;
                                for (int j = 0; j < diff; ++j)
                                {
                                    AddPlaneLeft(region, x, b_down, y, GetTextureTilePos(textureSprite), ref lastCount);
                                    b_down -= 1;
                                }
                            }

                            // Check Right
                            if ((x + 1) >= width)
                            {
                                AddPlaneRight(region, x, b, y, GetTextureTilePos(textureSprite), ref lastCount);
                            }
                            else if (GetBlockValue(x + 1, y) < b)
                            {
                                float val = GetBlockValue(x + 1, y);
                                float diff = b - val;
                                float b_down = b;
                                for (int j = 0; j < diff; ++j)
                                {
                                    AddPlaneRight(region, x, b_down, y, GetTextureTilePos(textureSprite), ref lastCount);
                                    b_down -= 1;
                                }
                            }
                        }
                    }
                    x_step = _widths[xr];
                }
                y_step = _heights[yr];
            }
        }

        private void GenerateMeshWithWater()
        {
            int x_step, y_step;
            x_step = y_step = 0;
            for (int yr = 0; yr < _heights.Count; ++yr)
            {
                x_step = 0;
                for (int xr = 0; xr < _widths.Count; ++xr)
                {
                    int region = xr + (yr * _widths.Count);
                    _newVertices[region].Clear();
                    _newUV[region].Clear();
                    _newIndices[region].Clear();
                    int lastCount = 0;
                    for (int y = y_step; y < _heights[yr]; ++y)
                    {
                        for (int x = x_step; x < _widths[xr]; ++x)
                        {
                            int x_block = x - x_step;
                            int y_block = y - y_step;
                            int width_block = _widths[xr] - (xr > 0 ? _widths[xr - 1] : 0);
                            var b = _blocks_region[region][x_block + (y_block * width_block)];
                            TerrainTextureSprite textureSprite = TerrainTextureSprite.GRASS_TOP;
                            float h = b;
                            if (b < groundHeight)
                            {
                                if (groundHeight - b == 1)
                                    textureSprite = TerrainTextureSprite.WATER_SHALLOW;
                                else
                                    textureSprite = TerrainTextureSprite.WATER_DEEP;
                                h = groundHeight - 1;
                            }
                            AddPlaneTop(region, x, h, y, GetTextureTilePos(textureSprite), ref lastCount);

                            textureSprite = TerrainTextureSprite.GRASS_SIDE;
                            if (b < groundHeight)
                            {
                                continue;
                            }

                            // Check front
                            if ((y - 1) < 0)
                            {
                                AddPlaneFront(region, x, b, y, GetTextureTilePos(textureSprite), ref lastCount);
                            }
                            else if (GetBlockValue(x, y - 1) < b)
                            {
                                float val = GetBlockValue(x, y - 1);
                                float diff = b - val;
                                float b_down = b;
                                for (int j = 0; j < diff; ++j)
                                {
                                    AddPlaneFront(region, x, b_down, y, GetTextureTilePos(textureSprite), ref lastCount);
                                    b_down -= 1;
                                }
                            }

                            // Check back
                            if ((y + 1) >= height)
                            {
                                AddPlaneBack(region, x, b, y, GetTextureTilePos(textureSprite), ref lastCount);
                            }
                            else if (GetBlockValue(x, y + 1) < b)
                            {
                                float val = GetBlockValue(x, y + 1);
                                float diff = b - val;
                                float b_down = b;
                                for (int j = 0; j < diff; ++j)
                                {
                                    AddPlaneBack(region, x, b_down, y, GetTextureTilePos(textureSprite), ref lastCount);
                                    b_down -= 1;
                                }
                            }

                            // Check Left
                            if ((x - 1) < 0)
                            {
                                AddPlaneLeft(region, x, b, y, GetTextureTilePos(textureSprite), ref lastCount);
                            }
                            else if (GetBlockValue(x - 1, y) < b)
                            {
                                float val = GetBlockValue(x - 1, y);
                                float diff = b - val;
                                float b_down = b;
                                for (int j = 0; j < diff; ++j)
                                {
                                    AddPlaneLeft(region, x, b_down, y, GetTextureTilePos(textureSprite), ref lastCount);
                                    b_down -= 1;
                                }
                            }

                            // Check Right
                            if ((x + 1) >= width)
                            {
                                AddPlaneRight(region, x, b, y, GetTextureTilePos(textureSprite), ref lastCount);
                            }
                            else if (GetBlockValue(x + 1, y) < b)
                            {
                                float val = GetBlockValue(x + 1, y);
                                float diff = b - val;
                                float b_down = b;
                                for (int j = 0; j < diff; ++j)
                                {
                                    AddPlaneRight(region, x, b_down, y, GetTextureTilePos(textureSprite), ref lastCount);
                                    b_down -= 1;
                                }
                            }
                        }
                    }
                    x_step = _widths[xr];
                }
                y_step = _heights[yr];
            }
        }

        private void UpdateMesh()
        {
            int regions = _widths.Count * _heights.Count;
            for (int i = 0; i < regions; ++i)
            {
                _meshes[i].Clear();
                _meshes[i].vertices = _newVertices[i].ToArray();
                _meshes[i].uv = _newUV[i].ToArray();
                _meshes[i].triangles = _newIndices[i].ToArray();
                _meshes[i].RecalculateNormals();

                MeshCollider meshColl;
                if ((meshColl = _regionGOs[i].GetComponent<MeshCollider>()) != null)
                    Destroy(meshColl);
                _regionGOs[i].AddComponent<MeshCollider>();
            }
        }

        private void AddPlaneTop(int region, float x, float y, float z, Vector2 uvTilePos, ref int lastCount)
        {
            if (region >= _newVertices.Count)
                Debug.LogError("Index out of range");

            _newVertices[region].Add(new Vector3(x, y, z + 1.0f));
            _newVertices[region].Add(new Vector3(x + 1.0f, y, z + 1.0f));
            _newVertices[region].Add(new Vector3(x + 1.0f, y, z));
            _newVertices[region].Add(new Vector3(x, y, z));

            _newUV[region].Add(new Vector2((uvTilePos.x - textureTile) / _textureSize.x, uvTilePos.y / _textureSize.y));
            _newUV[region].Add(new Vector2(uvTilePos.x / _textureSize.x, uvTilePos.y / _textureSize.y));
            _newUV[region].Add(new Vector2(uvTilePos.x / _textureSize.x, (uvTilePos.y - textureTile) / _textureSize.y));
            _newUV[region].Add(new Vector2((uvTilePos.x - textureTile) / _textureSize.x, (uvTilePos.y - textureTile) / _textureSize.y));

            _newIndices[region].Add(lastCount);
            _newIndices[region].Add(lastCount + 1);
            _newIndices[region].Add(lastCount + 3);
            _newIndices[region].Add(lastCount + 3);
            _newIndices[region].Add(lastCount + 1);
            _newIndices[region].Add(lastCount + 2);

            lastCount += 4;
        }

        private void AddPlaneLeft(int region, float x, float y, float z, Vector2 uvTilePos, ref int lastCount)
        {
            if (region >= _newVertices.Count)
                Debug.LogError("Index out of range");

            _newVertices[region].Add(new Vector3(x, y, z + 1.0f));
            _newVertices[region].Add(new Vector3(x, y, z));
            _newVertices[region].Add(new Vector3(x, y - 1.0f, z));
            _newVertices[region].Add(new Vector3(x, y - 1.0f, z + 1.0f));

            _newUV[region].Add(new Vector2((uvTilePos.x - textureTile) / _textureSize.x, uvTilePos.y / _textureSize.y));
            _newUV[region].Add(new Vector2(uvTilePos.x / _textureSize.x, uvTilePos.y / _textureSize.y));
            _newUV[region].Add(new Vector2(uvTilePos.x / _textureSize.x, (uvTilePos.y - textureTile) / _textureSize.y));
            _newUV[region].Add(new Vector2((uvTilePos.x - textureTile) / _textureSize.x, (uvTilePos.y - textureTile) / _textureSize.y));

            _newIndices[region].Add(lastCount);
            _newIndices[region].Add(lastCount + 1);
            _newIndices[region].Add(lastCount + 3);
            _newIndices[region].Add(lastCount + 3);
            _newIndices[region].Add(lastCount + 1);
            _newIndices[region].Add(lastCount + 2);

            lastCount += 4;
        }

        private void AddPlaneRight(int region, float x, float y, float z, Vector2 uvTilePos, ref int lastCount)
        {
            if (region >= _newVertices.Count)
                Debug.LogError("Index out of range");

            _newVertices[region].Add(new Vector3(x + 1.0f, y, z));
            _newVertices[region].Add(new Vector3(x + 1.0f, y, z + 1.0f));
            _newVertices[region].Add(new Vector3(x + 1.0f, y - 1.0f, z + 1.0f));
            _newVertices[region].Add(new Vector3(x + 1.0f, y - 1.0f, z));

            _newUV[region].Add(new Vector2((uvTilePos.x - textureTile) / _textureSize.x, uvTilePos.y / _textureSize.y));
            _newUV[region].Add(new Vector2(uvTilePos.x / _textureSize.x, uvTilePos.y / _textureSize.y));
            _newUV[region].Add(new Vector2(uvTilePos.x / _textureSize.x, (uvTilePos.y - textureTile) / _textureSize.y));
            _newUV[region].Add(new Vector2((uvTilePos.x - textureTile) / _textureSize.x, (uvTilePos.y - textureTile) / _textureSize.y));

            _newIndices[region].Add(lastCount);
            _newIndices[region].Add(lastCount + 1);
            _newIndices[region].Add(lastCount + 3);
            _newIndices[region].Add(lastCount + 3);
            _newIndices[region].Add(lastCount + 1);
            _newIndices[region].Add(lastCount + 2);

            lastCount += 4;
        }

        private void AddPlaneFront(int region, float x, float y, float z, Vector2 uvTilePos, ref int lastCount)
        {
            if (region >= _newVertices.Count)
                Debug.LogError("Index out of range");

            _newVertices[region].Add(new Vector3(x, y, z));
            _newVertices[region].Add(new Vector3(x + 1.0f, y, z));
            _newVertices[region].Add(new Vector3(x + 1.0f, y - 1.0f, z));
            _newVertices[region].Add(new Vector3(x, y - 1.0f, z));

            _newUV[region].Add(new Vector2((uvTilePos.x - textureTile) / _textureSize.x, uvTilePos.y / _textureSize.y));
            _newUV[region].Add(new Vector2(uvTilePos.x / _textureSize.x, uvTilePos.y / _textureSize.y));
            _newUV[region].Add(new Vector2(uvTilePos.x / _textureSize.x, (uvTilePos.y - textureTile) / _textureSize.y));
            _newUV[region].Add(new Vector2((uvTilePos.x - textureTile) / _textureSize.x, (uvTilePos.y - textureTile) / _textureSize.y));

            _newIndices[region].Add(lastCount);
            _newIndices[region].Add(lastCount + 1);
            _newIndices[region].Add(lastCount + 3);
            _newIndices[region].Add(lastCount + 3);
            _newIndices[region].Add(lastCount + 1);
            _newIndices[region].Add(lastCount + 2);

            lastCount += 4;
        }

        private void AddPlaneBack(int region, float x, float y, float z, Vector2 uvTilePos, ref int lastCount)
        {
            if (region >= _newVertices.Count)
                Debug.LogError("Index out of range");

            _newVertices[region].Add(new Vector3(x + 1.0f, y, z + 1.0f));
            _newVertices[region].Add(new Vector3(x, y, z + 1.0f));
            _newVertices[region].Add(new Vector3(x, y - 1.0f, z + 1.0f));
            _newVertices[region].Add(new Vector3(x + 1.0f, y - 1.0f, z + 1.0f));

            _newUV[region].Add(new Vector2((uvTilePos.x - textureTile) / _textureSize.x, uvTilePos.y / _textureSize.y));
            _newUV[region].Add(new Vector2(uvTilePos.x / _textureSize.x, uvTilePos.y / _textureSize.y));
            _newUV[region].Add(new Vector2(uvTilePos.x / _textureSize.x, (uvTilePos.y - textureTile) / _textureSize.y));
            _newUV[region].Add(new Vector2((uvTilePos.x - textureTile) / _textureSize.x, (uvTilePos.y - textureTile) / _textureSize.y));

            _newIndices[region].Add(lastCount);
            _newIndices[region].Add(lastCount + 1);
            _newIndices[region].Add(lastCount + 3);
            _newIndices[region].Add(lastCount + 3);
            _newIndices[region].Add(lastCount + 1);
            _newIndices[region].Add(lastCount + 2);

            lastCount += 4;
        }
    }
}
