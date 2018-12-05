using UnityEngine;
using System.Linq;

public class TerrainGenerator : MonoBehaviour {

    // Guide Image and Terrain Size
    public Texture2D goalTexture;
    public int edgeSize = 128;

    // All Terrain properties
    [Range(1, 100)]
    public float heightScale = 50;
    [Range(0.01f, 1.0f)]
    public float mixFraction = 0.7f;
    [Range(1, 6)]
    public int octaves = 3;
    [Range(0.1f, 1.0f)]
    public float persistence = 0.5f;
    [Range(1.0f, 100f)]
    public float noiseXspan = 5f;
    [Range(1.0f, 100f)]
    public float noiseYspan = 5f;

    private float noiseXstart = 0f;
    private float noiseYstart = 0f;

    private Terrain terrain;
    private float[,] heightField;

    private float[,,] splatmapData;

    // Road Properties
    [Range(4, 32)]
    public float roadWidth = 24f;
    [Range(-256, 256)]
    public float roadOffset = 0f;

    void Start () {
        terrain = GetComponent<Terrain>();
        heightField = new float[edgeSize, edgeSize];
        terrain.terrainData = GenerateTerrainGuidanceTexture(terrain.terrainData, goalTexture, mixFraction);
        AssignSplatMap(terrain.terrainData);
    }
	
	void Update () {
        //terrain.terrainData = GenerateTerrainGuidanceTexture(terrain.terrainData, goalTexture, mixFraction);
        //AssignSplatMap(terrain.terrainData);
    }

    private TerrainData GenerateTerrainGuidanceTexture(TerrainData terrainData, Texture2D guideTexture, float mixFraction)
    {
        terrainData.size = new Vector3(edgeSize, heightScale, edgeSize);
        terrainData.heightmapResolution = edgeSize;

        GenerateHeightGuidanceTexture(guideTexture, mixFraction);
        terrainData.SetHeights(0, 0, heightField);

        return terrainData;
    }

    private void GenerateHeightGuidanceTexture(Texture2D guideTexture, float mixFraciton)
    {
        for (int y = 0; y < edgeSize; y++)
        {
            for (int x = 0; x < edgeSize; x++)
            {
                heightField[y, x] = CalculateHeightGuidanceTexture(guideTexture, y, x, mixFraction);
            }
        }
    }

    private float CalculateHeightGuidanceTexture(Texture2D guideTexture, int y, int x, float mixFraction)
    {
        float xfrac = (float)x / (float)edgeSize;
        float yfrac = (float)y / (float)edgeSize;

        float greyScaleValue = guideTexture.GetPixelBilinear(xfrac, yfrac).grayscale;

        float noiseValue = CalculateHeightOctaves(y, x);

        return (greyScaleValue * mixFraction) + noiseValue * (1 - mixFraction);
    }

    private float CalculateHeightOctaves(int y, int x)
    {
        float noiseValue = 0.0f;
        float frequency = 1.0f;
        float amplitude = 1.0f;
        float maxValue = 1.0f;

        for (int i = 0; i < octaves; i++)
        {
            float perlinX = noiseXstart + ((float)x / (float)edgeSize) * noiseXspan * frequency;
            float perlinY = noiseYstart + ((float)y / (float)edgeSize) * noiseYspan * frequency;
            noiseValue += Mathf.PerlinNoise(perlinX * frequency, perlinY * frequency) * amplitude;

            maxValue += amplitude;
            amplitude *= persistence;
            frequency *= 2;
        }
        return noiseValue / maxValue;
    }

    private void AssignSplatMap(TerrainData terrainData)
    {
        splatmapData = new float[terrainData.alphamapWidth, terrainData.alphamapHeight, terrainData.alphamapLayers];
        float[] splatWeights = new float[terrainData.alphamapLayers];
        //Vector3 normal;

        Debug.Log("Layers: " + terrainData.alphamapLayers);
        Debug.Log("Height: " + terrainData.alphamapHeight);
        Debug.Log("Width: " + terrainData.alphamapWidth);

        for (int y = 0; y < terrainData.alphamapHeight; y++)
        {
            for (int x = 0; x < terrainData.alphamapWidth; x++)
            {
                float normX = x * 1.0f / (terrainData.alphamapWidth - 1);
                float normY = y * 1.0f / (terrainData.alphamapHeight - 1);

                //float height = terrainData.GetHeight(Mathf.RoundToInt(normY * terrainData.heightmapHeight), Mathf.RoundToInt(normX * terrainData.heightmapWidth));

                //normal = terrainData.GetInterpolatedNormal(normY, normX);

                // Add textures based on steepness (ground / cliff)
                var angle = terrainData.GetSteepness(normY, normX);
                var frac = angle / 90.0;

                if (frac < 0.75)
                {
                    splatWeights[0] = 1;
                    splatWeights[1] = 0;
                }
                else
                {
                    splatWeights[0] = 0;
                    splatWeights[1] = 1;
                }

                // Create road texture that runs down the middle plus some offset
                if (y >= ((terrainData.alphamapHeight / 2) - (roadWidth / 2) + roadOffset) && 
                    y <= ((terrainData.alphamapHeight / 2) + (roadWidth / 2) + roadOffset))
                {
                    splatWeights[0] = 0;
                    splatWeights[1] = 0;
                    splatWeights[2] = 1;
                }
                else
                {
                    splatWeights[2] = 0;
                }

                // Normalize splatweights and add them to the splatmapData with range(0 - 1)
                float z = splatWeights.Sum();

                for (int i = 0; i < splatWeights.Length; i++)
                {
                    splatWeights[i] /= z;
                    splatmapData[x, y, i] = splatWeights[i];
                }
            }
        }
        terrainData.SetAlphamaps(0, 0, splatmapData);
    }
}
