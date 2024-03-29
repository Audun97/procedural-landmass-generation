﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Threading;

//This class should define the public values
//this class should fetch  the noisemapfrom class Noise

public class MapGenerator : MonoBehaviour
{
    public enum DrawMode {NoiseMap, ColourMap, Mesh};
    public DrawMode drawMode;
    public Noise.NormalizeMode normalizeMode;
    public const int mapChunkSize = 241;
    [Range (0,6)]
    public int editorPreviewLOD;

    public float noiseScale;

    public bool autoUpdate;

    public int octaves;
    [Range(0, 1)]
    public float persistence;
    public float lacunarity;

    public int seed;

    public float heightMultiplier;
    public AnimationCurve meshHeightCurve;
    public Vector2 offset;

    public TerrainType[] regions;

    Queue <MapThreadInfo <MapData>> mapDataThreadInfoQueue = new Queue<MapThreadInfo<MapData>>();
    Queue<MapThreadInfo<MeshData>> meshDataThreadInfoQueue = new Queue<MapThreadInfo<MeshData>>();


    public void RequestMapData (Vector2 centre, Action <MapData> callback)
    {
        ThreadStart threadStart = delegate {MapDataThread(centre, callback);};
        new Thread(threadStart).Start();
    }

    void MapDataThread (Vector2 centre, Action<MapData> callback)
    {
        MapData mapData = GenerateMapData(centre);
        //we just want onw thread accessing it at a time
        lock (mapDataThreadInfoQueue)
        {
            //add data to queue
            mapDataThreadInfoQueue.Enqueue(new MapThreadInfo<MapData>(callback, mapData));
        }
       
    }

    public void RequestMeshData(MapData mapData, int lod, Action<MeshData> callback)
    {
        ThreadStart threadStart = delegate { MeshDataThread(mapData, lod, callback); };
        new Thread(threadStart).Start();
    }

    void MeshDataThread(MapData mapData, int lod, Action<MeshData> callback)
    {
        MeshData meshData = MeshGenerator.GenerateTerrainMesh(mapData.heightMap, heightMultiplier, meshHeightCurve, lod);
        lock (mapDataThreadInfoQueue)
        {
            //add data to queue
            meshDataThreadInfoQueue.Enqueue(new MapThreadInfo<MeshData>(callback, meshData));
        }

    }

    void Update()
    {
        if (mapDataThreadInfoQueue.Count > 0) 
        {
            for(int i = 0; i < mapDataThreadInfoQueue.Count; i++)
            {
                MapThreadInfo<MapData> threadInfo = mapDataThreadInfoQueue.Dequeue();
                //?????
                threadInfo.callback(threadInfo.parameter);
            }
        }

        if (meshDataThreadInfoQueue.Count > 0)
        {
            for (int i = 0; i < meshDataThreadInfoQueue.Count; i++)
            {
                MapThreadInfo<MeshData> threadInfo = meshDataThreadInfoQueue.Dequeue();
                //?????
                threadInfo.callback(threadInfo.parameter);
            }
        }
    }

    public void DrawMapInEditor()
    {
        MapDisplay display = FindObjectOfType<MapDisplay>();

        MapData mapData = GenerateMapData(Vector2.zero);

        if (drawMode == DrawMode.NoiseMap)
        {
            display.DrawTexture(TextureGenerator.TextureFromNoisemap(mapData.heightMap));
        }
        else if (drawMode == DrawMode.ColourMap)
        {
            display.DrawTexture(TextureGenerator.TextureFromColourmap(mapData.colourMap, mapChunkSize, mapChunkSize));
        }
        else if (drawMode == DrawMode.Mesh)
        {
            display.DrawMesh(MeshGenerator.GenerateTerrainMesh(mapData.heightMap, heightMultiplier, meshHeightCurve, editorPreviewLOD), TextureGenerator.TextureFromColourmap(mapData.colourMap, mapChunkSize, mapChunkSize));
        }
        
    }

    MapData GenerateMapData(Vector2 centre)
    {
        float[,] heightMap = Noise.GenerateNoiseMap(mapChunkSize, mapChunkSize, seed, noiseScale, octaves, lacunarity, persistence, centre + offset, normalizeMode);

        // we wanna assign colors to specific altitude values
        Color[] colourMap = new Color[mapChunkSize * mapChunkSize];

        for (int x = 0; x < mapChunkSize; x++)
        {
            for (int y = 0; y < mapChunkSize; y++)
            {
                // in gobal normalized mode we can not assume that currentAltitude is between 0 and 1
                float currentAltitude = heightMap[x, y];

                // set each altitude per pixel to coulour defined in struct
                for (int i = 0; i < regions.Length; i++)
                {
                    if (currentAltitude >= regions[i].altitude)
                    {
                        colourMap[y * mapChunkSize + x] = regions[i].colour;
                    }
                    else
                    {
                        //break only when value currentAltitude is smaller than a defined regions altitude
                        break;
                    }
                }
            }
        }
        return new MapData(heightMap, colourMap);
    }


    void OnValidate()
    {
        if (octaves < 0)
        {
            octaves = 0;
        }
        if (lacunarity < 1)
        {
            lacunarity = 1;
        }
    }

    struct MapThreadInfo <T>
    {
        public readonly Action<T> callback;
        public readonly T parameter;

        public MapThreadInfo (Action <T> callback, T parameter)
        {
            this.callback = callback;
            this.parameter = parameter;
        }
    }
}
//So that it will show up in the inspector
[System.Serializable]
public struct TerrainType
{
    public string name;
    public float altitude;
    public Color colour;


}
public struct MapData
{
    public readonly float[,] heightMap;
    public readonly Color[] colourMap;

    public MapData(float[,] heightMap, Color[] colourMap)
    {
        this.heightMap = heightMap;
        this.colourMap = colourMap;
    }
}
