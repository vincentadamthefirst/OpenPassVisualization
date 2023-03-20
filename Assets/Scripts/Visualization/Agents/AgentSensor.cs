﻿using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using UnityEngine;
using Debug = System.Diagnostics.Debug;

namespace Visualization.Agents {

    /**
     * Setup information for a sensor.
     */
    public struct SensorSetup {
        // name of this sensor
        public string sensorName;
        // color of this sensor
        public Color color;
    }

    /// <summary>
    /// Class representing a sensor in the scene. Has a child object containing the actual mesh representing the sensor
    /// view.
    /// </summary>
    public class AgentSensor : MonoBehaviour {
        // information on this sensors mesh 
        private Transform _child;
        private MeshFilter _meshFilter;
        private MeshRenderer _meshRenderer;

        private bool _active = false;
        private bool _on = true;

        public SensorSetup SensorSetup { get; set; }

        private void Awake() {
            _child = transform.GetChild(0);
            _meshFilter = _child.GetChild(0).GetComponent<MeshFilter>();
            _meshRenderer = _child.GetChild(0).GetComponent<MeshRenderer>();
        }

        public void Initialize() {
            
        }

        /// <summary>
        /// Sets the material for the sensor mesh of this sensor.
        /// </summary>
        /// <param name="meshMaterial">the new material for the sensor mesh</param>
        public void SetMeshMaterial(Material meshMaterial) {
            _meshRenderer.material = meshMaterial;
        }

        /// <summary>
        /// Updates the position and rotation of this sensor.
        /// </summary>
        /// <param name="agentPosition">the global position of the agent this sensor is attached to</param>
        /// <param name="localPosition">the local position of this sensor inside the agent</param>
        /// <param name="globalRotation">the global rotation of this sensors view frustum</param>
        private void UpdatePositionAndRotation(Vector3 agentPosition, Vector3 localPosition, float globalRotation) {
            _child.position = agentPosition;
            _child.rotation = Quaternion.Euler(0, (-globalRotation) * Mathf.Rad2Deg, 0);
        }

        /// <summary>
        /// Generates a new mesh based on a new opening angle and viewing distance.
        /// </summary>
        /// <param name="angleRadians">The new opening angle in radians</param>
        /// <param name="distance">The new sensor viewing distance in units (meters)</param>
        private void UpdateOpeningAngle(float angleRadians, float distance) {
            var newMesh = new Mesh();

            var currentAngle = -angleRadians / 2f;
            
            var verts = new List<Vector3> { Vector3.zero };
            var tris = new List<int>();

            for (var i = 0; i < 11; i++) {
                var x = distance * Mathf.Cos(currentAngle);
                var y = distance * Mathf.Sin(currentAngle);
                verts.Add(new Vector3(x, 0, y));
                if (i != 0)
                    tris.AddRange(new [] {0, verts.Count - 1, verts.Count - 2});

                currentAngle += angleRadians / 10;
            }

            newMesh.vertices = verts.ToArray();
            newMesh.triangles = tris.ToArray();

            newMesh.RecalculateNormals();
            _meshFilter.mesh = newMesh;


            // var newMesh = new Mesh();
            // var height = Mathf.Sin(angleRadians / 2f) * distance;
            // newMesh.vertices = new[]
            //     {Vector3.zero, new Vector3(height, 0, distance), new Vector3(-height, 0, distance)};
            // newMesh.triangles = new[] {0, 2, 1};
            //
            // newMesh.RecalculateNormals();
            // _meshFilter.mesh = newMesh;
        }

        [SuppressMessage("ReSharper", "PossibleNullReferenceException")]
        public void AgentUpdated(object agentObject) {
            var agent = agentObject as Agent;
            var sensorInfo = agent.GetSensorData(SensorSetup.sensorName);
            if (sensorInfo.OpeningChangedTowardsPrevious || sensorInfo.OpeningChangedTowardsNext)
                UpdateOpeningAngle(sensorInfo.OpeningAngle, sensorInfo.Distance);
            UpdatePositionAndRotation(agent.DynamicData.Position3D + new Vector3(0, 1, 0), sensorInfo.LocalPosition, sensorInfo.Heading);
        }

        /// <summary>
        /// De-/activates the display of this sensor. Overwritten by Active.
        /// </summary>
        public void SetOn(bool on) {
            _on = on;
            UpdateVisibility();
        }

        public void SetActive(bool active) {
            _active = active;
            UpdateVisibility();
        }

        private void UpdateVisibility() {
            _child.GetChild(0).gameObject.SetActive(_active && _on);
        }
    }
}