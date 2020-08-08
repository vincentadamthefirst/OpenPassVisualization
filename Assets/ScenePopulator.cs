﻿using UnityEngine;
using Visualization.OcclusionManagement;
using Debug = System.Diagnostics.Debug;

public class ScenePopulator : MonoBehaviour {
    
    
    [Header("Basic Object information")]
    public GameObject randomObjectPrefab;
    public int randomObjectCount = 20;
    public bool randomRotation = true;
    
    [Header("Area of spawn information")]
    public int areaWidth = 15;
    public int areaLength = 15;
    public int areaHeight = 15;

    private void Start() {
        var count = 0;
        Debug.Assert(Camera.main != null, "Camera.main != null");
        var cameraScript = Camera.main.GetComponent<AgentOcclusionManager>();

        while (count < randomObjectCount - 1) {
            var newRandom = Instantiate(randomObjectPrefab);
            newRandom.name = "Object " + count;
            newRandom.transform.position = new Vector3(Random.Range(-areaWidth / 2, areaWidth / 2), Random.Range(0, areaHeight), Random.Range(-areaLength / 2, areaLength / 2));
            newRandom.GetComponent<MeshRenderer>().material.renderQueue = 5000;
            if (randomRotation) newRandom.transform.rotation = Random.rotation;

            count++;
        }

        return;
        for (var i = 0; i < 10; i++) {
            var newCube = Instantiate(randomObjectPrefab);
            newCube.transform.position = new Vector3(3f - (float) i * 1.5f, 1, -30f);
            newCube.GetComponent<MeshRenderer>().material.renderQueue = 5000;
        }
    }
}
