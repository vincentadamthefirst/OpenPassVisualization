﻿using System.Collections.Generic;
using Settings;
using UnityEngine;
using Utils;

namespace Scenery.RoadNetwork.RoadObjects {
    
    /// <summary>
    /// Class representing a round RoadObject. Can be a generic cylinder or a predefined model. Currently supported
    /// predefined models:
    /// - Streetlamp
    /// - Pole
    /// </summary>
    public class RoadObjectRound : RoadObject {
        public float Radius { get; set; }

        private MeshRenderer[] _modelRenderers;

        private Material[][] _nonOccludedMaterials;
        private Material[][] _occludedMaterials;

        private Mesh _colliderMesh;

        private void Repeat() {
            if (RepeatParameters == null) return;

            var start = RepeatParameters.SStart;
            var end = RepeatParameters.Length;
            var dist = RepeatParameters.Distance;
            
            for (var s = 0f; s <= end; s += dist) {
                // Create a copy of the current RoadObject and change some values for it
                var newChild = Instantiate(this);
                newChild.RepeatParameters = null;
                newChild.Heading = Heading;
                newChild.T = RepeatParameters.GetT(s);
                newChild.ZOffset = RepeatParameters.GetZ(s);
                newChild.Height = RepeatParameters.GetHeight(s);
                newChild.S = s + start;
                newChild.Orientation = Orientation;
                newChild.Parent = Parent;
                newChild.RoadObjectType = RoadObjectType;
                newChild.SubType = SubType;
                newChild.RoadDesign = RoadDesign;
                newChild.name = name;
                newChild.Radius = Radius;
                newChild.Show();
            }

            markedForDelete = true;
        }

        public override void Show() {
            Repeat(); // repeat if the parameters are set
            if (markedForDelete) return;

            var rop = RoadDesign.GetRoadObjectPrefab(RoadObjectType, SubType);

            // ReSharper disable once SwitchStatementHandlesSomeKnownEnumValuesWithDefault
            switch (RoadObjectType) {
                case RoadObjectType.Tree:
                    ShowTree(rop);
                    break;
                case RoadObjectType.StreetLamp:
                    ShowStreetLamp(rop);
                    break;
                case RoadObjectType.Pole:
                    ShowPole(rop);
                    break;
                case RoadObjectType.Building:
                    ShowBuilding();
                    break;
                case RoadObjectType.CrossWalk:
                case RoadObjectType.ParkingSpace:
                case RoadObjectType.None: // these types are no supported round objects, destroy this object
                    markedForDelete = true;
                    break;
            }

            transform.parent = Parent.transform;

            _modelRenderers = transform.GetComponentsInChildren<MeshRenderer>();
            _nonOccludedMaterials = new Material[_modelRenderers.Length][];

            for (var i = 0; i < _modelRenderers.Length; i++) {
                _nonOccludedMaterials[i] = _modelRenderers[i].materials;
            }
            
            SetupOccludedMaterials();

            AddCollider();
        }

        public void SetLayer(int layer) {
            throw new System.NotImplementedException();
        }

        public override void OcclusionStart() {
            for (var i = 0; i < _modelRenderers.Length; i++) {
                _modelRenderers[i].materials = _occludedMaterials[i];
            }
        }

        public override void OcclusionEnd() {
            for (var i = 0; i < _modelRenderers.Length; i++) {
                _modelRenderers[i].materials = _nonOccludedMaterials[i];
            }
        }

        public override void SetupOccludedMaterials() {
            _occludedMaterials = new Material[_modelRenderers.Length][];

            for (var i = 0; i < _modelRenderers.Length; i++) {
                Material[] tmp;
                
                tmp = new Material[_modelRenderers[i].materials.Length];
                for (var j = 0; j < _modelRenderers[i].materials.Length; j++) {
                    tmp[j] = new Material(_nonOccludedMaterials[i][j]);
                    tmp[j].ChangeToTransparent(SettingsManager.Instance.Settings.minimalOpacity *
                                               (RoadObjectType == RoadObjectType.Tree ? .5f : 1f));
                }
                
                _occludedMaterials[i] = tmp;
            }
        }

        public override bool MaybeDelete() {
            if (!markedForDelete) return false;
            Destroy(this);
            return true;
        }

        private void ShowStreetLamp(RoadObjectPrefab rop) {
            if (rop == null) return;

            var prefab = Resources.Load<GameObject>($"Prefabs/Objects/RoadNetwork/RoadObjects/{rop.prefabName}");
            var streetLamp = Instantiate(prefab, transform, true);
            streetLamp.layer = 19;
            Height -= 1;

            var middleLocalScale = streetLamp.transform.GetChild(1).localScale;
            streetLamp.transform.GetChild(1).localScale =
                new Vector3(middleLocalScale.x, Height, middleLocalScale.z);
            streetLamp.transform.GetChild(2).localPosition += new Vector3(0, Height - 1, 0);

            var scale = 2 * Radius / rop.baseRadius;
            transform.GetChild(0).SetGlobalScale(new Vector3(scale, 1, scale));
            
            var m = Orientation == RoadObjectOrientation.Negative ? -1 : 1;
            streetLamp.transform.position = Parent.EvaluatePoint(S, m * Mathf.Abs(T), ZOffset);
        }

        private void ShowPole(RoadObjectPrefab rop) {
            if (rop == null) return;
            
            var prefab = Resources.Load<GameObject>($"Prefabs/Objects/RoadNetwork/RoadObjects/{rop.prefabName}");
            var pole = Instantiate(prefab, transform, true);
            pole.layer = 19;

            var middleLocalScale = pole.transform.GetChild(0).localScale;
            pole.transform.GetChild(0).localScale =
                new Vector3(middleLocalScale.x, Height, middleLocalScale.z);
            pole.transform.GetChild(1).localPosition += new Vector3(0, Height - 1, 0);
            var scaleBaseRadius = rop.baseRadius;
                    
            var scale = 2 * Radius / scaleBaseRadius;
            transform.GetChild(0).SetGlobalScale(new Vector3(scale, 1, scale));
            
            var m = Orientation == RoadObjectOrientation.Negative ? -1 : 1;
            pole.transform.position = Parent.EvaluatePoint(S, m * Mathf.Abs(T), ZOffset);
        }

        private void ShowTree(RoadObjectPrefab rop) {
            if (rop == null) return;
            
            var prefab = Resources.Load<GameObject>($"Prefabs/Objects/RoadNetwork/RoadObjects/{rop.prefabName}");
            var tree = Instantiate(prefab, transform, true);
            tree.layer = 19;
            
            var scaleA = 2 * Radius / rop.baseRadius;
            var scaleB = Height / rop.baseHeight;
                    
            tree.transform.SetGlobalScale(new Vector3(scaleA, scaleB, scaleA));
            
            var m = Orientation == RoadObjectOrientation.Negative ? -1 : 1;
            tree.transform.position = Parent.EvaluatePoint(S, m * Mathf.Abs(T), ZOffset);
            
            var completeHdg = Parent.EvaluateHeading(S) + Heading;
            tree.transform.Rotate(Vector3.up, Mathf.Rad2Deg * completeHdg);
        }

        private void ShowBuilding() {
            var buildingBase = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(buildingBase.GetComponent<CapsuleCollider>()); // remove the standard collider for cylinders
            buildingBase.layer = 19;
            
            buildingBase.transform.SetGlobalScale(new Vector3(Radius * 2, Height / 2, Radius * 2));
            buildingBase.GetComponent<MeshRenderer>().material =
                RoadDesign.GetRoadObjectMaterial(RoadObjectType, SubType).material;
            buildingBase.transform.parent = transform;
            
            var m = Orientation == RoadObjectOrientation.Negative ? -1 : 1;
            buildingBase.transform.position = Parent.EvaluatePoint(S, m * Mathf.Abs(T), ZOffset + Height / 2f);
        }

        private void AddCollider() {
            _colliderMesh = new Mesh();

            var vertices = new List<Vector3>();
            var triangles = new List<int>();

            var firstPointer = new Vector2(Radius, 0);
            var position = transform.GetChild(0).position;
            position.y = 0;

            for (var i = 0; i < 6; i++) {
                firstPointer.RotateRadians((2 * Mathf.PI) / 6);
                vertices.Add(position + new Vector3(firstPointer.x, ZOffset, firstPointer.y));
            }
            
            for (var i = 0; i < 6; i++) {
                firstPointer.RotateRadians((2 * Mathf.PI) / 6);
                vertices.Add(position + new Vector3(firstPointer.x, ZOffset + Height, firstPointer.y));
            }

            triangles.AddRange(new [] {
                3, 0, 1, 3, 1, 2, 3, 4, 0, 4, 5, 0,   // bottom
                0, 6, 7, 0, 7, 1,                     // sides...
                1, 7, 8, 1, 8, 2, 
                2, 8, 9, 2, 9, 3,
                3, 9, 10, 3, 10, 4,
                4, 10, 11, 4, 11, 5,
                5, 11, 6, 5, 6, 0,
                6, 9, 8, 6, 8, 7, 6, 10, 9, 6, 11, 10 // top
            });

            _colliderMesh.vertices = vertices.ToArray();
            _colliderMesh.triangles = triangles.ToArray();
            _colliderMesh.RecalculateNormals();
            _colliderMesh.Optimize();

            var coll = gameObject.AddComponent<MeshCollider>();
            coll.convex = true;
            coll.cookingOptions = MeshColliderCookingOptions.CookForFasterSimulation;
            coll.sharedMesh = _colliderMesh;
        }

        public override ElementOrigin ElementOrigin => ElementOrigin.OpenDrive;
    }
}